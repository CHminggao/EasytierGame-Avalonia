using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Cli;
using EasytierGame.Core.Models;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>成员列表行（旧版 member.vue 的 14 列）。</summary>
public sealed class MemberRow
{
    public string Hostname { get; init; } = "";

    /// <summary>方式（本机 / P2P / Relay）。“Local”已在显示层转成“本机”。</summary>
    public string Cost { get; init; } = "";

    public string Ipv4 { get; init; } = "";

    public string Latency { get; init; } = "";

    public string LossRate { get; init; } = "";

    public string NatType { get; init; } = "";

    public string Version { get; init; } = "";

    public string TunnelProto { get; init; } = "";

    /// <summary>代理方式提示（KCP / QUIC）。</summary>
    public string Proxy { get; init; } = "";

    public string RxBytes { get; init; } = "";

    public string TxBytes { get; init; } = "";

    /// <summary>连接远端地址（可能多行）。</summary>
    public string Connections { get; init; } = "";

    public string Id { get; init; } = "";
}

/// <summary>成员列表窗口的 ViewModel。每秒轮询 CLI（旧版行为）。</summary>
public partial class MembersViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private CancellationTokenSource? _cts;

    public MembersViewModel(AppServices services)
    {
        _services = services;
        Members = new ObservableCollection<MemberRow>();
        Proxies = new ObservableCollection<ProxyEntry>();
        _ = RefreshLoopAsync();
    }

    public ObservableCollection<MemberRow> Members { get; }

    public ObservableCollection<ProxyEntry> Proxies { get; }

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>连接已断开（旧版会弹“是否重试”）。</summary>
    public event EventHandler? ConnectionLost;

    private async Task RefreshLoopAsync()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            var result = await _services.Probe.GetMembersAsync(token).ConfigureAwait(false);

            if (!result.Ok || result.Value is null)
            {
                if (!token.IsCancellationRequested)
                {
                    ConnectionLost?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            var rows = result.Value.Select(ToRow).ToList();
            var proxyResult = await _services.Probe.GetProxyAsync(token).ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Members.Clear();
                foreach (var row in rows)
                {
                    Members.Add(row);
                }

                Proxies.Clear();
                if (proxyResult.Ok && proxyResult.Value is not null)
                {
                    foreach (var proxy in proxyResult.Value)
                    {
                        Proxies.Add(proxy);
                    }
                }
            });

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static MemberRow ToRow(Member member)
    {
        var proxyHint = member.Proxy is { Length: >= 2 } p && !string.IsNullOrEmpty(p[0])
            ? $"{p[0].ToUpperInvariant()}:{p[1]}"
            : "-";

        return new MemberRow
        {
            Hostname = member.Hostname,
            // Local → “本机”
            Cost = MemberDisplay.FormatCost(member.Cost),
            Ipv4 = member.Ipv4,
            Latency = FormatLatency(member.LatMs),
            LossRate = member.LossRate,
            // fullcone / symmetric … → NAT1-4
            NatType = MemberDisplay.FormatNatType(member.NatType),
            Version = member.Version,
            TunnelProto = member.TunnelProto,
            Proxy = proxyHint,
            RxBytes = member.RxBytes,
            TxBytes = member.TxBytes,
            Connections = string.Join("\n", member.ConnectionsAddrs),
            Id = member.Id,
        };
    }

    /// <summary>延迟格式化（旧版 formatLatency）。</summary>
    private static string FormatLatency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "-";
        }

        return double.TryParse(raw, out var value) && value >= 0
            ? value.ToString("F0")
            : "-";
    }

    /// <summary>剪贴板写入委托，由视图层注入。</summary>
    public static Action<string>? ClipboardWriter { get; set; }

    [RelayCommand]
    private void CopyRow(MemberRow? row)
    {
        if (row is null)
        {
            return;
        }

        var text = $"主机名={row.Hostname} IP={row.Ipv4} 方式={row.Cost} 延迟={row.Latency}ms 丢包={row.LossRate}";
        try
        {
            ClipboardWriter?.Invoke(text);
            _services.Toast.Success("复制成功");
        }
        catch
        {
            _services.Toast.Error("复制失败");
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _cts = null;
        GC.SuppressFinalize(this);
    }
}
