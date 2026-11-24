using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Args;
using EasytierGame.Core.Config;
using EasytierGame.Core.Models;
using EasytierGame.Core.Processes;
using EasytierGame.Core.Session;
using EasytierGame.Core.SystemIntegration;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>
/// 主窗口 ViewModel。布局与操作语义对齐旧版 <c>pages/index.vue</c>。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;

    /// <summary>协议下拉候选（旧版 supportProtocols）。</summary>
    public static string[] ProtocolOptions { get; } = { "tcp", "udp", "ws", "wss", "srv", "schema", "txt" };

    /// <summary>压缩算法选项（首项为默认值 none）。</summary>
    public static string[] CompressionOptions { get; } = { "none", "zstd", "lzo" };

    /// <summary>日志等级选项。</summary>
    public static string[] LogLevelOptions { get; } = { "error", "warn", "info", "debug", "trace" };

    public MainViewModel(AppServices services)
    {
        _services = services;

        Log = new ObservableCollection<string>();
        ServerUrls = new ObservableCollection<string>();
        BasePeers = new ObservableCollection<string>();
        Toasts = services.Toast.Items;

        _services.Monitor.StatusChanged += OnStatusChanged;
        _services.Monitor.OutputLine += OnOutputLine;
        _services.Monitor.UnexpectedExit += OnUnexpectedExit;
        _services.Monitor.TunDeviceReady += OnTunDeviceReady;
        _services.Processes.Started += OnProcessStarted;
    }

    // ------------------------------------------------------------------
    // 状态
    // ------------------------------------------------------------------

    public AppState State => _services.State;

    public AppConfig Config => _services.State.Config;

    /// <summary>实时日志（最多保留 1000 行）。</summary>
    public ObservableCollection<string> Log { get; }

    /// <summary>服务器地址（多选）。</summary>
    public ObservableCollection<string> ServerUrls { get; }

    /// <summary>服务器候选池。</summary>
    public ObservableCollection<string> BasePeers { get; }

    public ObservableCollection<ToastMessage> Toasts { get; }

    /// <summary>内核版本（"2.4.5" 之类，不含 "easytier-core " 前缀）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CoreVersionTag))]
    private string _coreVersion = "";

    /// <summary>内核标签文案。用单一字符串而非 Run，便于界面与自动化测试读取。</summary>
    public string CoreVersionTag => "内核 " + CoreVersion;

    /// <summary>联机状态文案：“未联机 / 联机中 / 联机成功”。</summary>
    [ObservableProperty]
    private string _connectionText = "未联机";

    /// <summary>状态标签配色 Key（Success / Info / Warning）。</summary>
    [ObservableProperty]
    private string _connectionTagKind = "Info";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartButtonText))]
    private bool _isStart;

    [ObservableProperty]
    private bool _isSuccessGetIp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isBusy;

    /// <summary>主按钮是否可用（旧版：startLoading || !coreVersion || update 时禁用）。</summary>
    public bool CanStart => !IsBusy && !string.IsNullOrEmpty(CoreVersion);

    public string StartButtonText => IsStart ? "停止联机" : "启动联机";

    /// <summary>内核是否在运行（内核升级前需要先停止）。</summary>
    [ObservableProperty]
    private bool _hasNewCoreVersion;

    /// <summary>服务器地址下拉框当前文本/选中项。</summary>
    [ObservableProperty]
    private string _selectedServerUrl = "";

    /// <summary>
    /// 局域网 IP 的界面绑定源。
    /// <para>
    /// <see cref="Core.Models.AppConfig"/> 是普通 POCO（不实现 INotifyPropertyChanged，
    /// Core 层不引用 WPF），直接绑 <c>Config.Ipv4</c> 时，
    /// 联机成功后由代码写入的新 IP <b>不会刷新界面</b>——这就是“联机成功但输入框仍为空”的原因。
    /// 这里用可通知属性代理：界面绑它，代码改它，两边同时维护 <c>Config.Ipv4</c>。
    /// </para>
    /// </summary>
    [ObservableProperty]
    private string _ipv4Text = "";

    /// <summary>把 <see cref="Ipv4Text"/> 写回 <c>Config.Ipv4</c>（双向绑定的下游）。</summary>
    partial void OnIpv4TextChanged(string value)
    {
        Config.Ipv4 = value ?? "";
    }

    // ------------------------------------------------------------------
    // 初始化
    // ------------------------------------------------------------------

    /// <summary>窗口显示后调用：加载内核版本、config.json、开机自启状态等。</summary>
    public async void Initialize()
    {
        SyncCollectionsFromState();
        SelectedServerUrl = State.Config.ServerUrl.FirstOrDefault() ?? "";

        await LoadCoreVersionAsync();
        await ApplyGuiConfigAsync();
        await RefreshAutoStartStateAsync();

        SyncCollectionsFromState();
        SelectedServerUrl = State.Config.ServerUrl.FirstOrDefault() ?? "";

        // 把已保存的 IP 载入界面（固定 IP 模式下用户填过的值在重启后要能看到）
        Ipv4Text = State.Config.Ipv4 ?? "";

        if (State.Config.EnablePreventSleep)
        {
            _services.Sys.PreventSleep();
        }
    }

    /// <summary>读取内核版本。</summary>
    public async Task LoadCoreVersionAsync()
    {
        CoreVersion = await _services.Updater.GetCoreVersionAsync();
    }

    /// <summary>
    /// 应用 <c>easytier/config.json</c>（存在则以它为准，与旧版一致）。
    /// </summary>
    public async Task ApplyGuiConfigAsync()
    {
        var file = _services.Paths.GuiConfigFile;
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(file);
            var serverUrls = GuiConfigWriter.Apply(json, State);
            SyncCollectionsFromState();

            // config.json 可能改写了 Config，同步到界面可绑定的属性
            SelectedServerUrl = State.Config.ServerUrl.FirstOrDefault() ?? "";
            Ipv4Text = State.Config.Ipv4 ?? "";

            _services.Toast.Info("已按 config.json 应用配置");

            if (State.CreateConfigInEasytier)
            {
                await WriteGuiConfigAsync(serverUrls);
            }
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"config.json 格式或编码错误：{ex.Message}");
        }
    }

    /// <summary>把 config.json 写回磁盘（<c>createConfigInEasytier</c> 开启时）。</summary>
    public async Task WriteGuiConfigAsync(IEnumerable<string>? extraServerUrls = null)
    {
        if (!State.CreateConfigInEasytier)
        {
            return;
        }

        try
        {
            var json = GuiConfigWriter.Build(State, extraServerUrls);
            await File.WriteAllTextAsync(_services.Paths.GuiConfigFile, json);
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"更新config.json失败：{ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 联机 / 断开
    // ------------------------------------------------------------------

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsStart)
        {
            await StopAsync();
        }
        else
        {
            await StartAsync();
        }
    }

    /// <summary>启动联机。</summary>
    public async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var resolved = BuildArgs();
        if (resolved is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Log.Clear();
            await _services.Processes.StartAsync(resolved);
            _services.Monitor.Start();

            IsStart = true;

            if (State.Config.EnablePreventSleep)
            {
                _services.Sys.PreventSleep();
            }

            await _services.SaveAsync();
        }
        catch (Exception ex)
        {
            _services.Toast.Error($"启动失败：{ex.Message}");
            _services.Monitor.Stop();
            IsStart = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>停止联机。</summary>
    public async Task StopAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _services.Monitor.Stop();
            await _services.Processes.StopAsync();
            await _services.SaveAsync();
        }
        finally
        {
            IsStart = false;
            IsSuccessGetIp = false;
            ConnectionText = "未联机";
            ConnectionTagKind = "Info";
            // 动态模式下停联机要清空 IP；固定模式保留用户填的值
            Ipv4Text = Config.Dhcp ? "" : Ipv4Text;
            _services.Sys.AllowSleep();

            IsBusy = false;
        }
    }

    /// <summary>重新联机（旧版：已连则先断、2 秒后再连）。</summary>
    [RelayCommand]
    private async Task ReconnectAsync()
    {
        if (IsStart)
        {
            _services.Toast.Info("正在重新联机...");
            await StopAsync();
            await Task.Delay(2000);
        }

        await StartAsync();
    }

    /// <summary>
    /// 构建客户端参数并处理错误提示/弹窗。返回 null 表示不可启动。
    /// </summary>
    public IReadOnlyList<string>? BuildArgs()
    {
        var context = new ClientArgsContext
        {
            ConfigFileExists = path => File.Exists(_services.Paths.Resolve(path)),
            ResolveConfigPath = path => _services.Paths.Resolve(path),
            LogDirectory = _services.Paths.LogDirArg,
        };

        SyncStateFromCollections();
        var result = ArgsBuilder.BuildClient(State, context);

        if (!string.IsNullOrEmpty(result.NormalizedProxyNetworks))
        {
            Config.ProxyNetworks = result.NormalizedProxyNetworks!;
        }

        switch (result.Status)
        {
            case ArgsBuildStatus.NoHostname:
            {
                // 成员名必填：提示并把焦点送到输入框，比只弹 toast 更好用
                _services.Toast.Warning(result.Message ?? "请先填写成员名");
                RequestFocusHostname?.Invoke(this, EventArgs.Empty);
                return null;
            }

            case ArgsBuildStatus.ConfigPathMissing:
            case ArgsBuildStatus.ConfigFileNotFound:
                State.ConfigStartEnable = false;
                State.ConfigPath = "";
                _services.Toast.Error(result.Message ?? "");
                return null;

            case ArgsBuildStatus.Ok when result.IsConfigFileMode:
                _services.Toast.Warning("使用配置文件中...");
                return result.Args;

            case ArgsBuildStatus.Ok:
                return result.Args;

            default:
                _services.Toast.Warning(result.Message ?? "");
                return null;
        }
    }

    // ------------------------------------------------------------------
    // 事件
    // ------------------------------------------------------------------

    private void OnStatusChanged(object? sender, ConnectionStatusChanged e)
    {
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        dispatcher.Post(() =>
        {
            switch (e.State)
            {
                case ConnectionState.Connected:
                    IsSuccessGetIp = true;
                    if (!string.IsNullOrWhiteSpace(e.Ipv4))
                    {
                        // 走可通知属性，输入框才能即时显示
                        Ipv4Text = e.Ipv4;
                    }

                    ConnectionText = "联机成功";
                    ConnectionTagKind = "Success";
                    break;

                case ConnectionState.Connecting:
                    IsSuccessGetIp = false;
                    ConnectionText = IsStart ? "联机中" : "未联机";
                    ConnectionTagKind = "Warning";
                    break;

                default:
                    IsSuccessGetIp = false;
                    ConnectionText = "未联机";
                    ConnectionTagKind = "Info";
                    break;
            }

            OnPropertyChanged(nameof(ConnectionText));
            OnPropertyChanged(nameof(ConnectionTagKind));
            OnPropertyChanged(nameof(IsSuccessGetIp));
        });
    }

    private void OnOutputLine(object? sender, string line)
    {
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        dispatcher.Post(() =>
        {
            // 最多保留 1000 行（旧版行为）
            if (Log.Count > 1000)
            {
                while (Log.Count > 1000)
                {
                    Log.RemoveAt(0);
                }
            }

            Log.Add(line);
        });
    }

    private void OnUnexpectedExit(object? sender, int exitCode)
    {
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        dispatcher.Post(() =>
        {
            IsStart = false;
            IsSuccessGetIp = false;
            ConnectionText = "未联机";
            ConnectionTagKind = "Info";
            if (exitCode != 0)
            {
                _services.Toast.Error($"内核已退出（code={exitCode}）");
            }
        });
    }

    /// <summary>
    /// TUN 网卡就绪 → 按界面配置设置跃点（旧版在日志回调里做的事）。
    /// </summary>
    private void OnTunDeviceReady(object? sender, string devName)
    {
        if (!Config.EnableNetCardMetric
            || Config.NetCardMetricValue < 1
            || Config.NetCardMetricValue > 9999)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            var ok = await _services.Sys.SetNetCardMetricAsync(devName, Config.NetCardMetricValue.ToString());
            if (!ok)
            {
                _services.Toast.Error("跃点设置失败");
            }
        });
    }

    private void OnProcessStarted(object? sender, int pid)
    {
        // 当前只有联机客户端一种角色，无需在此更新状态
    }

    // ------------------------------------------------------------------
    // 服务器地址维护（对应旧版 handleServerUrlChange / handleDeleteServerUrl）
    // ------------------------------------------------------------------

    /// <summary>
    /// 手动提交服务器地址。规则全部在 <see cref="ServerUrlEditor"/> 里，这里是适配层。
    /// </summary>
    /// <returns>归一化后的地址；输入为空时返回 null。</returns>
    public string? CommitServerUrl(string? raw)
    {
        var added = ServerUrlEditor.Add(
            raw,
            ServerUrls,
            BasePeers,
            Config.Protocol);

        if (added is null)
        {
            return null;
        }

        SelectedServerUrl = added;
        SyncStateFromCollections();
        _ = _services.SaveAsync();
        return added;
    }

    /// <summary>用户改动服务器地址后调用：识别协议前缀并同步候选池。</summary>
    public void OnServerUrlChanged()
    {
        SyncStateFromCollections();

        foreach (var protocol in ProtocolOptions)
        {
            foreach (var url in Config.ServerUrl)
            {
                if (url.Replace('\\', '/').StartsWith(protocol + "://", StringComparison.OrdinalIgnoreCase)
                    && !Config.Protocol.Contains(protocol))
                {
                    Config.Protocol.Add(protocol);
                }
            }
        }

        foreach (var url in Config.ServerUrl)
        {
            if (!BasePeers.Contains(url))
            {
                BasePeers.Add(url);
            }
        }

        _ = _services.SaveAsync();
    }

    /// <summary>从两个列表里删除一个服务器地址，必要时回填候选池第一项。</summary>
    public void DeleteServerUrl(string url)
    {
        var fallback = ServerUrlEditor.Remove(url, ServerUrls, BasePeers);

        if (fallback is not null)
        {
            SelectedServerUrl = fallback;
        }
        else if (string.Equals(SelectedServerUrl, url, StringComparison.OrdinalIgnoreCase))
        {
            SelectedServerUrl = ServerUrls.FirstOrDefault() ?? "";
        }

        SyncStateFromCollections();
        _ = _services.SaveAsync();
    }

    // ------------------------------------------------------------------
    // 开机自启 / 防休眠
    // ------------------------------------------------------------------

    public bool AutoStartEnabled => Core.SystemIntegration.AutoStart.IsEnabled();

    /// <summary>切换开机自启（任务计划）。</summary>
    public async Task ToggleAutoStartAsync()
    {
        var exe = Environment.ProcessPath ?? "";
        var target = !State.Config.AutoStart;
        var ok = Core.SystemIntegration.AutoStart.Set(target, exe);

        State.Config.AutoStart = ok && Core.SystemIntegration.AutoStart.IsEnabled();
        OnPropertyChanged(nameof(AutoStartEnabled));
        OnPropertyChanged(nameof(Config));

        if (!ok)
        {
            _services.Toast.Error("开机自启设置失败（可能需要管理员权限）");
        }

        await _services.SaveAsync();
    }

    /// <summary>每次启动重新注册一遍任务，防止 exe 路径变化（旧版 compatibleInitAutoStart）。</summary>
    private async Task RefreshAutoStartStateAsync()
    {
        var enabled = Core.SystemIntegration.AutoStart.IsEnabled();
        if (enabled)
        {
            Core.SystemIntegration.AutoStart.Set(false, "");
            Core.SystemIntegration.AutoStart.Set(true, Environment.ProcessPath ?? "");
            enabled = Core.SystemIntegration.AutoStart.IsEnabled();
        }

        State.Config.AutoStart = enabled;
        OnPropertyChanged(nameof(AutoStartEnabled));
        await _services.SaveAsync();
    }

    /// <summary>切换防休眠。</summary>
    public void ApplyPreventSleep()
    {
        if (Config.EnablePreventSleep)
        {
            _services.Sys.PreventSleep();
        }
        else
        {
            _services.Sys.AllowSleep();
        }

        _ = _services.SaveAsync();
    }

    // ------------------------------------------------------------------
    // 辅助
    // ------------------------------------------------------------------

    /// <summary>把状态里两个列表同步到 ObservableCollection（供列表控件绑定）。</summary>
    public void SyncCollectionsFromState()
    {
        ServerUrls.Clear();
        foreach (var url in State.Config.ServerUrl)
        {
            ServerUrls.Add(url);
        }

        BasePeers.Clear();
        foreach (var url in State.BasePeers)
        {
            BasePeers.Add(url);
        }
    }

    /// <summary>把 ObservableCollection 的改动写回状态。</summary>
    public void SyncStateFromCollections()
    {
        State.Config.ServerUrl = ServerUrls.ToList();
        State.BasePeers = BasePeers.ToList();
    }

    /// <summary>
    /// 剪贴板写入委托。由视图层注入（Avalonia 的剪贴板需要 TopLevel）。
    /// </summary>
    public static Action<string>? ClipboardWriter { get; set; }

    /// <summary>复制文本到剪贴板并提示。</summary>
    public void CopyText(string text, string successMessage = "复制成功")
    {
        try
        {
            ClipboardWriter?.Invoke(text);
            _services.Toast.Success(successMessage);
        }
        catch
        {
            _services.Toast.Error("复制失败");
        }
    }

    /// <summary>复制 IP（旧版 handleCopyIp：填了 V6 会先问一句）。</summary>
    public async Task CopyIpAsync()
    {
        var v6 = (Config.Ipv6 ?? "").Trim();
        var v4 = (Config.Ipv4 ?? "").Trim();

        if (v6.Length > 0)
        {
            var copyV6 = await _services.Dialogs.ConfirmAsync(
                "检测到您填写了IPV6地址，是否复制IPV6地址？",
                "提示",
                "复制IPV6",
                "复制IPV4",
                ConfirmKind.Primary);

            if (copyV6)
            {
                CopyText(v6);
                return;
            }
        }

        if (v4.Length == 0)
        {
            _services.Toast.Error("没有IPV4地址，无法复制");
            return;
        }

        CopyText(v4, IsStart || !Config.Dhcp ? "复制成功" : "复制成功,联机后IP可能会变化");
    }

    /// <summary>退出前处理（托盘“退出”）。每一步都有超时上限，不会挂死调用方。</summary>
    public async Task PrepareExitAsync()
    {
        try
        {
            _services.Monitor.Stop();
        }
        catch
        {
            // ignore
        }

        try
        {
            await _services.Processes.StopAsync().WaitAsync(TimeSpan.FromSeconds(4));
        }
        catch
        {
            // 停内核失败也要继续退出
        }

        try
        {
            _services.Sys.AllowSleep();
        }
        catch
        {
            // ignore
        }

        try
        {
            await _services.SaveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>请求视图把焦点移到“成员名”输入框（成员名为空时用）。</summary>
    public event EventHandler? RequestFocusHostname;

    /// <summary>供 View 调用的服务入口（打开其它窗口、提示框）。</summary>
    public AppServices Services => _services;
}
