namespace EasytierGame.Core;

/// <summary>
/// 应用路径约定。对应旧版 <c>env/.env.prod</c> 里的 VITE_* 常量，
/// 全部以 <b>exe 所在目录</b> 为根，保持“绿色包、整个文件夹拷走即可迁移”的原有特性。
/// </summary>
public sealed class AppPaths
{
    /// <summary>资源根目录（exe 所在目录）。</summary>
    public string Root { get; }

    /// <summary>UI 设置持久化文件（替代旧版 WebView 的 localStorage）。</summary>
    public string UiSettingsFile => Path.Combine(Root, "ui-settings.json");

    /// <summary>内核与工具目录。</summary>
    public string EasytierDir => Path.Combine(Root, "easytier");

    /// <summary>easytier-core.exe。</summary>
    public string CoreExe => Path.Combine(EasytierDir, "easytier-core.exe");

    /// <summary>easytier-cli.exe。</summary>
    public string CliExe => Path.Combine(EasytierDir, "easytier-cli.exe");

    /// <summary>界面配置文件（存在则覆盖界面配置）。</summary>
    public string GuiConfigFile => Path.Combine(EasytierDir, "config.json");

    /// <summary>内核日志目录（传给 --file-log-dir）。</summary>
    public string LogDir => Path.Combine(EasytierDir, "logs");

    /// <summary>GUI 自身日志目录。</summary>
    public string GuiLogDir => Path.Combine(EasytierDir, "guiLogs");

    /// <summary>内核 zip 缓存目录。</summary>
    public string CacheDir => Path.Combine(EasytierDir, "cache");

    /// <summary>外部配置文件（.toml/.yaml）目录。</summary>
    public string ConfigDir => Path.Combine(EasytierDir, "config");

    /// <summary>房间配置管理目录。</summary>
    public string ConfigAdminDir => Path.Combine(ConfigDir, ".admin");

    /// <summary>工具目录（WinIPBroadcast / ForceBindIP / nssm / EdgeWebView2 安装器等）。</summary>
    public string ToolDir => Path.Combine(EasytierDir, "tool");

    /// <summary>
    /// 传给内核 <c>--file-log-dir</c> 的值。
    /// 旧版传的是相对路径 <c>"easytier/logs/"</c>（内核工作目录=exe 目录），这里保持一致。
    /// </summary>
    public string LogDirArg => "easytier/logs/";

    public AppPaths(string? root = null)
    {
        Root = root ?? AppContext.BaseDirectory;
    }

    /// <summary>进程 pid 记录文件（用于清理崩溃后残留的内核进程）。</summary>
    public string ClientPidFile => Path.Combine(EasytierDir, ".client.pid");

    /// <summary>确保运行所需的目录都存在。</summary>
    public void EnsureDirectories()
    {
        foreach (var dir in new[] { EasytierDir, LogDir, GuiLogDir, CacheDir, ConfigDir, ConfigAdminDir, ToolDir })
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // 目录不可写时交由上层提示；这里不让启动失败
            }
        }
    }

    /// <summary>把旧版“相对资源根目录的路径”转成绝对路径。</summary>
    public string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Root;
        }

        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
