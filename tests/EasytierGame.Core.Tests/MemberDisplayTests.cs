using EasytierGame.Core.Cli;

namespace EasytierGame.Core.Tests;

/// <summary>
/// 成员列表展示映射测试。
/// 映射表取自旧版 member.vue 的 natMaps，本测试锁定这些值不被改错。
/// </summary>
public class MemberDisplayTests
{
    // ---------------- NAT 类型 ----------------

    [Theory]
    [InlineData("OpenInternet", "NAT0 公网")]
    [InlineData("NoPat", "NAT0 无PAT")]
    [InlineData("FullCone", "NAT1 全锥形")]
    [InlineData("Restricted", "NAT2 受限锥形")]
    [InlineData("PortRestricted", "NAT3 端口受限")]
    [InlineData("Symmetric", "NAT4 对称型")]
    [InlineData("SymmetricEasyDec", "NAT4 对称(easydec)")]
    [InlineData("SymmetricEasyInc", "NAT4 对称(easyinc)")]
    [InlineData("SymUdpFirewall", "NAT4 对称(UDP防火墙)")]
    public void FormatNatType_KnownValues(string input, string expected)
    {
        Assert.Equal(expected, MemberDisplay.FormatNatType(input));
    }

    [Theory]
    [InlineData("fullcone", "NAT1 全锥形")]
    [InlineData("FULLCONE", "NAT1 全锥形")]
    [InlineData("symmetric", "NAT4 对称型")]
    public void FormatNatType_IsCaseInsensitive(string input, string expected)
    {
        Assert.Equal(expected, MemberDisplay.FormatNatType(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatNatType_Empty_ReturnsDash(string? input)
    {
        Assert.Equal("-", MemberDisplay.FormatNatType(input));
    }

    [Fact]
    public void FormatNatType_Unknown_ReturnsRaw()
    {
        // 未识别时原样返回，不丢信息
        Assert.Equal("SomethingNew", MemberDisplay.FormatNatType("SomethingNew"));
    }

    [Fact]
    public void FormatNatType_TrimsWhitespace()
    {
        Assert.Equal("NAT4 对称型", MemberDisplay.FormatNatType("  symmetric  "));
    }

    // ---------------- 方式（cost）----------------

    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData("  Local  ")]
    public void FormatCost_Local_ShowsBenJi(string input)
    {
        Assert.Equal("本机", MemberDisplay.FormatCost(input));
    }

    [Theory]
    [InlineData("P2P", "P2P")]
    [InlineData("Relay", "Relay")]
    [InlineData("p2p", "p2p")]
    public void FormatCost_Other_KeepsOriginal(string input, string expected)
    {
        Assert.Equal(expected, MemberDisplay.FormatCost(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void FormatCost_Empty_ReturnsDash(string? input)
    {
        Assert.Equal("-", MemberDisplay.FormatCost(input));
    }

    // ---------------- 组合：一行成员数据 ----------------

    [Fact]
    public void LocalMember_RendersAsBenJi_WithMappedNat()
    {
        // 本机通常没有 NAT 类型
        Assert.Equal("本机", MemberDisplay.FormatCost("Local"));
        Assert.Equal("-", MemberDisplay.FormatNatType(""));
    }

    [Fact]
    public void RemoteMember_KeepsCost()
    {
        Assert.Equal("P2P", MemberDisplay.FormatCost("P2P"));
        Assert.Equal("NAT1 全锥形", MemberDisplay.FormatNatType("FullCone"));
    }
}
