namespace EasytierGame.Core.Models;

/// <summary>
/// 应用全量状态，对应 Vue 版 Pinia store 的 <c>state()</c>。
/// 只保留旧版 <c>persist.pick</c> 白名单里的字段（<c>winipBcPid</c>/<c>winipBcStart</c> 从未持久化，
/// 这里仍保留以兼容“房间配置管理”导出/导入的全量快照）。
/// </summary>
public sealed class AppState
{
    public AppConfig Config { get; set; } = new();

    /// <summary>是否启用子网代理。</summary>
    public bool CidrEnable { get; set; }

    /// <summary>通过系统内核转发子网代理数据包，禁用内置 NAT。</summary>
    public bool ProxyForwardBySystem { get; set; }

    /// <summary>
    /// 服务器地址候选池（下拉里的历史节点）。
    /// <para>
    /// 默认为空：不再内置公共服务器，由用户自行添加。
    /// 已保存的旧配置不受影响（仍会从 ui-settings.json 加载）。
    /// </para>
    /// </summary>
    public List<string> BasePeers { get; set; } = new();

    /// <summary>false = 亮色，true = 暗色。</summary>
    public bool Theme { get; set; }

    /// <summary>使用外部配置文件启动。</summary>
    public bool ConfigStartEnable { get; set; }

    /// <summary>外部配置文件相对路径（相对资源根目录）。</summary>
    public string ConfigPath { get; set; } = "";

    public bool WinIpBcAutoStart { get; set; } = true;

    /// <summary>是否在 easytier 目录生成 config.json。</summary>
    public bool CreateConfigInEasytier { get; set; }

    /// <summary>GitHub 下载加速前缀。</summary>
    public string GithubFastUrl { get; set; } = "https://ghfast.top/";

    // ---- 运行时字段（旧版不持久化）----
    public int WinipBcPid { get; set; }

    public bool WinipBcStart { get; set; }

    /// <summary>EasytierGame 自身最新版本号。</summary>
    public string LatestTagName { get; set; } = "";
}
