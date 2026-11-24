using Avalonia;
using Avalonia.Styling;

namespace EasytierGame.Desktop.Services;

/// <summary>
/// 主题切换。Avalonia 用 <see cref="Application.RequestedThemeVariant"/> 切换 FluentTheme，
/// 比 WPF 版换调色板字典简单得多。
/// </summary>
public static class ThemeManager
{
    /// <summary>当前是否暗色。</summary>
    public static bool IsDark { get; private set; }

    /// <summary>应用主题。</summary>
    public static void Apply(bool dark)
    {
        IsDark = dark;

        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>切换并返回新状态。</summary>
    public static bool Toggle()
    {
        Apply(!IsDark);
        return IsDark;
    }
}
