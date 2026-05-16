# Blitter

**Blitter** is a small, friendly 2D & 3D graphics programming library for .NET, built on top of [SDL3](https://www.libsdl.org/) (via the [SDL3-CS](https://github.com/edwardgushchin/SDL3-CS) bindings) and integrated with [SkiaSharp](https://github.com/mono/SkiaSharp). It wraps SDL3 in clean, idiomatic C# so you can focus on drawing things and making them move instead of wrestling with native interop and low-level GPU concepts.

> ⚠️ **Early days.** Blitter is in ongoing development. The API will likely go through changes. Use at your own risk — and have fun.

## What's in the box

- **Window2D** - bitmap/sprite-style 2D rendering
- **Window3D** - GPU-accelerated 3D rendering
- **SkiaSharp** Integration - Fonts, Filters, Canvas and more
- **Input** - keyboard, mouse, gamepad, and touch via simple events
- **Audio** - load, play & mix wav, mp3 and ogg sound files
- **Images** - load, save, manipulate pixels, apply filters
- **Shaders** - load, save, dynamic compilation
- **Blitter.Bits** - beyond the basics: useful tidbits for graphical apps
- **Blitter.Blocks** - building blocks: sprites, scenes, panels and more

## A 2D example

A bouncing red square.

```csharp
using Blitter;

var window = new Window2D(800, 600)
{
    Title = "Bouncing Square",
    BackgroundColor = Color.Black,
    CloseKey = Key.Escape
};

float x = 0, vx = 200; // pixels per second

await window.RunAsync(rd =>
{
    x += vx * rd.ElapsedSecondsSinceLastRender;

    if (x < 0 || x > window.Size.Width - 100) 
        vx = -vx;

    rd.DrawColor = Color.Red;
    rd.DrawFillRect(new Rect(x, 250, 100, 100));
});
```

## A 3D example

A spinning, colored triangle rendered with a built-in shader.

```csharp
using System.Numerics;
using Blitter;

var triangle = Mesh.Create<ColorVertex3D>(
[
    new(new Vertex3D( 0.0f,  0.5f, 0f), Color.Red),
    new(new Vertex3D( 0.5f, -0.5f, 0f), Color.Green),
    new(new Vertex3D(-0.5f, -0.5f, 0f), Color.Blue),
]);

var window = new Window3D
{
    Title = "Spinning Triangle",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape
};

await window.RunAsync(r =>
{
    var transform = Matrix4x4
        .CreateRotationZ(r.ElapsedSecondsSinceStart)
        .Scale(0.8f);

    r.DrawMesh(triangle, transform);
});
```

## Learn more

For full documentation, samples, and shader authoring details, see the project repository:

**https://github.com/mattwar/Blitter**

The [`samples/`](https://github.com/mattwar/Blitter/tree/main/samples) folder contains runnable single-file examples covering meshes, textures, blend/depth/cull modes, debug text, split-screen, render-to-image, and more.

## License

MIT. See [LICENSE](https://github.com/mattwar/Blitter/blob/main/LICENSE) and [THIRD-PARTY-NOTICES.md](https://github.com/mattwar/Blitter/blob/main/THIRD-PARTY-NOTICES.md) in the repository.
