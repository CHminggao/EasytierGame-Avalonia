namespace EasytierGame.Core.Cli;

/// <summary>
/// 成员列表的展示映射（纯函数，可单测）。
/// <para>
/// 两件事：
/// <list type="number">
/// <item><c>nat_type</c> 的原值（OpenInternet / fullcone / symmetric …）对普通用户不直观，
/// 转成 NAT0-4 这种通用说法；映射表取自旧版 <c>member.vue</c> 的 <c>natMaps</c>。</item>
/// <item><c>cost</c> 为 <c>Local</c> 表示“自己这台机器”，界面应显示“本机”而不是英文。</item>
/// </list>
/// </para>
/// </summary>
public static class MemberDisplay
{
    /// <summary>
    /// <c>nat_type</c> → 易懂文案。
    /// 键均按小写比较。
    /// </summary>
    private static readonly Dictionary<string, string> NatTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openinternet"] = "NAT0 公网",
        ["nopat"] = "NAT0 无PAT",
        ["fullcone"] = "NAT1 全锥形",
        ["restricted"] = "NAT2 受限锥形",
        ["portrestricted"] = "NAT3 端口受限",
        ["symmetric"] = "NAT4 对称型",
        ["symmetriceasydec"] = "NAT4 对称(easydec)",
        ["symmetriceasyinc"] = "NAT4 对称(easyinc)",
        ["symudpfirewall"] = "NAT4 对称(UDP防火墙)",
    };

    /// <summary>
    /// 把 <c>nat_type</c> 转成易懂文案。
    /// 空值返回 <c>"-"</c>；未识别的值原样返回（不丢信息）。
    /// </summary>
    public static string FormatNatType(string? natType)
    {
        var raw = (natType ?? "").Trim();
        if (raw.Length == 0)
        {
            return "-";
        }

        return NatTypeMap.TryGetValue(raw, out var mapped) ? mapped : raw;
    }

    /// <summary>
    /// 把 <c>cost</c> 转成界面文案：<c>Local</c> → “本机”，其余原样。
    /// </summary>
    public static string FormatCost(string? cost)
    {
        var raw = (cost ?? "").Trim();
        if (raw.Length == 0)
        {
            return "-";
        }

        return raw.Equals("Local", StringComparison.OrdinalIgnoreCase) ? "本机" : raw;
    }
}
