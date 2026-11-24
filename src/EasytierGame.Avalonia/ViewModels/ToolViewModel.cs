using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Models;
using EasytierGame.Core.SystemIntegration;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>
/// 增强工具窗口的 ViewModel。两个页签：
/// 网卡（查询网卡信息 + 修改跃点）/ 防火墙（状态 + 一键关闭 + Ping）。
/// </summary>
public partial class ToolViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly SysIntegration _sys;

    public ToolViewModel(AppServices services)
    {
        _services = services;
        _sys = services.Sys;

        NetCards = new ObservableCollection<NetCardInfo>();
        FirewallStates = new ObservableCollection<FirewallProfileState>();
        PingLog = new ObservableCollection<string>();
    }

    public AppState State => _services.State;

    public ObservableCollection<NetCardInfo> NetCards { get; }

    public ObservableCollection<FirewallProfileState> FirewallStates { get; }

    public ObservableCollection<string> PingLog { get; }

    // ---------------- 网卡跃点 ----------------

    [ObservableProperty]
    private NetCardInfo? _selectedNetCard;

    [ObservableProperty]
    private string _metricInput = "";

    /// <summary>选中网卡后把当前跃点回填到输入框。</summary>
    partial void OnSelectedNetCardChanged(NetCardInfo? value)
    {
        if (value is not null)
        {
            MetricInput = value.Metric;
        }
    }

    /// <summary>
    /// 查询网卡信息（netsh interface ipv4 show interface）。
    /// </summary>
    [RelayCommand]
    public async Task RefreshNetCardsAsync()
    {
        var list = await _sys.GetNetCardsAsync();
        NetCards.Clear();
        foreach (var card in list)
        {
            NetCards.Add(card);
        }

        if (list.Count == 0)
        {
            _services.Toast.Warning("未获取到网卡信息");
        }
    }

    /// <summary>保存选中网卡的跃点。</summary>
    [RelayCommand]
    private async Task SaveMetricAsync()
    {
        if (SelectedNetCard is null)
        {
            _services.Toast.Warning("请先选择一张网卡");
            return;
        }

        if (!int.TryParse(MetricInput, out var metric) || metric < 1 || metric > 9999)
        {
            _services.Toast.Error("跃点必须是 1-9999 之间的整数");
            return;
        }

        var ok = await _sys.SetNetCardMetricAsync(SelectedNetCard.Name, metric.ToString());
        if (ok)
        {
            _services.Toast.Success("设置成功");
            await RefreshNetCardsAsync();
        }
        else
        {
            _services.Toast.Error("设置失败（需要管理员权限）");
        }
    }

    // ---------------- 防火墙 ----------------

    /// <summary>读取防火墙状态。</summary>
    [RelayCommand]
    public async Task RefreshFirewallAsync()
    {
        var states = await _sys.GetFirewallStateAsync();
        FirewallStates.Clear();
        foreach (var state in states)
        {
            FirewallStates.Add(state);
        }
    }

    /// <summary>关闭所有防火墙（需要管理员权限）。</summary>
    [RelayCommand]
    private async Task DisableFirewallAsync()
    {
        var ok = await _services.Dialogs.ConfirmAsync(
            "确定{action}所有配置文件的防火墙吗？",
            "提示",
            "关闭",
            "取消",
            ConfirmKind.Danger,
            highlightAction: "关闭");

        if (!ok)
        {
            return;
        }

        var (success, _) = await _sys.DisableFirewallAsync();
        if (success)
        {
            _services.Toast.Success("关闭成功");
        }
        else
        {
            _services.Toast.Error("关闭失败（需要管理员权限）");
        }

        await RefreshFirewallAsync();
    }

    // ---------------- ping ----------------

    [ObservableProperty]
    private string _pingIp = "";

    /// <summary>ping 次数（字符串绑定，避免用户输入非数字时转换异常）。</summary>
    [ObservableProperty]
    private string _pingCount = "4";

    [ObservableProperty]
    private bool _isPinging;

    [RelayCommand]
    private async Task TestPingAsync(string? type)
    {
        if (string.IsNullOrWhiteSpace(PingIp))
        {
            _services.Toast.Error("请输入正确的IP");
            return;
        }

        var count = int.TryParse(PingCount, out var parsed) ? Math.Clamp(parsed, 1, 10) : 4;

        IsPinging = true;
        PingLog.Clear();
        try
        {
            var ipv6 = string.Equals(type, "v6", StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < count; i++)
            {
                var (ok, output) = await _sys.PingAsync(PingIp, ipv6);
                PingLog.Add($"{i + 1} - {(ok ? "成功" : "失败")} {FirstMeaningfulLine(output)}");

                if (i != count - 1)
                {
                    await Task.Delay(1000);
                }
            }

            PingLog.Add("完毕");
        }
        finally
        {
            IsPinging = false;
        }
    }

    private static string FirstMeaningfulLine(string output)
    {
        return output
            .Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0
                                 && !l.StartsWith("正在 Ping", StringComparison.OrdinalIgnoreCase)
                                 && !l.StartsWith("Pinging", StringComparison.OrdinalIgnoreCase))
            ?? "";
    }

    /// <summary>打开日志目录。</summary>
    [RelayCommand]
    private void OpenLogDir() => _sys.OpenDirectory(_services.Paths.LogDir);

    /// <summary>打开 config.json 所在目录。</summary>
    [RelayCommand]
    private void OpenConfigDir() => _sys.OpenDirectory(_services.Paths.EasytierDir);
}
