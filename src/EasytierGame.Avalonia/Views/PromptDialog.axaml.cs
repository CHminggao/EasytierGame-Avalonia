using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EasytierGame.Desktop.Views;

/// <summary>输入对话框。等价 Element Plus 的 <c>ElMessageBox.prompt</c>。</summary>
public partial class PromptDialog : Window
{
    public PromptDialog()
    {
        InitializeComponent();
    }

    public string ContentText
    {
        get => ContentBlock.Text ?? "";
        set => ContentBlock.Text = value;
    }

    public string Placeholder { get; set; } = "";

    public string ConfirmText
    {
        get => ConfirmButton.Content?.ToString() ?? "确定";
        set => ConfirmButton.Content = value;
    }

    public string CancelText
    {
        get => CancelButton.Content?.ToString() ?? "取消";
        set => CancelButton.Content = value;
    }

    /// <summary>用户输入的内容。</summary>
    public string InputText => InputBox.Text ?? "";

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        TitleText.Text = Title;
        InputBox.Watermark = Placeholder;
        InputBox.Focus();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
