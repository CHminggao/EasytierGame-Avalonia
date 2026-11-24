using Avalonia.Controls;
using Avalonia.Interactivity;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>导入分享对话框。</summary>
public partial class ImportShareDialog : Window
{
    public ImportShareDialog()
    {
        InitializeComponent();
        // 让确认框以本对话框为 Owner，避免在主窗口上嵌套模态
        Opened += (_, _) =>
        {
            if (DataContext is ImportShareViewModel vm)
            {
                vm.OwnerWindow = this;
            }
        };
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportShareViewModel vm && await vm.ApplyAsync())
        {
            Close(true);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
