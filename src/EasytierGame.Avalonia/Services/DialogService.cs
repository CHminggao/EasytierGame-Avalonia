using Avalonia.Controls;

namespace EasytierGame.Desktop.Services;

/// <summary>确认框的按钮语义（对应 ElConfirmDanger / ElConfirmPrimary / ElConfirmSucces）。</summary>
public enum ConfirmKind
{
    Primary,
    Danger,
    Success,
}

/// <summary>
/// 模态确认框，替代 Element Plus 的 <c>ElMessageBox.confirm/alert/prompt</c>。
/// <para>
/// <b>全异步</b>。早期版本用 <c>ShowDialog(...).GetAwaiter().GetResult()</c> 在 UI 线程上
/// 同步等模态窗口，会把 UI 线程锁死——对话框能显示但无法渲染/响应，
/// 表现为「程序卡住、弹窗空白」。现在一律 <c>await dialog.ShowDialog&lt;bool?&gt;(owner)</c>，
/// 调用方都必须是 async。
/// </para>
/// </summary>
public sealed class DialogService
{
    /// <summary>当前活动的主窗口，作为模态对话框的 Owner。</summary>
    public Window? Owner { get; set; }

    /// <summary>弹出确认框。返回 true 表示用户点了“确认”。</summary>
    public async Task<bool> ConfirmAsync(
        string content,
        string title = "提示",
        string confirmText = "确定",
        string cancelText = "取消",
        ConfirmKind kind = ConfirmKind.Primary,
        Window? owner = null,
        bool showCancel = true,
        string? highlightAction = null)
    {
        var dialog = new Views.MessageDialog
        {
            Title = title,
            ContentText = content,
            ConfirmText = confirmText,
            CancelText = cancelText,
            Kind = kind,
            ShowCancelButton = showCancel,
            HighlightAction = highlightAction,
        };

        var parent = owner ?? Owner;
        if (parent is null)
        {
            // 没有 Owner 时退化为非模态，不阻塞
            dialog.Show();
            return false;
        }

        return await dialog.ShowDialog<bool?>(parent).ConfigureAwait(true) == true;
    }

    /// <summary>只有“知道了”按钮的提示框。</summary>
    public Task AlertAsync(string content, string title = "提示", Window? owner = null, string buttonText = "知道了")
        => ConfirmAsync(content, title, buttonText, "", ConfirmKind.Primary, owner, showCancel: false);

    /// <summary>输入框提示。取消时返回 null。</summary>
    public async Task<string?> PromptAsync(
        string content,
        string title = "提示",
        string placeholder = "",
        string confirmText = "确定",
        string cancelText = "取消",
        Window? owner = null)
    {
        var dialog = new Views.PromptDialog
        {
            Title = title,
            ContentText = content,
            Placeholder = placeholder,
            ConfirmText = confirmText,
            CancelText = cancelText,
        };

        var parent = owner ?? Owner;
        if (parent is null)
        {
            dialog.Show();
            return null;
        }

        return await dialog.ShowDialog<bool?>(parent).ConfigureAwait(true) == true
            ? dialog.InputText
            : null;
    }
}
