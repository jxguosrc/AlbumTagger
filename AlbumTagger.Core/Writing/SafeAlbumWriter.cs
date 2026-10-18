using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Metadata;
using AlbumTagger.Core.Planning;

namespace AlbumTagger.Core.Writing;

public sealed class SafeAlbumWriter(IFlacMetadataStore metadataStore)
{
    public AlbumWriteResult Execute(AlbumWritePlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanWrite)
        {
            var diagnostics = plan.Diagnostics.Concat([
                new CoreDiagnostic(
                    "WRITE_PLAN_BLOCKED",
                    DiagnosticSeverity.Error,
                    "写入计划包含错误，未修改任何文件。"),
            ]);
            return new AlbumWriteResult(
                false,
                false,
                plan.Tracks.Select(track => new FileWriteResult(
                    track.Original.FilePath,
                    FileWriteStatus.NotWritten)),
                diagnostics);
        }

        var diagnosticsList = plan.Diagnostics.ToList();
        var unchanged = plan.Tracks.Where(track => !track.HasChanges).ToArray();
        var pending = plan.Tracks.Where(track => track.HasChanges).ToArray();
        if (pending.Length == 0)
        {
            return new AlbumWriteResult(
                true,
                false,
                unchanged.Select(track => new FileWriteResult(
                    track.Original.FilePath,
                    FileWriteStatus.Unchanged)),
                diagnosticsList);
        }

        var batchId = Guid.NewGuid().ToString("N");
        var staged = new List<StagedWrite>(pending.Length);
        TrackWritePlan? failedPlan = null;

        try
        {
            foreach (var trackPlan in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                failedPlan = trackPlan;

                var current = metadataStore.Read(trackPlan.Original.FilePath);
                if (!MetadataVerifier.MatchesOriginal(trackPlan.Original, current))
                {
                    throw new InvalidOperationException(
                        "文件在生成预览后被其他程序修改；为避免覆盖新内容，批量写入已停止。");
                }

                var paths = CreateArtifactPaths(trackPlan.Original.FilePath, batchId);
                File.Copy(trackPlan.Original.FilePath, paths.StagedPath, overwrite: false);
                var stagedWrite = new StagedWrite(trackPlan, paths.StagedPath, paths.BackupPath);
                staged.Add(stagedWrite);

                metadataStore.Apply(paths.StagedPath, trackPlan.Desired);
                var written = metadataStore.Read(paths.StagedPath);
                var verification = MetadataVerifier.VerifyWrittenCopy(trackPlan, written);
                diagnosticsList.AddRange(verification);
                if (verification.Any(item => item.Severity == DiagnosticSeverity.Error))
                {
                    throw new InvalidDataException("临时副本未通过写入后校验。原文件尚未修改。");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in staged)
            {
                var current = metadataStore.Read(item.Plan.Original.FilePath);
                if (!MetadataVerifier.MatchesOriginal(item.Plan.Original, current))
                {
                    failedPlan = item.Plan;
                    throw new InvalidOperationException(
                        "文件在临时副本校验期间被其他程序修改；为避免覆盖新内容，批量写入已停止。");
                }
            }
        }
        catch (OperationCanceledException)
        {
            diagnosticsList.Add(new CoreDiagnostic(
                "WRITE_CANCELLED",
                DiagnosticSeverity.Warning,
                "写入已取消；原文件没有被修改。"));
            CleanupStagedArtifacts(staged, diagnosticsList);
            return BeforeCommitFailure(plan, unchanged, failedPlan, diagnosticsList);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException
                                           or InvalidOperationException)
        {
            diagnosticsList.Add(new CoreDiagnostic(
                "WRITE_STAGING_FAILED",
                DiagnosticSeverity.Error,
                $"准备写入副本失败：{exception.Message}"));
            CleanupStagedArtifacts(staged, diagnosticsList);
            return BeforeCommitFailure(plan, unchanged, failedPlan, diagnosticsList);
        }

        var committed = new List<StagedWrite>(staged.Count);
        StagedWrite? committing = null;
        var recoveryFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in staged)
            {
                committing = item;
                File.Move(item.Plan.Original.FilePath, item.BackupPath);
                try
                {
                    File.Move(item.StagedPath, item.Plan.Original.FilePath);
                }
                catch (Exception) when (File.Exists(item.BackupPath))
                {
                    try
                    {
                        File.Move(item.BackupPath, item.Plan.Original.FilePath);
                    }
                    catch (Exception recoveryException) when (recoveryException is IOException
                                                               or UnauthorizedAccessException)
                    {
                        recoveryFailures.Add(item.Plan.Original.FilePath);
                        diagnosticsList.Add(new CoreDiagnostic(
                            "WRITE_ROLLBACK_FAILED",
                            DiagnosticSeverity.Error,
                            $"自动恢复失败：{item.Plan.Original.FilePath}。请保留同目录的 .backup.flac 文件。{recoveryException.Message}"));
                    }

                    throw;
                }

                committed.Add(item);
                committing = null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnosticsList.Add(new CoreDiagnostic(
                "WRITE_COMMIT_FAILED",
                DiagnosticSeverity.Error,
                $"替换原文件时失败，正在恢复已替换文件：{exception.Message}"));

            recoveryFailures.UnionWith(RollBack(committed, diagnosticsList));
            CleanupStagedArtifacts(staged, diagnosticsList);

            var results = plan.Tracks.Select(track =>
            {
                if (!track.HasChanges)
                {
                    return new FileWriteResult(track.Original.FilePath, FileWriteStatus.Unchanged);
                }

                var recoveryFailed = recoveryFailures.Contains(track.Original.FilePath);
                return new FileWriteResult(
                    track.Original.FilePath,
                    recoveryFailed ? FileWriteStatus.RecoveryRequired : FileWriteStatus.RolledBack,
                    ReferenceEquals(committing?.Plan, track) ? "提交该文件时失败。" : null);
            });

            return new AlbumWriteResult(false, committed.Count > 0, results, diagnosticsList);
        }

        foreach (var item in staged)
        {
            TryDelete(item.BackupPath, "WRITE_BACKUP_CLEANUP_FAILED", diagnosticsList);
        }

        var successResults = plan.Tracks.Select(track => new FileWriteResult(
            track.Original.FilePath,
            track.HasChanges ? FileWriteStatus.Written : FileWriteStatus.Unchanged));
        return new AlbumWriteResult(true, false, successResults, diagnosticsList);
    }

    private static AlbumWriteResult BeforeCommitFailure(
        AlbumWritePlan plan,
        IReadOnlyCollection<TrackWritePlan> unchanged,
        TrackWritePlan? failedPlan,
        IReadOnlyCollection<CoreDiagnostic> diagnostics)
    {
        var unchangedPaths = unchanged
            .Select(track => track.Original.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = plan.Tracks.Select(track => new FileWriteResult(
            track.Original.FilePath,
            unchangedPaths.Contains(track.Original.FilePath)
                ? FileWriteStatus.Unchanged
                : FileWriteStatus.NotWritten,
            ReferenceEquals(track, failedPlan) ? "准备该文件时失败。" : null));
        return new AlbumWriteResult(false, false, results, diagnostics);
    }

    private static HashSet<string> RollBack(
        IEnumerable<StagedWrite> committed,
        ICollection<CoreDiagnostic> diagnostics)
    {
        var failures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in committed.Reverse())
        {
            try
            {
                if (File.Exists(item.Plan.Original.FilePath))
                {
                    File.Move(item.Plan.Original.FilePath, item.StagedPath, overwrite: true);
                }

                File.Move(item.BackupPath, item.Plan.Original.FilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(item.Plan.Original.FilePath);
                diagnostics.Add(new CoreDiagnostic(
                    "WRITE_ROLLBACK_FAILED",
                    DiagnosticSeverity.Error,
                    $"自动恢复失败：{item.Plan.Original.FilePath}。请保留同目录的 .backup.flac 文件。{exception.Message}"));
            }
        }

        return failures;
    }

    private static void CleanupStagedArtifacts(
        IEnumerable<StagedWrite> staged,
        ICollection<CoreDiagnostic> diagnostics)
    {
        foreach (var item in staged)
        {
            TryDelete(item.StagedPath, "WRITE_STAGED_CLEANUP_FAILED", diagnostics);
        }
    }

    private static void TryDelete(
        string path,
        string diagnosticCode,
        ICollection<CoreDiagnostic> diagnostics)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new CoreDiagnostic(
                diagnosticCode,
                DiagnosticSeverity.Warning,
                $"无法清理临时文件 {path}：{exception.Message}"));
        }
    }

    private static ArtifactPaths CreateArtifactPaths(string originalPath, string batchId)
    {
        var directory = Path.GetDirectoryName(originalPath)
            ?? throw new InvalidOperationException("FLAC 文件没有父目录。");
        var baseName = Path.GetFileNameWithoutExtension(originalPath);
        return new ArtifactPaths(
            Path.Combine(directory, $".{baseName}.albumtagger.{batchId}.staged.flac"),
            Path.Combine(directory, $".{baseName}.albumtagger.{batchId}.backup.flac"));
    }

    private sealed record ArtifactPaths(string StagedPath, string BackupPath);

    private sealed record StagedWrite(TrackWritePlan Plan, string StagedPath, string BackupPath);
}
