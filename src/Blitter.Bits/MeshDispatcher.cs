using System.Collections.Concurrent;
using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Adapter that knows how to call the strongly-typed
/// <see cref="Renderer3D"/> draw entry points for a specific
/// <c>TVertex</c>. Lets non-generic dispatch code (a
/// <see cref="Materializer"/>) hand a base <see cref="Mesh"/> off to the
/// renderer without per-call reflection.
/// </summary>
internal interface IMeshDrawAdapter
{
    /// <summary>
    /// Calls <see cref="Renderer3D.DrawMesh{TVertex,TArgs}(Mesh{TVertex}, Image, Shader{TVertex,TArgs}, in TArgs)"/>
    /// after casting <paramref name="mesh"/> and <paramref name="shader"/> to
    /// the adapter's vertex/args types.
    /// </summary>
    void DrawTextured<TArgs>(
        Renderer3D renderer,
        Mesh mesh,
        Image texture,
        Shader shader,
        in TArgs args)
        where TArgs : unmanaged, IUniformArgs<TArgs>;

    /// <summary>
    /// Calls the scene-aware textured instanced draw entry point on
    /// <see cref="Renderer3D"/> after casting <paramref name="mesh"/>
    /// and <paramref name="shader"/> to the adapter's vertex/args/instance
    /// types. Throws <see cref="InvalidCastException"/> if the supplied
    /// shader's instance vertex layout doesn't match
    /// <typeparamref name="TInstance"/>.
    /// </summary>
    void DrawTexturedInstanced<TArgs, TInstance>(
        Renderer3D renderer,
        Mesh mesh,
        Image texture,
        Shader shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
        where TArgs : unmanaged, IUniformArgs<TArgs>
        where TInstance : unmanaged;
}

internal sealed class MeshDrawAdapter<TVertex> : IMeshDrawAdapter
    where TVertex : unmanaged
{
    public void DrawTextured<TArgs>(
        Renderer3D renderer,
        Mesh mesh,
        Image texture,
        Shader shader,
        in TArgs args)
        where TArgs : unmanaged, IUniformArgs<TArgs>
    {
        var typedMesh = (Mesh<TVertex>)mesh;
        var typedShader = (Shader<TVertex, TArgs>)shader;
        renderer.DrawMesh(typedMesh, texture, typedShader, in args);
    }

    public void DrawTexturedInstanced<TArgs, TInstance>(
        Renderer3D renderer,
        Mesh mesh,
        Image texture,
        Shader shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
        where TArgs : unmanaged, IUniformArgs<TArgs>
        where TInstance : unmanaged
    {
        var typedMesh = (Mesh<TVertex>)mesh;
        var typedShader = (Shader<TVertex, TArgs, TInstance>)shader;
        renderer.DrawMesh(typedMesh, texture, typedShader, in args, instances);
    }
}

/// <summary>
/// Caches one <see cref="IMeshDrawAdapter"/> per encountered vertex type.
/// </summary>
/// <remarks>
/// Adapter construction uses <see cref="Activator.CreateInstance(Type)"/>
/// over a closed generic type. Cheap on JIT; hostile to NativeAOT/trimming.
/// If AOT becomes a concern, expose a <c>Register&lt;TVertex&gt;()</c> path
/// callers can invoke at startup.
/// </remarks>
public static class MeshDispatcher
{
    private static readonly ConcurrentDictionary<Type, IMeshDrawAdapter> _adapters = new();

    internal static IMeshDrawAdapter For(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return _adapters.GetOrAdd(mesh.VertexType, CreateAdapter);
    }

    private static IMeshDrawAdapter CreateAdapter(Type vertexType)
    {
        var adapterType = typeof(MeshDrawAdapter<>).MakeGenericType(vertexType);
        return (IMeshDrawAdapter)Activator.CreateInstance(adapterType)!;
    }
}
