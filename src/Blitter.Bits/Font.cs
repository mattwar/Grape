using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace Blitter.Bits;

/// <summary>
/// Draws text in a 2D or 3D renderer using a monospace font, cached as a texture atlas.
/// </summary>
public sealed class Font : IDisposable
{
    private readonly Atlas _atlas;
    private readonly int _cellPixelW;
    private readonly int _cellPixelH;
    private readonly int _columns;
    private readonly int _rows;

    // Codepoint -> atlas slot. Hot-path lookup; avoids the per-rune
    // string allocation that going through Atlas's name map would cost.
    // The Atlas name map ("A" -> 0, "♥" -> 95, ...) exists alongside
    // this for external introspection (font.Atlas["A"]).
    private readonly Dictionary<int, int> _runeToSlot;

    // Caches the textured-quad mesh built for each string passed to
    // DrawText(Renderer3D). Keyed by reference identity, so callers that
    // reuse the same string instance (string literals, held fields,
    // interned values) skip the per-call mesh build and allocation.
    // Strings allocated per frame (e.g. interpolated readouts) won't hit
    // and pay the build cost as before. Cache entries self-evict when
    // the key string is GC'd.
    private readonly ConditionalWeakTable<string, Mesh<TextureVertex3D>> _meshCache = new();

    /// <summary>Cell width in atlas pixels (one monospace advance).</summary>
    public float CellWidth => _cellPixelW;

    /// <summary>Cell height in atlas pixels (line height).</summary>
    public float CellHeight => _cellPixelH;

    /// <summary>Number of glyphs baked into the atlas.</summary>
    public int GlyphCount => _runeToSlot.Count;

    /// <summary>The backing atlas, in case the caller wants to inspect or draw it directly.</summary>
    public Atlas Atlas => _atlas;

    /// <summary>True if the font has a baked glyph for the given codepoint.</summary>
    public bool Contains(Rune rune) => _runeToSlot.ContainsKey(rune.Value);

    /// <summary>
    /// Constructs a <see cref="Font"/> from a typeface family name.
    /// If the typeface is not found on the host OS, a platform default is used instead.
    /// </summary>
    public Font(string family, float pixelSize, bool bold = false, string? charset = null)
        : this(SKTypeface.FromFamilyName(family,
                bold ? SKFontStyle.Bold : SKFontStyle.Normal),
            pixelSize, charset, ownsTypeface: true)
    {
    }

    /// <summary>
    /// Constructs a <see cref="Font"/> from a list of typeface family names, in order of preference.
    /// The first family found on the host is used.
    /// If no families are found, a platform default is used instead.
    /// </summary>
    public Font(IEnumerable<string> familyFallbacks, float pixelSize, bool bold = false, string? charset = null)
        : this(ResolveFallback(familyFallbacks, bold), pixelSize, charset, ownsTypeface: true)
    {
    }

    /// <summary>
    /// Loads a typeface from a <c>.ttf</c> / <c>.otf</c> file on disk.
    /// </summary>
    public static Font Load(string filePath, float pixelSize, string? charset = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var typeface = SKTypeface.FromFile(filePath)
            ?? throw new InvalidOperationException($"Failed to load typeface from '{filePath}'.");
        return new Font(typeface, pixelSize, charset, ownsTypeface: true);
    }

    /// <summary>
    /// Loads a typeface from a <c>.ttf</c> / <c>.otf</c> stream.
    /// </summary>
    public static Font Load(Stream stream, float pixelSize, string? charset = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var typeface = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException("Failed to load typeface from stream.");
        return new Font(typeface, pixelSize, charset, ownsTypeface: true);
    }

    /// <summary>
    /// Constructs a <see cref="Font"/> from an existing Skia <see cref="SKTypeface"/>.
    /// </summary>
    public Font(SKTypeface typeface, float pixelSize, string? charset = null)
        : this(typeface, pixelSize, charset, ownsTypeface: false)
    {
    }

    private static SKTypeface ResolveFallback(IEnumerable<string> families, bool bold)
    {
        ArgumentNullException.ThrowIfNull(families);
        var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
        foreach (var name in families)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var tf = SKTypeface.FromFamilyName(name, style);
            if (tf is null) continue;
            // FromFamilyName silently substitutes when the name is missing;
            // accept only when the resolved face's family matches what we
            // asked for (case-insensitive). Generic aliases like "monospace"
            // never round-trip, so they always fall through to the
            // generic-monospace step below.
            if (string.Equals(tf.FamilyName, name, StringComparison.OrdinalIgnoreCase))
                return tf;
            tf.Dispose();
        }
        // Nothing in the user's list matched. Ask Skia for the generic
        // "monospace" alias before giving up. Skia maps this to a real
        // monospace face on every platform we target (Courier New on
        // Windows, Menlo on macOS, DejaVu/Liberation Mono on Linux), so
        // we accept whatever it returns -- the round-trip check would
        // reject it since the resolved FamilyName is never literally
        // "monospace".
        var mono = SKTypeface.FromFamilyName("monospace", style);
        if (mono is not null) return mono;

        // Last resort: the platform default (typically a proportional face).
        return SKTypeface.Default;
    }

    private Font(SKTypeface typeface, float pixelSize, string? charset, bool ownsTypeface)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        if (pixelSize <= 0f) throw new ArgumentOutOfRangeException(nameof(pixelSize));

        try
        {
            using var font = new SKFont(typeface, pixelSize);

            // Resolve the charset, dedupe by codepoint, and drop runes
            // that the typeface has no glyph for. Walk via Rune to get
            // surrogate-pair handling for free (e.g. emoji).
            charset ??= FontCharsets.AsciiPrintable;
            var supportedRunes = new List<Rune>(capacity: charset.Length);
            var seen = new HashSet<int>();
            foreach (var rune in charset.EnumerateRunes())
            {
                if (!seen.Add(rune.Value)) continue;
                if (typeface.GetGlyph(rune.Value) == 0) continue; // .notdef -- skip
                supportedRunes.Add(rune);
            }
            if (supportedRunes.Count == 0)
                throw new InvalidOperationException(
                    "No glyphs in the requested charset are available in the supplied typeface.");

            // Cell width = max measured width across all supported runes.
            // Probing only "MW@" misses CJK / emoji / display faces with
            // unusually wide outliers; iterating the full charset is O(N)
            // measurements at construction and gives a layout that never
            // overlaps regardless of script.
            float maxAdvance = 0f;
            Span<char> codeBuf = stackalloc char[2];
            foreach (var rune in supportedRunes)
            {
                int len = rune.EncodeToUtf16(codeBuf);
                font.MeasureText(codeBuf[..len], out var bounds);
                if (bounds.Width > maxAdvance) maxAdvance = bounds.Width;
            }
            // 1-pixel gutter on each side so anti-aliased edges from one
            // glyph don't bleed into the neighbour cell when sampled.
            _cellPixelW = Math.Max(1, (int)MathF.Ceiling(maxAdvance) + 2);

            var metrics = font.Metrics;
            float lineH = metrics.Descent - metrics.Ascent;
            _cellPixelH = Math.Max(1, (int)MathF.Ceiling(lineH) + 2);

            // Roughly-square grid sized for the supported-rune count.
            int n = supportedRunes.Count;
            _columns = (int)MathF.Ceiling(MathF.Sqrt(n));
            _rows    = (n + _columns - 1) / _columns;

            int atlasW = _cellPixelW * _columns;
            int atlasH = _cellPixelH * _rows;

            // Conservative ceiling that fits virtually every desktop GPU
            // (D3D 10 / GLES 3 floor). Larger atlases may upload fine on
            // modern hardware but will silently fail on older devices, so
            // refuse at construction with an actionable message.
            const int maxAtlasDim = 8192;
            if (atlasW > maxAtlasDim || atlasH > maxAtlasDim)
            {
                throw new InvalidOperationException(
                    "Font is too large. Reduce pixelSize or use a smaller charset.");
            }

            // Build the atlas image by driving a Skia canvas over a
            // transparent SKBitmap, then snapshotting the pixels into a
            // Blitter Image. Build the codepoint->slot map and the
            // Atlas name map ("A" -> 0, "♥" -> 95, ...) in the same pass.
            var info = new SKImageInfo(atlasW, atlasH, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bmp = new SKBitmap(info);
            _runeToSlot = new Dictionary<int, int>(n);
            var nameMap = new Dictionary<string, int>(n, StringComparer.Ordinal);
            var rects = new Rect[n];

            using (var canvas = new SKCanvas(bmp))
            using (var paint = new SKPaint
            {
                // Atlas glyphs are baked white; tint is multiplied in at
                // draw time. The font's Color is preserved as the default
                // tint when the caller doesn't supply one.
                Color = SKColors.White,
                IsAntialias = true,
            })
            {
                canvas.Clear(SKColors.Transparent);

                // Place each glyph's baseline so ascent..descent fits
                // with the 1-pixel top gutter.
                float baselineYInCell = 1f - metrics.Ascent;

                for (int slot = 0; slot < n; slot++)
                {
                    var rune = supportedRunes[slot];
                    int len = rune.EncodeToUtf16(codeBuf);
                    var glyphSpan = codeBuf[..len];
                    var glyphString = glyphSpan.ToString();

                    font.MeasureText(glyphSpan, out var bounds);
                    float xCenter = _cellPixelW * 0.5f - bounds.MidX;
                    int col = slot % _columns;
                    int row = slot / _columns;
                    canvas.DrawText(glyphString,
                        col * _cellPixelW + xCenter,
                        row * _cellPixelH + baselineYInCell,
                        font, paint);

                    rects[slot] = new Rect(
                        col * _cellPixelW, row * _cellPixelH,
                        _cellPixelW, _cellPixelH);
                    _runeToSlot[rune.Value] = slot;
                    nameMap[glyphString] = slot;
                }
            }

            var image = bmp.ToImage();
            _atlas = new Atlas(image, rects, nameMap);
        }
        finally
        {
            if (ownsTypeface) typeface.Dispose();
        }
    }

    /// <summary>
    /// Determines the font pixel size of the rectangle needed to draw the given text.
    /// </summary>
    public Vector2 Measure(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int runeCount = 0;
        foreach (var _ in text.EnumerateRunes()) runeCount++;
        return new Vector2(runeCount * _cellPixelW, _cellPixelH);
    }

    /// <summary>
    /// Draws text in 2D space
    /// </summary>
    public void DrawText(Renderer2D renderer, string text, float x, float y)
        => DrawText(renderer, text, Color.White, x, y);

    /// <summary>
    /// Draws text in 2D space
    /// </summary>
    public void DrawText(Renderer2D renderer, string text, Color color, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return;

        float cw = _cellPixelW;
        float ch = _cellPixelH;
        int i = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_runeToSlot.TryGetValue(rune.Value, out var slot))
                renderer.DrawImage(_atlas.Image, _atlas[slot], new Rect(x + i * cw, y, cw, ch), color);
            i++;
        }
    }

    /// <summary>
    /// Draws text in 3D space
    /// </summary>
    public void DrawText(Renderer3D renderer, string text, in Matrix4x4 transform)
        => DrawText(renderer, text, Color.White, in transform);

    /// <summary>
    /// Draws text in 3D space
    /// </summary>
    public void DrawText(Renderer3D renderer, string text, Color color, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return;

        if (!_meshCache.TryGetValue(text, out var mesh))
        {
            mesh = BuildTextMesh(text);
            _meshCache.Add(text, mesh);
        }

        var args = new TransformAndFColorArgs(transform, color);
        // Text glyphs are flat quads; arbitrary transforms (rotation,
        // billboarding, world-space placement) routinely flip them
        // back-to-front. Disable culling so the text is legible from
        // either side regardless of the renderer's current CullMode.
        // Use DepthMode.Transparent so the quad's fully-transparent
        // pixels don't write depth and occlude later text drawn behind
        // them.
        using (renderer.PushState())
        {
            renderer.CullMode = CullMode.None;
            renderer.DepthMode = DepthMode.Transparent;
            renderer.DrawMesh(mesh, _atlas.Image, Shaders.PositionTextureWithTransformAndColor, in args);
        }
    }

    private Mesh<TextureVertex3D> BuildTextMesh(string text)
    {
        // UVs are derived from the Atlas's pixel rects (single source of
        // truth for cell placement) divided by atlas dimensions.
        float atlasW = _atlas.Image.Size.Width;
        float atlasH = _atlas.Image.Size.Height;

        var verts = new List<TextureVertex3D>();
        int i = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_runeToSlot.TryGetValue(rune.Value, out var slot))
            {
                var rect = _atlas[slot];
                float u0 = rect.X / atlasW;
                float u1 = (rect.X + rect.Width) / atlasW;
                float v0 = rect.Y / atlasH;
                float v1 = (rect.Y + rect.Height) / atlasH;
                float x0 = i, x1 = i + 1;

                var tl = new TextureVertex3D(new Vertex3D(x0, 1f, 0f), new Vector2(u0, v0));
                var bl = new TextureVertex3D(new Vertex3D(x0, 0f, 0f), new Vector2(u0, v1));
                var tr = new TextureVertex3D(new Vertex3D(x1, 1f, 0f), new Vector2(u1, v0));
                var br = new TextureVertex3D(new Vertex3D(x1, 0f, 0f), new Vector2(u1, v1));

                verts.Add(tl);
                verts.Add(bl);
                verts.Add(br);
                verts.Add(tl);
                verts.Add(br);
                verts.Add(tr);
            }
            // Missing glyph: advance the layout column but emit no quad.
            i++;
        }

        return Mesh.Create<TextureVertex3D>(CollectionsMarshal.AsSpan(verts));
    }

    /// <summary>Disposes the backing atlas (and its image).</summary>
    public void Dispose() => _atlas.Dispose();
}
