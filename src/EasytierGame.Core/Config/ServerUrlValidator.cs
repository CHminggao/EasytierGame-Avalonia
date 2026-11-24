using System.Net;
using System.Text.RegularExpressions;

namespace EasytierGame.Core.Config;

/// <summary>校验结果。失败时 <see cref="Error"/> 给出可直接展示的中文原因。</summary>
public sealed record ServerUrlValidationResult(bool Ok, string? Error)
{
    public static ServerUrlValidationResult Success() => new(true, null);

    public static ServerUrlValidationResult Fail(string error) => new(false, error);
}

/// <summary>
/// 服务器节点地址的<b>静态</b>校验（不发网络请求，只看格式是否可用）。
/// <para>
/// 规则（按需求：必须带协议头 + 地址本身可用）：
/// <list type="number">
/// <item>必须带协议头，且属于支持的协议集合（默认 tcp/udp/ws/wss/srv/schema/txt）；</item>
/// <item>协议头后必须是 <c>host:port</c>；</item>
/// <item>host 必须是合法 IPv4 / IPv6（可带方括号）/ 合法域名；</item>
/// <item>port 必须是 1-65535 的整数；</item>
/// <item>不允许空白字符、不允许路径或查询串（那是 URL 不是节点地址）。</item>
/// </list>
/// </para>
/// </summary>
public static partial class ServerUrlValidator
{
    /// <summary>支持的协议（与 <see cref="ServerUrlEditor.Protocols"/> 一致）。</summary>
    public static readonly string[] SupportedProtocols = ServerUrlEditor.Protocols;

    /// <summary>协议头 + 剩余部分。</summary>
    [GeneratedRegex(@"^(?<scheme>[A-Za-z][A-Za-z0-9+.\-]*)://(?<rest>.+)$")]
    private static partial Regex SchemeRegex();

    /// <summary>域名：形如 a.b / a-b.c 的合法标签序列。</summary>
    [GeneratedRegex(@"^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$")]
    private static partial Regex HostnameRegex();

    /// <summary>
    /// 校验并返回结果。成功时 <paramref name="normalized"/> 为规范化后的地址。
    /// </summary>
    public static ServerUrlValidationResult Validate(string? raw, out string normalized)
    {
        normalized = ServerUrlEditor.Normalize(raw);

        if (normalized.Length == 0)
        {
            return ServerUrlValidationResult.Fail("请输入服务器地址");
        }

        // 1. 禁止空白 / 路径 / 查询串
        if (normalized.Any(char.IsWhiteSpace))
        {
            return ServerUrlValidationResult.Fail("地址不能包含空格");
        }

        if (normalized.Contains('/') && !normalized.Contains("://["))
        {
            // 只允许出现在 "://" 之后被 IPv6 方括号包住的情况之外，一律拒绝
            var afterScheme = normalized.Contains("://") ? normalized[(normalized.IndexOf("://", StringComparison.Ordinal) + 3)..] : normalized;
            if (afterScheme.Contains('/') || afterScheme.Contains('?'))
            {
                return ServerUrlValidationResult.Fail("地址不能包含路径或查询参数");
            }
        }

        // 2. 必须带协议头
        var match = SchemeRegex().Match(normalized);
        if (!match.Success)
        {
            return ServerUrlValidationResult.Fail("地址必须带协议头，例如 tcp://1.2.3.4:11010");
        }

        var scheme = match.Groups["scheme"].Value.ToLowerInvariant();
        if (!SupportedProtocols.Contains(scheme, StringComparer.OrdinalIgnoreCase))
        {
            return ServerUrlValidationResult.Fail(
                $"不支持的协议 “{scheme}”，可用：{string.Join(" / ", SupportedProtocols)}");
        }

        // 3. 拆 host:port
        var rest = match.Groups["rest"].Value;
        if (!TrySplitHostPort(rest, out var host, out var portText))
        {
            return ServerUrlValidationResult.Fail("地址格式应为 协议://主机:端口，例如 tcp://1.2.3.4:11010");
        }

        // 4. 端口
        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            return ServerUrlValidationResult.Fail("端口必须是 1-65535 之间的整数");
        }

        // 5. 主机
        if (!IsValidHost(host))
        {
            return ServerUrlValidationResult.Fail($"主机 “{host}” 不是合法的 IP 或域名");
        }

        // 规范化：协议小写 + IPv6 补回方括号 + 端口去前导零
        var hostText = IPAddress.TryParse(host, out var parsedHost)
                       && parsedHost.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{host}]"
            : host;

        normalized = $"{scheme}://{hostText}:{port}";
        return ServerUrlValidationResult.Success();
    }

    /// <summary>便捷重载。</summary>
    public static bool IsValid(string? raw) => Validate(raw, out _).Ok;

    /// <summary>
    /// 把 <c>host:port</c> 拆开。
    /// 支持 <c>1.2.3.4:11010</c>、<c>example.com:11010</c>、<c>[fd00::1]:11010</c>。
    /// </summary>
    private static bool TrySplitHostPort(string rest, out string host, out string portText)
    {
        host = "";
        portText = "";

        if (rest.Length == 0)
        {
            return false;
        }

        // IPv6 字面量：[fd00::1]:11010
        if (rest[0] == '[')
        {
            var close = rest.IndexOf(']');
            if (close <= 1)
            {
                return false;
            }

            host = rest[1..close];
            var tail = rest[(close + 1)..];
            if (tail.Length < 2 || tail[0] != ':')
            {
                return false;
            }

            portText = tail[1..];
            return portText.Length > 0;
        }

        var colon = rest.LastIndexOf(':');
        if (colon <= 0 || colon == rest.Length - 1)
        {
            return false;
        }

        host = rest[..colon];
        portText = rest[(colon + 1)..];

        // 裸 IPv6（多个冒号）不接受：必须用方括号
        if (host.Contains(':'))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidHost(string host)
    {
        if (host.Length == 0 || host.Length > 253)
        {
            return false;
        }

        // 形如“纯数字与点”的主机（1.2.3.4 / 999.1.1.1 / 1.2.3.4.5）
        // 必须先按 IPv4 严格校验：否则它们会被下面的域名正则当成合法域名放行。
        if (host.All(c => char.IsDigit(c) || c == '.'))
        {
            var parts = host.Split('.');
            return parts.Length == 4 && parts.All(p =>
                p.Length is > 0 and <= 3
                && p.All(char.IsDigit)
                && int.Parse(p) <= 255
                && (p.Length == 1 || p[0] != '0'));
        }

        // IPv6（方括号已去掉）
        if (IPAddress.TryParse(host, out var ip))
        {
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        // 域名
        return HostnameRegex().IsMatch(host);
    }
}
