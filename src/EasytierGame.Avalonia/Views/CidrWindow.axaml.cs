using Avalonia.Controls;
using EasytierGame.Desktop.ViewModels;

namespace EasytierGame.Desktop.Views;

/// <summary>子网代理窗口。</summary>
public partial class CidrWindow : Window
{
    public CidrWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is CidrViewModel vm)
        {
            _ = vm.SaveCommand.ExecuteAsync(null);
            vm.Dispose();
        }

        base.OnClosed(e);
    }
}
