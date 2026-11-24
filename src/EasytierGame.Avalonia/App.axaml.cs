using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasytierGame.Desktop.Services;
using EasytierGame.Desktop.ViewModels;
using EasytierGame.Desktop.Views;
using EasytierGame.Core;
using EasytierGame.Core.Config;
using EasytierGame.Core.Processes;

namespace EasytierGame.Desktop;

/// <summary>
/// 应用入口。负责：单实例、路径与配置加载、Core 层装配、主题初始化、主窗口显示。
/// 与 WPF 版行为保持一致。
/// </summary>
public class App : Application
{
    /// <summary>
    /// 单实例互斥名。
    /// <para>
    /// 与 WPF 版故意用<b>不同</b>的 GUID：两个版本是并行实现，
    /// 允许同时运行以便对比。若共用同一个名字，
    /// WPF 版在跑时 Avalonia 版会直接退出，看起来就像“启动失败”。
    /// </para>
    /// </summary>
    internal const string SingleInstanceName =
        "EasytierGame.Avalonia.SingleInstance.{7C3B5E92-4F18-4A6D-8B21-9E5D0C7A3B41}";

    private Mutex? _singleInstanceMutex;
    private AppPaths _paths = null!;
    private SettingsStore _settings = null!;
    private CoreProcessManager _processes = null!;

    /// <summary>全局服务容器。</summary>
    public AppServices Services { get; private set; } = null!;

    /// <summary>当前主窗口（托盘与退出流程共用）。</summary>
    public MainWindow? MainWindow { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

        // 1. 单实例
        if (!Core.SystemIntegration.SingleInstance.TryAcquire(SingleInstanceName, out var mutex))
        {
            Core.SystemIntegration.SingleInstance.ActivateExistingWindow("easytier-game");

            // 必须真正结束进程：仅 desktop.Shutdown() 在生命周期尚未启动时
            // 会留下一个无窗口的僵尸进程。
            Environment.Exit(0);
            return;
        }

        _singleInstanceMutex = mutex;

        // 2. 路径与配置
        _paths = new AppPaths();
        _paths.EnsureDirectories();

        _settings = new SettingsStore(_paths);
        var state = _settings.Load();

        // 3. Core 层装配
        _processes = new CoreProcessManager(_paths);
        _processes.ReapOrphans();

        Services = new AppServices(_paths, _settings, state, _processes);

        // 4. 主题
        ThemeManager.Apply(state.Theme);

        // 5. 主窗口
        var mainViewModel = new MainViewModel(Services);
        var window = new MainWindow { DataContext = mainViewModel };
        MainWindow = window;
        Services.AttachWindowManager(window.Windows);
        Services.Dialogs.Owner = window;

        var args = Environment.GetCommandLineArgs();
        var autoStart = args.Contains(Core.SystemIntegration.AutoStart.AutoStartArgument);

        desktop.MainWindow = window;

        if (!autoStart)
        {
            window.Show();
        }

        // 6. 托盘（主窗口必须已创建）
        try
        {
            Services.Tray = new TrayService(mainViewModel, () => MainWindow);
        }
        catch
        {
            // 托盘不可用不影响主流程
        }

        mainViewModel.Initialize();

        desktop.Exit += OnDesktopExit;

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            Services?.Tray?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            Services?.Shutdown();
        }
        catch
        {
            // ignore
        }

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
