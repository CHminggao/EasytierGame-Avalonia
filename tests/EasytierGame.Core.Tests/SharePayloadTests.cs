using EasytierGame.Core.Config;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Tests;

/// <summary>分享/导入配置的编解码测试（必须与浏览器 btoa+encodeURIComponent 互通）。</summary>
public class SharePayloadTests
{
    [Fact]
    public void EncodeUriComponent_MatchesBrowserSemantics()
    {
        // 浏览器 encodeURIComponent("a b/c?d=中") === "a%20b%2Fc%3Fd%3D%E4%B8%AD"
        Assert.Equal("a%20b%2Fc%3Fd%3D%E4%B8%AD", ShareCodec.EncodeUriComponent("a b/c?d=中"));
    }

    [Fact]
    public void EncodeUriComponent_KeepsUnreservedCharacters()
    {
        const string input = "-_.!~*'()AZaz09";
        Assert.Equal(input, ShareCodec.EncodeUriComponent(input));
    }

    [Fact]
    public void RoundTrip_PreservesChineseAndSymbols()
    {
        const string plain = """{"config":{"networkName":"房间 1","networkPassword":"p@ss/word"}}""";

        var encoded = ShareCodec.Encode(plain);
        var decoded = ShareCodec.Decode(encoded);

        Assert.Equal(plain, decoded);
    }

    [Fact]
    public void Decode_BrokenBase64_Throws()
    {
        Assert.Throws<FormatException>(() => ShareCodec.Decode("!!!not base64!!!"));
    }

    [Fact]
    public void TryDecode_ReturnsFalse_OnGarbage()
    {
        Assert.False(ShareCodec.TryDecode("abc", out var plain, out var error));
        Assert.Null(plain);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Build_ContainsExactlySharedFields()
    {
        var config = new AppConfig
        {
            NetworkName = "room",
            Hostname = "player1",       // 不在分享范围
            Ipv4 = "10.0.0.1",          // 不在分享范围
        };

        var json = SharePayload.BuildJson(config);

        Assert.Contains("\"networkName\": \"room\"", json);
        Assert.DoesNotContain("\"hostname\"", json);
        Assert.DoesNotContain("\"ipv4\"", json);
        Assert.DoesNotContain("\"proxyNetworks\"", json);
        Assert.DoesNotContain("\"autoStart\"", json);
        Assert.DoesNotContain("\"connectAfterStart\"", json);
        Assert.DoesNotContain("\"noTun\"", json);
        Assert.DoesNotContain("\"saveErrorLog\"", json);

        // 29 个字段全部在
        Assert.Contains("\"privateMode\"", json);
        Assert.Contains("\"enableQuicProxy\"", json);
        Assert.Contains("\"netCardMetricValue\"", json);
    }

    [Fact]
    public void Build_WrapsInConfigNode()
    {
        var json = SharePayload.BuildJson(new AppConfig());
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("config", out _));
    }

    [Fact]
    public void Apply_MergesOnlyPresentFields()
    {
        var config = new AppConfig
        {
            NetworkName = "old",
            Hostname = "keep-me",
            Ipv4 = "10.0.0.9",
        };

        var shareText = ShareCodec.Encode("""{"config":{"networkName":"new","networkPassword":"pw"}}""");

        var urls = SharePayload.Apply(shareText, config);

        Assert.Equal("new", config.NetworkName);
        Assert.Equal("pw", config.NetworkPassword);
        // 未出现在载荷里的字段保持不变
        Assert.Equal("keep-me", config.Hostname);
        Assert.Equal("10.0.0.9", config.Ipv4);
        Assert.Empty(urls);
    }

    [Fact]
    public void Apply_ServerUrl_AsArray()
    {
        var config = new AppConfig();
        var shareText = ShareCodec.Encode("""{"config":{"serverUrl":["a.cn:1","b.cn:2"]}}""");

        var urls = SharePayload.Apply(shareText, config);

        Assert.Equal(new[] { "a.cn:1", "b.cn:2" }, urls.ToArray());
        Assert.Equal(new[] { "a.cn:1", "b.cn:2" }, config.ServerUrl.ToArray());
    }

    [Fact]
    public void Apply_ServerUrl_AsCommaString()
    {
        var config = new AppConfig();
        var shareText = ShareCodec.Encode("""{"config":{"serverUrl":"a.cn:1,b.cn:2"}}""");

        var urls = SharePayload.Apply(shareText, config);

        Assert.Equal(new[] { "a.cn:1", "b.cn:2" }, urls.ToArray());
    }

    [Fact]
    public void Apply_ServerUrl_AsSingleString()
    {
        var config = new AppConfig();
        var shareText = ShareCodec.Encode("""{"config":{"serverUrl":"a.cn:1"}}""");

        var urls = SharePayload.Apply(shareText, config);

        Assert.Equal(new[] { "a.cn:1" }, urls.ToArray());
    }

    [Fact]
    public void Apply_CustomListenerData_ArrayBecomesLines()
    {
        var config = new AppConfig();
        var shareText = ShareCodec.Encode("""
        {"config":{"customListenerData":["tcp://0.0.0.0:11010","udp://0.0.0.0:11010"]}}
        """);

        SharePayload.Apply(shareText, config);

        Assert.Equal("tcp://0.0.0.0:11010\nudp://0.0.0.0:11010", config.CustomListenerData);
    }

    [Fact]
    public void Apply_RoundTripRestoresSharedFields()
    {
        var original = new AppConfig
        {
            NetworkName = "room",
            NetworkPassword = "pw",
            Protocol = new List<string> { "tcp", "ws" },
            ServerUrl = new List<string> { "x.cn:11010" },
            DisbleP2p = true,
            Compression = "zstd",
            EnableQuicProxy = true,
            NetCardMetricValue = 5,
        };

        var shareText = SharePayload.Build(original);

        var target = new AppConfig();
        SharePayload.Apply(shareText, target);

        Assert.Equal("room", target.NetworkName);
        Assert.Equal("pw", target.NetworkPassword);
        Assert.Equal(new[] { "tcp", "ws" }, target.Protocol.ToArray());
        Assert.Equal(new[] { "x.cn:11010" }, target.ServerUrl.ToArray());
        Assert.True(target.DisbleP2p);
        Assert.Equal("zstd", target.Compression);
        Assert.True(target.EnableQuicProxy);
        Assert.Equal(5, target.NetCardMetricValue);
    }

    [Fact]
    public void Apply_MissingConfigNode_Throws()
    {
        var shareText = ShareCodec.Encode("""{"other":1}""");

        Assert.Throws<FormatException>(() => SharePayload.Apply(shareText, new AppConfig()));
    }
}
