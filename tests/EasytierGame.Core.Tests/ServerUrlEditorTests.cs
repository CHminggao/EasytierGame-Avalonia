using EasytierGame.Core.Config;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 服务器地址维护逻辑测试。
/// 这些用例对应“无法新增服务器节点”的复现场景：
/// 手动输入的地址必须同时进入 <c>Config.ServerUrl</c>（参与 --peers）与候选池。
/// </summary>
public class ServerUrlEditorTests
{
    // ---------------- Normalize ----------------

    [Theory]
    [InlineData("  a.cn:11010  ", "a.cn:11010")]
    [InlineData("tcp:\\\\a.cn:11010", "tcp://a.cn:11010")]
    [InlineData("\"a.cn:11010\"", "a.cn:11010")]
    [InlineData("'a.cn:11010'", "a.cn:11010")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize(string? input, string expected)
    {
        Assert.Equal(expected, ServerUrlEditor.Normalize(input));
    }

    // ---------------- 协议识别 ----------------

    [Theory]
    [InlineData("tcp://a.cn:11010", true)]
    [InlineData("udp://a.cn:11010", true)]
    [InlineData("wss://a.cn:11010", true)]
    [InlineData("a.cn:11010", false)]
    [InlineData("", false)]
    public void HasProtocolPrefix(string input, bool expected)
    {
        Assert.Equal(expected, ServerUrlEditor.HasProtocolPrefix(input));
    }

    [Theory]
    [InlineData("tcp://a.cn:11010", "tcp")]
    [InlineData("UDP://a.cn:11010", "udp")]
    [InlineData("a.cn:11010", null)]
    public void GetProtocol(string input, string? expected)
    {
        Assert.Equal(expected, ServerUrlEditor.GetProtocol(input));
    }

    // ---------------- 新增（核心回归） ----------------

    [Fact]
    public void Add_WritesToBothLists()
    {
        var serverUrls = new List<string>();
        var basePeers = new List<string>();
        var protocols = new List<string> { "tcp" };

        var result = ServerUrlEditor.Add("a.cn:11010", serverUrls, basePeers, protocols);

        Assert.Equal("a.cn:11010", result);
        // 参与 --peers
        Assert.Contains("a.cn:11010", serverUrls);
        // 下拉候选池
        Assert.Contains("a.cn:11010", basePeers);
    }

    [Fact]
    public void Add_WithProtocolPrefix_AlsoAddsProtocol()
    {
        var serverUrls = new List<string>();
        var basePeers = new List<string>();
        var protocols = new List<string> { "tcp" };

        ServerUrlEditor.Add("udp://a.cn:11010", serverUrls, basePeers, protocols);

        Assert.Contains("udp", protocols);
        // 前缀地址原样保留，不再拆成 udp://udp://...
        Assert.Contains("udp://a.cn:11010", serverUrls);
    }

    [Fact]
    public void Add_Duplicate_DoesNotDuplicateAndKeepsOrder()
    {
        var serverUrls = new List<string> { "x.cn:1" };
        var basePeers = new List<string> { "x.cn:1" };
        var protocols = new List<string> { "tcp" };

        ServerUrlEditor.Add("x.cn:1", serverUrls, basePeers, protocols);

        Assert.Single(serverUrls);
        Assert.Single(basePeers);
        Assert.Equal("x.cn:1", serverUrls[0]);
    }

    [Fact]
    public void Add_TrimsAndNormalizesBackslashes()
    {
        var serverUrls = new List<string>();
        var basePeers = new List<string>();
        var protocols = new List<string> { "tcp" };

        var result = ServerUrlEditor.Add("  tcp:\\\\a.cn:11010  ", serverUrls, basePeers, protocols);

        Assert.Equal("tcp://a.cn:11010", result);
        Assert.Contains("tcp://a.cn:11010", serverUrls);
    }

    [Fact]
    public void Add_EmptyInput_ChangesNothing()
    {
        var serverUrls = new List<string> { "x.cn:1" };
        var basePeers = new List<string> { "x.cn:1" };
        var protocols = new List<string> { "tcp" };

        var result = ServerUrlEditor.Add("   ", serverUrls, basePeers, protocols);

        Assert.Null(result);
        Assert.Single(serverUrls);
        Assert.Single(basePeers);
    }

    [Fact]
    public void Add_ThenBuildArgs_ProducesPeers()
    {
        // 端到端回归：新增后必须能构建出 --peers，否则就是“提示需要配置服务端节点”的原因
        var state = new Models.AppState();
        state.Config.Protocol = new List<string> { "tcp" };
        state.Config.ServerUrl = new List<string>();
        state.Config.Hostname = "player1";   // 成员名现为必填，否则 BuildClient 直接拒绝

        var added = ServerUrlEditor.Add(
            "182.254.1.1:11010",
            state.Config.ServerUrl,
            state.BasePeers,
            state.Config.Protocol);

        Assert.Equal("182.254.1.1:11010", added);

        var result = Args.ArgsBuilder.BuildClient(state);

        Assert.Equal(Args.ArgsBuildStatus.Ok, result.Status);
        Assert.Contains("--peers", result.Args);
        Assert.Contains("tcp://182.254.1.1:11010", result.Args);
    }

    // ---------------- 删除 ----------------

    [Fact]
    public void Remove_DeletesFromBothLists()
    {
        var serverUrls = new List<string> { "a.cn:1", "b.cn:2" };
        var basePeers = new List<string> { "a.cn:1", "b.cn:2" };

        var fallback = ServerUrlEditor.Remove("a.cn:1", serverUrls, basePeers);

        Assert.Null(fallback);
        Assert.Equal(new[] { "b.cn:2" }, serverUrls.ToArray());
        Assert.Equal(new[] { "b.cn:2" }, basePeers.ToArray());
    }

    [Fact]
    public void Remove_LastOne_FallsBackToFirstCandidate()
    {
        var serverUrls = new List<string> { "a.cn:1" };
        var basePeers = new List<string> { "a.cn:1", "b.cn:2" };

        var fallback = ServerUrlEditor.Remove("a.cn:1", serverUrls, basePeers);

        // 不能留下“一个都没选”的状态
        Assert.Equal("b.cn:2", fallback);
        Assert.Equal(new[] { "b.cn:2" }, serverUrls.ToArray());
    }

    [Fact]
    public void Remove_NothingLeft_StaysEmpty()
    {
        var serverUrls = new List<string> { "a.cn:1" };
        var basePeers = new List<string> { "a.cn:1" };

        var fallback = ServerUrlEditor.Remove("a.cn:1", serverUrls, basePeers);

        Assert.Null(fallback);
        Assert.Empty(serverUrls);
        Assert.Empty(basePeers);
    }
}
