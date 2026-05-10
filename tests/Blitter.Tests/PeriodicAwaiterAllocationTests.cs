using Blitter.Utilities;

namespace Blitter.Tests;

[Trait("Category", "Gpu")]
public class PeriodicAwaiterAllocationTests
{
    // Per-iteration allocation slope budget (bytes/iter). We measure at
    // two different iteration counts and assert on the *marginal* bytes
    // per extra iteration. Fixed-cost background noise (xunit infra,
    // finalizer thread, GC bookkeeping, app event loop) appears in both
    // measurements and cancels out in the subtraction, so the slope
    // reflects only work that actually scales with iteration count.
    private const double PerIterationSlopeBudget = 8.0;

    private const int LowIterations = 256;
    private const int HighIterations = 2048;

    [Fact]
    public void Wait_DoesNotAllocatePerIteration()
    {
        // Drive the loop from the test thread; Wait() should not shift
        // us off it. The wake handle is pooled, so steady state is
        // alloc-free.
        using var awaiter = new PeriodicAwaiter(TimeSpan.FromMilliseconds(1));

        // Warmup: pool the handle, JIT, and crucially run past the
        // tiered compilation tier1->tier2 recompilation threshold
        // (~1000 hot calls) so the one-time JIT working-memory
        // allocation doesn't get attributed to a measured frame.
        for (var i = 0; i < 2048; i++)
            awaiter.Wait();

        var lowBytes = MeasureAllocated(() => awaiter.Wait(), LowIterations);
        var highBytes = MeasureAllocated(() => awaiter.Wait(), HighIterations);

        var slope = (double)(highBytes - lowBytes) / (HighIterations - LowIterations);

        Assert.True(
            slope <= PerIterationSlopeBudget,
            $"Per-iteration allocation slope {slope:N2} B/iter exceeds budget {PerIterationSlopeBudget:N2} B/iter " +
            $"(low: {lowBytes:N0} B over {LowIterations} iters; high: {highBytes:N0} B over {HighIterations} iters).");
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

    [Fact(Skip = "Diagnostic only; remove Skip to run.")]
    public void Wait_AllocationBreakdown()
    {
        // Diagnostic: measure awaiter.Wait() at several iteration counts
        // to see how allocations scale. Always fails so the numbers
        // appear in the test output.
        using var awaiter = new PeriodicAwaiter(TimeSpan.FromMilliseconds(1));
        for (var i = 0; i < 16; i++)
            awaiter.Wait();

        int[] sizes = [128, 256, 512, 1024, 2048];
        long[] bytes = new long[sizes.Length];
        long[] elapsedMs = new long[sizes.Length];

        for (var i = 0; i < sizes.Length; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bytes[i] = MeasureAllocated(() => awaiter.Wait(), sizes[i]);
            elapsedMs[i] = sw.ElapsedMilliseconds;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("awaiter.Wait() scaling:");
        for (var i = 0; i < sizes.Length; i++)
        {
            sb.AppendLine(
                $"  {sizes[i],6} iters: {bytes[i],10:N0} B total, " +
                $"{(double)bytes[i] / sizes[i],8:N2} B/iter, " +
                $"{elapsedMs[i],5} ms wall ({(double)bytes[i] / Math.Max(1, elapsedMs[i]),8:N1} B/ms)");
        }
        Assert.Fail(sb.ToString());
    }
}
