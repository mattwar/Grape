using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Blitter;

public static class SkiaSharpExtensions
{
    extension(BitmapImage image)
    {
        /// <summary>
        /// Copies pixels from <paramref name="bitmap"/> into <paramref name="image"/>.
        /// Only the overlapping region (top-left aligned) is copied; pixels
        /// outside the smaller of the two are left untouched.
        /// </summary>
        public void CopyFromBitmap(SKBitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (imageWidth, imageHeight) = image.Size;
            int width = Math.Min(bitmap.Width, imageWidth);
            int height = Math.Min(bitmap.Height, imageHeight);
            if (width == 0 || height == 0)
            {
                image.Invalidate();
                return;
            }

            // Fast path: both sides are 32bpp and the format pair is
            // either identical (memcpy) or differs only by R/B swap.
            // Falls through to per-pixel for everything else.
            if (TryFastCopyFromBitmap(bitmap, image, width, height))
            {
                image.Invalidate();
                return;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b, a) = bitmap.GetPixel(x, y);
                    image.SetPixel(x, y, new Color(r, g, b, a));
                }
            }

            image.Invalidate();
        }

        /// <summary>
        /// Copies pixels from <paramref name="image"/> into <paramref name="bitmap"/>.
        /// Only the overlapping region (top-left aligned) is copied; pixels
        /// outside the smaller of the two are left untouched.
        /// </summary>
        public void CopyToBitmap(SKBitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (imageWidth, imageHeight) = image.Size;
            int width = Math.Min(bitmap.Width, imageWidth);
            int height = Math.Min(bitmap.Height, imageHeight);
            if (width == 0 || height == 0)
                return;

            if (TryFastCopyToBitmap(image, bitmap, width, height))
                return;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b, a) = image.GetPixel(x, y);
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, a));
                }
            }
        }

        /// <summary>
        /// Draws into the image using a SkiaSharp <see cref="SKCanvas"/>.
        /// The image's existing pixels are used as the canvas backdrop;
        /// the canvas draws compose on top of them.
        /// </summary>
        /// <param name="drawAction">Callback that issues canvas draws.</param>
        public void DrawCanvas(Action<SKCanvas> drawAction)
            => DrawCanvasCore(image, null, drawAction);

        /// <summary>
        /// Draws into the image using a SkiaSharp <see cref="SKCanvas"/>,
        /// painting <paramref name="backgroundColor"/> behind the draws.
        /// </summary>
        /// <param name="backgroundColor">
        /// The background painted behind the draws. An opaque alpha
        /// (255) replaces the prior content (and skips copying the
        /// image into the bitmap, which is the perf win); a
        /// translucent alpha blends over the prior content using
        /// standard source-over compositing.
        /// </param>
        /// <param name="drawAction">Callback that issues canvas draws.</param>
        public void DrawCanvas(Blitter.Color backgroundColor, Action<SKCanvas> drawAction)
            => DrawCanvasCore(image, backgroundColor, drawAction);

        /// <summary>
        /// Creates a new <see cref="SKBitmap"/> the same size as the image
        /// and copies its pixels into it.
        /// </summary>
        public SKBitmap ToSKBitmap()
        {
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (width, height) = image.Size;
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bitmap = new SKBitmap(info);
            image.CopyToBitmap(bitmap);
            return bitmap;
        }

        /// <summary>
        /// Returns a new image the same size as the source with a
        /// Gaussian blur applied. The blur kernel is clamped at the
        /// image edges; pre-pad the source if you need bleed room.
        /// </summary>
        /// <param name="sigmaX">Horizontal blur standard deviation, in pixels.</param>
        /// <param name="sigmaY">Vertical blur standard deviation, in pixels.</param>
        public BitmapImage Blur(float sigmaX, float sigmaY)
        {
            using var filter = SKImageFilter.CreateBlur(sigmaX, sigmaY);
            return ApplyImageFilter(image, filter, padLeft: 0, padTop: 0, padRight: 0, padBottom: 0);
        }

        /// <summary>
        /// Returns a new image the same size as the source with a
        /// uniform Gaussian blur applied.
        /// </summary>
        /// <param name="sigma">Blur standard deviation, in pixels.</param>
        public BitmapImage Blur(float sigma) => image.Blur(sigma, sigma);

        /// <summary>
        /// Returns a new image with a drop shadow composited under the
        /// source. The result is automatically padded so the shadow is
        /// not clipped: the source's (0,0) appears at
        /// (<c>max(0,-dx) + ceil(3*sigmaX)</c>,
        /// <c>max(0,-dy) + ceil(3*sigmaY)</c>) in the returned image.
        /// </summary>
        /// <param name="dx">Horizontal shadow offset, in pixels.</param>
        /// <param name="dy">Vertical shadow offset, in pixels.</param>
        /// <param name="sigmaX">Horizontal shadow blur, in pixels.</param>
        /// <param name="sigmaY">Vertical shadow blur, in pixels.</param>
        /// <param name="color">Shadow color (alpha included).</param>
        public BitmapImage DropShadow(float dx, float dy, float sigmaX, float sigmaY, Blitter.Color color)
        {
            // Pad enough to fit the shadow's blurred footprint plus
            // the offset on whichever side the shadow falls.
            int blurX = (int)MathF.Ceiling(3f * sigmaX);
            int blurY = (int)MathF.Ceiling(3f * sigmaY);
            int padLeft   = blurX + (int)MathF.Ceiling(MathF.Max(0f, -dx));
            int padTop    = blurY + (int)MathF.Ceiling(MathF.Max(0f, -dy));
            int padRight  = blurX + (int)MathF.Ceiling(MathF.Max(0f,  dx));
            int padBottom = blurY + (int)MathF.Ceiling(MathF.Max(0f,  dy));

            using var filter = SKImageFilter.CreateDropShadow(
                dx, dy, sigmaX, sigmaY, new SKColor(color.R, color.G, color.B, color.A));
            return ApplyImageFilter(image, filter, padLeft, padTop, padRight, padBottom);
        }

        /// <summary>
        /// Returns a new image the same size as the source converted
        /// to grayscale via the standard luminance weights.
        /// </summary>
        public BitmapImage Grayscale()
        {
            // Rec. 601 luma coefficients applied to R, G, B; alpha untouched.
            ReadOnlySpan<float> matrix =
            [
                0.299f, 0.587f, 0.114f, 0f, 0f,
                0.299f, 0.587f, 0.114f, 0f, 0f,
                0.299f, 0.587f, 0.114f, 0f, 0f,
                0f,     0f,     0f,     1f, 0f,
            ];
            using var colorFilter = SKColorFilter.CreateColorMatrix(matrix.ToArray());
            using var filter = SKImageFilter.CreateColorFilter(colorFilter);
            return ApplyImageFilter(image, filter, 0, 0, 0, 0);
        }

        /// <summary>
        /// Returns a new image the same size as the source with each
        /// channel multiplied by <paramref name="tint"/>'s channels
        /// (treated as 0..1). Alpha is multiplied like the others, so
        /// a fully opaque tint preserves the source alpha.
        /// </summary>
        public BitmapImage Tint(Blitter.Color tint)
        {
            float r = tint.R / 255f;
            float g = tint.G / 255f;
            float b = tint.B / 255f;
            float a = tint.A / 255f;
            ReadOnlySpan<float> matrix =
            [
                r,  0f, 0f, 0f, 0f,
                0f, g,  0f, 0f, 0f,
                0f, 0f, b,  0f, 0f,
                0f, 0f, 0f, a,  0f,
            ];
            using var colorFilter = SKColorFilter.CreateColorMatrix(matrix.ToArray());
            using var filter = SKImageFilter.CreateColorFilter(colorFilter);
            return ApplyImageFilter(image, filter, 0, 0, 0, 0);
        }

        /// <summary>
        /// Returns a new image expanded by the dilate radius on each
        /// side, with bright pixels grown by a max filter of the given
        /// radius. Useful for outline / glow building.
        /// </summary>
        /// <param name="radiusX">Horizontal dilate radius, in pixels.</param>
        /// <param name="radiusY">Vertical dilate radius, in pixels.</param>
        public BitmapImage Dilate(int radiusX, int radiusY)
        {
            using var filter = SKImageFilter.CreateDilate(radiusX, radiusY);
            return ApplyImageFilter(image, filter, radiusX, radiusY, radiusX, radiusY);
        }

        /// <summary>
        /// Returns a new image the same size as the source with bright
        /// pixels shrunk by a min filter of the given radius. Useful
        /// for inset effects.
        /// </summary>
        /// <param name="radiusX">Horizontal erode radius, in pixels.</param>
        /// <param name="radiusY">Vertical erode radius, in pixels.</param>
        public BitmapImage Erode(int radiusX, int radiusY)
        {
            using var filter = SKImageFilter.CreateErode(radiusX, radiusY);
            return ApplyImageFilter(image, filter, 0, 0, 0, 0);
        }

        /// <summary>
        /// Returns a new image with the source shifted by
        /// (<paramref name="dx"/>, <paramref name="dy"/>). The result
        /// is padded so no content is clipped: the source's (0,0)
        /// appears at (<c>max(0,-dx)+dx</c>, <c>max(0,-dy)+dy</c>) in
        /// the returned image.
        /// </summary>
        public BitmapImage Offset(float dx, float dy)
        {
            int padLeft   = (int)MathF.Ceiling(MathF.Max(0f, -dx));
            int padTop    = (int)MathF.Ceiling(MathF.Max(0f, -dy));
            int padRight  = (int)MathF.Ceiling(MathF.Max(0f,  dx));
            int padBottom = (int)MathF.Ceiling(MathF.Max(0f,  dy));
            using var filter = SKImageFilter.CreateOffset(dx, dy);
            return ApplyImageFilter(image, filter, padLeft, padTop, padRight, padBottom);
        }

        /// <summary>
        /// Returns a new image the same size as the source with an
        /// arbitrary convolution kernel applied. The kernel is laid
        /// out row-major and must contain
        /// <paramref name="kernelWidth"/> &#215;
        /// <paramref name="kernelHeight"/> values.
        /// </summary>
        /// <param name="kernelWidth">Kernel width in samples.</param>
        /// <param name="kernelHeight">Kernel height in samples.</param>
        /// <param name="kernel">Row-major kernel weights.</param>
        /// <param name="gain">Output multiplier applied after convolution. Defaults to 1.</param>
        /// <param name="bias">Constant added to each channel after the gain. Defaults to 0.</param>
        public BitmapImage Convolve(int kernelWidth, int kernelHeight, ReadOnlySpan<float> kernel, float gain = 1f, float bias = 0f)
        {
            if (kernel.Length != kernelWidth * kernelHeight)
                throw new ArgumentException(
                    $"Kernel length {kernel.Length} does not match {kernelWidth}x{kernelHeight}.",
                    nameof(kernel));

            var kernelSize = new SKSizeI(kernelWidth, kernelHeight);
            // Center the kernel; matches the conventional sampling for
            // edge-detect / sharpen / emboss kernels.
            var kernelOffset = new SKPointI(kernelWidth / 2, kernelHeight / 2);
            using var filter = SKImageFilter.CreateMatrixConvolution(
                kernelSize, kernel.ToArray(), gain, bias,
                kernelOffset, SKShaderTileMode.Clamp,
                convolveAlpha: false);
            return ApplyImageFilter(image, filter, 0, 0, 0, 0);
        }

        /// <summary>
        /// Returns a new image the same size as the source with a
        /// magnifier lens covering <paramref name="lens"/>. Pixels
        /// inside the lens are sampled at <paramref name="zoom"/>
        /// magnification; pixels outside are unchanged.
        /// </summary>
        /// <param name="lens">Lens rectangle, in source-image pixels.</param>
        /// <param name="zoom">Zoom factor (1 = no magnification, 2 = 2x).</param>
        /// <param name="inset">
        /// Soft-blend border thickness, in pixels. Magnified pixels
        /// fade smoothly into the unscaled background over this many
        /// pixels at the lens edge. 0 produces a hard edge.
        /// </param>
        /// <param name="sampling">
        /// How magnified pixels are filtered.
        /// <see cref="ImageSampling.Linear"/> (default) is right for
        /// photographic content; <see cref="ImageSampling.Nearest"/>
        /// preserves crisp pixels for pixel-art / debug zoom uses.
        /// </param>
        public BitmapImage Magnify(Rect lens, float zoom, float inset = 0f, ImageSampling sampling = ImageSampling.Linear)
        {
            var lensRect = new SKRect(lens.X, lens.Y, lens.X + lens.Width, lens.Y + lens.Height);
            var skSampling = sampling == ImageSampling.Nearest
                ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
                : new SKSamplingOptions(SKFilterMode.Linear,  SKMipmapMode.None);
            using var filter = SKImageFilter.CreateMagnifier(
                lensRect, inset, zoom, skSampling, null, SKRect.Empty);
            return ApplyImageFilter(image, filter, 0, 0, 0, 0);
        }
    }

    // Fast pixel copy paths.
    //
    // Skia's Bgra8888 stores bytes B,G,R,A which on little-endian
    // matches SDL's ARGB8888 uint32 layout. Skia's Rgba8888 (R,G,B,A)
    // matches SDL's ABGR8888. The opposite pairings differ only by an
    // R/B swap, which is a cheap mask + shift the JIT auto-vectorizes.
    // Anything else (different bpp, indexed, exotic formats) drops to
    // the per-pixel SDL Map/GetRGBA path in the callers.

    private enum FastPath { None, Memcpy, SwapRB }

    private static FastPath DetermineFastPath(SKColorType src, PixelFormat dst) =>
        (src, dst) switch
        {
            (SKColorType.Bgra8888, PixelFormat.ARGB8888) => FastPath.Memcpy,
            (SKColorType.Rgba8888, PixelFormat.ABGR8888) => FastPath.Memcpy,
            (SKColorType.Bgra8888, PixelFormat.ABGR8888) => FastPath.SwapRB,
            (SKColorType.Rgba8888, PixelFormat.ARGB8888) => FastPath.SwapRB,
            _ => FastPath.None,
        };

    private static FastPath DetermineFastPath(PixelFormat src, SKColorType dst) =>
        (src, dst) switch
        {
            (PixelFormat.ARGB8888, SKColorType.Bgra8888) => FastPath.Memcpy,
            (PixelFormat.ABGR8888, SKColorType.Rgba8888) => FastPath.Memcpy,
            (PixelFormat.ABGR8888, SKColorType.Bgra8888) => FastPath.SwapRB,
            (PixelFormat.ARGB8888, SKColorType.Rgba8888) => FastPath.SwapRB,
            _ => FastPath.None,
        };

    private static unsafe bool TryFastCopyFromBitmap(SKBitmap bitmap, BitmapImage image, int width, int height)
    {
        if (image.BytesPerPixel != 4)
            return false;
        var path = DetermineFastPath(bitmap.ColorType, image.PixelFormat);
        if (path == FastPath.None)
            return false;

        int srcStride = bitmap.RowBytes;
        int dstStride = image.Pitch;
        byte* srcBase = (byte*)bitmap.GetPixels();
        var dstSpan = image.WritablePixels;

        fixed (byte* dstBase = dstSpan)
        {
            int rowBytes = width * 4;

            // one memory copy, fastest
            if (path == FastPath.Memcpy
                && srcStride == dstStride
                && srcStride == rowBytes
                && bitmap.Width == width
                && image.Size.Width == width)
            {
                long bytes = (long)rowBytes * height;
                Buffer.MemoryCopy(srcBase, dstBase, bytes, bytes);
                return true;
            }

            // otherwise, copy row by row, either memcpy or R/B swap.
            for (int y = 0; y < height; y++)
            {
                byte* srcRow = srcBase + y * srcStride;
                byte* dstRow = dstBase + y * dstStride;
                if (path == FastPath.Memcpy)
                    Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                else
                    SwapRBRow((uint*)srcRow, (uint*)dstRow, width);
            }
        }
        return true;
    }

    private static unsafe bool TryFastCopyToBitmap(BitmapImage image, SKBitmap bitmap, int width, int height)
    {
        if (image.BytesPerPixel != 4)
            return false;
        var path = DetermineFastPath(image.PixelFormat, bitmap.ColorType);
        if (path == FastPath.None)
            return false;

        int srcStride = image.Pitch;
        int dstStride = bitmap.RowBytes;
        var srcSpan = image.GetPixels();
        byte* dstBase = (byte*)bitmap.GetPixels();

        fixed (byte* srcBase = srcSpan)
        {
            int rowBytes = width * 4;

            // one memory copy, fastest
            if (path == FastPath.Memcpy
                && srcStride == dstStride
                && srcStride == rowBytes
                && image.Size.Width == width
                && bitmap.Width == width)
            {
                long bytes = (long)rowBytes * height;
                Buffer.MemoryCopy(srcBase, dstBase, bytes, bytes);
                return true;
            }

            // otherwise, copy row by row, either memcpy or R/B swap.
            for (int y = 0; y < height; y++)
            {
                byte* srcRow = srcBase + y * srcStride;
                byte* dstRow = dstBase + y * dstStride;
                if (path == FastPath.Memcpy)
                    Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                else
                    SwapRBRow((uint*)srcRow, (uint*)dstRow, width);
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void SwapRBRow(uint* src, uint* dst, int count)
    {
        // 0xAARRGGBB <-> 0xAABBGGRR: keep alpha+green, swap red/blue.
        for (int x = 0; x < count; x++)
        {
            uint v = src[x];
            dst[x] = (v & 0xFF00FF00u) | ((v & 0x00FF0000u) >> 16) | ((v & 0x000000FFu) << 16);
        }
    }

    private static void DrawCanvasCore(BitmapImage image, Blitter.Color? backgroundColor, Action<SKCanvas> drawAction)
    {
        ArgumentNullException.ThrowIfNull(drawAction);
        ObjectDisposedException.ThrowIf(image.IsDisposed, image);

        var (width, height) = image.Size;
        if (width == 0 || height == 0)
            return;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);

        // Stage the image's pixels as backdrop unless the caller
        // requested an opaque background -- an opaque background
        // overwrites everything anyway, so the copy would be wasted
        // work.
        var hasOpaqueBackground = backgroundColor is { A: 255 };
        if (!hasOpaqueBackground)
            image.CopyToBitmap(bitmap);

        using (var canvas = new SKCanvas(bitmap))
        {
            if (backgroundColor is { } c)
            {
                var skColor = new SKColor(c.R, c.G, c.B, c.A);
                if (c.A == 255)
                {
                    // Replace: equivalent to clearing the canvas
                    // since opaque alpha drops the prior content.
                    canvas.Clear(skColor);
                }
                else
                {
                    // Blend the translucent fill over the staged
                    // backdrop. DrawColor uses the canvas' default
                    // SrcOver blend mode.
                    canvas.DrawColor(skColor);
                }
            }
            drawAction(canvas);
            canvas.Flush();
        }

        image.CopyFromBitmap(bitmap);
    }

    private static BitmapImage ApplyImageFilter(
        BitmapImage source, SKImageFilter filter,
        int padLeft, int padTop, int padRight, int padBottom)
    {
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);

        var (sw, sh) = source.Size;
        int dw = sw + padLeft + padRight;
        int dh = sh + padTop + padBottom;
        if (dw <= 0 || dh <= 0)
            return Image.Create(Math.Max(1, dw), Math.Max(1, dh));

        // Stage source -> SKBitmap, draw with the filter into a same-
        // sized destination SKBitmap, then snapshot back to a new
        // Image. ABGR8888 + Rgba8888 keeps the byte layout aligned
        // with the fast copy path in CopyFromBitmap.
        using var srcBitmap = source.ToSKBitmap();
        var dstInfo = new SKImageInfo(dw, dh, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var dstBitmap = new SKBitmap(dstInfo);
        using (var canvas = new SKCanvas(dstBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { ImageFilter = filter };
            canvas.DrawBitmap(srcBitmap, padLeft, padTop, paint);
        }

        var result = Image.Create(dw, dh);
        result.CopyFromBitmap(dstBitmap);
        return result;
    }

    extension(SKBitmap bitmap)
    {
        /// <summary>
        /// Maps the bitmap's <see cref="SKColorType"/> onto the closest
        /// <see cref="Blitter.PixelFormat"/>. Returns
        /// <see cref="Blitter.PixelFormat.BGRA8888"/> when the bitmap's
        /// color type has no direct equivalent.
        /// </summary>
        public Blitter.PixelFormat PixelFormat => bitmap.ColorType switch
        {
            SKColorType.Bgra8888 => Blitter.PixelFormat.BGRA8888,
            SKColorType.Rgba8888 => Blitter.PixelFormat.RGBA8888,
            SKColorType.Rgb888x  => Blitter.PixelFormat.XRGB8888,
            SKColorType.Rgb565   => Blitter.PixelFormat.RGB565,
            SKColorType.Argb4444 => Blitter.PixelFormat.ARGB4444,
            SKColorType.Gray8    => Blitter.PixelFormat.Index8,
            _ => Blitter.PixelFormat.BGRA8888,
        };

        /// <summary>
        /// Creates a new <see cref="Image"/> the same size as the bitmap
        /// and copies its pixels into it.
        /// </summary>
        public BitmapImage ToImage(Blitter.PixelFormat? format = null)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            var image = Image.Create(bitmap.Width, bitmap.Height, format ?? bitmap.PixelFormat);
            image.CopyFromBitmap(bitmap);
            return image;
        }
    }

    extension (SKColor color)
    {
        public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
        {
            r = color.Red;
            g = color.Green;
            b = color.Blue;
            a = color.Alpha;
        }

        public Color ToColor() => new(color.Red, color.Green, color.Blue, color.Alpha);
    }

    extension(Renderer2D renderer)
    {
        /// <summary>
        /// Draws using a SkiaSharp <see cref="SKCanvas"/>.
        /// </summary>
        /// <param name="destination">Where on the renderer the result lands.</param>
        /// <param name="drawAction">Callback that issues canvas draws in canvas-local coordinates (origin at the destination's top-left).</param>
        public void DrawCanvas(Rect destination, Action<SKCanvas> drawAction)
            => DrawCanvasOnRenderer(renderer, destination, null, drawAction);

        /// <summary>
        /// Draws using a SkiaSharp <see cref="SKCanvas"/>,
        /// pre-filled with <paramref name="backgroundColor"/>.
        /// </summary>
        public void DrawCanvas(Rect destination, Blitter.Color backgroundColor, Action<SKCanvas> drawAction)
            => DrawCanvasOnRenderer(renderer, destination, backgroundColor, drawAction);
    }

    /// <summary>
    /// Per-renderer pooled scratch used by <c>Renderer2D.DrawCanvas</c>.
    /// The Image is ABGR8888 and the SKBitmap is Rgba8888 so the byte
    /// layout matches and the canvas-to-image copy hits the single
    /// memcpy fast path with no R/B swap.
    /// </summary>
    private sealed class CanvasDrawState : IDisposable
    {
        public BitmapImage? Image;
        public SKBitmap? Bitmap;
        public SKCanvas? Canvas;

        public void Ensure(int width, int height)
        {
            if (Image is { IsDisposed: false } && Image.Size == (width, height))
                return;

            DisposeResources();
            Image = Blitter.Image.Create(width, height, PixelFormat.ABGR8888);
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            Bitmap = new SKBitmap(info);
            Canvas = new SKCanvas(Bitmap);
        }

        public void Dispose() => DisposeResources();

        private void DisposeResources()
        {
            Canvas?.Dispose();
            Bitmap?.Dispose();
            Image?.Dispose();
            Canvas = null;
            Bitmap = null;
            Image = null;
        }
    }

    private static readonly ConditionalWeakTable<Renderer2D, CanvasDrawState> _renderDrawStateMap = new();

    private static void DrawCanvasOnRenderer(
        Renderer2D renderer,
        Rect destination,
        Blitter.Color? backgroundColor,
        Action<SKCanvas> drawAction)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(drawAction);

        // Round up so partial-pixel destinations still get full coverage.
        int width = (int)Math.Ceiling(destination.Width);
        int height = (int)Math.Ceiling(destination.Height);
        if (width <= 0 || height <= 0)
            return;

        var drawState = _renderDrawStateMap.GetValue(renderer, static r =>
        {
            var s = new CanvasDrawState();
            // Tie the scratch's lifetime to the renderer when possible
            // so native handles (SKBitmap, SKCanvas, Image) are released
            // on Dispose() instead of waiting for finalization.
            if (r is BitmapRenderer2D br)
                br.AddResource(s);
            return s;
        });

        drawState.Ensure(width, height);
        var canvas = drawState.Canvas!;
        var bitmap = drawState.Bitmap!;
        var image = drawState.Image!;

        if (backgroundColor is { } c)
            canvas.Clear(new SKColor(c.R, c.G, c.B, c.A));
        else
            canvas.Clear(SKColors.Transparent);

        drawAction(canvas);
        canvas.Flush();

        image.CopyFromBitmap(bitmap);
        renderer.DrawImage(image, destination);
    }

}

