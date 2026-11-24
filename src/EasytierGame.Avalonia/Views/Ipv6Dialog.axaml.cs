using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>IPv6 地址填写对话框。</summary>
public partial class Ipv6Dialog : Window
{
    public Ipv6Dialog()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var text = (ViewModel.Config.Ipv6 ?? "").Trim();
        ViewModel.Config.Ipv6 = text;

        if (text.Length > 0 && !IsValidIpv6(text))
        {
            ViewModel.Services.Toast.Error("请输入正确的IPV6地址");
            return;
        }

        _ = ViewModel.Services.SaveAsync();
        Close(true);
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.Config.Ipv6 = "";
            _ = ViewModel.Services.SaveAsync();
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>严格校验 IPv6。</summary>
    private static bool IsValidIpv6(string text)
    {
        return IPAddress.TryParse(text, out var address)
               && address.AddressFamily == AddressFamily.InterNetworkV6;
    }
}
