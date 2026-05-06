# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Render-loop redesign.** `Renderer2D` and `Renderer3D` now expose
  `Draw*` methods to queue work and `Render()` to flush a frame
  (acquire/encode/submit/present). The previous `Render*` queue methods
  and `Present()` flush are gone.
- Frame timings (`ElapsedSinceStart`, `ElapsedSinceLastRender`) and the
  per-frame clamp (`MaxFrameDelta`) live on the renderer. The
  `WindowRenderEventArgs<T>` wrapper has been removed; the `Rendering`
  event delivers `(Window, Renderer2D|Renderer3D)` directly.
- `Window2D` and `Window3D` expose a public `Renderer` property; the
  `Window.Render(Action<...>)` overloads have been removed in favor of
  using `window.Renderer.Render()` (which marshals to the application
  thread automatically).
- The window render loop is now paced by `Window.MinRenderInterval`
  (default ~16.67 ms / 60 Hz). `Invalidate()` requests a render; multiple
  invalidations within one interval coalesce. `Window.NextFrameAsync()`
  is exposed for manual loops to share the same cadence.
- `Renderer2D` / `Renderer3D` gained `BackgroundColor` (set via the
  window) and an `AutoClear` toggle. 2D clears lazily on first draw of
  the frame; 3D toggles `LoadOp.Clear`/`Load`.
- `Grape.Jelly` props now expose `Draw(Renderer2D)` instead of
  `Render(Renderer2D)` to align with the renderer verb model.
- `AsyncPeriodicTimer` switched from `DateTime.UtcNow` to `Stopwatch`
  (monotonic) for steadier cadence; gained a `Reset()` method.

## [0.1.0] - 2026-05-04

### Added
- Initial release of `Grape.Graphics` NuGet package.
- `Window2D` and `Window3D` classes wrapping SDL3.
- 2D rendering (`Renderer2D`, `BitmapRenderer2D`), 3D rendering (`Renderer3D`),
  shaders, meshes, vertices, and image loading.
- Input: keyboard, mouse, gamepad, touch.
- Audio: WAV loading and playback.
- Companion `Grape.Jelly` scene-graph helpers and `Grape.SkiaSharp` integration
  (not yet packaged).
- GitHub Actions workflows for CI and release-on-tag publishing.
- Third-party attribution in `THIRD-PARTY-NOTICES.md`.

[Unreleased]: https://github.com/mattwar/Grape/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/mattwar/Grape/releases/tag/v0.1.0
