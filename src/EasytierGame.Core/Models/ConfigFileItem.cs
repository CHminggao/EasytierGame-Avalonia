namespace EasytierGame.Core.Models;

/// <summary>
/// 配置文件启动对话框中的候选项（来自 <c>easytier/config/</c> 下的 .toml / .yaml）。
/// </summary>
public sealed class ConfigFileItem
{
    /// <summary>文件名，例如 <c>my_net.toml</c>。</summary>
    public string Name { get; set; } = "";

    /// <summary>相对资源根目录的路径，例如 <c>easytier/config/my_net.toml</c>。</summary>
    public string Path { get; set; } = "";
}

/// <summary>
/// GitHub Release 中与内核切换相关的条目。
/// 对应 Rust 侧 <c>fetch_easytier_list() -> Vec<Vec<[String;4]>></c> 的每个元素：
/// [tag_name, asset_name, browser_download_url, prerelease]。
/// </summary>
public sealed class CoreReleaseAsset
{
    public string TagName { get; set; } = "";

    public string AssetName { get; set; } = "";

    public string DownloadUrl { get; set; } = "";

    public bool Prerelease { get; set; }
}
