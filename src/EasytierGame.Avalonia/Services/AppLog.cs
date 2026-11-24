namespace EasytierGame.Desktop.Services;

/// <summary>
/// 极简文件日志。<para>
/// Avalonia 默认不把异常写到 stderr，界面事件里的异常会被静默吞掉；
/// 把关键错误落到 <c>easytier/guiLogs/avalonia.log</c> 才能排查。
/// 写日志本身失败时静默忽略，绝不影响主流程。
/// </para>
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();

    private static string LogFile =>
        Path.Combine(AppContext.BaseDirectory, "easytier", "guiLogs", "avalonia.log");

    public static void Write(string tag, Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        Write(tag, ex.ToString());
    }

    public static void Write(string tag, string? message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{tag}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // 日志失败不影响运行
        }
    }
}
