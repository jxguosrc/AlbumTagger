# AlbumTagger

面向 M3 同人音乐实体碟整理流程的本地 FLAC 标签工具。使用 .NET 10、Avalonia 12 和 ATL.NET，不依赖 MusicBee 或 foobar2000。

## 当前可用流程

1. 选择包含一张专辑 FLAC 的文件夹。
2. 填写专辑名；专辑艺术家可以留空以保留原值。
3. 从 Tumblr 或 BOOTH 粘贴整段曲目表。
4. 选择封面图片（可选）。
5. 点击“解析并匹配”，在表格中修正曲名或填写每轨 `ARTIST`。
6. 点击“检查写入计划”，确认逐文件变更。
7. 点击“确认并写入 FLAC”。

曲目数与 FLAC 数量不一致、曲目解析有错误或文件在预览后被其他程序修改时，写入都会被阻止。

## 在 Windows 上运行

安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，然后在 Windows Terminal 中运行：

```powershell
git clone https://github.com/jxguosrc/AlbumTagger.git
cd AlbumTagger
dotnet run --project AlbumTagger.App -c Release
```

生成 Windows x64 便携目录：

```powershell
dotnet publish AlbumTagger.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出位于 `AlbumTagger.App/bin/Release/net10.0/win-x64/publish/`。

## Core 已实现

- 顶层目录 FLAC 发现与自然排序
- 多行曲目表解析，包括 U-RASIA Tumblr 样本的“独立轨号 + 标题 + 附加信息”格式
- 文件与曲目按预览顺序匹配，数量不一致时阻止写入
- FLAC 现有标签、图片和音频帧 SHA-256 快照
- 明确区分文本字段的“保留 / 设置 / 清空”
- 写入前变更计划与逐字段差异
- `TITLE`、`ALBUM`、`TRACKNUMBER`、`TRACKTOTAL` 写入
- 显式启用后的 `ARTIST`、`ALBUMARTIST` 写入
- PNG、JPEG、GIF、WebP 正面封面嵌入；保留非正面图片
- 先写同目录临时副本，再重新读取并校验
- 确认所有副本通过后才批量替换原文件；提交失败时回滚
- 校验未知标签未变化、音频帧未变化

Core 只引用纯 .NET 与 ATL.NET，不引用 Avalonia，因此 UI 和写入逻辑可以分别测试。

## 基本调用顺序

```csharp
var files = new FlacFileDiscovery().Discover(albumFolder);
var parsed = new TracklistParser().Parse(pastedTracklist);
var matched = new TrackMatcher().Match(files, parsed.Tracks);

var draft = new AlbumEditDraft(
    albumTitle,
    matched.Matches
        .Select(item => new TrackEditDraft(
            item.File.FilePath,
            item.Track.TrackNumber,
            item.Track.Title))
        .ToArray(),
    AlbumArtist: TextFieldEdit.Keep,
    FrontCover: CoverImage.FromFile(coverPath));

var store = new AtlFlacMetadataStore();
var plan = new AlbumWritePlanBuilder(store).Build(draft);

// UI 应先展示 plan.Tracks[*].Changes 和 plan.Diagnostics。
if (plan.CanWrite)
{
    var result = new SafeAlbumWriter(store).Execute(plan);
}
```

## 开发与验证

```shell
dotnet test AlbumTagger.slnx
```

测试只修改临时目录中的 FLAC 副本。仓库内的小型静音 FLAC 用于标签、封面和音频帧完整性集成测试；Avalonia 窗口通过 Headless 平台测试。
