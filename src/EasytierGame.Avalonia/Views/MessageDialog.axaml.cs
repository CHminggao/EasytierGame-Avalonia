using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using EasytierGame.Desktop.Services;

namespace EasytierGame.Desktop.Views;

/// <summary>
/// 通用确认 / 提示对话框。等价 Element Plus 的
/// <c>ElConfirmDanger</c> / <c>ElConfirmPrimary</c> / <c>ElConfirmSucces</c>。
/// 支持在内容里用 <c>{action}</c> 占位，占位处按变体颜色高亮。
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    private string _contentText = "";

    public string ContentText
    {
        get => _contentText;
        set
        {
            _contentText = value;
            RenderContent();
        }
    }

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

    public bool ShowCancelButton
    {
        get => CancelButton.IsVisible;
        set => CancelButton.IsVisible = value;
    }

    public string? HighlightAction { get; set; }

    public ConfirmKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            ApplyKind();
        }
    }

    private ConfirmKind _kind = ConfirmKind.Primary;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        TitleText.Text = Title;
        ApplyKind();
        RenderContent();
    }

    private void ApplyKind()
    {
        ConfirmButton.Classes.Remove("danger");
        ConfirmButton.Classes.Remove("primary");

        ConfirmButton.Classes.Add(_kind switch
        {
            ConfirmKind.Danger => "danger",
            _ => "primary",
        });
    }

    private void RenderContent()
    {
        if (ContentBlock is null)
        {
            return;
        }

        ContentBlock.Inlines?.Clear();

        if (string.IsNullOrEmpty(_contentText))
        {
            return;
        }

        var action = HighlightAction ?? ConfirmText;
        var brush = _kind switch
        {
            ConfirmKind.Danger => new SolidColorBrush(Color.Parse("#F56C6C")),
            ConfirmKind.Success => new SolidColorBrush(Color.Parse("#67C23A")),
            _ => new SolidColorBrush(Color.Parse("#409EFF")),
        };

        var parts = _contentText.Split("{action}");
        var inlines = new Avalonia.Controls.Documents.InlineCollection();
        ContentBlock.Inlines = inlines;

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(parts[i]));
            }

            if (i < parts.Length - 1)
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(action)
                {
                    Foreground = brush,
                    FontWeight = FontWeight.SemiBold,
                });
            }
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
