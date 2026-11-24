using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>增强工具窗口。</summary>
public partial class ToolWindow : Window
{
    public ToolWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ToolViewModel vm)
            {
                await vm.RefreshFirewallAsync();
            }
        };
    }

    private async void OnRefreshNetCards(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToolViewModel vm)
        {
            await vm.RefreshNetCardsAsync();
        }
    }
}
