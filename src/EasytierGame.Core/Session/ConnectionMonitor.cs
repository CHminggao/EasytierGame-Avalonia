using EasytierGame.Core.Cli;
using EasytierGame.Core.Models;
using EasytierGame.Core.Processes;

namespace EasytierGame.Core.Session;

/// <summary>联机状态（对应旧版主界面标签的三种文案）。</summary>
public enum ConnectionState
{
    /// <summary>未联机。</summary>
    Idle,

    /// <summary>联机中（已拉起内核，尚未拿到本地 IP）。</summary>
    Connecting,

    /// <summary>联机成功（CLI 已返回 cost == "Local" 的 ipv4）。</summary>
    Connected,
}

/// <summary>状态变化事件的载荷。</summary>
public sealed record ConnectionStatusChanged(ConnectionState State, string Ipv4);

/// <summary>
/// 联机状态机。等价旧版 <c>pages/index.vue</c> 里那段“每秒轮询 <c>get_members_by_cli</c>
/// 找 <c>cost == "Local"</c> 的 ipv4”的逻辑，但拎成独立、可测试、无 UI 依赖的组件。
/// <para>
/// 额外承担旧版散落在日志回调里的一个副作用：
/// 命中 <c>tun device ready. dev: xxx</c> 时把网卡跃点设置回调出去。
/// </para>
/// </summary>
public sealed class ConnectionMonitor : IAsyncDisposable
{
    private static readonly System.Text.RegularExpressions.Regex TunDeviceRegex =
        new(@"tun device ready\. dev: (.*)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly CoreProcessManager _processes;
    private readonly CliProbe _probe;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ConnectionMonitor(CoreProcessManager processes, CliProbe probe)
    {
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));

        _processes.OutputLine += OnOutputLine;
        _processes.Exited += OnExited;
    }

    /// <summary>当前状态。</summary>
    public ConnectionState State { get; private set; } = ConnectionState.Idle;

    /// <summary>当前本地虚拟 IPv4（未连上时为空串）。</summary>
    public string Ipv4 { get; private set; } = "";

    /// <summary>状态变化。</summary>
    public event EventHandler<ConnectionStatusChanged>? StatusChanged;

    /// <summary>
    /// 检测到 TUN 网卡就绪。参数：网卡名。调用方据此设置跃点（旧版行为）。
    /// </summary>
    public event EventHandler<string>? TunDeviceReady;

    /// <summary>内核输出行（原始文本，供日志窗口显示）。</summary>
    public event EventHandler<string>? OutputLine;

    /// <summary>内核意外退出（非主动停止）。</summary>
    public event EventHandler<int>? UnexpectedExit;

    /// <summary>开始轮询。在 <see cref="CoreProcessManager.StartAsync"/> 之后调用。</summary>
    public void Start()
    {
        lock (_gate)
        {
            StopLoop();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetState(ConnectionState.Connecting, "");
            _loop = Task.Run(() => PollLoopAsync(token), CancellationToken.None);
        }
    }

    /// <summary>停止轮询并复位状态。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            StopLoop();
        }

        SetState(ConnectionState.Idle, "");
    }

    private void StopLoop()
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
        _loop = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var local = await _probe.GetLocalMemberAsync(ct).ConfigureAwait(false);
                if (local is not null && !string.IsNullOrWhiteSpace(local.Ipv4))
                {
                    SetState(ConnectionState.Connected, local.Ipv4);
                }
                else if (State == ConnectionState.Connected)
                {
                    // 旧版：拿不到本地信息就复位（ipReset）
                    SetState(ConnectionState.Connecting, "");
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // 单次查询失败不影响下一轮
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnOutputLine(object? sender, CoreOutputLine e)
    {
        OutputLine?.Invoke(this, e.Line);

        var match = TunDeviceRegex.Match(e.Line);
        if (match.Success)
        {
            var devName = match.Groups[1].Value.Trim();
            if (devName.Length > 0)
            {
                TunDeviceReady?.Invoke(this, devName);
            }
        }
    }

    private void OnExited(object? sender, CoreExited e)
    {
        // 主动停止时 State 已被置为 Idle，这里只上报“意外退出”
        if (State != ConnectionState.Idle)
        {
            SetState(ConnectionState.Idle, "");
            UnexpectedExit?.Invoke(this, e.ExitCode);
        }
    }

    private void SetState(ConnectionState state, string ipv4)
    {
        if (State == state && Ipv4 == ipv4)
        {
            return;
        }

        State = state;
        Ipv4 = ipv4;
        StatusChanged?.Invoke(this, new ConnectionStatusChanged(state, ipv4));
    }

    public ValueTask DisposeAsync()
    {
        _processes.OutputLine -= OnOutputLine;
        _processes.Exited -= OnExited;
        lock (_gate)
        {
            StopLoop();
        }

        return ValueTask.CompletedTask;
    }
}
