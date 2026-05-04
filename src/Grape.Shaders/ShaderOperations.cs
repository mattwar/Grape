namespace Grape.Shaders;

public enum ShaderBinaryOp
{
    Add, Sub, Mul, Div, Mod,
    MatMul,
    Eq, Ne, Lt, Le, Gt, Ge,
    And, Or,
    BitAnd, BitOr, BitXor, Shl, Shr,
}

public enum ShaderUnaryOp { Neg, Not, BitNot }

public enum ShaderIntrinsic
{
    // Math
    Abs, Sign, Floor, Ceil, Round, Trunc, Frac, Mod,
    Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
    Exp, Exp2, Log, Log2, Pow, Sqrt, InverseSqrt,
    Min, Max, Clamp, Saturate,
    Mix, Step, SmoothStep,

    // Vector
    Dot, Cross, Length, Distance, Normalize, Reflect, Refract,

    // Matrix
    Transpose, Determinant, Inverse,

    // Derivatives (fragment only)
    Ddx, Ddy, FWidth,

    // Bit-casts / conversions
    AsFloat, AsInt, AsUInt,
}

public enum ShaderBuiltin
{
    None,
    VertexIndex,
    InstanceIndex,
    Position,
    PointSize,
    FragCoord,
    FrontFacing,
    FragDepth,
}
