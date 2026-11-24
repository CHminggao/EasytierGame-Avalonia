using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Models;
using EasytierGame.Core.Update;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>内核管理对话框的 ViewModel。等价旧版 index.vue 的“内核管理”弹窗。</summary>
public partial class CoreManagerViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    public CoreManagerViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        Releases = new ObservableCollection<CoreReleaseAsset>();
        GithubFastUrl = services.State.GithubFastUrl;
        CurrentVersion = main.CoreVersion;
    }

    public ObservableCollection<CoreReleaseAsset> Releases { get; }

    [ObservableProperty]
    private string _currentVersion = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private CoreReleaseAsset? _selectedRelease;

    [ObservableProperty]
    private string _githubFastUrl = "";

    /// <summary>拉取内核 Release 列表（只保留 windows-x86_64）。</summary>
    [RelayCommand]
    public async Task LoadReleasesAsync()
    {
        IsLoading = true;
        try
        {
            // GetReleasesAsync 内部用了 ConfigureAwait(false)，续体可能在后台线程；
            // ObservableCollection 只能在 UI 线程修改，因此拿到结果后显式切回 UI 线程。
            var list = await _services.Updater.GetReleasesAsync().ConfigureAwait(true);

            Releases.Clear();
            foreach (var release in list)
            {
                Releases.Add(release);
            }

            if (Releases.Count == 0)
            {
                _services.Toast.Warning("未获取到可用的内核版本（需要能访问 GitHub）");
            }
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"获取内核列表失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>安装选中的内核版本。</summary>
    [RelayCommand]
    public async Task InstallAsync()
    {
        if (SelectedRelease is null)
        {
            _services.Toast.Error("请选择一个内核");
            return;
        }

        if (_services.Processes.IsRunning)
        {
            _services.Toast.Warning("请先停止自建服务和联机后再使用");
            return;
        }

        IsInstalling = true;
        Progress = 0;
        try
        {
            var downloadUrl = BuildDownloadUrl(SelectedRelease.DownloadUrl);
            var handler = new Progress<long>();
            void OnProgress(object? sender, (long Received, long Total) e)
            {
                if (e.Total > 0)
                {
                    Progress = (int)Math.Min(100, e.Received * 100 / e.Total);
                }
            }

            _services.Updater.Progress += OnProgress;
            try
            {
                var ok = await _services.Updater.DownloadAndInstallAsync(downloadUrl, SelectedRelease.AssetName);
                if (ok)
                {
                    _services.Toast.Success($"已切换到 {SelectedRelease.TagName}");
                    await _main.LoadCoreVersionAsync();
                    CurrentVersion = _main.CoreVersion;
                }
                else
                {
                    _services.Toast.Error("内核安装失败");
                }
            }
            finally
            {
                _services.Updater.Progress -= OnProgress;
            }
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>拼接 GitHub 加速前缀（旧版 checkUpdate 里的规则）。</summary>
    private string BuildDownloadUrl(string rawUrl)
    {
        var fast = (GithubFastUrl ?? "").Replace('\\', '/').Trim();
        if (string.IsNullOrEmpty(fast))
        {
            return rawUrl;
        }

        // 折叠多余斜杠后补一个结尾斜杠
        while (fast.Contains("//") && fast.IndexOf("//", StringComparison.Ordinal) > 8)
        {
            fast = fast.Replace("//", "/");
        }

        if (!fast.EndsWith('/'))
        {
            fast += "/";
        }

        return fast + rawUrl;
    }

    /// <summary>打开内核 zip 缓存目录。</summary>
    [RelayCommand]
    private void OpenCache() => _services.Sys.OpenDirectory(_services.Paths.CacheDir);

    /// <summary>保存加速地址。</summary>
    public async Task SaveAsync()
    {
        _services.State.GithubFastUrl = GithubFastUrl;
        await _services.SaveAsync();
    }
}
