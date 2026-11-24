namespace EasytierGame.Core.Args;

/// <summary>参数构建结果状态。用于让 UI 决定弹什么提示、要不要打开某个弹窗。</summary>
public enum ArgsBuildStatus
{
    /// <summary>参数可用。</summary>
    Ok,

    /// <summary>启用了“配置文件启动”但没选文件 → 旧版提示“配置文件不存在 请选择”并打开选择弹窗。</summary>
    ConfigPathMissing,

    /// <summary>选了配置文件但文件不存在 → 旧版清空 configPath、提示“配置文件不存在 请重新选择”。</summary>
    ConfigFileNotFound,

    /// <summary>没选服务器 → “请至少选择一个服务器”。</summary>
    NoServer,

    /// <summary>没勾协议且地址都不带协议前缀 → “请至少选择一个协议”。</summary>
    NoProtocol,

    /// <summary>没填成员名 → 启动联机前必须填写。</summary>
    NoHostname,
}

/// <summary>客户端参数构建的上下文（把环境依赖从参数逻辑里抽出来，方便测试）。</summary>
public sealed class ClientArgsContext
{
    /// <summary>判断“相对资源根目录的配置文件路径”是否存在。</summary>
    public Func<string, bool> ConfigFileExists { get; init; } = _ => true;

    /// <summary>把相对路径转成传给内核绝对路径（<c>-c</c> 需要绝对路径）。</summary>
    public Func<string, string> ResolveConfigPath { get; init; } = p => p;

    /// <summary>日志目录 —— 对应旧版 <c>import.meta.env.VITE_LOG_PATH</c>（"easytier/logs/"）。</summary>
    public string LogDirectory { get; init; } = "easytier/logs/";
}

/// <summary>参数构建结果。</summary>
public sealed class ArgsBuildResult
{
    public ArgsBuildStatus Status { get; init; } = ArgsBuildStatus.Ok;

    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();

    /// <summary>给用户看的提示文案（与旧版 <c>ElMessage</c> 文案一致），<see cref="ArgsBuildStatus.Ok"/> 时为 null。</summary>
    public string? Message { get; init; }

    /// <summary>
    /// 旧版会把清洗后的子网代理文本写回 store（去掉空行/首尾空格，保留全部行）。
    /// 非 null 时由调用方回写到 <c>AppConfig.ProxyNetworks</c>。
    /// </summary>
    public string? NormalizedProxyNetworks { get; init; }

    /// <summary>真正传给内核的参数（不是错误提示），用于判断是否可以启动。</summary>
    public bool IsRunnable => Status == ArgsBuildStatus.Ok && Args.Count > 0;

    /// <summary>是否走“外部配置文件”模式（<c>-c &lt;path&gt;</c>）。</summary>
    public bool IsConfigFileMode => Args.Count > 1 && Args[0] == "-c";
}
