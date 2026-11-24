using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasytierGame.Core.Json;

/// <summary>
/// 把 <c>"a,b"</c>、<c>"a"</c>、<c>["a","b"]</c> 三种写法都读成 <c>List&lt;string&gt;</c>。
/// 旧版 <c>serverUrl</c> 历史上是 string，后来改成数组，且 config.json 明确支持
/// “逗号分隔字符串”与“数组”两种写法，初始化时必须兼容。
/// 序列化方向固定写数组。
/// </summary>
public sealed class StringOrStringListConverter : JsonConverter<List<string>>
{
    /// <summary>让 null 也走本转换器（否则序列化器会直接赋 null）。</summary>
    public override bool HandleNull => true;

    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return new List<string>();

            case JsonTokenType.String:
            {
                var raw = reader.GetString() ?? "";
                return raw.Length == 0
                    ? new List<string>()
                    : raw.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0)
                        .ToList();
            }

            case JsonTokenType.StartArray:
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    switch (reader.TokenType)
                    {
                        case JsonTokenType.Null:
                            continue;
                        case JsonTokenType.String:
                        {
                            var s = reader.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                list.Add(s.Trim());
                            }

                            break;
                        }
                        default:
                        {
                            list.Add(reader.GetRawStringValue());
                            break;
                        }
                    }
                }

                return list;
            }

            default:
                throw new JsonException($"无法把 {reader.TokenType} 解析为字符串列表");
        }
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item ?? "");
        }

        writer.WriteEndArray();
    }
}

internal static class Utf8JsonReaderExtensions
{
    /// <summary>读取任意标量 token 的原始文本（数字/布尔等转成 string）。</summary>
    public static string GetRawStringValue(this ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => reader.GetString() ?? "",
        };
    }
}
