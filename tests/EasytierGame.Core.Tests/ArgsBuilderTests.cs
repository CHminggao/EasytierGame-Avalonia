using EasytierGame.Core.Args;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 参数等价性快照测试。期望值逐条对应旧版
/// <c>pages/index.vue::getArgs()</c> 与 <c>composables/server.ts::getServerArgs()</c> 的输出。
/// </summary>
public class ArgsBuilderTests
{
    // ------------------------------------------------------------------
    // 帮助函数
    // ------------------------------------------------------------------

    /// <summary>构造一个“最小可运行、其余全关”的状态，便于逐项打开开关做断言。</summary>
    private static AppState MinimalState()
    {
        return new AppState
        {
            Config = new AppConfig
            {
                Protocol = new List<string> { "tcp" },
                ServerUrl = new List<string> { "public.easytier.top:11010" },
                Hostname = "player1",   // 成员名现为必填，最小状态也要有
                Dhcp = true,
                EnableCustomListener = true,
                CustomListenerData = "tcp://0.0.0.0:11010",
                RelayAllPeerrpc = true,   // 默认开启
                EnableKcpProxy = true,    // 默认开启
                EnableUdpBroadcastRelay = true,
                EnableUpnp = true,
                DevName = true,
                DevNameValue = "etgame",
                EnableCustomProtocol = true,
                CustomProtocol = "tcp",
                SaveErrorLog = true,
                LogLevel = "error",
                Port = "0",
                Compression = "none",
            },
            CidrEnable = false,
            ProxyForwardBySystem = false,
        };
    }

    private static List<string> Args(AppState state) =>
        ArgsBuilder.BuildClient(state).Args.ToList();

    /// <summary>把参数数组压回一条命令串，便于整串比对。</summary>
    private static string Join(IEnumerable<string> args) => string.Join(" ", args);

    // ------------------------------------------------------------------
    // 默认最小配置
    // ------------------------------------------------------------------

    [Fact]
    public void MinimalConfig_ProducesExpectedSequence()
    {
        var state = MinimalState();

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.Ok, result.Status);
        Assert.Equal(
            new[]
            {
                "-d",
                // 成员名现为必填，MinimalState 默认填了 player1
                "--hostname", "player1",
                "--peers", "tcp://public.easytier.top:11010",
                "--default-protocol", "tcp",
                "-l", "tcp://0.0.0.0:11010",
                "--relay-all-peer-rpc", "true",
                "--file-log-level", "error", "--file-log-dir", "easytier/logs/",
                "--dev-name", "etgame",
                "--enable-kcp-proxy", "true",
                "--enable-udp-broadcast-relay", "true",
            },
            result.Args.ToArray());
    }

    // ------------------------------------------------------------------
    // 外部配置文件优先
    // ------------------------------------------------------------------

    [Fact]
    public void ConfigFileMode_ReturnsOnlyDashC()
    {
        var state = MinimalState();
        state.ConfigStartEnable = true;
        state.ConfigPath = "easytier/config/net.toml";

        var result = ArgsBuilder.BuildClient(state, new ClientArgsContext
        {
            ConfigFileExists = _ => true,
            ResolveConfigPath = p => @"C:\app\" + p.Replace('/', '\\'),
        });

        Assert.Equal(ArgsBuildStatus.Ok, result.Status);
        Assert.True(result.IsConfigFileMode);
        Assert.Equal(new[] { "-c", @"C:\app\easytier\config\net.toml" }, result.Args.ToArray());
    }

    [Fact]
    public void ConfigFileMode_MissingPath_ReportsConfigPathMissing()
    {
        var state = MinimalState();
        state.ConfigStartEnable = true;
        state.ConfigPath = "";

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.ConfigPathMissing, result.Status);
        Assert.Equal("配置文件不存在 请选择", result.Message);
        Assert.Empty(result.Args);
    }

    [Fact]
    public void ConfigFileMode_FileNotExists_ReportsNotFound()
    {
        var state = MinimalState();
        state.ConfigStartEnable = true;
        state.ConfigPath = "easytier/config/gone.toml";

        var result = ArgsBuilder.BuildClient(state, new ClientArgsContext { ConfigFileExists = _ => false });

        Assert.Equal(ArgsBuildStatus.ConfigFileNotFound, result.Status);
        Assert.Equal("配置文件不存在 请重新选择", result.Message);
    }

    // ------------------------------------------------------------------
    // 前置校验
    // ------------------------------------------------------------------

    [Fact]
    public void NoServer_ReportsNoServer()
    {
        var state = MinimalState();
        state.Config.ServerUrl.Clear();

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.NoServer, result.Status);
        Assert.Equal("请至少选择一个服务器", result.Message);
    }

    [Fact]
    public void NoProtocol_WithPlainUrls_ReportsNoProtocol()
    {
        var state = MinimalState();
        state.Config.Protocol.Clear();

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.NoProtocol, result.Status);
        Assert.Equal("请至少选择一个协议", result.Message);
    }

    [Fact]
    public void NoProtocol_ButAllUrlsPrefixed_IsAllowed()
    {
        var state = MinimalState();
        state.Config.Protocol.Clear();
        state.Config.ServerUrl = new List<string> { "tcp://public.easytier.top:11010" };

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.Ok, result.Status);
        Assert.Contains("tcp://public.easytier.top:11010", result.Args);
    }

    // ------------------------------------------------------------------
    // peers 展开
    // ------------------------------------------------------------------

    [Fact]
    public void Peers_ExpandsProtocolsByCartesianProduct()
    {
        var state = MinimalState();
        state.Config.Protocol = new List<string> { "tcp", "udp" };
        state.Config.ServerUrl = new List<string> { "a.cn:11010", "b.cn:11010" };

        var args = Args(state);
        var firstPeerIdx = args.IndexOf("--peers");
        var peers = args.Skip(firstPeerIdx + 1)
            .TakeWhile(a => !a.StartsWith("--"))
            .ToList();

        Assert.Equal(
            new[] { "tcp://a.cn:11010", "tcp://b.cn:11010", "udp://a.cn:11010", "udp://b.cn:11010" },
            peers.ToArray());
    }

    [Fact]
    public void Peers_PrefixedUrlsKeptAsIs_PlainOnesExpanded()
    {
        var state = MinimalState();
        state.Config.Protocol = new List<string> { "tcp" };
        state.Config.ServerUrl = new List<string> { "udp://fixed.cn:11010", "plain.cn:11010" };

        var args = Args(state);
        var peerIdx = args.IndexOf("--peers");
        var peers = args.Skip(peerIdx + 1)
            .TakeWhile(a => !a.StartsWith("--"))
            .ToList();

        // 带前缀的在前，展开的在后
        Assert.Equal(new[] { "udp://fixed.cn:11010", "tcp://plain.cn:11010" }, peers.ToArray());
    }

    [Fact]
    public void Peers_BackslashNormalizedToSlash()
    {
        var state = MinimalState();
        state.Config.ServerUrl = new List<string> { @"tcp:\\public.easytier.top:11010" };

        var args = Args(state);

        Assert.Contains("tcp://public.easytier.top:11010", args);
    }

    [Fact]
    public void DefaultProtocol_OnlyWhenProtocolListContainsIt()
    {
        var state = MinimalState();
        state.Config.Protocol = new List<string> { "udp" };
        state.Config.CustomProtocol = "tcp";

        var args = Args(state);

        Assert.DoesNotContain("--default-protocol", args);
    }

    // ------------------------------------------------------------------
    // 监听
    // ------------------------------------------------------------------

    [Fact]
    public void CustomListener_MultipleLines_EachBecomesListener()
    {
        var state = MinimalState();
        state.Config.CustomListenerData = "  tcp://0.0.0.0:11010  \n\nudp://0.0.0.0:11010\n";

        var args = Args(state);
        var idx = args.IndexOf("-l");
        var listeners = args.Skip(idx + 1)
            .TakeWhile(a => !a.StartsWith("--") && a != "-n")
            .ToList();

        Assert.Equal(new[] { "tcp://0.0.0.0:11010", "udp://0.0.0.0:11010" }, listeners.ToArray());
    }

    [Fact]
    public void NoListener_TakesPrecedenceOverCustomListener()
    {
        var state = MinimalState();
        state.Config.DisbleListenner = true;
        state.Config.EnableCustomListener = true;
        state.Config.CustomListenerData = "tcp://0.0.0.0:11010";

        var args = Args(state);

        Assert.Contains("--no-listener", args);
        Assert.DoesNotContain("-l", args);
    }

    [Fact]
    public void PortListener_UsedWhenCustomListenerDisabled()
    {
        var state = MinimalState();
        state.Config.EnableCustomListener = false;
        state.Config.Port = "11010";

        var args = Args(state);
        var idx = args.IndexOf("-l");

        Assert.True(idx >= 0);
        Assert.Equal("11010", args[idx + 1]);
    }

    // ------------------------------------------------------------------
    // 子网代理
    // ------------------------------------------------------------------

    [Fact]
    public void CidrDisabled_NoDashN()
    {
        var state = MinimalState();
        state.Config.ProxyNetworks = "192.168.1.0/24";
        state.CidrEnable = false;

        Assert.DoesNotContain("-n", Args(state));
    }

    [Fact]
    public void CidrEnabled_OnlyValidCidrBecomesDashN()
    {
        var state = MinimalState();
        state.CidrEnable = true;
        state.Config.ProxyNetworks = "192.168.1.0/24\nnot-a-cidr\n10.0.0.0/8";

        var result = ArgsBuilder.BuildClient(state);
        var args = result.Args.ToList();

        Assert.Equal(2, args.Count(a => a == "-n"));
        Assert.Contains("192.168.1.0/24", args);
        Assert.Contains("10.0.0.0/8", args);
        Assert.DoesNotContain("not-a-cidr", args);

        // 回写：保留全部行（含非法行），与旧版一致
        Assert.Equal("192.168.1.0/24\nnot-a-cidr\n10.0.0.0/8", result.NormalizedProxyNetworks);
    }

    // ------------------------------------------------------------------
    // 布尔开关 → 参数
    // ------------------------------------------------------------------

    [Theory]
    // 属性名（反射 setter）→ 期望参数（null 表示无输出以外的走向由其它测试覆盖）
    [InlineData(nameof(AppConfig.DisbleP2p), "--disable-p2p", false)]
    [InlineData(nameof(AppConfig.DisableIpv6), "--disable-ipv6", true)]
    [InlineData(nameof(AppConfig.DisableEncryption), "--disable-encryption", true)]
    [InlineData(nameof(AppConfig.MultiThread), "--multi-thread", true)]
    [InlineData(nameof(AppConfig.EnablExitNode), "--enable-exit-node", true)]
    [InlineData(nameof(AppConfig.NoTun), "--no-tun", true)]
    [InlineData(nameof(AppConfig.Latencyfirst), "--latency-first", true)]
    [InlineData(nameof(AppConfig.UseSmoltcp), "--use-smoltcp", true)]
    [InlineData(nameof(AppConfig.DisableUdpHolePunching), "--disable-udp-hole-punching", true)]
    [InlineData(nameof(AppConfig.DisableSymHolePunching), "--disable-sym-hole-punching", true)]
    [InlineData(nameof(AppConfig.DisableKcpInput), "--disable-kcp-input", true)]
    [InlineData(nameof(AppConfig.EnableQuicProxy), "--enable-quic-proxy", true)]
    [InlineData(nameof(AppConfig.DisableQuicInput), "--disable-quic-input", true)]
    [InlineData(nameof(AppConfig.Ipv6PublicAddrProvider), "--ipv6-public-addr-provider", true)]
    [InlineData(nameof(AppConfig.Ipv6PublicAddrAuto), "--ipv6-public-addr-auto", true)]
    [InlineData(nameof(AppConfig.DisableRelayData), "--disable-relay-data", true)]
    [InlineData(nameof(AppConfig.BindDeviceEnable), "--bind-device", true)]
    [InlineData(nameof(AppConfig.AcceptDNS), "--accept-dns", true)]
    [InlineData(nameof(AppConfig.PrivateMode), "--private-mode", true)]
    public void BooleanSwitch_ProducesExpectedFlag(string propertyName, string flag, bool expectTrailingTrue)
    {
        var state = MinimalState();
        typeof(AppConfig).GetProperty(propertyName)!.SetValue(state.Config, true);

        var args = Args(state);

        var idx = args.IndexOf(flag);
        Assert.True(idx >= 0, $"{propertyName} 应产生 {flag}");
        if (expectTrailingTrue)
        {
            Assert.Equal("true", args[idx + 1]);
        }
        else
        {
            Assert.NotEqual("true", args.ElementAtOrDefault(idx + 1));
        }
    }

    [Fact]
    public void Upnp_DisabledProducesDisableFlag()
    {
        var state = MinimalState();
        state.Config.EnableUpnp = false;

        var args = Args(state);
        var idx = args.IndexOf("--disable-upnp");

        Assert.True(idx >= 0);
        Assert.Equal("true", args[idx + 1]);
    }

    [Fact]
    public void ProxyForwardBySystem_ReadsFromStateNotConfig()
    {
        var state = MinimalState();
        state.ProxyForwardBySystem = true;

        var args = Args(state);
        var idx = args.IndexOf("--proxy-forward-by-system");

        Assert.True(idx >= 0);
        Assert.Equal("true", args[idx + 1]);
    }

    [Fact]
    public void SaveErrorLogDisabled_NoFileLogArgs()
    {
        var state = MinimalState();
        state.Config.SaveErrorLog = false;

        var args = Args(state);

        Assert.DoesNotContain("--file-log-level", args);
        Assert.DoesNotContain("--file-log-dir", args);
    }

    [Fact]
    public void LogDirectory_ComesFromContext()
    {
        var state = MinimalState();

        var result = ArgsBuilder.BuildClient(state, new ClientArgsContext { LogDirectory = @"D:\logs\" });
        var args = result.Args.ToList();
        var idx = args.IndexOf("--file-log-dir");

        Assert.Equal(@"D:\logs\", args[idx + 1]);
    }

    [Fact]
    public void Compression_NoneIsSkipped()
    {
        var state = MinimalState();
        state.Config.Compression = "none";
        Assert.DoesNotContain("--compression", Args(state));

        state.Config.Compression = "zstd";
        var args = Args(state);
        var idx = args.IndexOf("--compression");
        Assert.Equal("zstd", args[idx + 1]);
    }

    // ------------------------------------------------------------------
    // 端口转发 / 白名单 / IP
    // ------------------------------------------------------------------

    [Fact]
    public void PortForward_EachLineBecomesFlag()
    {
        var state = MinimalState();
        state.Config.EnablePortForward = true;
        state.Config.PortForwardData = "tcp://0.0.0.0:8080/192.168.1.5:80\n\n  udp://0.0.0.0:9000/192.168.1.5:9000  ";

        var args = Args(state);

        Assert.Equal(2, args.Count(a => a == "--port-forward"));
        Assert.Contains("tcp://0.0.0.0:8080/192.168.1.5:80", args);
        Assert.Contains("udp://0.0.0.0:9000/192.168.1.5:9000", args);
    }

    [Fact]
    public void Whitelists_OnlyWhenEnabledAndNonEmpty()
    {
        var state = MinimalState();
        state.Config.TcpWhitelistEnable = true;
        state.Config.TcpWhitelist = "80,443";
        state.Config.UdpWhitelistEnable = true;
        state.Config.UdpWhitelist = "   ";

        var args = Args(state);

        var tcpIdx = args.IndexOf("--tcp-whitelist");
        Assert.Equal("80,443", args[tcpIdx + 1]);
        Assert.DoesNotContain("--udp-whitelist", args);
    }

    [Fact]
    public void Ipv6_SkippedWhenIpv6Disabled()
    {
        var state = MinimalState();
        state.Config.Ipv6 = "fd00::1";
        state.Config.DisableIpv6 = true;

        Assert.DoesNotContain("--ipv6", Args(state));

        state.Config.DisableIpv6 = false;
        var args = Args(state);
        var idx = args.IndexOf("--ipv6");
        Assert.Equal("fd00::1", args[idx + 1]);
    }

    [Fact]
    public void Ipv4_IsTrimmed()
    {
        var state = MinimalState();
        state.Config.Dhcp = false;
        state.Config.Ipv4 = "  10.126.126.1  ";

        var args = Args(state);
        var idx = args.IndexOf("--ipv4");

        Assert.Equal("10.126.126.1", args[idx + 1]);
    }

    // ------------------------------------------------------------------
    // 辅助函数
    // ------------------------------------------------------------------

    [Fact]
    public void MixedArray_ProtocolOuterLoop()
    {
        var result = ArgsBuilder.MixedArray(
            new[] { "tcp", "udp" },
            new[] { "1.1.1.1", "2.2.2.2" },
            "://");

        Assert.Equal(
            new[] { "tcp://1.1.1.1", "tcp://2.2.2.2", "udp://1.1.1.1", "udp://2.2.2.2" },
            result.ToArray());
    }

    [Fact]
    public void SplitLines_TrimsAndDropsEmpty()
    {
        var result = ArgsBuilder.SplitLines(" a \n\n  b\r\n\tc\t\n");

        Assert.Equal(new[] { "a", @"b", "c" }, result.ToArray());
    }

    // ------------------------------------------------------------------
    // 多线程数（--multi-thread-count）
    // 只在开启多线程时才有意义；内核约定最小值为 2。
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void MultiThreadCount_BelowTwo_IsSkipped(int count)
    {
        var state = MinimalState();
        state.Config.MultiThread = true;
        state.Config.MultiThreadCount = count;

        var args = Args(state);

        // 勾了多线程但线程数非法 → 只传 --multi-thread，不传数量
        Assert.Contains("--multi-thread", args);
        Assert.DoesNotContain("--multi-thread-count", args);
    }

    [Theory]
    [InlineData(2, "2")]
    [InlineData(8, "8")]
    [InlineData(64, "64")]
    public void MultiThreadCount_AtLeastTwo_IsPassed(int count, string expected)
    {
        var state = MinimalState();
        state.Config.MultiThread = true;
        state.Config.MultiThreadCount = count;

        var args = Args(state);
        var idx = args.IndexOf("--multi-thread-count");

        Assert.True(idx >= 0);
        Assert.Equal(expected, args[idx + 1]);
    }

    [Fact]
    public void MultiThreadCount_NotPassedWhenMultiThreadUnchecked()
    {
        // 未勾选多线程时，即使线程数填了合法值也不应传给内核
        var state = MinimalState();
        state.Config.MultiThread = false;
        state.Config.MultiThreadCount = 4;

        var args = Args(state);

        Assert.DoesNotContain("--multi-thread", args);
        Assert.DoesNotContain("--multi-thread-count", args);
    }

    [Fact]
    public void SymHolePunching_False_NoFlag()
    {
        var state = MinimalState();
        state.Config.DisableSymHolePunching = false;

        Assert.DoesNotContain("--disable-sym-hole-punching", Args(state));
    }

    [Fact]
    public void SymHolePunching_IndependentOfUdpHolePunching()
    {
        // 两个开关互不影响
        var state = MinimalState();
        state.Config.DisableUdpHolePunching = false;
        state.Config.DisableSymHolePunching = true;

        var args = Args(state);

        Assert.Contains("--disable-sym-hole-punching", args);
        Assert.DoesNotContain("--disable-udp-hole-punching", args);
    }

    // ------------------------------------------------------------------
    // 成员名必填
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void Hostname_Empty_BlocksStart(string? hostname)
    {
        var state = MinimalState();
        state.Config.Hostname = hostname!;

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.NoHostname, result.Status);
        Assert.Equal("请先填写成员名", result.Message);
        Assert.False(result.IsRunnable);
    }

    [Fact]
    public void Hostname_Filled_Passes()
    {
        var state = MinimalState();
        state.Config.Hostname = "gm";

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.Ok, result.Status);
        // 成员名同时会作为 --hostname 传给内核
        Assert.Contains("--hostname", result.Args);
        Assert.Contains("gm", result.Args);
    }

    [Fact]
    public void Hostname_CheckedBeforeServerCheck()
    {
        // 两者都缺失时，优先提示成员名（更靠前的必填项）
        var state = MinimalState();
        state.Config.Hostname = "";
        state.Config.ServerUrl.Clear();

        var result = ArgsBuilder.BuildClient(state);

        Assert.Equal(ArgsBuildStatus.NoHostname, result.Status);
    }

    [Fact]
    public void Hostname_NotRequired_WhenUsingExternalConfigFile()
    {
        // 外部配置文件自带 hostname，不应被界面的成员名为难
        var state = MinimalState();
        state.Config.Hostname = "";
        state.ConfigStartEnable = true;
        state.ConfigPath = "easytier/config/net.toml";

        var result = ArgsBuilder.BuildClient(state, new ClientArgsContext
        {
            ConfigFileExists = _ => true,
            ResolveConfigPath = p => @"C:\app\" + p,
        });

        Assert.Equal(ArgsBuildStatus.Ok, result.Status);
        Assert.True(result.IsConfigFileMode);
    }
}
