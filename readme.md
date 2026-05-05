# Grape <img src="assets/grape.png" alt="Grape logo" height="48" align="center" />

[![CI](https://github.com/mattwar/Grape/actions/workflows/CI.yml/badge.svg)](https://github.com/mattwar/Grape/actions/workflows/CI.yml)
[![NuGet](https://img.shields.io/nuget/v/Grape.Graphics.svg?logo=nuget)](https://www.nuget.org/packages/Grape.Graphics)
[![Downloads](https://img.shields.io/nuget/dt/Grape.Graphics.svg?logo=nuget)](https://www.nuget.org/packages/Grape.Graphics)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)

**Grape** is a small, friendly graphics programming library for .NET, built on top of [SDL3](https://www.libsdl.org/) and integrated with [SkiaSharp](https://github.com/mono/SkiaSharp). It wraps many of SDL3's APIs in clean, idiomatic C# classes — and bridges SkiaSharp (which draws into bitmaps) so the pixels you paint with Skia end up on screen — so you can focus on drawing things and making them move instead of wrestling with native interop and low level GPU concepts.

> ⚠️ **Early days.** Grape is an ongoing project. The API is currently unstable and will likely change. Use at your own risk — and have fun.

## Why Grape?

SDL3 is fantastic, but using it directly from C# means a lot of P/Invoke, unsafe code, and boilerplate. Grape exists to give .NET developers a small, approachable surface for:

- Opening a window
- Drawing 2D sprites and primitives
- Drawing 3D meshes with custom shaders
- Reading keyboard, mouse, gamepad, and touch input
- Playing audio
- Loading images

…all without ever having to think about pointers or marshalling or interacting directly with the GPU.

## Features

- **`Window2D`** — bitmap/sprite-style 2D rendering
- **`Window3D`** — GPU-accelerated 3D rendering with custom shaders (compiled via SDL_shadercross from HLSL/SPIR-V)
- **Input** — keyboard, mouse, gamepad, and touch via simple events
- **Audio** — load and play WAV data
- **Images** — load PNG, JPEG, BMP, and more (via SkiaSharp); manipulate pixels; set color keys; draw with the SkiaSharp canvas API.
- **`Grape.Vine`** — a tiny scene-graph layer (sprites, props, panels, scenes) on top of `Window2D`. This is really experimental and may get removed.

## Installation

Grape is published as a NuGet package:

```sh
dotnet add package Grape.Graphics
```

The native SDL3 binaries are pulled in automatically via the `SDL3-CS.Native` and `SDL3-CS.Native.Shadercross` package dependencies — there is nothing to install separately.

Targets **.NET 9**.

## Hello, Window

```csharp
using Grape;

var window = new Window2D(800, 600)
{
    Title = "Hello, Grape",
    BackgroundColor = new Color(20, 20, 40)
};

window.KeyDown += (_, e) =>
{
    if (e.Key == Key.Escape)
        Application.Current.Dispose();
};

// Drive redraws at ~60Hz until the window closes.
var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
while (!window.IsDisposed)
{
    await timer.WaitForNextTickAsync();
    window.Invalidate();
}
```

## A 2D Sprite (using Grape.Vine)

```csharp
using Grape;
using Grape.Vine;

var window = new Window2D(800, 600) { Title = "Rocket" };

var image = Image.LoadImage("rocket.png");
image.SetAlpha(0, image.GetPixel(0, 0)); // color-key the background

var rocket = new Sprite(image, 400, 300, 0.2f)
{
    Speed = 600f,
    Heading = 45f
};

// ...attach to a scene, hook up input, drive the loop with PeriodicTimer + Invalidate.
```

See [src/Examples/2D/RicochetRocketExample.cs](src/Examples/2D/RicochetRocketExample.cs) for the full version.

## A 3D Triangle Swarm

```csharp
using System.Collections.Immutable;
using System.Numerics;
using Grape;

var triangle = ImmutableArray.Create(
    new ColorVertex3D(new Vertex3D( 0.0f,  0.12f, 0f), new Color(255,   0,   0)),
    new ColorVertex3D(new Vertex3D( 0.10f, -0.08f, 0f), new Color(  0, 255,   0)),
    new ColorVertex3D(new Vertex3D(-0.10f, -0.08f, 0f), new Color(  0,   0, 255)));

var window = new Window3D(800, 600) { Title = "Triangle Swarm" };

window.RenderingFrame += (_, renderer) =>
{
    var transform = Matrix4x4.CreateRotationZ((float)Environment.TickCount / 1000f);
    renderer.RenderMesh(triangle, Shaders.PositionColorTransform, transform);
};

var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
while (!window.IsDisposed)
{
    await timer.WaitForNextTickAsync();
    window.Invalidate();
}
```

More examples live in [src/Examples](src/Examples).

## Project Layout

| Project | What it is |
| --- | --- |
| `Grape` | Core library: windows, rendering (BMP), input, audio. |
| `Grape.SkiaSharp` | SkiaSharp integration: PNG/JPEG/etc. image loading, Skia canvas drawing into Grape images. |
| `Grape.Graphics` | Packaging project — bundles `Grape` + `Grape.SkiaSharp` into a single NuGet package. |
| `Grape.Vine` | Scene-graph helpers (sprites, scenes, panels) on top of `Grape`. |
| `Examples` | Runnable 2D and 3D sample programs. |

## Building from Source

```sh
git clone https://github.com/mattwar/Grape.git
cd Grape/src
dotnet build Grape.sln
dotnet run --project Examples
```

To produce a NuGet package locally:

```sh
dotnet pack src/Grape.Graphics/Grape.Graphics.csproj -c Release -o artifacts/nuget
```

## Status & Roadmap

Grape is pre-1.0 and changes frequently. Expect rough edges, missing features, and the occasional API rename. Issues and pull requests are welcome, but please don't depend on it for anything important yet.

## Acknowledgments

Grape stands on the shoulders of some excellent open-source projects:

- [SDL3](https://www.libsdl.org/) by Sam Lantinga and the SDL contributors — the cross-platform foundation for windowing, GPU, input, and audio.
- [SDL_shadercross](https://github.com/libsdl-org/SDL_shadercross) — runtime HLSL/SPIR-V shader translation used by `Window3D`.
- [SDL3-CS](https://github.com/flibitijibibo/SDL3-CS) by Colin Jackson — the C# bindings for SDL3.
- [SkiaSharp](https://github.com/mono/SkiaSharp) — image decoding (PNG, JPEG, etc.) and 2D canvas drawing into bitmaps. Grape bridges those bitmaps onto the screen.
- [Nito.AsyncEx](https://github.com/StephenCleary/AsyncEx) by Stephen Cleary — async coordination primitives.

Full copyright notices and license texts are reproduced in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License

[MIT](LICENSE). Third-party components retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
