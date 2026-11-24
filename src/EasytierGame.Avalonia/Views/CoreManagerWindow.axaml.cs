using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>内核管理对话框。</summary>
public partial class CoreManagerWindow : Window
{
    public CoreManagerWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is CoreManagerViewModel vm)
            {
                await vm.LoadReleasesAsync();
            }
        };
    }

    private void OnOpenGhProxy(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.Services.Sys.OpenPath("https://ghproxy.link/");
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CoreManagerViewModel vm)
        {
            _ = vm.SaveAsync();
        }

        Close();
    }
}
