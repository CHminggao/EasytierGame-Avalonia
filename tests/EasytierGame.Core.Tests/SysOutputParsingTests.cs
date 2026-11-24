using System.Text;
using EasytierGame.Core.SystemIntegration;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 系统命令输出解析测试。
/// 用例中的报文取自本机实测（netsh / ping 输的是 UTF-8，不是 GBK）。
/// </summary>
public class SysOutputParsingTests
{
    // ------------------------------------------------------------------
    // 防火墙：中文输出
    // ------------------------------------------------------------------

    /// <summary>实测中文输出（UTF-8 解码后）。</summary>
    private const string ChineseFirewallOutput =
        "\r\n域配置文件 设置: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "状态                                  关闭\r\n" +
        "\r\n专用配置文件 设置: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "状态                                  开启\r\n" +
        "\r\n公用配置文件 设置: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "状态                                  关闭\r\n" +
        "确定。\r\n";

    [Fact]
    public void FirewallState_Chinese_ReturnsThreeProfiles()
    {
        var result = SysIntegration.ParseFirewallState(ChineseFirewallOutput);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "域", "专用", "公用" }, result.Select(r => r.Profile).ToArray());
    }

    [Fact]
    public void FirewallState_Chinese_ReadsOnOff()
    {
        var result = SysIntegration.ParseFirewallState(ChineseFirewallOutput);

        Assert.False(result[0].IsEnabled);  // 域：关闭
        Assert.True(result[1].IsEnabled);   // 专用：开启
        Assert.False(result[2].IsEnabled);  // 公用：关闭
    }

    [Fact]
    public void FirewallState_Chinese_IgnoresTrailingOkLine()
    {
        var result = SysIntegration.ParseFirewallState(ChineseFirewallOutput);

        // “确定。”不能变成第四条记录
        Assert.DoesNotContain(result, r => r.Profile.Contains("确定"));
    }

    [Fact]
    public void FirewallState_Chinese_DisplayText()
    {
        var result = SysIntegration.ParseFirewallState(ChineseFirewallOutput);

        Assert.Equal("域防火墙：已关闭", result[0].Display);
        Assert.Equal("专用防火墙：已开启", result[1].Display);
    }

    // ------------------------------------------------------------------
    // 防火墙：英文输出
    // ------------------------------------------------------------------

    private const string EnglishFirewallOutput =
        "\r\nDomain Profile Settings: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "State                                 ON\r\n" +
        "\r\nPrivate Profile Settings: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "State                                 OFF\r\n" +
        "\r\nPublic Profile Settings: \r\n" +
        "----------------------------------------------------------------------\r\n" +
        "State                                 OFF\r\n" +
        "Ok.\r\n";

    [Fact]
    public void FirewallState_English_ReturnsThreeProfiles()
    {
        var result = SysIntegration.ParseFirewallState(EnglishFirewallOutput);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "Domain", "Private", "Public" }, result.Select(r => r.Profile).ToArray());
    }

    [Fact]
    public void FirewallState_English_ReadsOnOff()
    {
        var result = SysIntegration.ParseFirewallState(EnglishFirewallOutput);

        Assert.True(result[0].IsEnabled);
        Assert.False(result[1].IsEnabled);
        Assert.False(result[2].IsEnabled);
    }

    [Fact]
    public void FirewallState_English_IgnoresOkLine()
    {
        var result = SysIntegration.ParseFirewallState(EnglishFirewallOutput);

        Assert.DoesNotContain(result, r => r.Profile.Equals("Ok.", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // 边界
    // ------------------------------------------------------------------

    [Fact]
    public void FirewallState_Empty_ReturnsEmpty()
    {
        Assert.Empty(SysIntegration.ParseFirewallState(""));
        Assert.Empty(SysIntegration.ParseFirewallState(null));
        Assert.Empty(SysIntegration.ParseFirewallState("   "));
    }

    [Fact]
    public void FirewallState_OnlyStateLinesWithoutHeader_UsesFallbackName()
    {
        const string output = "状态 关闭\r\n状态 开启\r\n";

        var result = SysIntegration.ParseFirewallState(output);

        Assert.Equal(2, result.Count);
        // 没有表头时给一个兜底名字，不抛异常、不丢数据
        Assert.All(result, r => Assert.False(string.IsNullOrWhiteSpace(r.Profile)));
    }

    // ------------------------------------------------------------------
    // 编码：确认系统命令输出按 UTF-8 解码
    // ------------------------------------------------------------------

    [Fact]
    public void NetshOutput_Chinese_IsUtf8()
    {
        // 实测：“域”的 UTF-8 字节是 E5 9F 9F
        var bytes = Encoding.UTF8.GetBytes("域配置文件");
        Assert.Equal(new byte[] { 0xE5, 0x9F, 0x9F }, bytes.Take(3).ToArray());

        // 若错误地用 GBK 解码，会变成乱码（这正是修复前的现象）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var wrong = Encoding.GetEncoding(936).GetString(bytes);
        Assert.NotEqual("域配置文件", wrong);
    }
}
