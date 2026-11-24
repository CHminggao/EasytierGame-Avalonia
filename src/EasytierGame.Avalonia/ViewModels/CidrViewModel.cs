using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasytierGame.Desktop.Services;
using EasytierGame.Core.Models;

namespace EasytierGame.Desktop.ViewModels;

/// <summary>路由表行（子网代理窗口）。</summary>
public sealed class RouteRow
{
    public string Hostname { get; init; } = "";

    public string Ipv4 { get; init; } = "";

    public string ProxyCidrs { get; init; } = "";
}

/// <summary>子网代理窗口的 ViewModel。等价旧版 <c>cidr.vue</c>。</summary>
public partial class CidrViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private CancellationTokenSource? _cts;

    public CidrViewModel(AppServices services)
    {
        _services = services;
        Routes = new ObservableCollection<RouteRow>();
        _ = PollAsync();
    }

    public ObservableCollection<RouteRow> Routes { get; }

    public AppState State => _services.State;

    public AppConfig Config => _services.State.Config;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isSwitchEnabled;

    private async Task PollAsync()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            if (_services.Monitor.State == Core.Session.ConnectionState.Connected && State.CidrEnable)
            {
                var result = await _services.Probe.GetRouteAsync(token).ConfigureAwait(false);
                var rows = result.Ok && result.Value is not null
                    ? result.Value.Select(r => new RouteRow
                    {
                        Hostname = r.Hostname,
                        Ipv4 = r.Ipv4Addr ?? "",
                        ProxyCidrs = string.Join(", ", r.ProxyCidrs),
                    }).ToList()
                    : new List<RouteRow>();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Routes.Clear();
                    foreach (var row in rows)
                    {
                        Routes.Add(row);
                    }
                });
            }

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

    /// <summary>勾选“通过系统内核转发”后立即保存。</summary>
    partial void OnIsRunningChanged(bool value) => IsSwitchEnabled = !value;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _services.SaveAsync();
        if (_services.Monitor.State == Core.Session.ConnectionState.Connected)
        {
            _services.Toast.Info("配置已保存，重新联机后生效");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
