namespace AlbumTagger.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record CoreDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    int? SourceLine = null);
