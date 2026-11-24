using EasytierGame.Core.Config;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 服务器节点地址静态校验测试。
/// 需求：添加的节点必须带协议头（tcp/udp 等），且地址本身可用。
/// </summary>
public class ServerUrlValidatorTests
{
    // ---------------- 合法 ----------------

    [Theory]
    [InlineData("tcp://1.2.3.4:11010", "tcp://1.2.3.4:11010")]
    [InlineData("udp://1.2.3.4:11010", "udp://1.2.3.4:11010")]
    [InlineData("ws://example.com:11010", "ws://example.com:11010")]
    [InlineData("wss://example.com:11010", "wss://example.com:11010")]
    [InlineData("srv://public.easytier.top:11010", "srv://public.easytier.top:11010")]
    [InlineData("txt://a.b.c.d:1", "txt://a.b.c.d:1")]
    [InlineData("schema://node-1.example.com:65535", "schema://node-1.example.com:65535")]
    [InlineData("tcp://[fd00::1]:11010", "tcp://[fd00::1]:11010")]
    public void Valid(string input, string expectedNormalized)
    {
        var result = ServerUrlValidator.Validate(input, out var normalized);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(expectedNormalized, normalized);
    }

    [Theory]
    [InlineData("TCP://1.2.3.4:11010", "tcp://1.2.3.4:11010")]      // 协议统一小写
    [InlineData("tcp://1.2.3.4:011010", "tcp://1.2.3.4:11010")]     // 端口去前导零
    [InlineData("  tcp://1.2.3.4:11010  ", "tcp://1.2.3.4:11010")]   // 去首尾空白
    public void Normalized(string input, string expected)
    {
        var result = ServerUrlValidator.Validate(input, out var normalized);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(expected, normalized);
    }

    // ---------------- 缺少协议头（本次需求重点）----------------

    [Theory]
    [InlineData("1.2.3.4:11010")]
    [InlineData("example.com:11010")]
    [InlineData("public.easytier.top:11010")]
    [InlineData("[fd00::1]:11010")]
    public void MissingScheme_Rejected(string input)
    {
        var result = ServerUrlValidator.Validate(input, out _);

        Assert.False(result.Ok);
        Assert.Contains("协议头", result.Error);
    }

    [Fact]
    public void MissingScheme_SuggestsFormat()
    {
        var result = ServerUrlValidator.Validate("1.2.3.4:11010", out _);

        Assert.False(result.Ok);
        // 提示里要给出正确写法，用户才知道怎么改
        Assert.Contains("tcp://", result.Error);
    }

    // ---------------- 不支持的协议 ----------------

    [Theory]
    [InlineData("http://1.2.3.4:11010")]
    [InlineData("https://1.2.3.4:11010")]
    [InlineData("ftp://1.2.3.4:11010")]
    [InlineData("quic://1.2.3.4:11010")]
    public void UnsupportedScheme_Rejected(string input)
    {
        var result = ServerUrlValidator.Validate(input, out _);

        Assert.False(result.Ok);
        Assert.Contains("不支持的协议", result.Error);
    }

    // ---------------- 端口 ----------------

    [Theory]
    [InlineData("tcp://1.2.3.4:0")]
    [InlineData("tcp://1.2.3.4:65536")]
    [InlineData("tcp://1.2.3.4:abc")]
    [InlineData("tcp://1.2.3.4:-1")]
    [InlineData("tcp://1.2.3.4:")]
    public void BadPort_Rejected(string input)
    {
        var result = ServerUrlValidator.Validate(input, out _);

        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData("tcp://1.2.3.4:1")]
    [InlineData("tcp://1.2.3.4:65535")]
    public void PortBoundaries_Accepted(string input)
    {
        Assert.True(ServerUrlValidator.IsValid(input));
    }

    // ---------------- 主机 ----------------

    [Theory]
    [InlineData("tcp://999.1.1.1:11010")]      // 越界
    [InlineData("tcp://1.2.3:11010")]          // 段数不足
    [InlineData("tcp://1.2.3.4.5:11010")]      // 段数过多
    [InlineData("tcp://1.2.3.4:11010:11010")]  // 端口拆错
    [InlineData("tcp://:11010")]               // 空主机
    public void BadHost_Rejected(string input)
    {
        Assert.False(ServerUrlValidator.IsValid(input));
    }

    [Theory]
    [InlineData("tcp://my-server.example.com:11010")]
    [InlineData("tcp://a.b:11010")]
    [InlineData("tcp://node1:11010")]          // 单标签主机名允许
    public void GoodHost_Accepted(string input)
    {
        Assert.True(ServerUrlValidator.IsValid(input));
    }

    // ---------------- 格式杂项 ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_Rejected(string? input)
    {
        var result = ServerUrlValidator.Validate(input, out _);

        Assert.False(result.Ok);
        Assert.Contains("请输入", result.Error);
    }

    [Fact]
    public void SpaceInside_Rejected()
    {
        var result = ServerUrlValidator.Validate("tcp://1.2.3.4 :11010", out _);

        Assert.False(result.Ok);
        Assert.Contains("空格", result.Error);
    }

    [Theory]
    [InlineData("tcp://1.2.3.4:11010/path")]
    [InlineData("tcp://1.2.3.4:11010?x=1")]
    public void PathOrQuery_Rejected(string input)
    {
        var result = ServerUrlValidator.Validate(input, out _);

        Assert.False(result.Ok);
        Assert.Contains("路径", result.Error);
    }

    [Fact]
    public void NoPort_Rejected()
    {
        Assert.False(ServerUrlValidator.IsValid("tcp://1.2.3.4"));
    }

    // ---------------- 与 ArgsBuilder 的衔接 ----------------

    [Fact]
    public void ValidUrl_ProducesPeersThatKeepPrefix()
    {
        // 带前缀的地址在 --peers 里原样使用，不再按协议列表展开
        var state = new Models.AppState();
        state.Config.ServerUrl = new List<string>();
        state.Config.Protocol = new List<string> { "tcp" };
        state.Config.Hostname = "player1";   // 成员名现为必填

        ServerUrlEditor.Add("udp://1.2.3.4:11010", state.Config.ServerUrl, state.BasePeers, state.Config.Protocol);

        var result = Args.ArgsBuilder.BuildClient(state);

        Assert.Equal(Args.ArgsBuildStatus.Ok, result.Status);
        Assert.Contains("udp://1.2.3.4:11010", result.Args);
        // 不应出现展开后的畸形地址
        Assert.DoesNotContain("udp://udp://1.2.3.4:11010", result.Args);
    }
}
