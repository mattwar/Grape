using System.Globalization;

namespace Grape;

/// <summary>
/// Wavefront MTL (material library) loader. Supports the subset Grape
/// can render today: <c>newmtl</c>, <c>Kd</c> (diffuse color), and
/// <c>map_Kd</c> (diffuse texture). Everything else is parsed but
/// ignored, including specular, illumination model, transparency, and
/// the various PBR extensions some authoring tools emit.
/// </summary>
internal static class MTL
{
    private static readonly char[] WhitespaceSeparators = { ' ', '\t' };

    /// <summary>
    /// Reads <paramref name="path"/> and returns its materials keyed
    /// by name. Duplicate <c>newmtl</c> entries within the file follow
    /// "later wins"; callers merging multiple libraries get the same
    /// behavior by overlaying the returned dictionaries in order.
    /// </summary>
    public static Dictionary<string, Material> Load(string path)
    {
        var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;

        string? currentName = null;
        Color currentDiffuse = Color.White;
        Image? currentTexture = null;

        using var reader = new StreamReader(path);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] == '#')
                continue;

            var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;

            switch (tokens[0])
            {
                case "newmtl":
                    Flush(currentName, currentDiffuse, currentTexture, materials);
                    currentName = tokens.Length >= 2 ? tokens[1] : null;
                    currentDiffuse = Color.White;
                    currentTexture = null;
                    break;

                case "Kd":
                    if (tokens.Length >= 4)
                    {
                        // Components are 0..1 floats; scale to byte.
                        float r = ParseFloat(tokens[1]);
                        float g = ParseFloat(tokens[2]);
                        float b = ParseFloat(tokens[3]);
                        currentDiffuse = new Color(
                            (byte)Math.Clamp(r * 255f, 0f, 255f),
                            (byte)Math.Clamp(g * 255f, 0f, 255f),
                            (byte)Math.Clamp(b * 255f, 0f, 255f));
                    }
                    break;

                case "map_Kd":
                    // Last token wins as the filename. MTL allows
                    // option flags before the filename (e.g. "-s 1 1 1
                    // foo.png"); a strict parser would interpret each
                    // flag, but the practical "last token = filename"
                    // rule handles real-world files better than
                    // "second token", since most authoring tools don't
                    // emit the options anyway.
                    if (tokens.Length >= 2)
                    {
                        var texPath = OBJ.ResolveRelative(directory, tokens[^1]);
                        currentTexture = TryLoadImage(texPath);
                    }
                    break;

                // Quietly ignored (parsed but not applied):
                //   Ka (ambient), Ks (specular), Ns (specular exponent),
                //   d / Tr (transparency), illum, Ke (emissive),
                //   map_Ks/map_Ns/map_Bump/norm, Pr/Pm (PBR), etc.
                default:
                    break;
            }
        }

        Flush(currentName, currentDiffuse, currentTexture, materials);
        return materials;
    }

    private static void Flush(
        string? name,
        Color diffuse,
        Image? texture,
        Dictionary<string, Material> materials)
    {
        if (string.IsNullOrEmpty(name))
            return;
        materials[name] = new Material
        {
            Name = name,
            DiffuseColor = diffuse,
            DiffuseTexture = texture,
        };
    }

    // Tolerate texture loading failures rather than failing the whole
    // model load. A missing or unsupported texture leaves the material
    // texture-less; the renderer will fall back to the model's 1x1
    // white placeholder, so the geometry still renders -- just
    // without its painted detail.
    private static Image? TryLoadImage(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return Image.Load(path, mipmaps: true);
        }
        catch
        {
            return null;
        }
    }

    private static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
}
