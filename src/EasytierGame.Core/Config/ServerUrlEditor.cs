namespace EasytierGame.Core.Config;

/// <summary>
/// 服务器地址集合的维护逻辑（纯函数式、可单测、不依赖 WPF）。
/// <para>
/// 单独抽出来的原因：旧实现把“新增服务器”完全寄托在 WPF <c>ComboBox</c> 的
/// <c>Text</c> 双向绑定 + <c>SelectionChanged</c> 上，手动输入的地址既没有提交路径，
/// 也难以验证是否真的写进了参与 <c>--peers</c> 的列表。这里把规则固定成
/// “输入什么就归一化成什么、写到哪里、协议怎么推断”三段纯逻辑。
/// </para>
/// </summary>
public static class ServerUrlEditor
{
    /// <summary>支持的协议（与 MainViewModel.ProtocolOptions 保持一致）。</summary>
    public static readonly string[] Protocols = { "tcp", "udp", "ws", "wss", "srv", "schema", "txt" };

    /// <summary>
    /// 归一化用户输入：去首尾空白、反斜杠转斜杠、去掉包裹的引号。
    /// 空输入返回空串。
    /// </summary>
    public static string Normalize(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0)
        {
            return "";
        }

        text = text.Replace('\\', '/');

        // 用户从别处复制时可能带引号
        if (text.Length >= 2
            && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            text = text[1..^1].Trim();
        }

        return text;
    }

    /// <summary>
    /// 地址是否带协议前缀（例如 <c>tcp://host:port</c>）。
    /// 带前缀的地址在 <c>--peers</c> 里原样使用，不再按协议列表展开。
    /// </summary>
    public static bool HasProtocolPrefix(string? url)
    {
        var text = Normalize(url);
        foreach (var protocol in Protocols)
        {
            if (text.StartsWith(protocol + "://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>从带前缀的地址里取出协议名（不带前缀时返回 null）。</summary>
    public static string? GetProtocol(string? url)
    {
        var text = Normalize(url);
        foreach (var protocol in Protocols)
        {
            if (text.StartsWith(protocol + "://", StringComparison.OrdinalIgnoreCase))
            {
                return protocol;
            }
        }

        return null;
    }

    /// <summary>
    /// 把一个地址写入“参与联机”的列表（<c>Config.ServerUrl</c>）与候选池（<c>BasePeers</c>）。
    /// <list type="bullet">
    /// <item>两者都去重后追加；已存在则不动，保证顺序稳定；</item>
    /// <item>带 <c>xx://</c> 前缀时把协议并入协议列表（否则 <c>--peers</c> 展开会漏）；</item>
    /// <item>返回归一化后的地址；空输入返回 null 且不改任何集合。</item>
    /// </list>
    /// </summary>
    public static string? Add(
        string? raw,
        IList<string> serverUrls,
        IList<string> basePeers,
        IList<string> protocols)
    {
        ArgumentNullException.ThrowIfNull(serverUrls);
        ArgumentNullException.ThrowIfNull(basePeers);
        ArgumentNullException.ThrowIfNull(protocols);

        var url = Normalize(raw);
        if (url.Length == 0)
        {
            return null;
        }

        if (!serverUrls.Contains(url))
        {
            serverUrls.Add(url);
        }

        if (!basePeers.Contains(url))
        {
            basePeers.Add(url);
        }

        var protocol = GetProtocol(url);
        if (protocol is not null && !protocols.Contains(protocol))
        {
            protocols.Add(protocol);
        }

        return url;
    }

    /// <summary>
    /// 从两个列表里删除某个地址。删除后若“参与联机”列表为空，
    /// 自动回填候选池第一项（避免界面变成“一个服务器都没选”而无法启动）。
    /// </summary>
    /// <returns>回填进来的地址；没有回填时返回 null。</returns>
    public static string? Remove(string? raw, IList<string> serverUrls, IList<string> basePeers)
    {
        ArgumentNullException.ThrowIfNull(serverUrls);
        ArgumentNullException.ThrowIfNull(basePeers);

        var url = Normalize(raw);
        if (url.Length == 0)
        {
            return null;
        }

        RemoveAll(serverUrls, url);
        RemoveAll(basePeers, url);

        if (serverUrls.Count == 0 && basePeers.Count > 0)
        {
            var fallback = basePeers[0];
            serverUrls.Add(fallback);
            return fallback;
        }

        return null;
    }

    private static void RemoveAll(IList<string> list, string value)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
            {
                list.RemoveAt(i);
            }
        }
    }
}
