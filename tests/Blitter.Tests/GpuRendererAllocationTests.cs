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
    // Per-frame allocation slope budget (bytes/frame). We measure at two
    // different frame counts and assert on the *marginal* bytes per extra
    // frame. Fixed-cost background noise (xunit infra, finalizer thread,
    // GC bookkeeping, SDL pump) appears in both measurements and cancels
    // out in the subtraction, so the slope reflects only work that actually
    // scales with frame count. The eventual goal is ~0; small positive (or
    // negative) values are jitter.
    private const double PerFrameSlopeBudget = 8.0;

    // Frame counts for the two measurement runs. The gap (high - low)
    // must be large enough that the absolute allocation difference
    // dominates measurement jitter.
    private const int LowFrames = 256;
    private const int HighFrames = 2048;

    [Fact]
    public void DrawMesh_DoesNotAllocatePerFrame()
    {
        // Run from the test thread (not the app thread) so this also
        // covers the cross-thread Application.Current.Send marshaling
        // path. The slope budget assumes that path is alloc-free in
        // steady state.
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

        void RunFrame()
        {
            renderer.Configure(Color.Black);
            renderer.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
            renderer.Render();
        }

        // Warmup: absorb one-time costs (JIT, pipeline cache, command
        // pool growth, mesh GPU upload, scratch buffer rentals). Run
        // past the tiered compilation tier1->tier2 recompilation
        // threshold (~1000 hot calls) so the one-time JIT working-memory
        // allocation doesn't land in the measurement window.
        for (var i = 0; i < 2048; i++)
            RunFrame();

        // Two measurements at different lengths so the slope cancels
        // fixed-cost background noise. Process-wide counter:
        // GetTotalAllocatedBytes(precise: true) sees every thread, which
        // matters because GpuRenderer marshals work onto the app thread.
        var lowBytes = MeasureAllocated(RunFrame, LowFrames);
        var highBytes = MeasureAllocated(RunFrame, HighFrames);

        var slope = (double)(highBytes - lowBytes) / (HighFrames - LowFrames);

        Assert.True(
            slope <= PerFrameSlopeBudget,
            $"Per-frame allocation slope {slope:N2} B/frame exceeds budget {PerFrameSlopeBudget:N2} B/frame " +
            $"(low: {lowBytes:N0} B over {LowFrames} frames; high: {highBytes:N0} B over {HighFrames} frames).");
    }

    private static long MeasureAllocated(Action body, int iterations)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var i = 0; i < iterations; i++)
            body();
        return GC.GetTotalAllocatedBytes(precise: true) - before;
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
            renderer.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
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
            renderer.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
            renderer.Render();
        });

        Assert.Fail(
            $"per-frame breakdown over {N} iters:\n" +
            $"  noop baseline:           {(double)noopBytes / N:N1} B\n" +
            $"  Configure:               {(double)(configureBytes - noopBytes) / N:N1} B\n" +
            $"  DrawMesh + Render:       {(double)(queueRenderBytes - noopBytes) / N:N1} B");
    }
}
