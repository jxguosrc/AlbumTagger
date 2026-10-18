using AlbumTagger.App.Services;
using AlbumTagger.App.ViewModels;
using AlbumTagger.App.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace AlbumTagger.App.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Window_LoadsTheCompleteMinimalWorkflow()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(new NullFilePicker()),
        };

        window.Show();

        Assert.NotNull(window.FindControl<Button>("FolderButton"));
        Assert.NotNull(window.FindControl<Button>("CoverButton"));
        Assert.NotNull(window.FindControl<TextBox>("TracklistInput"));
        Assert.NotNull(window.FindControl<Button>("PreviewButton"));
        Assert.NotNull(window.FindControl<Button>("PlanButton"));
        Assert.NotNull(window.FindControl<Button>("WriteButton"));
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);

        public Task<string?> PickCoverImageAsync() => Task.FromResult<string?>(null);
    }
}
