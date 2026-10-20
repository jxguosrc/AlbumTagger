using System.Collections.ObjectModel;
using System.ComponentModel;
using AlbumTagger.App.Services;
using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Files;
using AlbumTagger.Core.Metadata;
using AlbumTagger.Core.Parsing;
using AlbumTagger.Core.Planning;
using AlbumTagger.Core.Writing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlbumTagger.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IFilePickerService filePicker;
    private readonly FlacFileDiscovery discovery = new();
    private readonly TracklistParser parser = new();
    private readonly TrackMatcher matcher = new();
    private readonly IFlacMetadataStore metadataStore = new AtlFlacMetadataStore();

    private AlbumWritePlan? preparedPlan;
    private string albumFolder = string.Empty;
    private string albumTitle = string.Empty;
    private string albumArtist = string.Empty;
    private string coverPath = string.Empty;
    private string tracklistText = string.Empty;
    private string statusText = "先选择专辑文件夹，然后粘贴曲目表。";
    private bool isBusy;
    private bool matchIsValid;

    public MainWindowViewModel(IFilePickerService filePicker)
    {
        this.filePicker = filePicker;
        ChooseFolderCommand = new AsyncRelayCommand(ChooseFolderAsync, () => !IsBusy);
        ChooseCoverCommand = new AsyncRelayCommand(ChooseCoverAsync, () => !IsBusy);
        ParseCommand = new RelayCommand(ParseAndMatch, () => !IsBusy);
        PreparePlanCommand = new AsyncRelayCommand(PreparePlanAsync, () => !IsBusy && Tracks.Count > 0);
        WriteCommand = new AsyncRelayCommand(WriteAsync, () => CanWrite);
    }

    public ObservableCollection<TrackRowViewModel> Tracks { get; } = [];

    public ObservableCollection<string> Diagnostics { get; } = [];

    public IAsyncRelayCommand ChooseFolderCommand { get; }

    public IAsyncRelayCommand ChooseCoverCommand { get; }

    public IRelayCommand ParseCommand { get; }

    public IAsyncRelayCommand PreparePlanCommand { get; }

    public IAsyncRelayCommand WriteCommand { get; }

    public string AlbumFolder
    {
        get => albumFolder;
        private set => SetProperty(ref albumFolder, value);
    }

    public string AlbumTitle
    {
        get => albumTitle;
        set
        {
            if (SetProperty(ref albumTitle, value))
            {
                InvalidatePlan();
            }
        }
    }

    public string AlbumArtist
    {
        get => albumArtist;
        set
        {
            if (SetProperty(ref albumArtist, value))
            {
                InvalidatePlan();
            }
        }
    }

    public string CoverPath
    {
        get => coverPath;
        private set
        {
            if (SetProperty(ref coverPath, value))
            {
                InvalidatePlan();
            }
        }
    }

    public string TracklistText
    {
        get => tracklistText;
        set
        {
            if (SetProperty(ref tracklistText, value))
            {
                matchIsValid = false;
                InvalidatePlan();
                NotifyCommandStates();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                NotifyCommandStates();
                OnPropertyChanged(nameof(CanWrite));
            }
        }
    }

    public bool CanWrite => !IsBusy && preparedPlan?.CanWrite == true;

    public void LoadFolder(string folderPath)
    {
        AlbumFolder = Path.GetFullPath(folderPath);
        Tracks.Clear();
        Diagnostics.Clear();
        matchIsValid = false;
        InvalidatePlan();

        try
        {
            var fileCount = discovery.Discover(AlbumFolder).Count;
            StatusText = $"已找到 {fileCount} 个 FLAC。粘贴曲目表后点击“解析并匹配”。";
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
            Diagnostics.Add(exception.Message);
            StatusText = "无法读取该文件夹。";
        }
    }

    public void SelectCover(string filePath)
    {
        CoverPath = Path.GetFullPath(filePath);
        StatusText = $"已选择封面：{Path.GetFileName(CoverPath)}";
    }

    public void ParseAndMatch()
    {
        Diagnostics.Clear();
        Tracks.Clear();
        matchIsValid = false;
        InvalidatePlan();

        if (string.IsNullOrWhiteSpace(AlbumFolder))
        {
            Diagnostics.Add("请先选择专辑文件夹。");
            StatusText = "尚未选择文件夹。";
            return;
        }

        try
        {
            var files = discovery.Discover(AlbumFolder);
            var parsed = parser.Parse(TracklistText);
            var snapshots = new Dictionary<string, AudioFileSnapshot>(StringComparer.OrdinalIgnoreCase);
            var filesWithNumbers = new List<AudioFileReference>(files.Count);
            foreach (var file in files)
            {
                try
                {
                    var snapshot = metadataStore.Read(file.FilePath);
                    snapshots.Add(file.FilePath, snapshot);
                    filesWithNumbers.Add(file with { ExistingTrackNumber = snapshot.TrackNumber });
                }
                catch (Exception exception) when (exception is IOException
                                                   or UnauthorizedAccessException
                                                   or InvalidDataException
                                                   or ArgumentException)
                {
                    Diagnostics.Add($"读取 {file.FileName} 失败：{exception.Message}");
                    filesWithNumbers.Add(file);
                }
            }

            var matched = matcher.Match(filesWithNumbers, parsed.Tracks);
            matchIsValid = matched.CanWrite && !parsed.HasErrors && snapshots.Count == files.Count;
            AddDiagnostics(parsed.Diagnostics);
            AddDiagnostics(matched.Diagnostics);

            foreach (var item in matched.Matches)
            {
                var existingTitle = snapshots.TryGetValue(item.File.FilePath, out var snapshot)
                    ? snapshot.Title
                    : string.Empty;

                var row = new TrackRowViewModel(
                    item.File.FilePath,
                    item.File.FileName,
                    item.Track.TrackNumber,
                    item.Track.Title,
                    item.Track.RawMetadata,
                    existingTitle);
                row.PropertyChanged += OnTrackRowChanged;
                Tracks.Add(row);
            }

            StatusText = matchIsValid
                ? $"已匹配 {Tracks.Count} 轨。可以编辑表格，然后检查写入计划。"
                : "匹配结果包含错误，请根据提示修正曲目表或文件。";
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
            Diagnostics.Add(exception.Message);
            StatusText = "解析或读取文件失败。";
        }

        NotifyCommandStates();
    }

    public async Task PreparePlanAsync()
    {
        if (Tracks.Count == 0)
        {
            StatusText = "没有可写入的曲目。";
            return;
        }

        if (!matchIsValid)
        {
            Diagnostics.Add("曲目解析或文件匹配仍有错误；请重新解析并匹配。写入已被阻止。");
            StatusText = "曲目与文件没有形成完整的一一对应关系。";
            return;
        }

        IsBusy = true;
        Diagnostics.Clear();
        StatusText = "正在读取 FLAC 并生成写入计划……";

        try
        {
            var albumTitleSnapshot = AlbumTitle;
            var albumArtistSnapshot = AlbumArtist;
            var coverPathSnapshot = CoverPath;
            var trackSnapshots = Tracks.Select(row => new TrackInput(
                row.FilePath,
                row.TrackNumber,
                row.Title,
                row.Artist)).ToArray();

            var plan = await Task.Run(() =>
            {
                var cover = string.IsNullOrWhiteSpace(coverPathSnapshot)
                    ? null
                    : CoverImage.FromFile(coverPathSnapshot);
                var draft = new AlbumEditDraft(
                    albumTitleSnapshot,
                    trackSnapshots.Select(track => new TrackEditDraft(
                        track.FilePath,
                        track.TrackNumber,
                        track.Title,
                        string.IsNullOrWhiteSpace(track.Artist)
                            ? TextFieldEdit.Keep
                            : TextFieldEdit.Set(track.Artist))).ToArray(),
                    string.IsNullOrWhiteSpace(albumArtistSnapshot)
                        ? TextFieldEdit.Keep
                        : TextFieldEdit.Set(albumArtistSnapshot),
                    cover);
                return new AlbumWritePlanBuilder(metadataStore).Build(draft);
            });

            preparedPlan = plan;
            AddDiagnostics(plan.Diagnostics);
            ApplyChangeSummaries(plan);
            StatusText = plan.CanWrite
                ? $"计划检查完成：{plan.Tracks.Count(track => track.HasChanges)} 个文件将被修改。"
                : "写入计划包含错误，无法写入。";
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidDataException)
        {
            preparedPlan = null;
            Diagnostics.Add(exception.Message);
            StatusText = "无法生成写入计划。";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanWrite));
            NotifyCommandStates();
        }
    }

    public async Task WriteAsync()
    {
        var plan = preparedPlan;
        if (plan?.CanWrite != true)
        {
            StatusText = "请先检查写入计划。";
            return;
        }

        IsBusy = true;
        Diagnostics.Clear();
        StatusText = "正在安全写入并校验 FLAC，请不要移动文件……";

        try
        {
            var result = await Task.Run(() => new SafeAlbumWriter(metadataStore).Execute(plan));
            AddDiagnostics(result.Diagnostics);

            foreach (var file in result.Files)
            {
                var row = Tracks.FirstOrDefault(item => string.Equals(
                    item.FilePath,
                    file.FilePath,
                    StringComparison.OrdinalIgnoreCase));
                if (row is not null)
                {
                    row.ChangeSummary = file.Status switch
                    {
                        FileWriteStatus.Written => "已写入并校验",
                        FileWriteStatus.Unchanged => "无需修改",
                        FileWriteStatus.RolledBack => "写入失败，已恢复原文件",
                        FileWriteStatus.RecoveryRequired => "需要人工恢复备份",
                        _ => "未写入",
                    };
                }
            }

            StatusText = result.Succeeded
                ? "整张专辑写入并校验完成。"
                : result.WasRolledBack
                    ? "写入失败，已尝试恢复原文件。请查看提示。"
                    : "写入没有开始或未完成。请查看提示。";
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException
                                           or InvalidOperationException)
        {
            Diagnostics.Add($"写入失败：{exception.Message}");
            StatusText = "写入过程中发生错误。请检查文件和提示信息。";
        }
        finally
        {
            preparedPlan = null;
            IsBusy = false;
            OnPropertyChanged(nameof(CanWrite));
            NotifyCommandStates();
        }
    }

    private async Task ChooseFolderAsync()
    {
        var selected = await filePicker.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            LoadFolder(selected);
        }
    }

    private async Task ChooseCoverAsync()
    {
        var selected = await filePicker.PickCoverImageAsync();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SelectCover(selected);
        }
    }

    private void ApplyChangeSummaries(AlbumWritePlan plan)
    {
        var plansByPath = plan.Tracks.ToDictionary(
            track => track.Original.FilePath,
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in Tracks)
        {
            row.ChangeSummary = plansByPath.TryGetValue(row.FilePath, out var trackPlan)
                ? trackPlan.Changes.Count == 0
                    ? "无需修改"
                    : string.Join("；", trackPlan.Changes.Select(FormatChange))
                : "计划中没有该文件";
        }
    }

    private static string FormatChange(TagFieldChange change)
    {
        var before = string.IsNullOrEmpty(change.Before) ? "(空)" : change.Before;
        var after = string.IsNullOrEmpty(change.After) ? "(空)" : change.After;
        return $"{change.Field}: {before} → {after}";
    }

    private void AddDiagnostics(IEnumerable<CoreDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var line = diagnostic.SourceLine is null ? string.Empty : $" 第 {diagnostic.SourceLine} 行";
            Diagnostics.Add($"[{SeverityName(diagnostic.Severity)}]{line} {diagnostic.Message}");
        }
    }

    private static string SeverityName(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "错误",
        DiagnosticSeverity.Warning => "警告",
        _ => "信息",
    };

    private void OnTrackRowChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(TrackRowViewModel.Title) or nameof(TrackRowViewModel.Artist))
        {
            InvalidatePlan();
        }
    }

    private void InvalidatePlan()
    {
        preparedPlan = null;
        OnPropertyChanged(nameof(CanWrite));
        WriteCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommandStates()
    {
        ChooseFolderCommand.NotifyCanExecuteChanged();
        ChooseCoverCommand.NotifyCanExecuteChanged();
        ParseCommand.NotifyCanExecuteChanged();
        PreparePlanCommand.NotifyCanExecuteChanged();
        WriteCommand.NotifyCanExecuteChanged();
    }

    private sealed record TrackInput(string FilePath, int TrackNumber, string Title, string Artist);
}
