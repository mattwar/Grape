# Blitter

**Blitter** is a small, friendly 2D & 3D graphics programming library for .NET, built on top of [SDL3](https://www.libsdl.org/) (via the [SDL3-CS](https://github.com/edwardgushchin/SDL3-CS) bindings) and integrated with [SkiaSharp](https://github.com/mono/SkiaSharp). It wraps SDL3 in clean, idiomatic C# so you can focus on drawing things and making them move instead of wrestling with native interop and low-level GPU concepts.

> ⚠️ **Early days.** Blitter is in ongoing development. The API will likely go through changes. Use at your own risk — and have fun.

## What's in the box

- **`Window2D`** - bitmap/sprite-style 2D rendering
- **`Window3D`** - GPU-accelerated 3D rendering
- **SkiaSharp** Integration - Fonts, Filters, Canvas and more
- **Input** - keyboard, mouse, gamepad, and touch via simple events
- **Audio** - load and play WAV data
- **Images** - load, save, manipulate pixels, apply filters
- **Shaders** - load, save, dynamic compilation
- **`Blitter.Bits`** - beyond the basics: useful tidbits for graphical apps
- **`Blitter.Blocks`** - building blocks: sprites, scenes, panels and more

The SDL3 and other native binaries are pulled in automatically; there is nothing to install separately.

## A 2D example

A bouncing red square. The `Rendering` event fires when the window needs to repaint; calling `Invalidate()` schedules the next frame.

```csharp
using Blitter;

var window = new Window2D(800, 600)
{
    Title = "Bouncing Square",
    BackgroundColor = new Color(20, 20, 40),
    CloseKey = Key.Escape
};

float x = 0, vx = 200; // pixels per second

window.Rendering += (w, rd) =>
{
    var dt = (float)rd.ElapsedSinceLastRender.TotalSeconds;
    x += vx * dt;
    if (x < 0 || x > w.Size.Width - 100) vx = -vx;

    rd.DrawColor = new Color(220, 60, 60);
    rd.DrawFillRect(new Rect(x, 250, 100, 100));

    w.Invalidate(); // queue next render to cause animation
};

await window.WaitForCloseAsync();
```

## A 3D example (manual render loop)

A spinning, colored triangle rendered with a built-in shader. Instead of using the `Rendering` event, this version drives frames itself: queue draws on the renderer inside `window.RunAsync(...)`, which paces the loop on a dedicated thread and presents each frame for you. Multiple windows can be composed via `Task.WhenAll`.

```csharp
using System.Numerics;
using Blitter;

var triangle = Mesh.Create<ColorVertex3D>(
[
    new(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
    new(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
    new(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255)),
]);

var window = new Window3D
{
    Title = "Spinning Triangle",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape
};

await window.RunAsync(r =>
{
    var t = (float)r.ElapsedSinceStart.TotalSeconds;
    var (width, height) = window.Size;
    var aspect = (float)height / width;
    var transform =
        Matrix4x4.CreateRotationZ(t) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    r.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
});
```

## Learn more

For full documentation, samples, and shader authoring details, see the project repository:

**https://github.com/mattwar/Blitter**

The [`samples/`](https://github.com/mattwar/Blitter/tree/main/samples) folder contains runnable single-file examples covering meshes, textures, blend/depth/cull modes, debug text, split-screen, render-to-image, and more.

## License

MIT. See [LICENSE](https://github.com/mattwar/Blitter/blob/main/LICENSE) and [THIRD-PARTY-NOTICES.md](https://github.com/mattwar/Blitter/blob/main/THIRD-PARTY-NOTICES.md) in the repository.
