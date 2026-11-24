using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>联机日志窗口。日志按关键字着色。</summary>
public partial class LogWindow : Window
{
    private INotifyCollectionChanged? _observed;

    public LogWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Attach();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Attach()
    {
        if (ViewModel is null)
        {
            return;
        }

        LogList.ItemsSource = ViewModel.Log;
        LogList.ItemTemplate = BuildItemTemplate();

        _observed = ViewModel.Log;
        _observed.CollectionChanged += OnLogChanged;
        ScrollToEnd();
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ScrollToEnd();
        }
    }

    private void ScrollToEnd()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel is { Log.Count: > 0 })
            {
                LogList.ScrollIntoView(ViewModel.Log[^1]);
            }
        });
    }

    /// <summary>按关键字着色：error/fail → 红，warn → 橙，new peer connection → 蓝。</summary>
    private static IDataTemplate BuildItemTemplate()
    {
        return new FuncDataTemplate<string>((text, _) =>
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(5, 2, 5, 2),
                Foreground = ColorFor(text),
            };
            return block;
        }, supportsRecycling: true);
    }

    private static IBrush ColorFor(string? text)
    {
        var lower = (text ?? "").ToLowerInvariant();

        var key = "SecondaryForegroundBrush";
        if (lower.Contains("error") || lower.Contains("fail") || lower.Contains("exception") || lower.Contains("err"))
        {
            key = "DangerBrush";
        }
        else if (lower.Contains("warn"))
        {
            key = "WarningBrush";
        }
        else if (lower.Contains("new peer connection"))
        {
            key = "PrimaryBrush";
        }

        if (Application.Current?.TryFindResource(key, out var res) == true && res is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse("#606266"));
    }

    private void OnCopyAll(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.CopyText(string.Join(Environment.NewLine, ViewModel.Log), "复制记录成功");
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnLogChanged;
            _observed = null;
        }

        base.OnClosed(e);
    }
}
