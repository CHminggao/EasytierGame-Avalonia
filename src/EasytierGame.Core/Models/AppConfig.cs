namespace EasytierGame.Core.Models;

/// <summary>
/// 对应 Vue 版 <c>stores/index.ts</c> 中 <c>config</c> 节点的全部字段。
/// 属性名刻意沿用旧拼写（DisbleP2p / DisbleListenner / EnablExitNode / Latencyfirst），
/// 由 <see cref="Json.JsonDefaults"/> 的 camelCase 命名策略落成旧 JSON 键名，
/// 保证老用户的 config.json 与 Pinia 持久化数据可直接读取。
/// </summary>
public sealed class AppConfig
{
    /// <summary>网络协议，默认 ["tcp"]。</summary>
    public List<string> Protocol { get; set; } = new() { "tcp" };

    /// <summary>服务器地址。默认为空：不再内置公共服务器，由用户自行添加。</summary>
    public List<string> ServerUrl { get; set; } = new();

    public string NetworkName { get; set; } = "";

    public string NetworkPassword { get; set; } = "";

    public string Hostname { get; set; } = "";

    public string Ipv4 { get; set; } = "";

    public string Ipv6 { get; set; } = "";

    /// <summary>TCP 端口白名单开关。</summary>
    public bool TcpWhitelistEnable { get; set; }

    public bool UdpWhitelistEnable { get; set; }

    public string TcpWhitelist { get; set; } = "";

    public string UdpWhitelist { get; set; } = "";

    /// <summary>子网代理 CIDR，一行一个。</summary>
    public string ProxyNetworks { get; set; } = "";

    /// <summary>开机自启（任务计划）。</summary>
    public bool AutoStart { get; set; }

    /// <summary>软件启动后自动联机。</summary>
    public bool ConnectAfterStart { get; set; }

    public bool DisableIpv6 { get; set; }

    /// <summary>监听端口（未启用自定义监听时使用）。</summary>
    public string Port { get; set; } = "0";

    public bool EnableCustomListener { get; set; } = true;

    /// <summary>自定义监听地址，一行一个。</summary>
    public string CustomListenerData { get; set; } = "tcp://0.0.0.0:11010\nudp://0.0.0.0:11010";

    /// <summary>不监听任何端口（旧拼写）。</summary>
    public bool DisbleListenner { get; set; }

    public bool DisableEncryption { get; set; }

    public bool MultiThread { get; set; }

    /// <summary>
    /// 运行时工作线程数（<c>--multi-thread-count</c>）。
    /// 内核约定：独占多线程模式下默认及最小值为 2。
    /// 界面默认填 2，低于 2 时归一到 2。
    /// </summary>
    public int MultiThreadCount { get; set; } = 2;

    /// <summary>允许成为出口节点（旧拼写）。</summary>
    public bool EnablExitNode { get; set; }

    public bool NoTun { get; set; }

    /// <summary>低延迟模式（旧拼写）。</summary>
    public bool Latencyfirst { get; set; }

    public bool UseSmoltcp { get; set; }

    public bool DisableUdpHolePunching { get; set; }

    /// <summary>
    /// 禁用基于生日攻击的对称 NAT (NAT4) UDP 打洞（<c>--disable-sym-hole-punching</c>）。
    /// 该打洞方式可能被运营商封锁，遇到 NAT4 打不通时可试。
    /// </summary>
    public bool DisableSymHolePunching { get; set; }

    public bool RelayAllPeerrpc { get; set; } = true;

    /// <summary>强制中转（旧拼写，对应 --disable-p2p）。</summary>
    public bool DisbleP2p { get; set; }

    public bool Dhcp { get; set; } = true;

    public bool SaveErrorLog { get; set; } = true;

    public string LogLevel { get; set; } = "error";

    public bool DevName { get; set; } = true;

    public string DevNameValue { get; set; } = "etgame";

    public bool EnableCustomProtocol { get; set; } = true;

    public string CustomProtocol { get; set; } = "tcp";

    public bool EnableNetCardMetric { get; set; } = true;

    public int NetCardMetricValue { get; set; } = 1;

    public bool EnablePreventSleep { get; set; }

    /// <summary>Windows UDP 广播中继。</summary>
    public bool EnableUdpBroadcastRelay { get; set; } = true;

    /// <summary>UPnP 端口映射，默认开启（关闭时传 --disable-upnp）。</summary>
    public bool EnableUpnp { get; set; } = true;

    public bool Ipv6PublicAddrProvider { get; set; }

    public bool Ipv6PublicAddrAuto { get; set; }

    public bool DisableRelayData { get; set; }

    /// <summary>压缩算法，"none" 表示不传参。</summary>
    public string Compression { get; set; } = "none";

    public bool EnableKcpProxy { get; set; } = true;

    public bool DisableKcpInput { get; set; }

    public bool EnableQuicProxy { get; set; }

    public bool DisableQuicInput { get; set; }

    public bool BindDeviceEnable { get; set; }

    /// <summary>魔法 DNS。</summary>
    public bool AcceptDNS { get; set; }

    public bool PrivateMode { get; set; }

    public bool EnablePortForward { get; set; }

    /// <summary>端口转发规则，一行一个。</summary>
    public string PortForwardData { get; set; } = "";
}
