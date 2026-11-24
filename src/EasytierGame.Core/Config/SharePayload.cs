using System.Text.Json;
using System.Text.Json.Nodes;
using EasytierGame.Core.Json;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Config;

/// <summary>
/// “分享联机配置 / 导入联机配置”的载荷。
/// 字段集合严格等于旧版 <c>handleStartCommand("share_config")</c> 里解构出的 29 个字段，
/// 明确<b>不包含</b>：虚拟 IP、主机名、子网代理、smoltcp、开机自启、自动联机、
/// 出口节点、no-tun、错误日志相关（旧版弹窗里提示过“不会分享”这些）。
/// </summary>
public static class SharePayload
{
    /// <summary>参与分享的字段（顺序即序列化顺序，camelCase 键名）。</summary>
    private static readonly string[] SharedKeys =
    {
        "protocol",
        "serverUrl",
        "networkName",
        "networkPassword",
        "dhcp",
        "disableIpv6",
        "disbleP2p",
        "enableCustomProtocol",
        "customProtocol",
        "disbleListenner",
        "port",
        "enableCustomListener",
        "customListenerData",
        "devName",
        "devNameValue",
        "enableNetCardMetric",
        "netCardMetricValue",
        "disableEncryption",
        "multiThread",
        "latencyfirst",
        "disableUdpHolePunching",
        "relayAllPeerrpc",
        "compression",
        "enablePreventSleep",
        "enableKcpProxy",
        "disableKcpInput",
        "enableQuicProxy",
        "disableQuicInput",
        "privateMode",
    };

    /// <summary>不会分享的字段，供 UI 提示文案使用。</summary>
    public const string NotSharedHint =
        "虚拟网IP，主机名，子网代理，为子网代理启用smoltcp堆栈，开机自启，软件启动自动联机，出口节点，不生成Tun网卡，错误日志相关配置";

    /// <summary>构造分享载荷的 JSON（<c>{"config":{...29 个字段}}</c>）。</summary>
    public static string BuildJson(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var source = JsonNode.Parse(JsonDefaults.Serialize(config))!.AsObject();
        var payloadConfig = new JsonObject();

        foreach (var key in SharedKeys)
        {
            if (source.TryGetPropertyValue(key, out var value))
            {
                payloadConfig[key] = value?.DeepClone();
            }
        }

        var root = new JsonObject { ["config"] = payloadConfig };
        return root.ToJsonString(JsonDefaults.Options);
    }

    /// <summary>构造分享字符串（Base64）。</summary>
    public static string Build(AppConfig config) => ShareCodec.Encode(BuildJson(config));

    /// <summary>
    /// 解析分享字符串并应用到 config。
    /// 与旧版 <c>handleStartImport</c> 一致：
    /// <list type="bullet">
    /// <item>只合并载荷里出现的字段，其余保持不变；</item>
    /// <item>serverUrl 兼容「数组 / 逗号串 / 单个字符串」三种形态。</item>
    /// </list>
    /// </summary>
    /// <returns>解析出的服务器地址列表，供调用方并入 basePeers。</returns>
    public static List<string> Apply(string shareText, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!ShareCodec.TryDecode(shareText, out var plain, out var error) || plain is null)
        {
            throw new FormatException($"分享内容无法解析: {error}");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(plain, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            throw new FormatException($"分享内容不是合法 JSON: {ex.Message}", ex);
        }

        if (root is not JsonObject rootObj
            || !rootObj.TryGetPropertyValue("config", out var configNode)
            || configNode is not JsonObject incoming)
        {
            throw new FormatException("分享内容缺少 config 节点");
        }

        var resultingServerUrls = new List<string>();

        foreach (var (key, value) in incoming)
        {
            switch (key)
            {
                case "serverUrl":
                {
                    resultingServerUrls = ParseServerUrl(value);
                    config.ServerUrl = resultingServerUrls.Count > 0
                        ? new List<string>(resultingServerUrls)
                        : config.ServerUrl;
                    break;
                }

                case "customListenerData":
                {
                    config.CustomListenerData = value is JsonArray arr
                        ? string.Join("\n", arr.Select(x => x?.GetValue<string>() ?? ""))
                        : value?.GetValue<string>() ?? config.CustomListenerData;
                    break;
                }

                default:
                {
                    ApplyScalar(config, key, value);
                    break;
                }
            }
        }

        return resultingServerUrls;
    }

    private static List<string> ParseServerUrl(JsonNode? value)
    {
        return value switch
        {
            JsonArray array => array.Select(x => x?.GetValue<string>() ?? "")
                .Where(x => x.Length > 0)
                .ToList(),
            JsonValue v when v.TryGetValue<string>(out var text) => text.Split(',')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList(),
            _ => new List<string>(),
        };
    }

    private static void ApplyScalar(AppConfig config, string key, JsonNode? value)
    {
        if (value is null)
        {
            return;
        }

        var property = typeof(AppConfig).GetProperties()
            .FirstOrDefault(p => string.Equals(
                JsonNamingPolicy.CamelCase.ConvertName(p.Name),
                key,
                StringComparison.OrdinalIgnoreCase));

        if (property is null || !property.CanWrite)
        {
            return;
        }

        try
        {
            var raw = value.ToJsonString();
            var typed = JsonSerializer.Deserialize(raw, property.PropertyType, JsonDefaults.Options);
            if (typed is not null)
            {
                property.SetValue(config, typed);
            }
        }
        catch
        {
            // 单个字段类型不符时跳过，不阻断整份导入
        }
    }
}
