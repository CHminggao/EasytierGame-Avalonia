using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>房间配置管理对话框。</summary>
public partial class ConfigAdminDialog : Window
{
    public ConfigAdminDialog()
    {
        InitializeComponent();
        // 让确认框以本对话框为 Owner，避免在主窗口上嵌套模态
        Opened += (_, _) =>
        {
            if (DataContext is ConfigAdminViewModel vm)
            {
                vm.OwnerWindow = this;
            }
        };
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
