using System.Numerics;

namespace Blitter.Tests;

// Allocation-regression tests for the GPU renderer. Drives a real
// ImageGpuRenderer (no window required) so the test exercises the
// same draw/queue/present path the production renderer uses.
//
// Tagged Gpu so machines without a usable graphics driver can filter
// it out:  dotnet test --filter "Category!=Gpu"
[Trait("Category", "Gpu")]
public class GpuRendererAllocationTests
{
    // Per-frame allocation budget (bytes). The eventual goal is 0 (or
    // close to it); the current value is a baseline above today's
    // measured per-frame allocations, set so the test catches *new*
    // regressions while existing allocations are tracked down. Ratchet
    // this number down as those leaks are fixed.
    // Per-frame allocation budget (bytes). The eventual goal is 0 (or
    // close to it); the current value is a baseline above today's
    // measured per-frame allocations, set so the test catches *new*
    // regressions while existing allocations are tracked down. Ratchet
    // this number down as those leaks are fixed.
    private const long PerFrameAllocationBudget = 64;

    [Fact]
    public void DrawMesh_DoesNotAllocatePerFrame()
    {
        // Run from the test thread (not the app thread) so this also
        // covers the cross-thread Application.Current.Send marshaling
        // path. The allocation budget assumes that path is alloc-free
        // in steady state.
        RunAllocationTest();
    }

    private static void RunAllocationTest()
    {
        // A single colored triangle, uploaded once. Re-drawn every
        // frame to keep the queue non-empty without changing mesh
        // version (which would force a GPU re-upload).
        var triangle = Mesh.Create(new[]
        {
            new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
            new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
            new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255)),
        });

        using var image = Image.Create(64, 64);
        using var renderer = new ImageGpuRenderer(GpuDevice.Default, image);

        var transform = Matrix4x4.CreateScale(0.8f);

        // Warmup: absorb one-time costs (JIT, pipeline cache, command
        // pool growth, mesh GPU upload, scratch buffer rentals).
        const int WarmupFrames = 8;
        for (var i = 0; i < WarmupFrames; i++)
        {
            renderer.Configure(Color.Black);
            renderer.DrawMesh(triangle, ShaderSets.PositionColorWithTransform, transform);
            renderer.Render();
        }

        // Measured run.
        const int MeasuredFrames = 64;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Process-wide counter: GpuRenderer marshals the actual render
        // work onto the application thread via Application.Current.Send,
        // so a per-thread counter would miss the bulk of the allocations.
        // GetTotalAllocatedBytes(precise: true) sees every thread.
        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var i = 0; i < MeasuredFrames; i++)
        {
            renderer.Configure(Color.Black);
            renderer.DrawMesh(triangle, ShaderSets.PositionColorWithTransform, transform);
            renderer.Render();
        }
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        var perFrame = (double)allocated / MeasuredFrames;
        Assert.True(
            perFrame <= PerFrameAllocationBudget,
            $"Per-frame allocation {perFrame:N1} bytes exceeds budget {PerFrameAllocationBudget} bytes " +
            $"({allocated:N0} bytes over {MeasuredFrames} frames).");
    }

    // Diagnostic test that splits the per-frame work into Configure /
    // DrawMesh / Render, so we can see which phase is responsible for
    // any remaining per-frame allocation. Always fails (it just reports
    // numbers); use --filter "DisplayName~Breakdown" when investigating.
    [Fact(Skip = "Diagnostic only; remove Skip to run.")]
    public void DrawMesh_AllocationBreakdown()
    {
        Application.Current.Send(_ => RunBreakdown(), null);
    }

    private static void RunBreakdown()
    {
        var triangle = Mesh.Create(new[]
        {
            new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
            new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
            new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255)),
        });

        using var image = Image.Create(64, 64);
        using var renderer = new ImageGpuRenderer(GpuDevice.Default, image);
        var transform = Matrix4x4.CreateScale(0.8f);

        for (var i = 0; i < 8; i++)
        {
            renderer.Configure(Color.Black);
            renderer.DrawMesh(triangle, ShaderSets.PositionColorWithTransform, transform);
            renderer.Render();
        }

        const int N = 1024;
        long Measure(Action act)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var before = GC.GetTotalAllocatedBytes(precise: true);
            for (var i = 0; i < N; i++) act();
            return GC.GetTotalAllocatedBytes(precise: true) - before;
        }

        var noopBytes = Measure(() => { });
        var configureBytes = Measure(() => renderer.Configure(Color.Black));
        var queueRenderBytes = Measure(() =>
        {
            renderer.DrawMesh(triangle, ShaderSets.PositionColorWithTransform, transform);
            renderer.Render();
        });

        Assert.Fail(
            $"per-frame breakdown over {N} iters:\n" +
            $"  noop baseline:           {(double)noopBytes / N:N1} B\n" +
            $"  Configure:               {(double)(configureBytes - noopBytes) / N:N1} B\n" +
            $"  DrawMesh + Render:       {(double)(queueRenderBytes - noopBytes) / N:N1} B");
    }
}
