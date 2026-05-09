// xUnit defaults to running test classes in parallel within an
// assembly. Blitter's Application is process-singleton state (SDL only
// supports one), so concurrent tests that touch Application/Image/
// Renderer can race. Force serial execution.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
