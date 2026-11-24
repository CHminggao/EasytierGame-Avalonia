using System.Text.RegularExpressions;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Args;

/// <summary>
/// 把界面配置翻译成 <c>easytier-core.exe</c> 的命令行参数。
/// 逐行等价于旧版 <c>pages/index.vue::getArgs()</c> 与 <c>composables/server.ts::getServerArgs()</c>，
/// <b>参数的顺序、开关条件、布尔值写法（部分带 "true"、部分不带）必须完全一致</b>，
/// 因此本类刻意不做任何“顺手优化”。测试见 <c>ArgsBuilderTests</c>。
/// </summary>
public static partial class ArgsBuilder
{
    /// <summary>子网代理 CIDR 校验（旧版正则）。</summary>
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+/\d+$")]
    private static partial Regex CidrRegex();

    /// <summary>地址是否已自带协议前缀，例如 <c>tcp://host:port</c>。</summary>
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*://")]
    private static partial Regex ProtocolPrefixRegex();

    /// <summary>
    /// 客户端参数。返回 <see cref="ArgsBuildResult"/>。
    /// </summary>
    public static ArgsBuildResult BuildClient(AppState state, ClientArgsContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        context ??= new ClientArgsContext();

        var config = state.Config;
        var args = new List<string>();

        // ---- 1. 外部配置文件优先，命中后其余界面配置全部忽略 ----
        if (state.ConfigStartEnable)
        {
            if (string.IsNullOrWhiteSpace(state.ConfigPath))
            {
                return new ArgsBuildResult
                {
                    Status = ArgsBuildStatus.ConfigPathMissing,
                    Message = "配置文件不存在 请选择",
                };
            }

            if (!context.ConfigFileExists(state.ConfigPath))
            {
                return new ArgsBuildResult
                {
                    Status = ArgsBuildStatus.ConfigFileNotFound,
                    Message = "配置文件不存在 请重新选择",
                };
            }

            var absPath = context.ResolveConfigPath(state.ConfigPath);
            return new ArgsBuildResult
            {
                Status = ArgsBuildStatus.Ok,
                Args = new[] { "-c", absPath },
            };
        }

        // ---- 2. 前置校验 ----

        // 成员名必填：未填时启动联机无意义（同网中无法区分节点），直接拦住
        if (string.IsNullOrWhiteSpace(config.Hostname))
        {
            return new ArgsBuildResult
            {
                Status = ArgsBuildStatus.NoHostname,
                Message = "请先填写成员名",
            };
        }

        if (config.ServerUrl.Count <= 0)
        {
            return new ArgsBuildResult
            {
                Status = ArgsBuildStatus.NoServer,
                Message = "请至少选择一个服务器",
            };
        }

        var formatUrlArr = config.ServerUrl
            .Select(url => (url ?? "").Replace('\\', '/'))
            .ToList();

        // 地址全部自带协议前缀时无需勾选协议
        if (config.Protocol.Count <= 0 && !formatUrlArr.All(HasProtocolPrefix))
        {
            return new ArgsBuildResult
            {
                Status = ArgsBuildStatus.NoProtocol,
                Message = "请至少选择一个协议",
            };
        }

        // ---- 3. 基础身份 ----
        if (config.Dhcp)
        {
            args.Add("-d");
        }

        if (!string.IsNullOrEmpty(config.Hostname))
        {
            args.Add("--hostname");
            args.Add(config.Hostname);
        }

        if (!string.IsNullOrEmpty(config.NetworkName))
        {
            args.Add("--network-name");
            args.Add(config.NetworkName);
        }

        if (!string.IsNullOrEmpty(config.NetworkPassword))
        {
            args.Add("--network-secret");
            args.Add(config.NetworkPassword);
        }

        if (!string.IsNullOrEmpty(config.Ipv4))
        {
            args.Add("--ipv4");
            args.Add(config.Ipv4.Trim());
        }

        // ---- 4. peers：带前缀的原样直连，未带前缀的按勾选协议展开 ----
        var peerSpecifiedUrls = formatUrlArr.Where(HasProtocolPrefix).ToList();
        var plainUrls = formatUrlArr.Where(url => !HasProtocolPrefix(url)).ToList();
        args.Add("--peers");
        args.AddRange(peerSpecifiedUrls);
        args.AddRange(MixedArray(config.Protocol, plainUrls, "://"));

        if (config.EnableCustomProtocol && config.Protocol.Contains(config.CustomProtocol))
        {
            args.Add("--default-protocol");
            args.Add(config.CustomProtocol);
        }

        if (config.DisbleP2p)
        {
            args.Add("--disable-p2p");
        }

        if (config.DisableIpv6)
        {
            args.Add("--disable-ipv6");
            args.Add("true");
        }

        // ---- 5. 监听 ----
        if (config.DisbleListenner)
        {
            args.Add("--no-listener");
        }

        if (!config.DisbleListenner && config.EnableCustomListener && !string.IsNullOrEmpty(config.CustomListenerData))
        {
            var customListener = SplitLines(config.CustomListenerData);
            if (customListener.Count > 0)
            {
                args.Add("-l");
                args.AddRange(customListener);
            }
        }

        if (!config.DisbleListenner && !config.EnableCustomListener && !string.IsNullOrEmpty(config.Port))
        {
            args.Add("-l");
            args.Add(config.Port);
        }

        // ---- 6. 子网代理 ----
        string? normalizedProxyNetworks = null;
        if (state.CidrEnable && !string.IsNullOrEmpty(config.ProxyNetworks))
        {
            var formatProxyNetworks = config.ProxyNetworks.Split('\n')
                .Select(line => line.Trim())
                .ToList();

            foreach (var cidr in formatProxyNetworks.Where(cidr => CidrRegex().IsMatch(cidr)))
            {
                args.Add("-n");
                args.Add(cidr);
            }

            // 旧版回写的是“去空行后的全部行”，非仅合法项
            normalizedProxyNetworks = string.Join("\n", formatProxyNetworks);
        }

        // ---- 7. 传输与网络开关 ----
        if (config.DisableEncryption)
        {
            args.Add("--disable-encryption");
            args.Add("true");
        }

        if (config.MultiThread)
        {
            args.Add("--multi-thread");
            args.Add("true");

            // 工作线程数只在开启多线程时才有意义，且内核约定最小值为 2。
            // 没勾多线程时不再单独传 --multi-thread-count。
            if (config.MultiThreadCount >= 2)
            {
                args.Add("--multi-thread-count");
                args.Add(config.MultiThreadCount.ToString());
            }
        }

        if (config.EnablExitNode)
        {
            args.Add("--enable-exit-node");
            args.Add("true");
        }

        if (config.NoTun)
        {
            args.Add("--no-tun");
            args.Add("true");
        }

        if (config.Latencyfirst)
        {
            args.Add("--latency-first");
            args.Add("true");
        }

        if (config.UseSmoltcp)
        {
            args.Add("--use-smoltcp");
            args.Add("true");
        }

        if (config.DisableUdpHolePunching)
        {
            args.Add("--disable-udp-hole-punching");
            args.Add("true");
        }

        if (config.DisableSymHolePunching)
        {
            args.Add("--disable-sym-hole-punching");
            args.Add("true");
        }

        if (config.RelayAllPeerrpc)
        {
            args.Add("--relay-all-peer-rpc");
            args.Add("true");
        }

        if (config.SaveErrorLog)
        {
            args.Add("--file-log-level");
            args.Add(config.LogLevel);
            args.Add("--file-log-dir");
            args.Add(context.LogDirectory);
        }

        if (config.DevName && !string.IsNullOrEmpty(config.DevNameValue))
        {
            args.Add("--dev-name");
            args.Add(config.DevNameValue);
        }

        if (!string.IsNullOrEmpty(config.Compression) && config.Compression != "none")
        {
            args.Add("--compression");
            args.Add(config.Compression);
        }

        // ---- 8. 代理与 NAT ----
        if (config.EnableKcpProxy)
        {
            args.Add("--enable-kcp-proxy");
            args.Add("true");
        }

        if (config.DisableKcpInput)
        {
            args.Add("--disable-kcp-input");
            args.Add("true");
        }

        if (config.EnableQuicProxy)
        {
            args.Add("--enable-quic-proxy");
            args.Add("true");
        }

        if (config.DisableQuicInput)
        {
            args.Add("--disable-quic-input");
            args.Add("true");
        }

        if (config.EnableUdpBroadcastRelay)
        {
            args.Add("--enable-udp-broadcast-relay");
            args.Add("true");
        }

        if (!config.EnableUpnp)
        {
            args.Add("--disable-upnp");
            args.Add("true");
        }

        if (config.Ipv6PublicAddrProvider)
        {
            args.Add("--ipv6-public-addr-provider");
            args.Add("true");
        }

        if (config.Ipv6PublicAddrAuto)
        {
            args.Add("--ipv6-public-addr-auto");
            args.Add("true");
        }

        if (config.DisableRelayData)
        {
            args.Add("--disable-relay-data");
            args.Add("true");
        }

        if (config.BindDeviceEnable)
        {
            args.Add("--bind-device");
            args.Add("true");
        }

        if (state.ProxyForwardBySystem)
        {
            args.Add("--proxy-forward-by-system");
            args.Add("true");
        }

        if (config.AcceptDNS)
        {
            args.Add("--accept-dns");
            args.Add("true");
        }

        // ---- 9. 端口转发 ----
        if (config.EnablePortForward && !string.IsNullOrEmpty(config.PortForwardData))
        {
            foreach (var rule in SplitLines(config.PortForwardData))
            {
                args.Add("--port-forward");
                args.Add(rule);
            }
        }

        if (config.PrivateMode)
        {
            args.Add("--private-mode");
            args.Add("true");
        }

        // ---- 10. 虚拟 IPv6 与端口白名单 ----
        if (!string.IsNullOrEmpty(config.Ipv6) && !config.DisableIpv6)
        {
            args.Add("--ipv6");
            args.Add(config.Ipv6.Trim());
        }

        var tcpWhitelist = (config.TcpWhitelist ?? "").Trim();
        if (config.TcpWhitelistEnable && !string.IsNullOrEmpty(tcpWhitelist))
        {
            args.Add("--tcp-whitelist");
            args.Add(tcpWhitelist);
        }

        var udpWhitelist = (config.UdpWhitelist ?? "").Trim();
        if (config.UdpWhitelistEnable && !string.IsNullOrEmpty(udpWhitelist))
        {
            args.Add("--udp-whitelist");
            args.Add(udpWhitelist);
        }

        return new ArgsBuildResult
        {
            Status = ArgsBuildStatus.Ok,
            Args = args,
            NormalizedProxyNetworks = normalizedProxyNetworks,
        };
    }

    /// <summary>
    /// 旧 <c>utils/index.ts::mixedArray</c>：按“协议 × 地址”做笛卡尔展开，
    /// 结果为 <c>[p1+split+u1, p1+split+u2, p2+split+u1, ...]</c>（协议在外层）。
    /// </summary>
    public static List<string> MixedArray(IEnumerable<string> protocols, IEnumerable<string> values, string split = "")
    {
        var result = new List<string>();
        foreach (var protocol in protocols)
        {
            foreach (var value in values)
            {
                result.Add($"{protocol}{split}{value}");
            }
        }

        return result;
    }

    /// <summary>按行拆分 → 每行 Trim → 去掉空行。对应旧版 <c>split("\n").map(trim).filter(Boolean)</c>。</summary>
    public static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        return text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static bool HasProtocolPrefix(string url)
    {
        return ProtocolPrefixRegex().IsMatch((url ?? "").Replace('\\', '/').Trim());
    }
}
