using AlbumTagger.App.Services;
using AlbumTagger.App.ViewModels;
using AlbumTagger.Core.Metadata;

namespace AlbumTagger.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task ParseAndPreparePlan_WithOneFlac_EnablesWriting()
    {
        using var directory = TestDirectory.Create();
        directory.CopyFixture("01.flac");
        var viewModel = new MainWindowViewModel(new NullFilePicker())
        {
            AlbumTitle = "幻想航路",
            TracklistText = "01\n星の向こう",
        };

        viewModel.LoadFolder(directory.Path);
        viewModel.ParseAndMatch();
        await viewModel.PreparePlanAsync();

        var track = Assert.Single(viewModel.Tracks);
        Assert.Equal("星の向こう", track.Title);
        Assert.True(viewModel.CanWrite);
        Assert.Contains("1 个文件将被修改", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteWorkflow_WritesTagsAndCoverThroughTheViewModel()
    {
        using var directory = TestDirectory.Create();
        var flacPath = directory.CopyFixture("01.flac");
        var coverPath = directory.CopyCover("cover.png");
        var viewModel = new MainWindowViewModel(new NullFilePicker())
        {
            AlbumTitle = "幻想航路",
            AlbumArtist = "架空音楽サークル",
            TracklistText = "01\n星の向こう",
        };

        viewModel.LoadFolder(directory.Path);
        viewModel.SelectCover(coverPath);
        viewModel.ParseAndMatch();
        await viewModel.PreparePlanAsync();
        await viewModel.WriteAsync();

        var written = new AtlFlacMetadataStore().Read(flacPath);
        Assert.Equal("星の向こう", written.Title);
        Assert.Equal("幻想航路", written.Album);
        Assert.Equal("架空音楽サークル", written.AlbumArtist);
        Assert.Single(written.EmbeddedPictures, picture => picture.PictureType == "Front");
        Assert.Equal("整张专辑写入并校验完成。", viewModel.StatusText);
    }

    [Fact]
    public async Task PreparePlan_WhenFileAndTrackCountsDiffer_RemainsBlocked()
    {
        using var directory = TestDirectory.Create();
        directory.CopyFixture("01.flac");
        directory.CopyFixture("02.flac");
        var viewModel = new MainWindowViewModel(new NullFilePicker())
        {
            AlbumTitle = "不完整专辑",
            TracklistText = "01. 只有一条曲目",
        };

        viewModel.LoadFolder(directory.Path);
        viewModel.ParseAndMatch();
        await viewModel.PreparePlanAsync();

        Assert.False(viewModel.CanWrite);
        Assert.Contains(viewModel.Diagnostics, message => message.Contains("FLAC 数量为 2", StringComparison.Ordinal));
        Assert.Contains("没有形成完整的一一对应关系", viewModel.StatusText, StringComparison.Ordinal);
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickCoverImageAsync() => Task.FromResult<string?>(null);
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
                $"AlbumTagger.App.Tests.{Guid.NewGuid():N}");
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

        public string CopyCover(string destinationName)
        {
            var source = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover.png");
            var destination = System.IO.Path.Combine(Path, destinationName);
            File.Copy(source, destination);
            return destination;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
