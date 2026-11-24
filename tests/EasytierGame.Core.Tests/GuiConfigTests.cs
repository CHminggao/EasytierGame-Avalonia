using System.Text.Json;
using EasytierGame.Core.Config;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Tests;

/// <summary><c>easytier/config.json</c> 生成与读取测试。</summary>
public class GuiConfigTests
{
    private static AppState StateWithPeers()
    {
        var state = new AppState();
        state.Config.ServerUrl = new List<string> { "public.easytier.top:11010" };
        // BasePeers 默认为空（不再内置公共服务器），交集类用例需要显式给出候选池
        state.BasePeers = new List<string> { "public.easytier.top:11010", "public.easytier.net:11010" };
        state.Config.CustomListenerData = "tcp://0.0.0.0:11010\nudp://0.0.0.0:11010";
        return state;
    }

    [Fact]
    public void Build_ExcludesNonConnectionFields()
    {
        var state = StateWithPeers();
        state.Config.ProxyNetworks = "192.168.1.0/24";
        state.Config.AutoStart = true;
        state.Config.SaveErrorLog = true;
        state.Config.BindDeviceEnable = true;
        state.Config.AcceptDNS = true;
        state.Config.EnablePortForward = true;
        state.Config.PortForwardData = "tcp://0.0.0.0:1/1.1.1.1:2";

        var json = GuiConfigWriter.Build(state);

        Assert.DoesNotContain("\"proxyNetworks\"", json);
        Assert.DoesNotContain("\"autoStart\"", json);
        Assert.DoesNotContain("\"saveErrorLog\"", json);
        Assert.DoesNotContain("\"bindDeviceEnable\"", json);
        Assert.DoesNotContain("\"acceptDNS\"", json);
        Assert.DoesNotContain("\"enablePortForward\"", json);
        Assert.DoesNotContain("\"portForwardData\"", json);
    }

    [Fact]
    public void Build_ServerUrlIsArray()
    {
        var json = GuiConfigWriter.Build(StateWithPeers());
        using var doc = JsonDocument.Parse(json);

        var serverUrl = doc.RootElement.GetProperty("serverUrl");
        Assert.Equal(JsonValueKind.Array, serverUrl.ValueKind);
        Assert.Equal("public.easytier.top:11010", serverUrl[0].GetString());
    }

    [Fact]
    public void Build_CustomListenerDataIsArray()
    {
        var json = GuiConfigWriter.Build(StateWithPeers());
        using var doc = JsonDocument.Parse(json);

        var listeners = doc.RootElement.GetProperty("customListenerData");
        Assert.Equal(JsonValueKind.Array, listeners.ValueKind);
        Assert.Equal(2, listeners.GetArrayLength());
    }

    [Fact]
    public void Build_ServerUrlIntersectedWithBasePeers()
    {
        var state = StateWithPeers();
        // 用户临时输入的私有地址不应被固化进 config.json
        state.Config.ServerUrl = new List<string> { "public.easytier.top:11010", "stranger.cn:11010" };

        var json = GuiConfigWriter.Build(state);
        using var doc = JsonDocument.Parse(json);

        var arr = doc.RootElement.GetProperty("serverUrl");
        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("public.easytier.top:11010", arr[0].GetString());
    }

    [Fact]
    public void Build_ExtraServerUrlsAreMergedAndDeduped()
    {
        var state = StateWithPeers();
        state.BasePeers = new List<string> { "public.easytier.top:11010", "public.easytier.net:11010" };

        var json = GuiConfigWriter.Build(state, new[] { "public.easytier.net:11010", "public.easytier.top:11010" });
        using var doc = JsonDocument.Parse(json);

        var arr = doc.RootElement.GetProperty("serverUrl");
        Assert.Equal(2, arr.GetArrayLength());
    }

    [Fact]
    public void Build_KeyOrder_StartsWithServerUrl()
    {
        var json = GuiConfigWriter.Build(StateWithPeers());
        using var doc = JsonDocument.Parse(json);

        var first = doc.RootElement.EnumerateObject().First().Name;
        var second = doc.RootElement.EnumerateObject().Skip(1).First().Name;
        var third = doc.RootElement.EnumerateObject().Skip(2).First().Name;

        Assert.Equal("serverUrl", first);
        Assert.Equal("enableCustomListener", second);
        Assert.Equal("customListenerData", third);
    }

    [Fact]
    public void Apply_OverridesConfigAndReturnsServerUrls()
    {
        var state = new AppState();
        const string json = """
        {
            "serverUrl": ["a.cn:1", "b.cn:2"],
            "networkName": "room",
            "customListenerData": ["tcp://0.0.0.0:11010"]
        }
        """;

        var urls = GuiConfigWriter.Apply(json, state);

        Assert.Equal(new[] { "a.cn:1", "b.cn:2" }, urls.ToArray());
        Assert.Equal("room", state.Config.NetworkName);
        Assert.Equal("tcp://0.0.0.0:11010", state.Config.CustomListenerData);
        // 界面当前选择取第一个
        Assert.Equal(new[] { "a.cn:1" }, state.Config.ServerUrl.ToArray());
        // basePeers 并入
        Assert.Contains("a.cn:1", state.BasePeers);
        Assert.Contains("b.cn:2", state.BasePeers);
    }

    [Fact]
    public void Apply_ServerUrlAsString_SplitByComma()
    {
        var state = new AppState();
        const string json = """{ "serverUrl": "a.cn:1,b.cn:2", "networkName": "room" }""";

        var urls = GuiConfigWriter.Apply(json, state);

        Assert.Equal(new[] { "a.cn:1", "b.cn:2" }, urls.ToArray());
    }

    [Fact]
    public void Apply_KcpAndQuicBothOn_DisablesQuic()
    {
        var state = new AppState();
        const string json = """{ "enableKcpProxy": true, "enableQuicProxy": true }""";

        GuiConfigWriter.Apply(json, state);

        Assert.True(state.Config.EnableKcpProxy);
        Assert.False(state.Config.EnableQuicProxy);
    }

    [Fact]
    public void Apply_WithCommentsAndTrailingCommas()
    {
        var state = new AppState();
        const string json = """
        {
            // 房间名
            "networkName": "room",
            "dhcp": true,
        }
        """;

        GuiConfigWriter.Apply(json, state);

        Assert.Equal("room", state.Config.NetworkName);
        Assert.True(state.Config.Dhcp);
    }

    [Fact]
    public void Apply_BrokenJson_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => GuiConfigWriter.Apply("{ broken", new AppState()));
    }

    [Fact]
    public void BuildThenApply_RoundTrips()
    {
        var source = StateWithPeers();
        source.Config.NetworkName = "room";
        source.Config.Protocol = new List<string> { "tcp", "udp" };

        var json = GuiConfigWriter.Build(source);

        var target = new AppState();
        GuiConfigWriter.Apply(json, target);

        Assert.Equal("room", target.Config.NetworkName);
        Assert.Equal(new[] { "tcp", "udp" }, target.Config.Protocol.ToArray());
        Assert.Equal("tcp://0.0.0.0:11010\nudp://0.0.0.0:11010", target.Config.CustomListenerData);
    }
}
