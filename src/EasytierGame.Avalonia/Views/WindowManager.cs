using Avalonia.Controls;
using Avalonia.Platform.Storage;
using EasytierGame.Desktop.Services;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>
/// 子窗口管理。对应旧版 <c>composables/windows.ts::etWindows</c>：
/// <list type="bullet">
/// <item>已打开且可见 → 直接激活已开窗口；</item>
/// <item>已打开但不可见 → 复用；</item>
/// <item>未打开 → 新建。</item>
/// </list>
/// </summary>
public sealed class WindowManager
{
    private readonly Window _owner;
    private readonly MainViewModel _main;
    private readonly Dictionary<string, Window> _windows = new();

    public WindowManager(Window owner, MainViewModel main)
    {
        _owner = owner;
        _main = main;
    }

    /// <summary>主窗启动按钮旁的下拉菜单（对应旧版 ElDropdown 的命令）。</summary>
    public void ShowStartMenu(Control anchor)
    {
        var menu = new ContextMenu();

        menu.Items.Add(Leaf("使用外部配置文件", () => ShowConfigFilePicker()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Leaf("房间配置管理", () => ShowConfigAdmin()));
        menu.Items.Add(Leaf("保存当前房间配置", () => _ = SaveCurrentRoomConfigAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Leaf("子网代理", () => ShowCidr()));
        menu.Items.Add(Leaf("重新联机", () => _ = _main.ReconnectCommand.ExecuteAsync(null)));

        menu.Open(anchor);
    }

    /// <summary>“分享/导入”下拉。</summary>
    public void ShowShareMenu(Control anchor)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Leaf("分享联机配置", () => _ = ShareConfigAsync()));
        menu.Items.Add(Leaf("导入联机配置", ShowImportDialog));
        menu.Open(anchor);
    }

    public void ShowLog()
    {
        ShowOrCreate("log", () => new LogWindow { DataContext = _main },
            new WindowOptions(Width: 620, Height: 400, Resizable: false));
    }

    public void ShowMembers()
    {
        ShowOrCreate("member", () => new MembersWindow { DataContext = new MembersViewModel(_main.Services) },
            new WindowOptions(Width: 900, Height: 400));
    }

    public void ShowAdvance()
    {
        ShowOrCreate("advance", () => new AdvanceWindow { DataContext = _main },
            new WindowOptions(Width: 620, Height: 520));
    }

    public void ShowTool()
    {
        ShowOrCreate("tool", () => new ToolWindow { DataContext = new ToolViewModel(_main.Services) },
            new WindowOptions(Width: 480, Height: 500));
    }

    public void ShowCidr()
    {
        ShowOrCreate("cidr", () => new CidrWindow { DataContext = new CidrViewModel(_main.Services) },
            new WindowOptions(Width: 620, Height: 400, Resizable: false));
    }

    public void ShowCoreManager()
    {
        var dialog = new CoreManagerWindow { DataContext = new CoreManagerViewModel(_main.Services, _main) };
        dialog.ShowDialog(_owner);
    }

    public void ShowConfigFilePicker()
    {
        var dialog = new ConfigFileDialog { DataContext = new ConfigFileViewModel(_main.Services, _main) };
        dialog.ShowDialog(_owner);
    }

    public void ShowConfigAdmin()
    {
        var dialog = new ConfigAdminDialog { DataContext = new ConfigAdminViewModel(_main.Services, _main) };
        dialog.ShowDialog(_owner);
    }

    public void ShowImportDialog()
    {
        var dialog = new ImportShareDialog { DataContext = new ImportShareViewModel(_main.Services, _main) };
        dialog.ShowDialog(_owner);
    }

    public void ShowIpv6Editor()
    {
        var dialog = new Ipv6Dialog { DataContext = _main };
        dialog.ShowDialog(_owner);
    }

    /// <summary>保存当前房间配置（base64 快照写入 config/.admin/&lt;name&gt;）。</summary>
    public async Task SaveCurrentRoomConfigAsync()
    {
        var services = _main.Services;
        var name = await services.Dialogs.PromptAsync(
            "请输入配置的存储名",
            "提示",
            "请输入2个字以上的存储名",
            "保存",
            "取消",
            _owner);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!Core.Utils.FileNameValidator.IsValidWindowsFileName(name))
        {
            services.Toast.Error("文件名不合法");
            return;
        }

        Directory.CreateDirectory(services.Paths.ConfigAdminDir);
        var path = Path.Combine(services.Paths.ConfigAdminDir, name);

        if (File.Exists(path))
        {
            var overwrite = await services.Dialogs.ConfirmAsync("配置名已存在，是否覆盖?", "提示", "确定", "取消",
                ConfirmKind.Primary, _owner);
            if (!overwrite)
            {
                return;
            }
        }

        try
        {
            var json = Core.Json.JsonDefaults.Serialize(services.State);
            var encoded = Core.Config.ShareCodec.Encode(json);
            await File.WriteAllTextAsync(path, encoded);
            services.Toast.Success("保存成功");
        }
        catch (Exception ex)
        {
            services.Toast.Error($"保存失败：{ex.Message}");
        }
    }

    private async Task ShareConfigAsync()
    {
        try
        {
            var text = Core.Config.SharePayload.Build(_main.Config);
            ClipboardService.SetText(_owner, text);
            _main.Services.Toast.Success("配置已复制");

            await _main.Services.Dialogs.AlertAsync(
                $"不会分享 {Core.Config.SharePayload.NotSharedHint}",
                "提示",
                _owner,
                "知道了");
        }
        catch (Exception ex)
        {
            _main.Services.Toast.Error($"分享失败：{ex.Message}");
        }
    }

    private static MenuItem Leaf(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>单例窗口的通用创建逻辑。</summary>
    private void ShowOrCreate(string key, Func<Window> factory, WindowOptions options)
    {
        if (_windows.TryGetValue(key, out var existing))
        {
            if (existing.IsVisible)
            {
                existing.Activate();
                return;
            }

            existing.Show();
            existing.Activate();
            return;
        }

        var window = factory();
        window.Width = options.Width;
        window.Height = options.Height;

        if (options.MinWidth > 0)
        {
            window.MinWidth = options.MinWidth;
        }

        if (options.MinHeight > 0)
        {
            window.MinHeight = options.MinHeight;
        }

        if (!options.Resizable)
        {
            window.CanResize = false;
        }

        window.Closed += (_, _) => _windows.Remove(key);
        _windows[key] = window;
        window.Show(_owner);
    }

    private readonly record struct WindowOptions(
        double Width,
        double Height,
        double MinWidth = 0,
        double MinHeight = 0,
        bool Resizable = true);
}

/// <summary>
/// 剪贴板辅助。Avalonia 的剪贴板必须从 <see cref="TopLevel"/> 获取，
/// 因此统一在这里处理，并给 ViewModel 的静态委托赋值。
/// </summary>
public static class ClipboardService
{
    /// <summary>把文本写入剪贴板。UI 线程内调用。</summary>
    public static void SetText(TopLevel? topLevel, string text)
    {
        try
        {
            var clipboard = topLevel?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            // 不要 GetResult() 同步等：Windows 剪贴板（OLE）需要消息循环，
            // 在 UI 线程上阻塞等待会死锁。发后不管即可。
            _ = clipboard.SetTextAsync(text ?? "");
        }
        catch
        {
            // 剪贴板偶发占用，忽略
        }
    }

    /// <summary>把剪贴板委托接到两个 ViewModel 上。</summary>
    public static void Attach(TopLevel topLevel)
    {
        MainViewModel.ClipboardWriter = text => SetText(topLevel, text);
        MembersViewModel.ClipboardWriter = text => SetText(topLevel, text);
    }
}
