using System.Diagnostics;
using System.Text;

namespace EasytierGame.Core.Processes;

/// <summary>
/// 管理 <c>easytier-core.exe</c> 的生命周期（只管理联机客户端进程）。
/// 等价于旧版 Rust <c>run_command</c> / <c>stop_command</c>，但修掉两个原实现缺陷：
/// <list type="number">
/// <item>pid 写入 <c>easytier/.client.pid</c>，应用崩溃后下次启动可回收孤儿进程；</item>
/// <item>停止时先尝试优雅关闭，超时再强杀进程树，且所有等待都带超时，不会卡住调用方。</item>
/// </list>
/// 本类只做进程，不含任何 UI 依赖。
/// </summary>
public sealed class CoreProcessManager : IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();
    private RunningCore? _running;

    public CoreProcessManager(AppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>收到一行标准输出（stderr 也合并进来，避免丢日志）。</summary>
    public event EventHandler<CoreOutputLine>? OutputLine;

    /// <summary>进程退出。</summary>
    public event EventHandler<CoreExited>? Exited;

    /// <summary>进程启动后拿到 pid。</summary>
    public event EventHandler<int>? Started;

    /// <summary>内核是否在运行。</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running is not null && !_running.Process.HasExited;
            }
        }
    }

    /// <summary>当前 pid，不在运行时为 0。</summary>
    public int Pid
    {
        get
        {
            lock (_gate)
            {
                return _running is not null && !_running.Process.HasExited ? _running.Process.Id : 0;
            }
        }
    }

    /// <summary>
    /// 启动内核。参数为空时不启动并返回 null。
    /// </summary>
    /// <exception cref="FileNotFoundException">找不到 easytier-core.exe。</exception>
    public async Task<int?> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            return null;
        }

        var exe = _paths.CoreExe;
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException("找不到 easytier-core.exe", exe);
        }

        // 重复启动：先停掉旧的
        await StopAsync().ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = _paths.Root,       // 旧版以当前 exe 目录为工作目录
            UseShellExecute = false,
            CreateNoWindow = true,                // 对应 creation_flags 0x08000000
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Emit(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Emit(e.Data);
            }
        };
        process.Exited += (_, _) => OnProcessExited(process);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        lock (_gate)
        {
            _running = new RunningCore(process);
        }

        WritePidFile(process.Id);
        Started?.Invoke(this, process.Id);

        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        return process.Id;
    }

    /// <summary>
    /// 停止内核。先 <c>CloseMainWindow</c>，超时后 <c>Kill(entireProcessTree)</c>。
    /// 每一处等待都有超时，保证不会无限阻塞。
    /// </summary>
    public async Task StopAsync(int gracefulTimeoutMs = 500, int killTimeoutMs = 3000)
    {
        RunningCore? core;
        lock (_gate)
        {
            core = _running;
            _running = null;
        }

        DeletePidFile();

        if (core is null)
        {
            return;
        }

        var process = core.Process;
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                    using var timeout = new CancellationTokenSource(gracefulTimeoutMs);
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 优雅关闭超时，走强杀
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);

                    // 强杀后仍给一个上限等待，避免因句柄未释放而卡死
                    using var killTimeout = new CancellationTokenSource(killTimeoutMs);
                    try
                    {
                        await process.WaitForExitAsync(killTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 已发出强杀指令，不再等待
                    }
                }
            }
        }
        catch
        {
            // 进程已消失或权限不足，忽略
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// 清理上次运行残留的孤儿进程（应用崩溃/被强杀后 pid 文件仍在）。
    /// 应在启动时、UI 就绪前调用一次。
    /// </summary>
    public void ReapOrphans()
    {
        var pidFile = _paths.ClientPidFile;
        try
        {
            if (!File.Exists(pidFile))
            {
                return;
            }

            var text = File.ReadAllText(pidFile).Trim();
            if (int.TryParse(text, out var pid) && pid > 0)
            {
                try
                {
                    using var orphan = System.Diagnostics.Process.GetProcessById(pid);
                    // 只杀我们自己拉起的同名进程，避免误杀 pid 复用后的无关进程
                    if (string.Equals(orphan.ProcessName, "easytier-core", StringComparison.OrdinalIgnoreCase))
                    {
                        orphan.Kill(entireProcessTree: true);
                    }
                }
                catch (ArgumentException)
                {
                    // 进程已不存在
                }
                catch
                {
                    // 权限等原因无法结束，留给用户手动处理
                }
            }
        }
        catch
        {
            // pid 文件不可读，忽略
        }
        finally
        {
            DeletePidFile();
        }
    }

    private void OnProcessExited(System.Diagnostics.Process process)
    {
        var exitCode = -1;
        try
        {
            exitCode = process.ExitCode;
        }
        catch
        {
            // 进程已释放，忽略
        }

        lock (_gate)
        {
            if (_running is not null && ReferenceEquals(_running.Process, process))
            {
                _running = null;
            }
        }

        DeletePidFile();
        Exited?.Invoke(this, new CoreExited(exitCode));
    }

    private void Emit(string line) => OutputLine?.Invoke(this, new CoreOutputLine(line));

    private void WritePidFile(int pid)
    {
        try
        {
            Directory.CreateDirectory(_paths.EasytierDir);
            File.WriteAllText(_paths.ClientPidFile, pid.ToString());
        }
        catch
        {
            // 写 pid 失败不影响运行
        }
    }

    private void DeletePidFile()
    {
        try
        {
            var file = _paths.ClientPidFile;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        lock (_gate)
        {
            try
            {
                _running?.Process.Dispose();
            }
            catch
            {
                // ignore
            }

            _running = null;
        }
    }

    private sealed record RunningCore(System.Diagnostics.Process Process);
}
