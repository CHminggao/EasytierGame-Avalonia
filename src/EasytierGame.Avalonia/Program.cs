using Avalonia;
using Avalonia.Logging;
using EasytierGame.Desktop.Services;

namespace EasytierGame.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 把未处理异常与 Avalonia 日志落到文件，避免"静默失败"
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write("Unhandled", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Write("UnobservedTask", e.Exception);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ => Logger.Sink = new FileLogSink());
}

/// <summary>把 Avalonia 内部日志写到 easytier/guiLogs/avalonia.log，便于排查界面问题。</summary>
internal sealed class FileLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        => AppLog.Write($"Avalonia/{area}", messageTemplate);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
        params object?[] propertyValues)
        => AppLog.Write($"Avalonia/{area}", messageTemplate + " | " + string.Join(", ", propertyValues));
}
