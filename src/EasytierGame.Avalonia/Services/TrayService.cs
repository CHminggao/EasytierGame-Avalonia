using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Services;

/// <summary>
/// 系统托盘。等价 WPF 版 <c>TrayService</c> 与旧版 <c>composables/tray.ts</c>：
/// 左键切换显隐、右键菜单（显示/隐藏、联机/断开、重新联机、主题切换、退出）。
/// <para>
/// 使用 Avalonia 内置的 <see cref="TrayIcon"/>（Windows 后端为 Shell_NotifyIcon）。
/// 关键：<b>绝不在菜单点击回调里销毁托盘图标</b>——托盘图标是右键菜单的宿主，
/// 在菜单 Click 中隐藏它会拆掉菜单消息循环，导致菜单永久卡住。
/// </para>
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly TrayIcon? _icon;
    private readonly MainViewModel _main;
    private readonly Func<Window?> _windowProvider;
    private readonly WindowIcon? _normalIcon;
    private readonly WindowIcon? _inactiveIcon;

    public TrayService(MainViewModel main, Func<Window?> windowProvider)
    {
        _main = main;
        _windowProvider = windowProvider;

        _normalIcon = LoadIcon("icon.ico");
        _inactiveIcon = LoadIcon("icon-inactive.ico") ?? _normalIcon;

        try
        {
            _icon = new TrayIcon
            {
                ToolTipText = "EasyTierGame",
                Icon = _normalIcon,
                Menu = BuildMenu(),
                IsVisible = true,
            };

            _icon.Clicked += (_, _) => ToggleVisibility();
        }
        catch
        {
            // 某些环境下托盘不可用（例如无 shell），不让启动失败
            _icon = null;
        }

        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsSuccessGetIp) or nameof(MainViewModel.ConnectionText))
            {
                UpdateIconAndTooltip();
            }
        };
    }

    /// <summary>设置托盘图标与提示文本（联机成功/失败状态）。</summary>
    public void UpdateIconAndTooltip()
    {
        if (_icon is null)
        {
            return;
        }

        _icon.Icon = _main.IsSuccessGetIp ? _inactiveIcon : _normalIcon;

        var status = string.IsNullOrWhiteSpace(_main.Config.Ipv4)
            ? _main.ConnectionText
            : $"{_main.ConnectionText}  IP: {_main.Config.Ipv4}";

        _icon.ToolTipText = $"EasyTierGame\n{_main.CoreVersion}\n{status}";
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        menu.Items.Add(Item("显示 / 隐藏", ToggleVisibility));
        menu.Items.Add(Item("联机 / 断开", () => _ = _main.ToggleConnectionCommand.ExecuteAsync(null)));
        menu.Items.Add(Item("重新联机", () => _ = _main.ReconnectCommand.ExecuteAsync(null)));
        menu.Items.Add(Item("主题切换", ToggleTheme));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("退出", ExitApplication));

        return menu;
    }

    private static NativeMenuItem Item(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private void ToggleVisibility()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = _windowProvider();
            if (window is null)
            {
                return;
            }

            if (window.IsVisible)
            {
                window.Hide();
                return;
            }

            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
        });
    }

    private void ToggleTheme()
    {
        var dark = ThemeManager.Toggle();
        _main.State.Theme = dark;
        _ = _main.Services.SaveAsync();
    }

    private void ExitApplication()
    {
        // 不在菜单回调里销毁图标，交给 App.OnExit 统一 Dispose
        Dispatcher.UIThread.Post(() =>
        {
            if (_windowProvider() is Views.MainWindow main)
            {
                main.ExitApplication();
            }
            else
            {
                (Application.Current?.ApplicationLifetime
                    as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            }
        });
    }

    private static WindowIcon? LoadIcon(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "easytier", "icons", fileName);
        if (File.Exists(path))
        {
            try
            {
                return new WindowIcon(path);
            }
            catch
            {
                // 落到内嵌图标
            }
        }

        try
        {
            var exe = Environment.ProcessPath;
            return exe is null ? null : new WindowIcon(exe);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_icon is not null)
            {
                _icon.IsVisible = false;
                _icon.Dispose();
            }
        }
        catch
        {
            // ignore
        }
    }
}
