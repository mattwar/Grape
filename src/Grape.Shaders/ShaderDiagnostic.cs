namespace Grape.Shaders;

/// <summary>Severity of a <see cref="ShaderDiagnostic"/>.</summary>
public enum ShaderDiagnosticSeverity { Info, Warning, Error }

/// <summary>A diagnostic attached to a <see cref="ShaderElement"/>.</summary>
public sealed class ShaderDiagnostic(
    ShaderDiagnosticSeverity severity,
    string code,
    string message)
{
    public ShaderDiagnosticSeverity Severity { get; } = severity;
    public string Code { get; } = code;
    public string Message { get; } = message;

    public override string ToString() => $"{Severity} {Code}: {Message}";
}
