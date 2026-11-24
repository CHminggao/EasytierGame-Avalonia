using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EasytierGame.Desktop.Views;

/// <summary>多行文本编辑对话框（自定义监听地址 / 端口转发规则等）。</summary>
public partial class TextEditorDialog : Window
{
    public TextEditorDialog()
    {
        InitializeComponent();
    }

    public string Hint
    {
        get => HintText.Text ?? "";
        set
        {
            HintText.Text = value ?? "";
            HintText.IsVisible = !string.IsNullOrWhiteSpace(value);
        }
    }

    /// <summary>编辑框内容。注意不是 <see cref="ContentControl.Content"/>。</summary>
    public new string Content
    {
        get => Editor.Text ?? "";
        set => Editor.Text = value ?? "";
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
