using EasytierGame.Core.Json;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Config;

/// <summary>
/// UI 设置的持久化。替代旧版 WebView 的 <c>localStorage</c>（键 <c>__glj_persisted_main</c>）。
/// <para>
/// 文件位置：<b>exe 同目录</b> 的 <c>ui-settings.json</c> —— 保证“整个文件夹拷走即可迁移”，
/// 且不写注册表（延续原项目取向）。
/// </para>
/// </summary>
public sealed class SettingsStore
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsStore(AppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>设置文件完整路径。</summary>
    public string FilePath => _paths.UiSettingsFile;

    /// <summary>
    /// 加载设置。文件不存在/损坏时返回默认状态（不让应用启动失败，与旧版 localStorage 行为一致）。
    /// </summary>
    public AppState Load()
    {
        try
        {
            if (!File.Exists(_paths.UiSettingsFile))
            {
                return new AppState();
            }

            var json = File.ReadAllText(_paths.UiSettingsFile);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppState();
            }

            if (JsonDefaults.TryDeserialize<AppState>(json, out var state, out _) && state is not null)
            {
                Normalize(state);
                return state;
            }

            return new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    /// <summary>保存设置（原子写入：先写临时文件再替换，避免写一半断电损坏）。</summary>
    public async Task SaveAsync(AppState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = JsonDefaults.Serialize(state);
            var temp = _paths.UiSettingsFile + ".tmp";

            Directory.CreateDirectory(_paths.Root);
            await File.WriteAllTextAsync(temp, json, ct).ConfigureAwait(false);

            if (File.Exists(_paths.UiSettingsFile))
            {
                File.Replace(temp, _paths.UiSettingsFile, null);
            }
            else
            {
                File.Move(temp, _paths.UiSettingsFile);
            }
        }
        catch
        {
            // 写盘失败不阻断 UI；下次修改会再尝试
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 同步保存（退出前使用）。
    /// <para>
    /// 带超时上限：如果另一个保存任务超时后仍持着 <c>_gate</c>，
    /// 这里不能无限等待，否则退出流程会永久卡住。
    /// </para>
    /// </summary>
    public void Save(AppState state, int timeoutMs = 2000)
    {
        try
        {
            var task = SaveAsync(state);
            if (task.Wait(timeoutMs))
            {
                return;
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// 兼容性修正，对应旧版 <c>initGuiJson</c> 之后的若干收敛规则：
    /// KCP 与 QUIC 代理不能同时开启（同时开时关 QUIC）。
    /// </summary>
    private static void Normalize(AppState state)
    {
        state.Config ??= new AppConfig();
        state.Config.ServerUrl ??= new List<string>();
        state.Config.Protocol ??= new List<string>();
        state.BasePeers ??= new List<string>();

        if (state.Config.EnableKcpProxy && state.Config.EnableQuicProxy)
        {
            state.Config.EnableQuicProxy = false;
        }

        // 线程数下限为 2（内核约定），旧配置里可能存着 0
        if (state.Config.MultiThreadCount < 2)
        {
            state.Config.MultiThreadCount = 2;
        }

        // 压缩算法空值时回落到 none
        if (string.IsNullOrWhiteSpace(state.Config.Compression))
        {
            state.Config.Compression = "none";
        }
    }
}
