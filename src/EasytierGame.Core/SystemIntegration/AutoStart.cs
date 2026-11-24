using System.Runtime.InteropServices;
using System.Text;

namespace EasytierGame.Core.SystemIntegration;

/// <summary>
/// 开机自启。等价旧版 Rust <c>autostart</c> / <c>autostart_is_enabled</c>，
/// 但改用 Windows 自带的 <c>schtasks.exe</c>，避免引入 TaskScheduler COM 依赖，
/// 行为与旧版一致：任务放在 <c>\easytierGame\auto start</c>，登录时触发、最高权限、带 <c>--task-auto-start</c>。
/// </summary>
public static class AutoStart
{
    /// <summary>开机自启时附加的命令行参数（旧版 <c>TASKAUTOSTART_ARG</c>）。</summary>
    public const string AutoStartArgument = "--task-auto-start";

    /// <summary>任务计划中的路径（旧版固定值）。</summary>
    public const string TaskPath = @"\easytierGame\auto start";

    /// <summary>查询是否已启用。</summary>
    public static bool IsEnabled()
    {
        try
        {
            var (exitCode, output) = Run("schtasks", new[] { "/Query", "/TN", TaskPath });
            if (exitCode != 0)
            {
                return false;
            }

            // 输出里含 "已禁用"/"Disabled" 视为未启用
            return !output.Contains("已禁用", StringComparison.OrdinalIgnoreCase)
                   && !output.Contains("Disabled", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>启用或禁用开机自启。</summary>
    /// <param name="exePath">当前 exe 绝对路径。</param>
    /// <param name="enabled">true 启用，false 删除任务。</param>
    public static bool Set(bool enabled, string exePath)
    {
        if (!enabled)
        {
            // 删除任务与其所在文件夹（不存在时忽略错误）
            Run("schtasks", new[] { "/Delete", "/TN", TaskPath, "/F" });
            return !IsEnabled();
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Environment.ProcessPath ?? "";
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        // /SC ONLOGON：登录触发；/RL HIGHEST：最高权限；/F：覆盖同名任务
        var (exitCode, _) = Run("schtasks", new[]
        {
            "/Create",
            "/TN", TaskPath,
            "/TR", $"\"{exePath}\" {AutoStartArgument}",
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/F",
        });

        return exitCode == 0 && IsEnabled();
    }

    private static (int ExitCode, string Output) Run(string fileName, IEnumerable<string> args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
        {
            return (-1, "");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15_000);

        return (process.HasExited ? process.ExitCode : -1,
            string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
    }
}

/// <summary>单实例守卫。等价 Tauri <c>tauri_plugin_single_instance</c>。</summary>
public static class SingleInstance
{
    /// <summary>
    /// 尝试成为唯一实例。返回 false 表示已有实例在运行（调用方应唤起旧窗口并退出）。
    /// </summary>
    public static bool TryAcquire(string name, out Mutex? mutex)
    {
        mutex = new Mutex(true, name, out var createdNew);
        return createdNew;
    }

    /// <summary>
    /// 唤起已运行实例的主窗口：向同名窗口类广播注册消息。
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static void ActivateExistingWindow(string windowTitle)
    {
        var handle = FindWindow(null, windowTitle);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(handle, SW_RESTORE);
        SetForegroundWindow(handle);
    }
}
