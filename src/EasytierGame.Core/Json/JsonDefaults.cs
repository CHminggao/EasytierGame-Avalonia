using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasytierGame.Core.Json;

/// <summary>
/// 全局 JSON 选项。这里的约定必须与 Vue 版一致：
/// <list type="number">
/// <item>属性名 camelCase，键名与旧 localStorage / config.json 完全相同；</item>
/// <item>允许 <c>//</c> 注释与尾逗号 —— <c>easytier/config_template.json</c> 带注释；</item>
/// <item>中文不转义，保证写出的 config.json 可读；</item>
/// <item>缩进 4 空格，对齐旧版 <c>JSON.stringify(x, null, 4)</c>。</item>
/// </list>
/// </summary>
public static class JsonDefaults
{
    /// <summary>默认（写文件用）选项：缩进、宽松读取。</summary>
    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>
    /// 创建选项。<paramref name="writeIndented"/> 为 true 时输出 4 空格缩进
    /// （.NET 8 的 <c>JsonWriterOptions.IndentSize</c> 尚不可用，故由 <see cref="Serialize{T}"/> 后处理实现）。
    /// </summary>
    public static JsonSerializerOptions Create(bool writeIndented = true) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = writeIndented,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new LenientStringConverter(),
            new StringOrStringListConverter(),
        },
    };

    /// <summary>序列化：缩进 4 空格（与旧版 <c>JSON.stringify(..., null, 4)</c> 对齐）。</summary>
    public static string Serialize<T>(T value) => Indent4(JsonSerializer.Serialize(value, Options));

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>尝试反序列化，失败返回 false 并带出异常信息（供 UI 提示“config.json 格式或编码错误”）。</summary>
    public static bool TryDeserialize<T>(string json, out T? value, out string? error)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(json, Options);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            value = default;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 把 2 空格缩进改成 4 空格。
    /// 安全前提：序列化输出中的真实换行只来自缩进（字符串内的 \n 已被转义为两个字符）。
    /// </summary>
    internal static string Indent4(string json)
    {
        if (string.IsNullOrEmpty(json) || !json.Contains('\n'))
        {
            return json;
        }

        var lines = json.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var spaces = 0;
            while (spaces < line.Length && line[spaces] == ' ')
            {
                spaces++;
            }

            if (spaces > 0)
            {
                lines[i] = new string(' ', spaces * 2) + line[spaces..];
            }
        }

        return string.Join('\n', lines);
    }
}
