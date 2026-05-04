namespace Grape.Shaders;

/// <summary>A shader-side type. Reference-equal when interned via the type system.</summary>
public abstract class ShaderType
{
    private protected ShaderType() { }
}

public sealed class VoidType  : ShaderType { internal VoidType()  { } }
public sealed class BoolType  : ShaderType { internal BoolType()  { } }
public sealed class IntType   : ShaderType { internal IntType()   { } }
public sealed class UIntType  : ShaderType { internal UIntType()  { } }
public sealed class FloatType : ShaderType { internal FloatType() { } }

public sealed class VectorType(ShaderType component, int n) : ShaderType
{
    public ShaderType Component { get; } = component;
    public int N { get; } = n;
}

public sealed class MatrixType(ShaderType component, int rows, int cols) : ShaderType
{
    public ShaderType Component { get; } = component;
    public int Rows { get; } = rows;
    public int Cols { get; } = cols;
}

/// <summary>Length of 0 means runtime-sized (storage buffer tail).</summary>
public sealed class ArrayType(ShaderType element, int length) : ShaderType
{
    public ShaderType Element { get; } = element;
    public int Length { get; } = length;
}

public sealed class StructField(string name, ShaderType type)
{
    public string Name { get; } = name;
    public ShaderType Type { get; } = type;
}

public sealed class StructType(string name, ImmutableArray<StructField> fields) : ShaderType
{
    public string Name { get; } = name;
    public ImmutableArray<StructField> Fields { get; } = fields;
}

public sealed class Texture2DType      : ShaderType { internal Texture2DType()      { } }
public sealed class Texture3DType      : ShaderType { internal Texture3DType()      { } }
public sealed class TextureCubeType    : ShaderType { internal TextureCubeType()    { } }
public sealed class Texture2DArrayType : ShaderType { internal Texture2DArrayType() { } }
public sealed class SamplerType        : ShaderType { internal SamplerType()        { } }


public abstract class ShaderTypeSystem
{
    public static ShaderType Void  { get; } = new VoidType();
    public static ShaderType Bool  { get; } = new BoolType();
    public static ShaderType Int   { get; } = new IntType();
    public static ShaderType UInt  { get; } = new UIntType();
    public static ShaderType Float { get; } = new FloatType();

    public static ShaderType Texture2D      { get; } = new Texture2DType();
    public static ShaderType Texture3D      { get; } = new Texture3DType();
    public static ShaderType TextureCube    { get; } = new TextureCubeType();
    public static ShaderType Texture2DArray { get; } = new Texture2DArrayType();
    public static ShaderType Sampler        { get; } = new SamplerType();

    public abstract VectorType GetVector(ShaderType component, int n);
    public abstract MatrixType GetMatrix(ShaderType component, int rows, int cols);
    public abstract ArrayType  GetArray(ShaderType element, int length = 0);
    public abstract StructType GetStruct(string name, ImmutableArray<StructField> fields);
}

/// <summary>
/// Default type system. Vectors, matrices, and arrays are structurally
/// interned. Structs are nominal -- each call returns a distinct instance.
/// </summary>
public sealed class StandardShaderTypeSystem : ShaderTypeSystem
{
    private readonly ConcurrentDictionary<(ShaderType Component, int N), VectorType> _vectors = new();
    private readonly ConcurrentDictionary<(ShaderType Component, int Rows, int Cols), MatrixType> _matrices = new();
    private readonly ConcurrentDictionary<(ShaderType Element, int Length), ArrayType> _arrays = new();

    public override VectorType GetVector(ShaderType component, int n)
    {
        if (n is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(n), n, "Vector length must be 2, 3, or 4.");
        if (component is not (BoolType or IntType or UIntType or FloatType))
            throw new ArgumentException("Vector component type must be a scalar.", nameof(component));
        return _vectors.GetOrAdd((component, n), static k => new VectorType(k.Component, k.N));
    }

    public override MatrixType GetMatrix(ShaderType component, int rows, int cols)
    {
        if (rows is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(rows));
        if (cols is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(cols));
        if (component is not FloatType)
            throw new ArgumentException("Matrix component type must be float.", nameof(component));
        return _matrices.GetOrAdd((component, rows, cols), static k => new MatrixType(k.Component, k.Rows, k.Cols));
    }

    public override ArrayType GetArray(ShaderType element, int length = 0)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return _arrays.GetOrAdd((element, length), static k => new ArrayType(k.Element, k.Length));
    }

    public override StructType GetStruct(string name, ImmutableArray<StructField> fields)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Struct name required.", nameof(name));
        if (fields.IsDefaultOrEmpty) throw new ArgumentException("Struct must have at least one field.", nameof(fields));
        return new StructType(name, fields);
    }
}
