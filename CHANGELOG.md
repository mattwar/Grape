# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- glTF 2.0 (`.glb` / `.gltf`) loader via SharpGLTF; `Model.Load` now
  dispatches `.glb`/`.gltf` paths through it.
- glTF v1 loads geometry, base-color factor, and base-color texture
  only (no animations, skinning, morph targets, or full PBR yet).
- `Meshes` class with built-in mesh generators for cubes, planes, spheres,
  cylinders, cones, capsules, tori, platonic solids, axes, grids,
  rectangles, circles, and ellipses.
- `Mesh<T>.Transform(matrix)` bakes a transform into vertex positions
  (and properly transforms normals via the inverse-transpose).
- `Mesh<T>.Concat(other)` and `Concat(other, transform)` combine two
  meshes into one for static-batching / mesh composition.
- `Mesh<T>.Update(vertices)` replaces vertex data while keeping the
  existing index buffer; bumps `Version` so the renderer re-uploads.
- `IUpdateContext` / `IUpdateContext2D` / `IUpdateContext3D` interfaces
  with matching `UpdateContext` / `UpdateContext2D` / `UpdateContext3D`
  structs for typed per-frame inputs.
- `IUpdatable<TCtx>` and `IDrawable2D` / `IDrawable3D` contracts for
  stateful objects that participate in update/render loops.
- `Renderer2D.GetUpdateContext()` returns an `UpdateContext2D`;
  `Renderer3D.GetUpdateContext()` returns an `UpdateContext3D`.
- `CameraController` abstract base for camera-driving controllers;
  implements `IUpdatable<UpdateContext3D>` + `IDrawable3D` and owns a
  `Camera` property.
- `CameraOrbiter` — mouse-drag yaw/pitch + scroll zoom around a target.
- `CameraFlyer` — WASD + Q/E + mouse-look free 6-DOF camera.
- `CameraWalker` — first-person ground-walker (WASD + mouse-look,
  movement on the horizontal plane).
- `CameraFollower` — exponentially-smoothed follow camera tracking a
  moving target.
- `Renderer2D.DrawCanvas(rect, [background,] action)` draws via a
  SkiaSharp `SKCanvas`; scratch bitmap/canvas/image pooled per renderer.
- `SKBitmap.ToImage()` extension snapshots a Skia bitmap into a Blitter
  `Image` for use with `DrawImage` (one GPU upload, reused per call).
- `Blitter.Bits.Atlas` pairs an `Image` with a list of pixel-space
  rectangles (and an optional name → index map) for sprite-sheet draws.
- `Atlas.Grid(image, columns, rows)` slices an image into a uniform
  grid of cells indexed in row-major order.
- `Blitter.Bits.Font` bakes a SkiaSharp typeface into a monospace glyph
  atlas with `DrawText` for both `Renderer2D` and `Renderer3D`.
- `Font.Load(path|stream, ...)` loads a `.ttf`/`.otf` file for portable
  cross-platform rendering; `Font(IEnumerable<string>, ...)` takes a
  CSS-style family-fallback list and uses the first installed match.
- `Font` constructors take an optional `charset` string for arbitrary
  Unicode coverage (default `FontCharsets.AsciiPrintable`); surrogate
  pairs in input strings are handled via `Rune` iteration.
- `FontCharsets` exposes reusable charset constants (`AsciiPrintable`,
  `Digits`, `UppercaseLatin`, `LowercaseLatin`).
- New samples: `SkiaCanvas`, `SkiaBitmap`, `FontText` showcasing the
  SkiaSharp rendering integration and `Blitter.Bits.Font`.

### Changed
- Renamed `Mesh<T>.Reset` to `Mesh<T>.Update`.
- Renamed `Image.RenderCanvas` to `Image.DrawCanvas` for naming
  consistency with the rest of the `Draw*` family.
- Default window size (parameterless `Window2D` / `Window3D` ctor) is
  now half the primary display's usable bounds instead of 100x100.
- `GpuRenderer` now converts non-native-format textures to `ABGR8888`
  for upload instead of throwing, with a one-time warning per format.
- `Image.Decode` now allocates surfaces in `ABGR8888` (the GPU fast
  format) regardless of the source bitmap's color type.
- Jelly `Prop` now implements `IUpdatable<UpdateContext2D>` and
  `IDrawable2D`; `Update` takes `UpdateContext2D` instead of
  `UpdateContext`.

### Fixed
- Texture upload failures no longer tear down the entire frame: the
  failing image is logged once and affected draws are skipped.

### Removed
- `Mesh<T>` constructors and `Reset` overload that took
  `ImmutableArray<T>`. Build the mesh once with `Mesh.Create(...)` and,
  if dynamic, call `mesh.Update(vertices)` to push new contents.
- `Mesh<T>` constructors are now `internal`. Construct meshes via
  `Mesh.Create(...)`; this also gets type-inference from collection
  expressions.
- `Renderer3D.DrawMesh` extension overloads that took raw vertex arrays
  / `ImmutableArray<T>`. Wrap your vertices in a `Mesh<T>` once via
  `Mesh.Create(...)` and pass the mesh; this keeps the renderer's
  GPU-buffer cache working across frames.

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
- `AsyncPeriodicTimer` now uses `Stopwatch` for monotonic cadence.
- `AsyncPeriodicTimer` gained a `Reset()` method.

[0.2.0]: https://github.com/mattwar/Blitter/releases/tag/v0.2.0
[0.1.1]: https://github.com/mattwar/Blitter/releases/tag/v0.1.1
