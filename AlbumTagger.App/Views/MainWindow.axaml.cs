using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AlbumTagger.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
