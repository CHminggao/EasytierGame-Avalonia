using System.Text;

namespace EasytierGame.Core.Config;

/// <summary>
/// “配置分享 / 导入”的编解码：等价旧版
/// <c>btoa(encodeURIComponent(JSON.stringify(obj)))</c> 与
/// <c>JSON.parse(decodeURIComponent(atob(text)))</c>。
/// <para>
/// 必须与浏览器完全兼容，所以这里自己实现 <c>encodeURIComponent</c> 的保留字符集合
/// （<c>A-Z a-z 0-9 - _ . ! ~ * ' ( )</c> 不转义），不能用 <c>Uri.EscapeDataString</c>，
/// 否则与旧版互换时会得到不一致的字符串。
/// </para>
/// </summary>
public static class ShareCodec
{
    private const string Unreserved = "-_.!~*'()";

    /// <summary>把明文编码成分享字符串（Base64）。</summary>
    public static string Encode(string plainText)
    {
        var escaped = EncodeUriComponent(plainText);
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(escaped));
    }

    /// <summary>把分享字符串解码成明文。失败时抛 <see cref="FormatException"/>。</summary>
    public static string Decode(string shareText)
    {
        if (string.IsNullOrWhiteSpace(shareText))
        {
            throw new FormatException("分享内容为空");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(shareText.Trim());
        }
        catch (FormatException ex)
        {
            throw new FormatException("分享内容不是合法的 Base64", ex);
        }

        var escaped = Encoding.ASCII.GetString(bytes);
        return DecodeUriComponent(escaped);
    }

    /// <summary>尝试解码，失败返回 false。</summary>
    public static bool TryDecode(string shareText, out string? plainText, out string? error)
    {
        try
        {
            plainText = Decode(shareText);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            plainText = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>与浏览器 <c>encodeURIComponent</c> 一致的实现。</summary>
    public static string EncodeUriComponent(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var sb = new StringBuilder(value.Length * 3);
        foreach (var ch in value)
        {
            if (IsUnreserved(ch))
            {
                sb.Append(ch);
                continue;
            }

            // UTF-8 编码后逐字节转 %XX
            var bytes = ch <= 0x7F
                ? new[] { (byte)ch }
                : Encoding.UTF8.GetBytes(ch.ToString());
            foreach (var b in bytes)
            {
                sb.Append('%');
                sb.Append(b.ToString("X2"));
            }
        }

        return sb.ToString();
    }

    /// <summary>与浏览器 <c>decodeURIComponent</c> 一致的实现（容错：非法 % 序列原样保留）。</summary>
    public static string DecodeUriComponent(string escaped)
    {
        if (string.IsNullOrEmpty(escaped))
        {
            return "";
        }

        var bytes = new List<byte>(escaped.Length);
        for (var i = 0; i < escaped.Length; i++)
        {
            var ch = escaped[i];
            if (ch == '%' && i + 2 < escaped.Length
                && IsHex(escaped[i + 1]) && IsHex(escaped[i + 2]))
            {
                bytes.Add(Convert.ToByte(escaped.Substring(i + 1, 2), 16));
                i += 2;
                continue;
            }

            bytes.AddRange(Encoding.UTF8.GetBytes(ch.ToString()));
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool IsUnreserved(char ch)
    {
        return (ch >= 'A' && ch <= 'Z')
               || (ch >= 'a' && ch <= 'z')
               || (ch >= '0' && ch <= '9')
               || Unreserved.IndexOf(ch) >= 0;
    }

    private static bool IsHex(char ch)
    {
        return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
    }
}
