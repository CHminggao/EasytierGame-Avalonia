using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>高级选项窗口。</summary>
public partial class AdvanceWindow : Window
{
    public AdvanceWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnOpenConfigDir(object? sender, RoutedEventArgs e)
        => ViewModel?.Services.Sys.OpenDirectory(ViewModel.Services.Paths.EasytierDir);

    private void OnOpenLogDir(object? sender, RoutedEventArgs e)
        => ViewModel?.Services.Sys.OpenDirectory(ViewModel.Services.Paths.LogDir);

    private void OnPreventSleepChanged(object? sender, RoutedEventArgs e)
        => ViewModel?.ApplyPreventSleep();

    /// <summary>编辑自定义监听地址。</summary>
    private async void OnEditListener(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new TextEditorDialog
        {
            Title = "自定义监听地址",
            Hint = "一行一个，例如：\ntcp://0.0.0.0:11010\nudp://0.0.0.0:11010",
            Content = ViewModel.Config.CustomListenerData,
        };

        if (await dialog.ShowDialog<bool?>(this) == true)
        {
            ViewModel.Config.CustomListenerData = dialog.Content;
            _ = ViewModel.Services.SaveAsync();
        }
    }

    /// <summary>编辑端口转发规则。</summary>
    private async void OnEditPortForward(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new TextEditorDialog
        {
            Title = "端口转发配置",
            Hint = "每行一个转发规则\n格式：协议://本地地址:本地端口/目标地址:目标端口\n例如：tcp://0.0.0.0:8080/192.168.1.5:80",
            Content = ViewModel.Config.PortForwardData,
        };

        if (await dialog.ShowDialog<bool?>(this) == true)
        {
            ViewModel.Config.PortForwardData = dialog.Content;
            _ = ViewModel.Services.SaveAsync();
        }
    }
}
