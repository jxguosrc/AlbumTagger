using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Metadata;

namespace AlbumTagger.Core.Planning;

public sealed record TagFieldChange(string Field, string? Before, string? After);

public sealed class TrackWritePlan
{
    public TrackWritePlan(
        AudioFileSnapshot original,
        DesiredTrackMetadata desired,
        IEnumerable<TagFieldChange> changes)
    {
        Original = original;
        Desired = desired;
        Changes = Array.AsReadOnly(changes.ToArray());
    }

    public AudioFileSnapshot Original { get; }

    public DesiredTrackMetadata Desired { get; }

    public IReadOnlyList<TagFieldChange> Changes { get; }

    public bool HasChanges => Changes.Count > 0;
}

public sealed class AlbumWritePlan
{
    public AlbumWritePlan(
        IEnumerable<TrackWritePlan> tracks,
        IEnumerable<CoreDiagnostic> diagnostics)
    {
        Tracks = Array.AsReadOnly(tracks.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<TrackWritePlan> Tracks { get; }

    public IReadOnlyList<CoreDiagnostic> Diagnostics { get; }

    public bool CanWrite => Tracks.Count > 0
        && Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
