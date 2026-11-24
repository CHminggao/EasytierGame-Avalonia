using System.Text.Json.Serialization;

namespace EasytierGame.Core.Models;

/// <summary>
/// <c>easytier-cli -o json peer list</c> 的输出元素。
/// 旧前端全部按 string 处理（<c>lat_ms</c>/<c>loss_rate</c>/<c>rx_bytes</c> 都是带单位的字符串），
/// 这里保留 string 类型，避免反序列化时因格式差异抛异常。
/// 字段名是 CLI 的 snake_case，必须显式标注 <see cref="JsonPropertyNameAttribute"/>。
/// </summary>
public sealed class Member
{
    public string Cidr { get; set; } = "";

    public string Ipv4 { get; set; } = "";

    public string Hostname { get; set; } = "";

    /// <summary>"Local" / "P2P" / "Relay" 等。</summary>
    public string Cost { get; set; } = "";

    [JsonPropertyName("lat_ms")]
    public string LatMs { get; set; } = "";

    [JsonPropertyName("loss_rate")]
    public string LossRate { get; set; } = "";

    [JsonPropertyName("rx_bytes")]
    public string RxBytes { get; set; } = "";

    [JsonPropertyName("tx_bytes")]
    public string TxBytes { get; set; } = "";

    [JsonPropertyName("tunnel_proto")]
    public string TunnelProto { get; set; } = "";

    [JsonPropertyName("nat_type")]
    public string NatType { get; set; } = "";

    public string Id { get; set; } = "";

    public string Version { get; set; } = "";

    /// <summary>由 <c>peer -v</c> 的输出补齐，界面用于显示连接远端地址。</summary>
    [JsonIgnore]
    public List<string> ConnectionsAddrs { get; set; } = new();

    /// <summary>由 <c>proxy</c> 的输出补齐：[transport_type, state]。</summary>
    [JsonIgnore]
    public string[]? Proxy { get; set; }
}

/// <summary><c>easytier-cli -v -o json peer</c> 的连通性信息（只取界面用到的部分）。</summary>
public sealed class PeerConnections
{
    public PeerConnectionsRoute Route { get; set; } = new();

    public PeerConnectionsPeer Peer { get; set; } = new();
}

public sealed class PeerConnectionsRoute
{
    [JsonPropertyName("peer_id")]
    public long PeerId { get; set; }

    [JsonPropertyName("ipv4_addr")]
    public string? Ipv4Addr { get; set; }
}

public sealed class PeerConnectionsPeer
{
    [JsonPropertyName("peer_id")]
    public long PeerId { get; set; }

    public List<PeerConnection> Conns { get; set; } = new();
}

public sealed class PeerConnection
{
    [JsonPropertyName("conn_id")]
    public string ConnId { get; set; } = "";

    public PeerConnectionTunnel? Tunnel { get; set; }
}

public sealed class PeerConnectionTunnel
{
    [JsonPropertyName("tunnel_type")]
    public string TunnelType { get; set; } = "";

    [JsonPropertyName("local_addr")]
    public PeerConnectionEndpoint? LocalAddr { get; set; }

    [JsonPropertyName("remote_addr")]
    public PeerConnectionEndpoint? RemoteAddr { get; set; }
}

public sealed class PeerConnectionEndpoint
{
    public string Url { get; set; } = "";
}

/// <summary><c>easytier-cli -o json proxy</c> 的输出元素。</summary>
public sealed class ProxyEntry
{
    public string Src { get; set; } = "";

    public string Dst { get; set; } = "";

    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = "";

    /// <summary>"Connected" / "Closed"。</summary>
    public string State { get; set; } = "";

    [JsonPropertyName("transport_type")]
    public string TransportType { get; set; } = "";
}

/// <summary><c>easytier-cli -o json route</c> 的输出元素（子网代理页路由表）。</summary>
public sealed class RouteEntry
{
    [JsonPropertyName("peer_id")]
    public long PeerId { get; set; }

    [JsonPropertyName("ipv4_addr")]
    public string? Ipv4Addr { get; set; }

    [JsonPropertyName("next_hop_peer_id")]
    public long NextHopPeerId { get; set; }

    public long Cost { get; set; }

    [JsonPropertyName("path_latency")]
    public long PathLatency { get; set; }

    [JsonPropertyName("proxy_cidrs")]
    public List<string> ProxyCidrs { get; set; } = new();

    public string Hostname { get; set; } = "";
}
