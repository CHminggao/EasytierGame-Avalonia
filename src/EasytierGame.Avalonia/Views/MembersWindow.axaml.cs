using Avalonia;
using Avalonia.Controls;
using EasytierGame.Desktop.Services;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>成员列表窗口。断线时提示重试。</summary>
public partial class MembersWindow : Window
{
    public MembersWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private MembersViewModel? ViewModel => DataContext as MembersViewModel;

    private void OnOpened(object? sender, EventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        // 剪贴板需要 TopLevel
        MembersViewModel.ClipboardWriter = text => ClipboardService.SetText(this, text);
        ViewModel.ConnectionLost += OnConnectionLost;
    }

    private void OnConnectionLost(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var services = (Application.Current as App)?.Services;
            if (services is null)
            {
                Close();
                return;
            }

            // 必须 await：之前同步调用后立刻 Close()，弹窗根本来不及显示
            await services.Dialogs.ConfirmAsync(
                "连接已断开,是否重新尝试?",
                "警告",
                "重试",
                "关闭窗口",
                ConfirmKind.Danger,
                this);

            // 简化处理：关闭窗口，由用户重新打开
            Close();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ConnectionLost -= OnConnectionLost;
            ViewModel.Dispose();
        }

        base.OnClosed(e);
    }
}
