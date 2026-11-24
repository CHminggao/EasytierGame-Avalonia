namespace EasytierGame.Core.Utils;

/// <summary>文件名合法性校验（等价旧版 <c>utils/index.ts::isValidWindowsFileName</c>）。</summary>
public static class FileNameValidator
{
    private static readonly char[] InvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// 是否为合法的 Windows 文件名（不含路径）。
    /// 旧版要求至少 2 个字符（“请输入2个字以上的存储名”）。
    /// </summary>
    public static bool IsValidWindowsFileName(string? name, int minLength = 2)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < minLength)
        {
            return false;
        }

        var trimmed = name.Trim();

        if (trimmed.IndexOfAny(InvalidChars) >= 0)
        {
            return false;
        }

        // 不能以空格或点结尾
        if (trimmed.EndsWith(' ') || trimmed.EndsWith('.'))
        {
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(trimmed);
        return !ReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase);
    }
}
