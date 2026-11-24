using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace EasytierGame.Desktop.Services;

/// <summary>Toast 消息类型（对应 ElMessage 的 type）。</summary>
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>一条提示。</summary>
public sealed class ToastMessage
{
    public required string Text { get; init; }

    public ToastKind Kind { get; init; } = ToastKind.Info;

    /// <summary>用于 XAML 绑定：语义色 Key。</summary>
    public string KindName => Kind.ToString().ToLowerInvariant();
}

/// <summary>
/// 全局提示队列，替代 Element Plus 的 <c>ElMessage</c>。
/// <para>
/// Avalonia 版与 WPF 版行为一致：<b>只用 Post 投递，绝不同步等待 UI 线程</b>，
/// 避免「后台线程等 UI、UI 等模态循环」的死锁。
/// </para>
/// </summary>
public sealed class ToastService
{
    public ObservableCollection<ToastMessage> Items { get; } = new();

    public void Show(string text, ToastKind kind = ToastKind.Info, int durationMs = 2600)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowCore(text, kind, durationMs);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ShowCore(text, kind, durationMs));
        }
    }

    private void ShowCore(string text, ToastKind kind, int durationMs)
    {
        var message = new ToastMessage { Text = text, Kind = kind };
        Items.Insert(0, message);

        while (Items.Count > 4)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Items.Remove(message);
        };
        timer.Start();
    }

    public void Info(string text) => Show(text, ToastKind.Info);

    public void Success(string text) => Show(text, ToastKind.Success);

    public void Warning(string text) => Show(text, ToastKind.Warning);

    public void Error(string text) => Show(text, ToastKind.Error);
}
