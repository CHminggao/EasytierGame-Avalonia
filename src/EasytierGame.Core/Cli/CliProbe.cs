using System.Diagnostics;
using System.Text.Json;
using EasytierGame.Core.Json;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Cli;

/// <summary>CLI 调用结果。</summary>
public sealed record CliResult<T>(bool Ok, T? Value, string? Error)
{
    public static CliResult<T> Success(T value) => new(true, value, null);

    public static CliResult<T> Failure(string error) => new(false, default, error);
}

/// <summary>
/// 封装 <c>easytier-cli.exe</c> 的查询。
/// 等价旧版 <c>get_members_by_cli</c> / <c>get_members_connections_cli</c> /
/// <c>get_members_proxy</c> / <c>get_route_by_cli</c>。
/// 特殊字符串 <c>_EasytierGameCliFailedToConnect_</c> 在旧前端被当作“未连上”的信号，
/// 这里用 <c>Ok == false</c> 表达，由 UI 决定提示方式。
/// </summary>
public sealed class CliProbe
{
    private readonly AppPaths _paths;
    private readonly TimeSpan _timeout;

    public CliProbe(AppPaths paths, TimeSpan? timeout = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _timeout = timeout ?? TimeSpan.FromSeconds(6);
    }

    /// <summary><c>peer list</c>：成员列表基础信息。</summary>
    public Task<CliResult<List<Member>>> GetPeersAsync(CancellationToken ct = default)
        => QueryAsync<List<Member>>(new[] { "-o", "json", "peer", "list" }, ct);

    /// <summary><c>-v peer</c>：带连接明细，用于补充远端地址。</summary>
    public Task<CliResult<List<PeerConnections>>> GetPeerConnectionsAsync(CancellationToken ct = default)
        => QueryAsync<List<PeerConnections>>(new[] { "-v", "-o", "json", "peer" }, ct);

    /// <summary><c>proxy</c>：子网代理 / KCP 代理状态。</summary>
    public Task<CliResult<List<ProxyEntry>>> GetProxyAsync(CancellationToken ct = default)
        => QueryAsync<List<ProxyEntry>>(new[] { "-o", "json", "proxy" }, ct);

    /// <summary><c>route</c>：路由表（子网代理页）。</summary>
    public Task<CliResult<List<RouteEntry>>> GetRouteAsync(CancellationToken ct = default)
        => QueryAsync<List<RouteEntry>>(new[] { "-o", "json", "route" }, ct);

    /// <summary>
    /// 组合查询：把 peer list / peer -v / proxy 合成界面要的成员行。
    /// 与旧版 <c>member.vue</c> 的合并规则一致：
    /// 用 <c>route.peer_id</c> 匹配成员 <c>id</c> 补 <c>connections_addrs</c>；
    /// 用 <c>proxy.dst</c> 的 IP 段匹配成员 <c>ipv4</c>，补 <c>proxy = [transport_type, state]</c>。
    /// </summary>
    public async Task<CliResult<List<Member>>> GetMembersAsync(CancellationToken ct = default)
    {
        var peers = await GetPeersAsync(ct).ConfigureAwait(false);
        if (!peers.Ok || peers.Value is null)
        {
            return CliResult<List<Member>>.Failure(peers.Error ?? "获取成员列表失败");
        }

        var members = peers.Value;

        var connections = await GetPeerConnectionsAsync(ct).ConfigureAwait(false);
        if (connections.Ok && connections.Value is not null)
        {
            foreach (var member in members)
            {
                var conn = connections.Value.FirstOrDefault(c => c.Route.PeerId.ToString() == member.Id);
                member.ConnectionsAddrs = conn?.Peer.Conns
                    .Select(c => c.Tunnel?.RemoteAddr?.Url ?? "")
                    .Where(url => url.Length > 0)
                    .ToList() ?? new List<string>();
            }
        }

        var proxies = await GetProxyAsync(ct).ConfigureAwait(false);
        if (proxies.Ok && proxies.Value is not null)
        {
            var proxyMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var proxy in proxies.Value)
            {
                var key = proxy.Dst.Split(':')[0];
                proxyMap[key] = new[] { proxy.TransportType, proxy.State };
            }

            foreach (var member in members)
            {
                var ipKey = member.Ipv4.Split(':')[0];
                member.Proxy = proxyMap.TryGetValue(ipKey, out var value)
                    ? value
                    : member.Proxy ?? new[] { "", "Closed" };
            }
        }

        return CliResult<List<Member>>.Success(members);
    }

    /// <summary>本地节点信息（<c>cost == "Local"</c>）。联机成功判定用它。</summary>
    public async Task<Member?> GetLocalMemberAsync(CancellationToken ct = default)
    {
        var peers = await GetPeersAsync(ct).ConfigureAwait(false);
        if (!peers.Ok || peers.Value is null)
        {
            return null;
        }

        return peers.Value.FirstOrDefault(m => string.Equals(m.Cost, "Local", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<CliResult<T>> QueryAsync<T>(IReadOnlyList<string> args, CancellationToken ct)
    {
        if (!File.Exists(_paths.CliExe))
        {
            return CliResult<T>.Failure("找不到 easytier-cli.exe");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _paths.CliExe,
            WorkingDirectory = _paths.Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                return CliResult<T>.Failure("无法启动 easytier-cli.exe");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
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

                return CliResult<T>.Failure("easytier-cli 超时");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return CliResult<T>.Failure(string.IsNullOrWhiteSpace(stderr) ? "easytier-cli 返回非零退出码" : stderr.Trim());
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return CliResult<T>.Failure("easytier-cli 无输出");
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(stdout, JsonDefaults.Options);
                return value is null
                    ? CliResult<T>.Failure("easytier-cli 输出无法解析")
                    : CliResult<T>.Success(value);
            }
            catch (JsonException ex)
            {
                return CliResult<T>.Failure($"easytier-cli 输出不是合法 JSON: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return CliResult<T>.Failure(ex.Message);
        }
    }
}
