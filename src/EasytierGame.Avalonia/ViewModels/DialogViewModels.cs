using System.IO;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Models;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>外部配置文件选择对话框（旧版 configStart）。</summary>
public partial class ConfigFileViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    public ConfigFileViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        Files = new ObservableCollection<ConfigFileItem>();
        Enabled = services.State.ConfigStartEnable;
        _ = ReloadAsync();
    }

    public ObservableCollection<ConfigFileItem> Files { get; }

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private ConfigFileItem? _selectedFile;

    /// <summary>扫描 <c>easytier/config/</c> 下的 .toml/.yaml。</summary>
    [RelayCommand]
    public async Task ReloadAsync()
    {
        Files.Clear();
        var dir = _services.Paths.ConfigDir;
        Directory.CreateDirectory(dir);

        foreach (var file in Directory.EnumerateFiles(dir).OrderBy(Path.GetFileName))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".toml" or ".yaml" or ".yml")
            {
                Files.Add(new ConfigFileItem
                {
                    Name = Path.GetFileName(file),
                    Path = $"easytier/config/{Path.GetFileName(file)}",
                });
            }
        }

        SelectedFile = Files.FirstOrDefault(f => f.Path == _services.State.ConfigPath);
        await Task.CompletedTask;
    }

    /// <summary>打开配置目录。</summary>
    [RelayCommand]
    private void OpenDirectory() => _services.Sys.OpenDirectory(_services.Paths.ConfigDir);

    /// <summary>确认选择。</summary>
    public async Task<bool> ApplyAsync()
    {
        _services.State.ConfigStartEnable = Enabled;

        if (Enabled)
        {
            if (SelectedFile is null)
            {
                _services.Toast.Warning("配置文件不存在 请选择");
                return false;
            }

            var abs = _services.Paths.Resolve(SelectedFile.Path);
            if (!File.Exists(abs))
            {
                _services.Toast.Error("配置文件不存在");
                return false;
            }

            _services.State.ConfigPath = SelectedFile.Path;
        }
        else
        {
            _services.State.ConfigPath = "";
        }

        await _services.SaveAsync();
        return true;
    }
}

/// <summary>房间配置管理对话框（旧版 config_admin）。</summary>
public partial class ConfigAdminViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    /// <summary>作为确认框的 Owner。由宿主对话框设置，避免在主窗口上嵌套模态。</summary>
    public Window? OwnerWindow { get; set; }

    public ConfigAdminViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        Files = new ObservableCollection<string>();
        _ = ReloadAsync();
    }

    public ObservableCollection<string> Files { get; }

    [ObservableProperty]
    private string? _selectedFile;

    [RelayCommand]
    public async Task ReloadAsync()
    {
        Files.Clear();
        var dir = _services.Paths.ConfigAdminDir;
        Directory.CreateDirectory(dir);

        foreach (var file in Directory.EnumerateFiles(dir).OrderBy(Path.GetFileName))
        {
            Files.Add(Path.GetFileName(file));
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenDirectory() => _services.Sys.OpenDirectory(_services.Paths.ConfigAdminDir);

    /// <summary>删除选中配置。</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (string.IsNullOrEmpty(SelectedFile))
        {
            _services.Toast.Warning("请选择配置");
            return;
        }

        var ok = await _services.Dialogs.ConfirmAsync("确定{action}选中的配置吗?", "提示", "确定", "取消",
            ConfirmKind.Danger, OwnerWindow, highlightAction: "删除");
        if (!ok)
        {
            return;
        }

        try
        {
            File.Delete(Path.Combine(_services.Paths.ConfigAdminDir, SelectedFile));
            _services.Toast.Success("删除成功");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"删除配置失败：{ex.Message}");
        }
    }

    /// <summary>切换到选中配置（覆盖当前状态）。</summary>
    [RelayCommand]
    private async Task ExchangeAsync()
    {
        if (string.IsNullOrEmpty(SelectedFile))
        {
            _services.Toast.Warning("请选择配置");
            return;
        }

        var ok = await _services.Dialogs.ConfirmAsync("会覆盖当前配置,确定切换吗?", "提示", "确定", "取消", ConfirmKind.Primary, OwnerWindow);
        if (!ok)
        {
            return;
        }

        if (_main.IsStart)
        {
            _services.Toast.Warning("停止联机..");
            await _main.StopAsync();
        }

        try
        {
            var encoded = await File.ReadAllTextAsync(Path.Combine(_services.Paths.ConfigAdminDir, SelectedFile!));
            var json = Core.Config.ShareCodec.Decode(encoded);
            var state = Core.Json.JsonDefaults.Deserialize<AppState>(json);
            if (state is null)
            {
                throw new FormatException("配置内容为空");
            }

            _services.State.Config = state.Config;
            _services.State.BasePeers = state.BasePeers;
            _services.State.CidrEnable = state.CidrEnable;
            _services.State.ProxyForwardBySystem = state.ProxyForwardBySystem;
            _services.State.CreateConfigInEasytier = state.CreateConfigInEasytier;

            _main.SyncCollectionsFromState();
            await _services.SaveAsync();
            _services.Toast.Success("切换成功");
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"读取配置失败：{ex.Message}");
        }
    }
}

/// <summary>导入分享对话框（旧版 import_config）。</summary>
public partial class ImportShareViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    /// <summary>作为确认框的 Owner。由宿主对话框设置，避免在主窗口上嵌套模态。</summary>
    public Window? OwnerWindow { get; set; }

    public ImportShareViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
    }

    [ObservableProperty]
    private string _input = "";

    /// <summary>解析并应用分享内容。</summary>
    public async Task<bool> ApplyAsync()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            _services.Toast.Warning("请粘贴分享的配置");
            return false;
        }

        var ok = await _services.Dialogs.ConfirmAsync("确定导入?", "提示", "确定", "取消", ConfirmKind.Primary, OwnerWindow);
        if (!ok)
        {
            return false;
        }

        try
        {
            var serverUrls = Core.Config.SharePayload.Apply(Input, _services.State.Config);
            foreach (var url in serverUrls)
            {
                if (!_services.State.BasePeers.Contains(url))
                {
                    _services.State.BasePeers.Add(url);
                }
            }

            _main.SyncCollectionsFromState();
            await _services.SaveAsync();
            _services.Toast.Success("导入成功");
            return true;
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"导入失败：{ex.Message}");
            return false;
        }
    }
}
