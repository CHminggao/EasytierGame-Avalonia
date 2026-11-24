using EasytierGame.Core;
using EasytierGame.Core.Cli;
using EasytierGame.Core.Config;
using EasytierGame.Core.Models;
using EasytierGame.Core.Processes;
using EasytierGame.Core.Session;
using EasytierGame.Core.SystemIntegration;
using EasytierGame.Core.Update;

namespace EasytierGame.Desktop.Services;

/// <summary>
/// 应用级服务容器。把所有 Core 层组件装配好，供各 ViewModel 共享。
/// 单进程内存共享状态后，旧版那套「localStorage + storage 事件 + 隐藏监听窗口 + 650ms emitTo 轮询」
/// 全部不再需要。
/// </summary>
public sealed class AppServices : IAsyncDisposable
{
    public AppServices(AppPaths paths, SettingsStore settings, AppState state, CoreProcessManager processes)
    {
        Paths = paths;
        Settings = settings;
        State = state;
        Processes = processes;

        Probe = new CliProbe(paths);
        Monitor = new ConnectionMonitor(processes, Probe);
        Updater = new CoreUpdater(paths);
        Sys = new SysIntegration();
        Toast = new ToastService();
        Dialogs = new DialogService();
    }

    public ToastService Toast { get; }

    public DialogService Dialogs { get; }

    /// <summary>子窗口管理器（主窗创建后注入）。</summary>
    public Views.WindowManager? Windows { get; private set; }

    /// <summary>托盘服务（App 启动时创建）。</summary>
    public Services.TrayService? Tray { get; set; }

    internal void AttachWindowManager(Views.WindowManager manager) => Windows = manager;

    public AppPaths Paths { get; }

    public SettingsStore Settings { get; }

    /// <summary>全局状态（旧版 Pinia store 的等价物）。</summary>
    public AppState State { get; }

    public CoreProcessManager Processes { get; }

    public CliProbe Probe { get; }

    public ConnectionMonitor Monitor { get; }

    public CoreUpdater Updater { get; }

    public SysIntegration Sys { get; }

    /// <summary>请求保存设置（各 ViewModel 修改状态后调用）。</summary>
    public Task SaveAsync() => Settings.SaveAsync(State);

    /// <summary>退出前清理：停内核、恢复休眠、保存设置。</summary>
    public void Shutdown()
    {
        try
        {
            Sys.AllowSleep();
        }
        catch
        {
            // ignore
        }

        try
        {
            // 带超时上限，避免强杀不走时卡住退出流程
            Task.Run(() => Processes.StopAsync()).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore
        }

        try
        {
            Settings.Save(State);
        }
        catch
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Monitor.DisposeAsync().ConfigureAwait(false);
        await Processes.DisposeAsync().ConfigureAwait(false);
    }
}
