using System.Text.Json.Nodes;
using EasytierGame.Core.Json;

namespace EasytierGame.Core.Config;

/// <summary>
/// 生成 / 读取 <c>easytier/config.json</c>。
/// 语义必须与旧版 <c>composables/configJson.ts::updateConfigJson</c> 一致：
/// <list type="bullet">
/// <item>写入前剔除界面的“非联机参数”：proxyNetworks / autoStart / saveErrorLog /
///       bindDeviceEnable / acceptDNS / enablePortForward / portForwardData；</item>
/// <item>serverUrl 与 customListenerData 以数组形式写回；</item>
/// <item>serverUrl 取 (当前选择 ∪ 传入值) 去重后与 basePeers 求交集 —— 保证 config.json
///       只包含“已知公共服务器”，不会把用户临时输入的地址固化下来。</item>
/// </list>
/// </summary>
public static class GuiConfigWriter
{
    /// <summary>写入 config.json 时需要剔除的字段（camelCase 键名）。</summary>
    private static readonly string[] ExcludedKeys =
    {
        "proxyNetworks",
        "autoStart",
        "saveErrorLog",
        "bindDeviceEnable",
        "acceptDNS",
        "enablePortForward",
        "portForwardData",
    };

    /// <summary>
    /// 构造 config.json 的 JSON 文本。
    /// </summary>
    /// <param name="state">当前应用状态。</param>
    /// <param name="extraServerUrls">额外要并入的服务器地址（旧版 <c>configJsonSeverUrl</c>），可为 null。</param>
    public static string Build(Models.AppState state, IEnumerable<string>? extraServerUrls = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var merged = (state.Config.ServerUrl ?? new List<string>())
            .Concat(extraServerUrls ?? Enumerable.Empty<string>())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct()
            .Where(url => state.BasePeers.Contains(url))
            .ToList();

        var node = JsonNode.Parse(JsonDefaults.Serialize(state.Config))!.AsObject();

        foreach (var key in ExcludedKeys)
        {
            node.Remove(key);
        }

        // serverUrl：数组
        var serverUrlArray = new JsonArray();
        foreach (var url in merged)
        {
            serverUrlArray.Add(url);
        }

        node["serverUrl"] = serverUrlArray;

        // customListenerData：数组（界面里是换行分隔的字符串）
        var listenerArray = new JsonArray();
        foreach (var line in Args.ArgsBuilder.SplitLines(state.Config.CustomListenerData))
        {
            listenerArray.Add(line);
        }

        node["customListenerData"] = listenerArray;

        // 保持旧版键顺序：serverUrl / enableCustomListener / customListenerData 在前
        var ordered = new JsonObject
        {
            ["serverUrl"] = node["serverUrl"]!.DeepClone(),
            ["enableCustomListener"] = node["enableCustomListener"]?.DeepClone() ?? JsonValue.Create(true),
            ["customListenerData"] = node["customListenerData"]!.DeepClone(),
        };

        foreach (var (key, value) in node.ToList())
        {
            if (key is "serverUrl" or "enableCustomListener" or "customListenerData")
            {
                continue;
            }

            node.Remove(key);
            ordered[key] = value?.DeepClone();
        }

        return JsonDefaults.Indent4(ordered.ToJsonString(JsonDefaults.Options));
    }

    /// <summary>
    /// 把 config.json 的内容应用回状态。
    /// 与旧版一致：<b>存在即覆盖界面配置</b>，未出现的字段保持默认/当前值。
    /// 另外处理两个历史兼容点：
    /// <list type="bullet">
    /// <item>serverUrl 支持 string / 逗号串 / 数组；</item>
    /// <item>KCP 与 QUIC 代理同时开启时强制关掉 QUIC（旧版 <c>initGuiJson</c> 的规则）。</item>
    /// </list>
    /// </summary>
    /// <returns>config.json 里出现的 serverUrl（写回 config.json 时用）。</returns>
    public static List<string> Apply(string jsonText, Models.AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!JsonDefaults.TryDeserialize<Models.AppConfig>(jsonText, out var config, out var error) || config is null)
        {
            throw new FormatException($"config.json 格式或编码错误: {error}");
        }

        var node = JsonNode.Parse(jsonText, documentOptions: new System.Text.Json.JsonDocumentOptions
        {
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        })!.AsObject();

        // serverUrl 原样取出（可能是字符串）
        var rawServerUrl = node["serverUrl"];
        var saveServerUrl = new List<string>();
        if (rawServerUrl is JsonArray array)
        {
            saveServerUrl = array.Select(x => x?.GetValue<string>() ?? "")
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
        }
        else if (rawServerUrl is JsonValue value && value.TryGetValue<string>(out var rawText))
        {
            saveServerUrl = rawText.Split(',')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
        }

        // 兼容：enableCustomListener / customListenerData 可能在、也可能不在
        if (node.TryGetPropertyValue("customListenerData", out var listenerNode) && listenerNode is JsonArray listenerArray)
        {
            config.CustomListenerData = string.Join("\n", listenerArray.Select(x => x?.GetValue<string>() ?? ""));
        }

        // 旧版规则：同时开启 KCP 与 QUIC 代理时禁用 QUIC
        if (config.EnableKcpProxy && config.EnableQuicProxy)
        {
            config.EnableQuicProxy = false;
        }

        // serverUrl：以 config.json 为准（旧版会另存一份，然后只取第一个作为当前选择）
        if (saveServerUrl.Count > 0)
        {
            state.BasePeers = saveServerUrl.Concat(state.BasePeers).Distinct().ToList();
        }

        // 应用字段（不含 serverUrl，由调用方决定 显示用哪些）
        var keepServerUrl = state.Config.ServerUrl;
        state.Config = config;
        state.Config.ServerUrl = saveServerUrl.Count > 0
            ? new List<string> { saveServerUrl[0] }
            : (keepServerUrl.Count > 0 ? keepServerUrl : new List<string>());

        return saveServerUrl;
    }
}
