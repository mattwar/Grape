namespace Grape.Shaders.Emitters.Spv;

/// <summary>
/// SPIR-V module-level magic numbers and version words.
/// SPIR-V 1.0 / Vulkan 1.0 environment.
/// </summary>
internal static class SpvHeader
{
    public const uint Magic     = 0x07230203u;
    /// <summary>SPIR-V 1.0 version word: major=1, minor=0.</summary>
    public const uint Version10 = 0x00010000u;
    /// <summary>Generator ID (0 = unknown / unregistered tool).</summary>
    public const uint Generator = 0u;
    /// <summary>Schema reserved word (always 0 in 1.x).</summary>
    public const uint Schema    = 0u;
}

/// <summary>The instructions we emit. Values match the SPIR-V spec exactly.</summary>
internal enum SpvOp : ushort
{
    Nop                       = 0,
    Source                    = 3,
    Name                      = 5,
    MemberName                = 6,
    ExtInstImport             = 11,
    ExtInst                   = 12,
    MemoryModel               = 14,
    EntryPoint                = 15,
    ExecutionMode             = 16,
    Capability                = 17,
    TypeVoid                  = 19,
    TypeBool                  = 20,
    TypeInt                   = 21,
    TypeFloat                 = 22,
    TypeVector                = 23,
    TypeMatrix                = 24,
    TypeImage                 = 25,
    TypeSampler               = 26,
    TypeSampledImage          = 27,
    TypeArray                 = 28,
    TypeRuntimeArray          = 29,
    TypeStruct                = 30,
    TypePointer               = 32,
    TypeFunction              = 33,
    ConstantTrue              = 41,
    ConstantFalse             = 42,
    Constant                  = 43,
    ConstantComposite         = 44,
    Function                  = 54,
    FunctionParameter         = 55,
    FunctionEnd               = 56,
    FunctionCall              = 57,
    Variable                  = 59,
    Load                      = 61,
    Store                     = 62,
    AccessChain               = 65,
    Decorate                  = 71,
    MemberDecorate            = 72,
    VectorShuffle             = 79,
    CompositeConstruct        = 80,
    CompositeExtract          = 81,
    CompositeInsert           = 82,
    SampledImage              = 86,
    ImageSampleImplicitLod    = 87,
    ImageSampleExplicitLod    = 88,
    ConvertFToU               = 109,
    ConvertFToS               = 110,
    ConvertSToF               = 111,
    ConvertUToF               = 112,
    UConvert                  = 113,
    SConvert                  = 114,
    FConvert                  = 115,
    Bitcast                   = 124,
    SNegate                   = 126,
    FNegate                   = 127,
    IAdd                      = 128,
    FAdd                      = 129,
    ISub                      = 130,
    FSub                      = 131,
    IMul                      = 132,
    FMul                      = 133,
    UDiv                      = 134,
    SDiv                      = 135,
    FDiv                      = 136,
    UMod                      = 137,
    SRem                      = 138,
    FRem                      = 141,
    VectorTimesScalar         = 142,
    MatrixTimesScalar         = 143,
    VectorTimesMatrix         = 144,
    MatrixTimesVector         = 145,
    MatrixTimesMatrix         = 146,
    Transpose                 = 84, // (also reachable via ext-inst MatrixInverse on <=4x4)
    Dot                       = 148,
    ShiftRightLogical         = 194,
    ShiftRightArithmetic      = 195,
    ShiftLeftLogical          = 196,
    BitwiseOr                 = 197,
    BitwiseXor                = 198,
    BitwiseAnd                = 199,
    Not                       = 200,
    LogicalOr                 = 166,
    LogicalAnd                = 167,
    LogicalNot                = 168,
    Select                    = 169,
    IEqual                    = 170,
    INotEqual                 = 171,
    UGreaterThan              = 172,
    SGreaterThan              = 173,
    UGreaterThanEqual         = 174,
    SGreaterThanEqual         = 175,
    ULessThan                 = 176,
    SLessThan                 = 177,
    ULessThanEqual            = 178,
    SLessThanEqual            = 179,
    FOrdEqual                 = 180,
    FOrdNotEqual              = 182,
    FOrdLessThan              = 184,
    FOrdGreaterThan           = 186,
    FOrdLessThanEqual         = 188,
    FOrdGreaterThanEqual      = 190,
    DPdx                      = 207,
    DPdy                      = 208,
    Fwidth                    = 209,
    Phi                       = 245,
    LoopMerge                 = 246,
    SelectionMerge            = 247,
    Label                     = 248,
    Branch                    = 249,
    BranchConditional         = 250,
    Return                    = 253,
    ReturnValue               = 254,
    Unreachable               = 255,
    Kill                      = 252,
}

internal enum SpvCapability : uint
{
    Matrix                    = 0,
    Shader                    = 1,
    Sampled1D                 = 11,
    Image1D                   = 44,
    SampledBuffer             = 46,
    StorageImageExtendedFormats = 26,
    ImageQuery                = 50,
    DerivativeControl         = 51,
}

internal enum SpvAddressingModel : uint
{
    Logical    = 0,
    Physical32 = 1,
    Physical64 = 2,
}

internal enum SpvMemoryModel : uint
{
    Simple   = 0,
    GLSL450  = 1,
    OpenCL   = 2,
    Vulkan   = 3,
}

internal enum SpvExecutionModel : uint
{
    Vertex                  = 0,
    TessellationControl     = 1,
    TessellationEvaluation  = 2,
    Geometry                = 3,
    Fragment                = 4,
    GLCompute               = 5,
    Kernel                  = 6,
}

internal enum SpvExecutionMode : uint
{
    Invocations              = 0,
    SpacingEqual             = 1,
    OriginUpperLeft          = 7,
    OriginLowerLeft          = 8,
    EarlyFragmentTests       = 9,
    DepthReplacing           = 12,
    LocalSize                = 17,
}

internal enum SpvStorageClass : uint
{
    UniformConstant = 0,
    Input           = 1,
    Uniform         = 2,
    Output          = 3,
    Workgroup       = 4,
    CrossWorkgroup  = 5,
    Private         = 6,
    Function        = 7,
    Generic         = 8,
    PushConstant    = 9,
    AtomicCounter   = 10,
    Image           = 11,
    StorageBuffer   = 12,
}

internal enum SpvDim : uint
{
    Dim1D     = 0,
    Dim2D     = 1,
    Dim3D     = 2,
    DimCube   = 3,
    DimRect   = 4,
    DimBuffer = 5,
    DimSubpassData = 6,
}

internal enum SpvImageFormat : uint
{
    Unknown = 0,
}

internal enum SpvDecoration : uint
{
    Block            = 2,
    BufferBlock      = 3,
    RowMajor         = 4,
    ColMajor         = 5,
    ArrayStride      = 6,
    MatrixStride     = 7,
    Builtin          = 11,
    NoPerspective    = 13,
    Flat             = 14,
    Centroid         = 16,
    Sample           = 17,
    Invariant        = 18,
    Restrict         = 19,
    Aliased          = 20,
    Volatile         = 21,
    Coherent         = 23,
    NonWritable      = 24,
    NonReadable      = 25,
    Uniform          = 26,
    Location         = 30,
    Component        = 31,
    Index            = 32,
    Binding          = 33,
    DescriptorSet    = 34,
    Offset           = 35,
    NoContraction    = 42,
}

internal enum SpvBuiltIn : uint
{
    Position       = 0,
    PointSize      = 1,
    ClipDistance   = 3,
    CullDistance   = 4,
    VertexId       = 5,
    InstanceId     = 6,
    PrimitiveId    = 7,
    InvocationId   = 8,
    Layer          = 9,
    ViewportIndex  = 10,
    TessLevelOuter = 11,
    TessLevelInner = 12,
    TessCoord      = 13,
    PatchVertices  = 14,
    FragCoord      = 15,
    PointCoord     = 16,
    FrontFacing    = 17,
    SampleId       = 18,
    SamplePosition = 19,
    SampleMask     = 20,
    FragDepth      = 22,
    HelperInvocation = 23,
    NumWorkgroups  = 24,
    WorkgroupSize  = 25,
    WorkgroupId    = 26,
    LocalInvocationId    = 27,
    GlobalInvocationId   = 28,
    LocalInvocationIndex = 29,
    VertexIndex    = 42, // Vulkan-flavored
    InstanceIndex  = 43,
}

[Flags]
internal enum SpvFunctionControl : uint
{
    None     = 0,
    Inline   = 1,
    DontInline = 2,
    Pure     = 4,
    Const    = 8,
}

[Flags]
internal enum SpvSelectionControl : uint
{
    None        = 0,
    Flatten     = 1,
    DontFlatten = 2,
}

[Flags]
internal enum SpvLoopControl : uint
{
    None         = 0,
    Unroll       = 1,
    DontUnroll   = 2,
    DependencyInfinite = 4,
    DependencyLength   = 8,
}

[Flags]
internal enum SpvMemoryAccess : uint
{
    None     = 0,
    Volatile = 1,
    Aligned  = 2,
    Nontemporal = 4,
}

[Flags]
internal enum SpvImageOperands : uint
{
    None  = 0,
    Bias  = 1,
    Lod   = 2,
    Grad  = 4,
    ConstOffset = 8,
    Offset      = 16,
}

/// <summary>
/// GLSL.std.450 extended instruction set opcodes (we use a subset).
/// Reached via <c>OpExtInst &lt;set-id&gt; &lt;op&gt; &lt;operands...&gt;</c>.
/// </summary>
internal enum GlslStd450 : uint
{
    Round       = 1,
    RoundEven   = 2,
    Trunc       = 3,
    FAbs        = 4,
    SAbs        = 5,
    FSign       = 6,
    SSign       = 7,
    Floor       = 8,
    Ceil        = 9,
    Fract       = 10,
    Sin         = 13,
    Cos         = 14,
    Tan         = 15,
    Asin        = 16,
    Acos        = 17,
    Atan        = 18,
    Atan2       = 25,
    Pow         = 26,
    Exp         = 27,
    Log         = 28,
    Exp2        = 29,
    Log2        = 30,
    Sqrt        = 31,
    InverseSqrt = 32,
    Determinant = 33,
    MatrixInverse = 34,
    FMin        = 37,
    UMin        = 38,
    SMin        = 39,
    FMax        = 40,
    UMax        = 41,
    SMax        = 42,
    FClamp      = 43,
    UClamp      = 44,
    SClamp      = 45,
    FMix        = 46,
    Step        = 48,
    SmoothStep  = 49,
    Length      = 66,
    Distance    = 67,
    Cross       = 68,
    Normalize   = 69,
    Reflect     = 71,
    Refract     = 72,
    FMod        = 35,
}
