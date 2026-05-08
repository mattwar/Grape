# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- `Meshes` class with built-in mesh generators for cubes, planes, spheres,
  cylinders, cones, capsules, tori, platonic solids, axes, grids,
  rectangles, circles, and ellipses.
- `Mesh<T>.Transform(matrix)` bakes a transform into vertex positions
  (and properly transforms normals via the inverse-transpose).
- `Mesh<T>.Concat(other)` and `Concat(other, transform)` combine two
  meshes into one for static-batching / mesh composition.

## [0.2.0] - 2026-05-07

### Added
- `Image.Load(path, mipmaps)` -- single entry point for image files.
- `Image.Decode(ReadOnlySpan<byte>, mipmaps)` for in-memory image bytes.
- `Model.Load(path)` and `.obj` / `.mtl` loaders.
- OBJ loader smooths per-position normals when `vn` is absent.
- Depth buffer + per-draw `DepthMode` (`Default`/`Transparent`/`Overlay`).
- Index buffers via `Mesh.Create(verts, indices)` (transparent indexed draws).
- Backface culling: `Renderer3D.CullMode` (`None`/`Back`/`Front`).
- Per-mesh `Topology` (`TriangleList`/`Strip`/`LineList`/`LineStrip`/`Points`).
- `Renderer3D.Wireframe` toggle deriving deduped edge indices on demand.
- Per-draw `BlendMode` (`Alpha`/`Opaque`/`Additive`/`Multiply`).
- `Renderer3D.Viewport` and `ClipRect` (scissor).
- Hardware instancing via `InstancedShaderSet<TVertex,TArgs,TInstance>` and `Renderer3D.DrawMesh(... instances)`.
- Built-in instanced shaders: `PositionInstanced`, `PositionColorInstanced`, `PositionTextureInstanced`.
- Mipmap chains + anisotropic filtering via `Image.Mipmaps` flag (auto-generated on the GPU).
- `Cubemap` type + `Image.Flip` / `Image.Rotate` helpers + `Shaders.Skybox` shader set.
- MSAA via `Renderer3D.Antialiasing` (`None`/`X2`/`X4`/`X8`).
- `Camera3D` (`PerspectiveCamera`, `OrthographicCamera`) integrated as `Renderer3D.Camera`.
- Lighting: `Renderer3D.AmbientLight`, `DirectionalLight`, `PointLights` consumed via `IUniformArgs<TSelf>`.
- Built-in lit shaders: `ShaderSets.LitColor`, `ShaderSets.LitTexture` with `LitArgs`.
- `PushState()` returning a scoped `StateScope` snapshotting all renderer state.
- New samples: `OrbitingTetrahedra`, `OverlayTetrahedron`, `CullingComparison`, `IndexedCube`, `LinesAndTriangles`, `WireframeCube`, `TriangleSwarm`, `TriangleSwarmInstanced`, `MipmapsAnimated`, `Skybox`, `Antialiasing`, `BlendModes`, `SplitScreen3D`, `ClippedScene`, `LitCube`, `OrbitingLight`, `PointLights`, `LoadObjModel`, `StanfordBunny`.

### Changed
- Folded `Grape.SkiaSharp` into `Grape`; SkiaSharp is now a core dep.
- `Image.Load` dispatches by extension: `.bmp` via SDL, others via SkiaSharp.
- `Image.Save` dispatches by extension: `.bmp` via SDL, `.png`/`.jpg`/`.webp` via SkiaSharp.
- Reorganised source into `Grape.Devices`, `Grape.Events`, `Grape.Shaders` namespaces.
- Split monolithic `Events.cs`, `GpuDevice.cs`, and `Shader.cs` into per-type files.
- Flattened `Graphics/` and `System/` subfolders into the project root.

### Removed
- `Image.LoadBitmap` (use `Image.Load` instead).
- Standalone `Grape.SkiaSharp` project (its types live in `Grape` now).

## [0.1.1] - 2026-05-04

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
- Package-scoped `README.md` rendered on the nuget.org package page.

### Changed
- Renderers now expose `Draw*` to queue work and `Render()` to flush a frame.
- Frame timings and `MaxFrameDelta` moved from event args onto the renderer.
- `WindowRenderEventArgs<T>` removed; `Rendering` delivers `(Window, Renderer)`.
- Windows expose a public `Renderer`; `Window.Render(Action<...>)` overloads removed.
- Render loop paced by `Window.MinRenderInterval` (default ~60 Hz).
- `Invalidate()` calls within one tick coalesce into a single render.
- `Window.NextFrameAsync()` lets manual loops share the same cadence.
- Renderers gained `BackgroundColor` and an `AutoClear` toggle.
- `Grape.Jelly` props now expose `Draw(Renderer2D)` instead of `Render(...)`.
- `AsyncPeriodicTimer` now uses `Stopwatch` for monotonic cadence.
- `AsyncPeriodicTimer` gained a `Reset()` method.

[0.2.0]: https://github.com/mattwar/Grape/releases/tag/v0.2.0
[0.1.1]: https://github.com/mattwar/Grape/releases/tag/v0.1.1
