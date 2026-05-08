# Grape.Graphics

**Grape** is a small, friendly graphics programming library for .NET, built on top of [SDL3](https://www.libsdl.org/) (via the [SDL3-CS](https://github.com/edwardgushchin/SDL3-CS) bindings) and integrated with [SkiaSharp](https://github.com/mono/SkiaSharp). It wraps SDL3 in clean, idiomatic C# so you can focus on drawing things and making them move instead of wrestling with native interop and low-level GPU concepts.

> ⚠️ **Early days.** Grape is an ongoing project. The API is unstable and will likely change.

## What's in the box

- **`Window2D`** — bitmap/sprite-style 2D rendering
- **`Window3D`** — GPU-accelerated 3D rendering with custom shaders (HLSL/SPIR-V via SDL_shadercross)
- **Input** — keyboard, mouse, gamepad, and touch via simple events
- **Audio** — load and play WAV data
- **Images** — load PNG, JPEG, BMP, etc. (via SkiaSharp); pixel manipulation; SkiaSharp canvas drawing
- **`Grape.Jelly`** — a small experimental scene-graph layer (sprites, props, panels, scenes)

The native SDL3 binaries are pulled in automatically — there is nothing to install separately. Targets **.NET 9**.

## A 2D example

A bouncing red square. The `Rendering` event fires when the window needs to repaint; calling `Invalidate()` schedules the next frame.

```csharp
using Grape;

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

A spinning, colored triangle rendered with a built-in shader. Instead of using `Rendering` event, this version drives frames itself: queue draws on `window.Renderer`, call `Render()` to flush, and `await window.NextFrameAsync()` to pace the loop.

```csharp
using System.Numerics;
using Grape;

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

while (!window.IsClosed)
{
    var rd = window.Renderer;
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = window.Size;
    var aspect = (float)height / width;
    var transform =
        Matrix4x4.CreateRotationZ(t) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    rd.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
    rd.Render();

    await window.NextFrameAsync();
}
```

## Learn more

For full documentation, samples, and shader authoring details, see the project repository:

**https://github.com/mattwar/Grape**

The [`samples/`](https://github.com/mattwar/Grape/tree/main/samples) folder contains runnable single-file examples covering meshes, textures, blend/depth/cull modes, debug text, split-screen, render-to-image, and more.

## License

MIT. See [LICENSE](https://github.com/mattwar/Grape/blob/main/LICENSE) and [THIRD-PARTY-NOTICES.md](https://github.com/mattwar/Grape/blob/main/THIRD-PARTY-NOTICES.md) in the repository.
