using ATL;
using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Metadata;
using AlbumTagger.Core.Planning;
using AlbumTagger.Core.Writing;

namespace AlbumTagger.Tests;

public sealed class SafeAlbumWriterTests
{
    [Fact]
    public void Execute_WritesConfirmedFieldsAndCoverWithoutChangingAudioOrOtherMetadata()
    {
        using var directory = TestDirectory.Create();
        var filePath = directory.CopyFixture("01.flac");
        SeedExistingMetadata(filePath);

        var store = new AtlFlacMetadataStore();
        var before = store.Read(filePath);
        var cover = CoverImage.FromBytes(TestImages.Png);
        var plan = new AlbumWritePlanBuilder(store).Build(new AlbumEditDraft(
            "幻想航路",
            [new TrackEditDraft(filePath, 1, "星の向こう", TextFieldEdit.Keep)],
            TextFieldEdit.Set("架空音楽サークル"),
            cover));

        Assert.True(plan.CanWrite);
        Assert.Contains(plan.Tracks[0].Changes, change => change.Field == "COVER_FRONT");

        var result = new SafeAlbumWriter(store).Execute(plan);
        var after = store.Read(filePath);

        Assert.True(result.Succeeded);
        Assert.False(result.WasRolledBack);
        Assert.Equal(FileWriteStatus.Written, Assert.Single(result.Files).Status);
        Assert.DoesNotContain(result.Diagnostics, item => item.Severity == DiagnosticSeverity.Error);
        Assert.Equal("星の向こう", after.Title);
        Assert.Equal("幻想航路", after.Album);
        Assert.Equal("既存アーティスト", after.Artist);
        Assert.Equal("架空音楽サークル", after.AlbumArtist);
        Assert.Equal(1, after.TrackNumber);
        Assert.Equal(1, after.TrackTotal);
        Assert.Equal(before.AudioFramesSha256, after.AudioFramesSha256);
        Assert.Equal("残す値", after.AdditionalFields["X-ALBUMTAGGER-TEST"]);
        Assert.Single(after.EmbeddedPictures, picture => picture.PictureType == "Front");
        Assert.Single(after.EmbeddedPictures, picture => picture.PictureType == "Back");
        Assert.Equal(
            cover.Sha256,
            after.EmbeddedPictures.Single(picture => picture.PictureType == "Front").Sha256);
        Assert.Empty(directory.FindTransactionArtifacts());
    }

    [Fact]
    public void Execute_WhenStagingWriteFails_LeavesOriginalUntouched()
    {
        using var directory = TestDirectory.Create();
        var firstPath = directory.CopyFixture("01.flac");
        var secondPath = directory.CopyFixture("02.flac");
        SeedExistingMetadata(firstPath);
        SeedExistingMetadata(secondPath);

        var realStore = new AtlFlacMetadataStore();
        var firstBefore = realStore.Read(firstPath);
        var secondBefore = realStore.Read(secondPath);
        var failingStore = new FailingApplyMetadataStore(realStore, failOnApply: 2);
        var plan = new AlbumWritePlanBuilder(failingStore).Build(new AlbumEditDraft(
            "新专辑",
            [
                new TrackEditDraft(firstPath, 1, "新曲名 1"),
                new TrackEditDraft(secondPath, 2, "新曲名 2"),
            ]));

        var result = new SafeAlbumWriter(failingStore).Execute(plan);
        var firstAfter = realStore.Read(firstPath);
        var secondAfter = realStore.Read(secondPath);

        Assert.False(result.Succeeded);
        Assert.False(result.WasRolledBack);
        Assert.All(result.Files, file => Assert.Equal(FileWriteStatus.NotWritten, file.Status));
        Assert.Contains(result.Diagnostics, item => item.Code == "WRITE_STAGING_FAILED");
        Assert.Equal(firstBefore.Title, firstAfter.Title);
        Assert.Equal(firstBefore.Album, firstAfter.Album);
        Assert.Equal(firstBefore.AudioFramesSha256, firstAfter.AudioFramesSha256);
        Assert.Equal(secondBefore.Title, secondAfter.Title);
        Assert.Equal(secondBefore.Album, secondAfter.Album);
        Assert.Equal(secondBefore.AudioFramesSha256, secondAfter.AudioFramesSha256);
        Assert.Empty(directory.FindTransactionArtifacts());
    }

    private static void SeedExistingMetadata(string filePath)
    {
        var track = new Track(filePath)
        {
            Title = "旧曲名",
            Album = "旧专辑",
            Artist = "既存アーティスト",
        };
        track.AdditionalFields["X-ALBUMTAGGER-TEST"] = "残す値";
        track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(
            TestImages.Png,
            PictureInfo.PIC_TYPE.Back));

        Assert.True(track.Save());
    }

    private sealed class FailingApplyMetadataStore(IFlacMetadataStore inner, int failOnApply) : IFlacMetadataStore
    {
        private int applyCount;

        public AudioFileSnapshot Read(string filePath) => inner.Read(filePath);

        public void Apply(string filePath, DesiredTrackMetadata metadata)
        {
            applyCount++;
            if (applyCount == failOnApply)
            {
                throw new IOException("测试注入的写入失败。");
            }

            inner.Apply(filePath, metadata);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"AlbumTagger.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
        }

        public string CopyFixture(string destinationName)
        {
            var source = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.flac");
            var destination = System.IO.Path.Combine(Path, destinationName);
            File.Copy(source, destination);
            return destination;
        }

        public string[] FindTransactionArtifacts() => Directory
            .EnumerateFiles(Path)
            .Where(file => System.IO.Path.GetFileName(file).Contains(
                ".albumtagger.",
                StringComparison.Ordinal))
            .ToArray();

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
