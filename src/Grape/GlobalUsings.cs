global using SDL3;

// Tests reach into a few internals (SpirvReflection, etc.); production
// callers don't, and shouldn't.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Grape.Tests")]
