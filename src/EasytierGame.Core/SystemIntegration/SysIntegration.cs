using System.Diagnostics;

namespace EasytierGame.Core.SystemIntegration;

/// <summary>Windows 系统集成：防休眠、防火墙、网卡跃点、ping、程序启动。</summary>
public sealed class SysIntegration
{
    private static readonly object SleepGate = new();

    /// <summary>
    /// 系统命令（netsh / ping）的输出编码。
    /// <para>
    /// 实测：在中文 Windows 上这两种命令输出的<b>就是 UTF-8 字节</b>
    /// （例如「域」= <c>E5 9F 9F</c>），不是 GBK。
    /// 早期版本强制用 GBK(936) 解码，反而把正常中文变成了「鍩熼厤缃」之类的乱码。
    /// 因此统一用 UTF-8，且不再提供编码切换开关。
    /// </para>
    /// </summary>
    private static readonly System.Text.Encoding OutputEncoding = System.Text.Encoding.UTF8;

    // ---------------------------------------------------------------
    // 防休眠（等价 Rust prevent_sleep / allow_sleep）
    // ---------------------------------------------------------------

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    /// <summary>阻止系统休眠/息屏。</summary>
    public bool PreventSleep()
    {
        lock (SleepGate)
        {
            return SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED) != 0;
        }
    }

    /// <summary>恢复休眠。</summary>
    public bool AllowSleep()
    {
        lock (SleepGate)
        {
            return SetThreadExecutionState(ES_CONTINUOUS) != 0;
        }
    }

    // ---------------------------------------------------------------
    // 防火墙
    // ---------------------------------------------------------------

    /// <summary>关闭所有配置文件的防火墙（需要管理员权限）。</summary>
    public async Task<(bool Ok, string Output)> DisableFirewallAsync(CancellationToken ct = default)
        => await RunAsync("netsh", new[] { "advfirewall", "set", "allprofiles", "state", "off" }, ct).ConfigureAwait(false);

    /// <summary>读取防火墙状态（域/专用/公用）。</summary>
    public async Task<List<FirewallProfileState>> GetFirewallStateAsync(CancellationToken ct = default)
    {
        var (ok, output) = await RunAsync("netsh", new[] { "advfirewall", "show", "allprofiles", "state" }, ct)
            .ConfigureAwait(false);

        return ok ? ParseFirewallState(output) : new List<FirewallProfileState>();
    }

    /// <summary>
    /// 解析 <c>netsh advfirewall show allprofiles state</c> 的输出。
    /// <para>
    /// 中文输出形如：<c>域配置文件 设置: / ------ / 状态    关闭</c>，
    /// 英文形如：<c>Domain Profile Settings: /  State    ON</c>。
    /// 旧实现用“按空格从尾部切”取名字，会得到“设置”而丢掉“域配置文件”，
    /// 还会把末尾的“确定。”当成一条记录。这里按行对的方式解析并扔掉噪声行。
    /// </para>
    /// </summary>
    public static List<FirewallProfileState> ParseFirewallState(string? output)
    {
        var result = new List<FirewallProfileState>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        var lines = output
            .Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !l.All(c => c == '-'))          // 分隔线
            .Where(l => !IsFirewallNoise(l))            // “确定。”“OK.”
            .ToList();

        string? pendingProfile = null;
        foreach (var line in lines)
        {
            if (TryGetFirewallState(line, out var isEnabled))
            {
                // 当前行是“状态 xxx”，它归属上一条表头
                result.Add(new FirewallProfileState(
                    pendingProfile ?? $"配置文件 {result.Count + 1}",
                    isEnabled));
                pendingProfile = null;
                continue;
            }

            // 表头行
            pendingProfile = ExtractProfileName(line);
        }

        return result;
    }

    private static bool IsFirewallNoise(string line)
    {
        return line.StartsWith("确定", StringComparison.Ordinal)
               || line.Equals("OK.", StringComparison.OrdinalIgnoreCase)
               || line.Equals("Ok.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从“状态 关闭 / State ON”这类行里取开关状态。</summary>
    private static bool TryGetFirewallState(string line, out bool isEnabled)
    {
        isEnabled = false;

        var isStateLine = line.StartsWith("状态", StringComparison.Ordinal)
                          || line.StartsWith("State", StringComparison.OrdinalIgnoreCase);
        if (!isStateLine)
        {
            return false;
        }

        var value = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "";

        isEnabled = value.Equals("开启", StringComparison.Ordinal)
                    || value.Equals("启用", StringComparison.Ordinal)
                    || value.Equals("ON", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("ENABLE", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>从“域配置文件 设置: / Domain Profile Settings:”里取“域”这类名字。</summary>
    private static string ExtractProfileName(string line)
    {
        var text = line.TrimEnd(':', '：').Trim();

        // 中文：“域配置文件 设置” -> “域”
        var cnIndex = text.IndexOf("配置文件", StringComparison.Ordinal);
        if (cnIndex > 0)
        {
            return text[..cnIndex].Trim();
        }

        // 英文：“Domain Profile Settings” -> “Domain”
        var enIndex = text.IndexOf(" Profile", StringComparison.OrdinalIgnoreCase);
        if (enIndex > 0)
        {
            return text[..enIndex].Trim();
        }

        return text;
    }

    // ---------------------------------------------------------------
    // 网卡跃点
    // ---------------------------------------------------------------

    /// <summary>
    /// 读取 IPv4 网卡列表（索引 / 跃点 / MTU / 状态 / 名称）。
    /// 等价旧版 tool.vue 的“查询网卡信息”（netsh interface ipv4 show interface）。
    /// </summary>
    public async Task<List<NetCardInfo>> GetNetCardsAsync(CancellationToken ct = default)
    {
        var result = new List<NetCardInfo>();
        var (ok, output) = await RunAsync("netsh", new[] { "interface", "ipv4", "show", "interface" }, ct)
            .ConfigureAwait(false);
        if (!ok)
        {
            return result;
        }

        var lines = output
            .Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .Select(l => string.Join(' ', l.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(l => l.Length > 0)
            .Skip(2) // 表头两行
            .ToList();

        foreach (var line in lines)
        {
            var parts = line.Split(' ');
            if (parts.Length < 5)
            {
                continue;
            }

            result.Add(new NetCardInfo
            {
                Index = parts[0],
                Metric = parts[1],
                Mtu = parts[2],
                Status = parts[3],
                Name = string.Join(' ', parts.Skip(4)),
            });
        }

        return result;
    }

    /// <summary>设置网卡跃点（数值越小优先级越高）。</summary>
    public async Task<bool> SetNetCardMetricAsync(string name, string metric, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(metric))
        {
            return false;
        }

        var (ok, _) = await RunAsync(
            "netsh",
            new[] { "interface", "ipv4", "set", "interface", name, $"metric={metric}" },
            ct).ConfigureAwait(false);
        return ok;
    }

    // ---------------------------------------------------------------
    // Ping
    // ---------------------------------------------------------------

    /// <summary>单次 ping（用于增强工具的连通性测试）。</summary>
    public async Task<(bool Ok, string Output)> PingAsync(string ip, bool ipv6 = false, CancellationToken ct = default)
    {
        var args = ipv6 ? new[] { "-6", "-n", "1", ip } : new[] { "-n", "1", ip };
        return await RunAsync("ping", args, ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // 外部程序启动
    // ---------------------------------------------------------------

    /// <summary>用资源管理器打开目录。</summary>
    public void OpenDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch
        {
            // ignore
        }

        TryStart("explorer.exe", new[] { path });
    }

    /// <summary>用默认程序打开文件/URL。</summary>
    public void OpenPath(string path) => TryStartShell(path);

    /// <summary>直接启动的目标程序（例如游戏快捷方式）。</summary>
    public bool StartProgram(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return TryStartShell(path);
        }

        return TryStart("explorer.exe", new[] { path });
    }

    private static bool TryStartShell(string path)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true };
            using var process = Process.Start(psi);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStart(string fileName, IEnumerable<string> arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool Ok, string Output)> RunAsync(
        string fileName,
        IEnumerable<string> args,
        CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = OutputEncoding,
                StandardErrorEncoding = OutputEncoding,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return (false, "");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                return (false, "");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var text = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            return (process.ExitCode == 0, text);
        }
        catch
        {
            return (false, "");
        }
    }
}

/// <summary>网卡信息（增强工具“网卡跃点”页）。</summary>
public sealed class NetCardInfo
{
    public string Index { get; set; } = "";

    public string Metric { get; set; } = "";

    public string Mtu { get; set; } = "";

    public string Status { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>是否已连接（供界面高亮联机用的网卡）。</summary>
    public bool IsConnected => Status.Contains("已连接", StringComparison.OrdinalIgnoreCase)
                               || Status.Contains("connected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>防火墙某一配置文件（域/专用/公用）的开关状态。</summary>
/// <param name="Profile">配置文件名，例如“域”“专用”“公用”。</param>
/// <param name="IsEnabled">true = 防火墙开启（危险），false = 已关闭。</param>
public sealed record FirewallProfileState(string Profile, bool IsEnabled)
{
    /// <summary>界面展示文案。</summary>
    public string Display => $"{Profile}防火墙：{(IsEnabled ? "已开启" : "已关闭")}";
}
