using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// Direct P/Invoke shims for SDL functions whose SDL3-CS bindings (3.2.20)
/// only expose managed array overloads. These accept raw pointers so we can
/// pass pinned managed memory directly without an intermediate copy.
/// </summary>
internal static partial class SDL3Native
{
    private const string SDLLibrary = "SDL3";

    [LibraryImport(SDLLibrary, EntryPoint = "SDL_RenderLines")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static unsafe partial bool SDL_RenderLines(IntPtr renderer, void* points, int count);

    [LibraryImport(SDLLibrary, EntryPoint = "SDL_RenderPoints")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static unsafe partial bool SDL_RenderPoints(IntPtr renderer, void* points, int count);

    [LibraryImport(SDLLibrary, EntryPoint = "SDL_RenderFillRects")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static unsafe partial bool SDL_RenderFillRects(IntPtr renderer, void* rects, int count);
}
