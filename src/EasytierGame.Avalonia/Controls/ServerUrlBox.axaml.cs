using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EasytierGame.Core.Config;

namespace EasytierGame.Desktop.Controls;

/// <summary>
/// 服务器节点地址输入组件。
/// <para>
/// 与 WPF 版行为一致：对外只暴露 <see cref="CommitRequested"/> / <see cref="DeleteRequested"/> /
/// <see cref="CopyRequested"/> 三个事件，校验集中在 <see cref="ServerUrlValidator"/>，
/// 不在窗口 code-behind 里散落校验逻辑。
/// </para>
/// </summary>
public partial class ServerUrlBox : UserControl
{
    /// <summary>候选池（下拉显示用）。</summary>
    public static readonly StyledProperty<IEnumerable<string>?> CandidatesProperty =
        AvaloniaProperty.Register<ServerUrlBox, IEnumerable<string>?>(nameof(Candidates));

    /// <summary>当前地址（双向）。</summary>
    public static readonly StyledProperty<string> CurrentUrlProperty =
        AvaloniaProperty.Register<ServerUrlBox, string>(nameof(CurrentUrl), "");

    private bool _suppressSync;

    public ServerUrlBox()
    {
        InitializeComponent();
        CandidateCombo.ItemsSource = Array.Empty<string>();
    }

    /// <summary>校验通过后的提交请求（参数为归一化地址）。</summary>
    public event EventHandler<string>? CommitRequested;

    /// <summary>下拉项里的删除请求。</summary>
    public event EventHandler<string>? DeleteRequested;

    /// <summary>下拉项里的复制请求。</summary>
    public event EventHandler<string>? CopyRequested;

    public IEnumerable<string>? Candidates
    {
        get => GetValue(CandidatesProperty);
        set => SetValue(CandidatesProperty, value);
    }

    public string CurrentUrl
    {
        get => GetValue(CurrentUrlProperty);
        set => SetValue(CurrentUrlProperty, value);
    }

    /// <summary>当前输入文本。</summary>
    public string InputText => UrlInput.Text ?? "";

    /// <summary>把输入框设为指定值。</summary>
    public void SetInput(string? url)
    {
        _suppressSync = true;
        UrlInput.Text = url ?? "";
        _suppressSync = false;
    }

    /// <summary>只做静态校验并展示错误。</summary>
    public ServerUrlValidationResult ValidateInput()
    {
        var result = ServerUrlValidator.Validate(InputText, out _);
        SetError(result.Ok ? "" : result.Error ?? "");
        return result;
    }

    /// <summary>清除错误提示（用于切换/显示已有地址时不报错）。</summary>
    public void ClearError() => SetError("");

    /// <summary>校验并提交。成功返回归一化地址。</summary>
    public string? Commit()
    {
        var result = ServerUrlValidator.Validate(InputText, out var normalized);
        if (!result.Ok)
        {
            SetError(result.Error ?? "地址无效");
            return null;
        }

        SetError("");
        CommitRequested?.Invoke(this, normalized);
        return normalized;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CandidatesProperty)
        {
            CandidateCombo.ItemsSource = Candidates ?? Array.Empty<string>();
        }
        else if (change.Property == CurrentUrlProperty && !_suppressSync)
        {
            if (!string.IsNullOrEmpty(CurrentUrl) && UrlInput.Text != CurrentUrl)
            {
                SetInput(CurrentUrl);
            }
        }
    }

    private void SetError(string text)
    {
        ErrorText.Text = text;
        ErrorRow.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        Commit();
        e.Handled = true;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => Commit();

    private void OnItemCopyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((sender as Control)?.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            CopyRequested?.Invoke(this, url);
        }
    }

    private void OnItemDeleteClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((sender as Control)?.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            DeleteRequested?.Invoke(this, url);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CandidateCombo.SelectedItem is string url && !string.IsNullOrWhiteSpace(url))
        {
            SetInput(url);

            // 不在这里校验：下拉自动选中首项时会把默认的无协议头地址
            // （如 public.easytier.top:11010）判为非法，启动即显示红字。
            // 校验只在用户显式提交（回车/添加）时进行。
            ClearError();
        }
    }
}
