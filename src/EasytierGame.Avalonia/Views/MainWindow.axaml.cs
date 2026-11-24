using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>
/// 主窗口。关闭按钮 = 隐藏到托盘（与 WPF 版一致），只有托盘“退出”才真正结束进程。
/// </summary>
public partial class MainWindow : Window
{
    private bool _reallyExit;
    private bool _exiting;
    private WindowManager? _windows;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>子窗口管理器（主窗与托盘共用同一份）。</summary>
    public WindowManager Windows => _windows ??= new WindowManager(this, ViewModel!);

    private void OnOpened(object? sender, EventArgs e)
    {
        // 剪贴板需要 TopLevel
        ClipboardService.Attach(this);

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainViewModel.ConnectionTagKind)
                    or nameof(MainViewModel.ConnectionText))
                {
                    UpdateConnectionTag();
                }
            };

            ViewModel.RequestFocusHostname += (_, _) => FocusHostname();
            UpdateConnectionTag();
        }

        Services.ThemeManager.Apply(ViewModel?.State.Theme ?? false);
    }

    /// <summary>
    /// 状态标签配色：联机成功用实心绿底白字，其余用浅色底 + 强调色文字。
    /// </summary>
    private void UpdateConnectionTag()
    {
        if (ViewModel is null)
        {
            return;
        }

        var kind = ViewModel.ConnectionTagKind;

        if (kind == "Success")
        {
            ConnectionTag.Background = Brush("SuccessBrush", Color.Parse("#67C23A"));
            ConnectionTagText.Foreground = Brushes.White;
            ConnectionTagText.FontWeight = FontWeight.SemiBold;
            return;
        }

        ConnectionTag.Background = Brush(kind + "LightBrush", Color.Parse("#F4F4F5"));
        ConnectionTagText.Foreground = Brush(kind + "Brush", Color.Parse("#909399"));
        ConnectionTagText.FontWeight = FontWeight.Normal;
    }

    private static IBrush Brush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key, out var res) == true && res is IBrush b)
        {
            return b;
        }

        return new SolidColorBrush(fallback);
    }

    /// <summary>把焦点移到“成员名”输入框并全选。</summary>
    private void FocusHostname()
    {
        Dispatcher.UIThread.Post(() =>
        {
            HostnameBox.Focus();
            HostnameBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    /// <summary>由托盘“退出”调用。清理丢到后台，完成后 Shutdown。</summary>
    public void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _reallyExit = true;

        _ = Task.Run(async () =>
        {
            try
            {
                if (ViewModel is not null)
                {
                    await ViewModel.PrepareExitAsync().WaitAsync(TimeSpan.FromSeconds(6));
                }
            }
            catch
            {
                // 清理失败也必须退出
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    (Application.Current?.ApplicationLifetime
                        as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                });
            }
        });

        Hide();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_reallyExit)
        {
            // 按关闭按钮 = 收进托盘
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    // ------------------------------------------------------------------
    // 服务器地址
    // ------------------------------------------------------------------

    private void OnServerUrlCommit(object? sender, string normalizedUrl)
    {
        if (ViewModel is null)
        {
            return;
        }

        var alreadyThere = ViewModel.BasePeers
            .Any(x => string.Equals(x, normalizedUrl, StringComparison.OrdinalIgnoreCase));

        var committed = ViewModel.CommitServerUrl(normalizedUrl);
        if (committed is null)
        {
            return;
        }

        ServerUrlBox.SetInput(committed);
        ViewModel.Services.Toast.Success(alreadyThere
            ? $"已选择服务器：{committed}"
            : $"已新增服务器：{committed}");
    }

    private void OnServerUrlDelete(object? sender, string url)
    {
        if (ViewModel is null || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        ViewModel.DeleteServerUrl(url);
        ViewModel.SyncCollectionsFromState();
        ServerUrlBox.SetInput(ViewModel.SelectedServerUrl);
    }

    private void OnServerUrlCopy(object? sender, string url)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.CopyText(url, $"已复制：{url}");
    }

    // ------------------------------------------------------------------
    // 联机
    // ------------------------------------------------------------------

    private async void OnStartStop(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ToggleConnectionCommand.ExecuteAsync(null);
    }

    private void OnOpenStartMenu(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        Windows.ShowStartMenu(StartMenuButton);
    }

    private void OnShowLog(object? sender, RoutedEventArgs e) => Windows.ShowLog();

    private void OnShowMembers(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!ViewModel.IsStart)
        {
            ViewModel.Services.Toast.Warning("请先启动联机");
            return;
        }

        Windows.ShowMembers();
    }

    private void OnShowAdvance(object? sender, RoutedEventArgs e) => Windows.ShowAdvance();

    private void OnShowTool(object? sender, RoutedEventArgs e) => Windows.ShowTool();

    private void OnCoreManage(object? sender, RoutedEventArgs e) => Windows.ShowCoreManager();

    private void OnOpenShareMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is Control anchor)
        {
            Windows.ShowShareMenu(anchor);
        }
    }

    private void OnToggleAutoStart(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            _ = ViewModel.ToggleAutoStartAsync();
        }
    }

    private async void OnCopyIp(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CopyIpAsync();
        }
    }

    private void OnEditIpv6(object? sender, RoutedEventArgs e) => Windows.ShowIpv6Editor();
}
