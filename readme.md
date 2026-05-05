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
- **`Grape.Jelly`** — a tiny scene-graph layer (sprites, props, panels, scenes) on top of `Window2D`. This is really experimental and may get removed.

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
    BackgroundColor = new Color(20, 20, 40),
    CloseKey = Key.Esc
};

await window.WaitForCloseAsync();
```

## Animating with `Rendering` event

Grape allows you to drive animation through the `Rendering` event. Call `Invalidate()` from your handler to schedule the next frame.

```csharp
using Grape;

var window = new Window2D(800, 600) { Title = "Animation", CloseKey = Key.Esc };

window.Rendering += (w, frame) =>
{
    // update similution and use frame.Renderer to draw
    ...
    w.Invalidate(); // schedule the next frame
};

await window.WaitForCloseAsync();
```

## Animating with a manual rendering loop

Grape also allows you to take control of everything and do it your own way.

```csharp
using Grape;

var window = new Window2D(800, 600) { Title = "Animation", CloseKey = Key.Esc };

// use periodic timer to avoid 100% CPU, and have even intervals between frames
var timer = new AsyncPeriodicTimer(TimeSpan.FromSeconds(1.0 / 60));

while (!window.IsClosed)
{
    // update simulation
    ...

    window.Render(frame =>
    {
        // draw using frame.Renderer
    });

    await timer.NextPeriod();
};
```

## A 3D Triangle Swarm

```csharp
using System.Collections.Immutable;
using System.Numerics;
using Grape;

var triangle = ImmutableArray.Create(
    new ColorVertex3D(new Vertex3D( 0.0f,  0.12f, 0f), new Color(255,   0,   0)),
    new ColorVertex3D(new Vertex3D( 0.10f, -0.08f, 0f), new Color(  0, 255,   0)),
    new ColorVertex3D(new Vertex3D(-0.10f, -0.08f, 0f), new Color(  0,   0, 255)));

var window = new Window3D(800, 600) { Title = "Triangle Swarm", CloseKey = Key.Esc };

window.RenderingFrame += (w, frame) =>
{
    var t = (float)frame.ElapsedSinceWindowCreated.TotalSeconds;
    var transform = Matrix4x4.CreateRotationZ(t);
    frame.Renderer.RenderMesh(triangle, transform);
    w.Invalidate();
};

await window.WaitForCloseAsync();
```

## A 2D Sprite (using Grape.Jelly)

Grape.Jelly defines some higher level concepts like sprites, panels and scenes 
that let you compose and layer graphic elements together.

The sprite implements it own update and rending logic and moves itself across frames.

```csharp
using Grape;
using Grape.Jelly;

var window = new Window2D(800, 600) { Title = "Rocket", CloseKey = Key.Esc };

var image = Image.LoadImage("rocket.png");
image.SetAlpha(0, image.GetPixel(0, 0)); // color-key the background

var rocket = new Sprite(image, 400, 300, 0.2f)
{
    Speed = 600f,
    Heading = 45f
};

window.Rendering += (w, frame) =>
{
    var ctx = new UpdateContext
    {
        ElapsedSinceStart = frame.ElapsedSinceWindowCreated,
        ElaspsedSinceLastUpdate = frame.ElapsedSinceLastFrame,
        Bounds = new Rect(0, 0, w.Size.Width, w.Size.Height),
    };
    rocket.Update(ctx);
    rocket.Render(frame.Renderer);
    w.Invalidate();
};

await window.WaitForCloseAsync();
```

See [samples/RicochetRocket.cs](samples/RicochetRocket.cs) for the full version.


More examples live in [samples/](samples/) — each is a single `.cs` file you can run directly with `dotnet run samples/<name>.cs` (.NET 10+).

## Project Layout

| Project | What it is |
| --- | --- |
| `Grape` | Core library: windows, rendering (BMP), input, audio. |
| `Grape.SkiaSharp` | SkiaSharp integration: PNG/JPEG/etc. image loading, Skia canvas drawing into Grape images. |
| `Grape.Graphics` | Packaging project — bundles `Grape`, `Grape.SkiaSharp`, and `Grape.Jelly` into a single NuGet package. |
| `Grape.Jelly` | Scene-graph helpers (sprites, scenes, panels) on top of `Grape`. |

## Building from Source

```sh
git clone https://github.com/mattwar/Grape.git
cd Grape
dotnet build src/Grape.sln
dotnet run samples/TriangleSwarm.cs
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
