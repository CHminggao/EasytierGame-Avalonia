using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasytierGame.Core.Json;

/// <summary>
/// 宽松的 string 读取：接受 JSON 字符串、数字、布尔、null，以及字符串数组。
/// <para>
/// 前几种是为了用户手工编辑 <c>config.json</c> 时的常见误差（<c>port: 11010</c> 而非 <c>"11010"</c>）；
/// 数组形态是历史包袱：<c>customListenerData</c> 在 config.json 里是数组，
/// 而界面里是换行分隔的字符串，这里直接拼成多行，避免调用方再写转换。
/// </para>
/// 序列化方向始终写字符串。
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Null => "",
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString()
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.StartArray => ReadArrayAsLines(ref reader),
            _ => throw new JsonException($"无法把 {reader.TokenType} 解析为字符串"),
        };
    }

    private static string ReadArrayAsLines(ref Utf8JsonReader reader)
    {
        var items = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            items.Add(reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() ?? "",
                JsonTokenType.Null => "",
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                _ => "",
            });
        }

        return string.Join("\n", items);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ?? "");
    }
}
