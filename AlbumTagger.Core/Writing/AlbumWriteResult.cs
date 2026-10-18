using AlbumTagger.Core.Diagnostics;

namespace AlbumTagger.Core.Writing;

public enum FileWriteStatus
{
    Unchanged,
    Written,
    NotWritten,
    RolledBack,
    RecoveryRequired,
}

public sealed record FileWriteResult(string FilePath, FileWriteStatus Status, string? Message = null);

public sealed class AlbumWriteResult
{
    public AlbumWriteResult(
        bool succeeded,
        bool wasRolledBack,
        IEnumerable<FileWriteResult> files,
        IEnumerable<CoreDiagnostic> diagnostics)
    {
        Succeeded = succeeded;
        WasRolledBack = wasRolledBack;
        Files = Array.AsReadOnly(files.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public bool Succeeded { get; }

    public bool WasRolledBack { get; }

    public IReadOnlyList<FileWriteResult> Files { get; }

    public IReadOnlyList<CoreDiagnostic> Diagnostics { get; }
}
