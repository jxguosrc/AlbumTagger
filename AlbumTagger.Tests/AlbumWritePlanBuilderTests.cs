using AlbumTagger.Core.Metadata;
using AlbumTagger.Core.Planning;

namespace AlbumTagger.Tests;

public sealed class AlbumWritePlanBuilderTests
{
    [Fact]
    public void Build_BlocksNonSequentialTrackNumbersBeforeReadingFiles()
    {
        var builder = new AlbumWritePlanBuilder(new UnexpectedMetadataStore());
        var draft = new AlbumEditDraft(
            "Album",
            [new TrackEditDraft("missing.flac", 2, "Title")]);

        var plan = builder.Build(draft);

        Assert.False(plan.CanWrite);
        Assert.Contains(plan.Diagnostics, item => item.Code == "PLAN_NON_SEQUENTIAL_TRACKS");
        Assert.Contains(plan.Diagnostics, item => item.Code == "PLAN_FILE_NOT_FOUND");
    }

    private sealed class UnexpectedMetadataStore : IFlacMetadataStore
    {
        public AudioFileSnapshot Read(string filePath) =>
            throw new InvalidOperationException("无效计划不应读取文件。");

        public void Apply(string filePath, DesiredTrackMetadata metadata) =>
            throw new InvalidOperationException("无效计划不应写入文件。");
    }
}
