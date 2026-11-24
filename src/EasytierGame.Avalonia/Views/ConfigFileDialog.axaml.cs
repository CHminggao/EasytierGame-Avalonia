using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>外部配置文件选择对话框。</summary>
public partial class ConfigFileDialog : Window
{
    public ConfigFileDialog()
    {
        InitializeComponent();
    }

    private async void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConfigFileViewModel vm && await vm.ApplyAsync())
        {
            Close(true);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
