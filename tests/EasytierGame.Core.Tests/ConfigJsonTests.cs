using EasytierGame.Core.Json;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 配置序列化等价性测试。重点是三类兼容：
/// 旧键名（camelCase + 旧拼写）、string / 数组双形态、带注释的 config.json。
/// </summary>
public class ConfigJsonTests
{
    [Fact]
    public void DefaultState_UsesLegacyKeyNames()
    {
        var json = JsonDefaults.Serialize(new AppState());

        // 旧拼写必须原样出现，否则老配置读不到
        Assert.Contains("\"disbleP2p\"", json);
        Assert.Contains("\"disbleListenner\"", json);
        Assert.Contains("\"enablExitNode\"", json);
        Assert.Contains("\"latencyfirst\"", json);
        Assert.Contains("\"netCardMetricValue\"", json);
        Assert.Contains("\"serverUrl\"", json);
    }

    [Fact]
    public void Serialize_IsIndented()
    {
        // 旧版写 config.json 用 JSON.stringify(x, null, 4)（4 空格）；
        // .NET 8 的 JsonWriterOptions 不支持指定缩进宽度（IndentSize 需 .NET 9+），
        // 因此由 JsonDefaults.Indent4 后处理成 4 空格。
        var json = JsonDefaults.Serialize(new AppState());

        Assert.Contains("\n", json);
        Assert.Contains("\n    \"config\": {", json);
        Assert.Contains("\n        \"protocol\": [", json);
    }

    [Fact]
    public void Serialize_DoesNotEscapeChinese()
    {
        var state = new AppState();
        state.Config.NetworkName = "我的房间";

        var json = JsonDefaults.Serialize(state);

        Assert.Contains("我的房间", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void ServerUrl_AsString_SplitByComma()
    {
        const string json = """{ "config": { "serverUrl": "a.cn:11010,b.cn:11010" } }""";

        var state = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Equal(new[] { "a.cn:11010", "b.cn:11010" }, state.Config.ServerUrl.ToArray());
    }

    [Fact]
    public void ServerUrl_AsSingleString_BecomesSingleItem()
    {
        const string json = """{ "config": { "serverUrl": "public.easytier.top:11010" } }""";

        var state = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Single(state.Config.ServerUrl);
        Assert.Equal("public.easytier.top:11010", state.Config.ServerUrl[0]);
    }

    [Fact]
    public void ServerUrl_AsArray_KeptInOrder()
    {
        const string json = """{ "config": { "serverUrl": ["x.cn:1", "y.cn:2"] } }""";

        var state = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Equal(new[] { "x.cn:1", "y.cn:2" }, state.Config.ServerUrl.ToArray());
    }

    [Fact]
    public void ServerUrl_AsEmptyString_BecomesEmptyList()
    {
        const string json = """{ "config": { "serverUrl": "" } }""";

        var state = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Empty(state.Config.ServerUrl);
    }

    [Fact]
    public void ServerUrl_AsNull_BecomesEmptyList()
    {
        const string json = """{ "config": { "serverUrl": null } }""";

        var state = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Empty(state.Config.ServerUrl);
    }

    [Fact]
    public void Json_WithLineComments_IsParsed()
    {
        // 对应 easytier/config_template.json 的写法
        const string json = """
        {
            // 协议类型
            "protocol": ["tcp", "udp"], // 行尾注释
            "networkName": "room" /* 块注释 */
        }
        """;

        var config = JsonDefaults.Deserialize<AppConfig>(json)!;

        Assert.Equal(new[] { "tcp", "udp" }, config.Protocol.ToArray());
        Assert.Equal("room", config.NetworkName);
    }

    [Fact]
    public void Json_WithTrailingCommas_IsParsed()
    {
        const string json = """{ "networkName": "room", "dhcp": true, }""";

        var config = JsonDefaults.Deserialize<AppConfig>(json)!;

        Assert.Equal("room", config.NetworkName);
        Assert.True(config.Dhcp);
    }

    [Fact]
    public void Json_IsCaseInsensitive()
    {
        const string json = """{ "NetworkName": "room", "disbleP2p": true }""";

        var config = JsonDefaults.Deserialize<AppConfig>(json)!;

        Assert.Equal("room", config.NetworkName);
        Assert.True(config.DisbleP2p);
    }

    [Fact]
    public void UnknownFields_AreIgnored()
    {
        // 旧版 config.json 里被 persist.pick 排除的字段（proxyNetworks/autoStart 等）
        // 在生成 config.json 时被剔除，回读时也应容错
        const string json = """{ "networkName": "room", "someFutureField": 123 }""";

        var config = JsonDefaults.Deserialize<AppConfig>(json)!;

        Assert.Equal("room", config.NetworkName);
    }

    [Fact]
    public void TryDeserialize_BrokenJson_ReturnsError()
    {
        var ok = JsonDefaults.TryDeserialize<AppConfig>("{ this is not json", out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void NumericValues_WrittenAsString_AreRead()
    {
        // 老配置里可能把数字写成字符串
        const string json = """{ "netCardMetricValue": "5", "port": 11010 }""";

        var config = JsonDefaults.Deserialize<AppConfig>(json)!;

        Assert.Equal(5, config.NetCardMetricValue);
        Assert.Equal("11010", config.Port);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var state = new AppState();
        state.Config.NetworkName = "room";
        state.Config.Protocol = new List<string> { "tcp", "ws" };
        state.Config.ServerUrl = new List<string> { "a.cn:11010" };
        state.Config.DisbleP2p = true;
        state.Config.NetCardMetricValue = 7;
        state.ProxyForwardBySystem = true;

        var json = JsonDefaults.Serialize(state);
        var back = JsonDefaults.Deserialize<AppState>(json)!;

        Assert.Equal("room", back.Config.NetworkName);
        Assert.Equal(new[] { "tcp", "ws" }, back.Config.Protocol.ToArray());
        Assert.Equal(new[] { "a.cn:11010" }, back.Config.ServerUrl.ToArray());
        Assert.True(back.Config.DisbleP2p);
        Assert.Equal(7, back.Config.NetCardMetricValue);
        Assert.True(back.ProxyForwardBySystem);
    }

    [Fact]
    public void MemberList_ParsesCliOutput()
    {
        // 字段名取自 easytier-cli -o json peer list
        const string json = """
        [
            {
                "cidr": "",
                "ipv4": "10.126.126.1",
                "hostname": "player1",
                "cost": "Local",
                "lat_ms": "0.0",
                "loss_rate": "0.0",
                "rx_bytes": "1.2 kB",
                "tx_bytes": "3.4 kB",
                "tunnel_proto": "",
                "nat_type": "",
                "id": "1234567890",
                "version": "2.4.5"
            }
        ]
        """;

        var members = JsonDefaults.Deserialize<List<Member>>(json)!;

        Assert.Single(members);
        Assert.Equal("Local", members[0].Cost);
        Assert.Equal("10.126.126.1", members[0].Ipv4);
        Assert.Equal("1.2 kB", members[0].RxBytes);
    }

    [Fact]
    public void RouteList_ParsesCliOutput()
    {
        const string json = """
        [
            {
                "peer_id": 42,
                "ipv4_addr": "10.126.126.1",
                "next_hop_peer_id": 0,
                "cost": 1,
                "path_latency": 3,
                "proxy_cidrs": ["192.168.1.0/24"],
                "hostname": "host"
            }
        ]
        """;

        var routes = JsonDefaults.Deserialize<List<RouteEntry>>(json)!;

        Assert.Single(routes);
        Assert.Equal("host", routes[0].Hostname);
        Assert.Equal(new[] { "192.168.1.0/24" }, routes[0].ProxyCidrs.ToArray());
    }

    [Fact]
    public void ProxyList_ParsesCliOutput()
    {
        const string json = """
        [
            { "src": "1.2.3.4:80", "dst": "10.126.126.2:80", "start_time": "2024", "state": "Connected", "transport_type": "kcp" }
        ]
        """;

        var proxies = JsonDefaults.Deserialize<List<ProxyEntry>>(json)!;

        Assert.Single(proxies);
        Assert.Equal("kcp", proxies[0].TransportType);
    }
}
