using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using EasytierGame.Core.Json;
using EasytierGame.Core.Models;

namespace EasytierGame.Core.Update;

/// <summary>GitHub Release 的 asset。</summary>
public sealed class GitHubAsset
{
    public string Name { get; set; } = "";

    public string BrowserDownloadUrl { get; set; } = "";
}

/// <summary>GitHub Release。</summary>
public sealed class GitHubRelease
{
    public string TagName { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Prerelease { get; set; }

    public long Id { get; set; }

    public List<GitHubAsset> Assets { get; set; } = new();
}

/// <summary>
/// 内核（easytier-core）版本查询、下载、解压。
/// 等价旧版 Rust <c>fetch_releases</c> / <c>fetch_easytier_list</c> / <c>download_easytier_zip</c> / <c>unzip</c>，
/// 但修掉两个原实现缺陷：
/// <list type="number">
/// <item>不再用 <c>unwrap()/expect()</c>，下载失败只是返回 false 而不 panic；</item>
/// <item>解压后清理临时 zip 与缓存目录判断更稳。</item>
/// </list>
/// </summary>
public sealed class CoreUpdater
{
    private const string ReleasesApi = "https://api.github.com/repos/easytier/easytier/releases";
    private const string WindowsAssetKeyword = "windows-x86_64";

    private static readonly string[] UserAgents =
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0",
    };

    private readonly AppPaths _paths;
    private readonly HttpClient _http;

    public CoreUpdater(AppPaths paths, HttpClient? http = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>下载进度回调：(已下载字节, 总字节)。总字节未知时为 0。</summary>
    public event EventHandler<(long Received, long Total)>? Progress;

    /// <summary>获取内核 Release 列表（只保留包含 windows-x86_64 资源的条目）。</summary>
    public async Task<List<CoreReleaseAsset>> GetReleasesAsync(bool includePrerelease = false, CancellationToken ct = default)
    {
        var result = new List<CoreReleaseAsset>();

        var releases = await FetchReleasesAsync(ct).ConfigureAwait(false);
        if (releases is null)
        {
            return result;
        }

        foreach (var release in releases)
        {
            foreach (var asset in release.Assets)
            {
                if (!asset.Name.Contains(WindowsAssetKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 旧前端过滤掉名字含 gui 的资源，且默认不含 prerelease
                if (asset.Name.Contains("gui", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (release.Prerelease && !includePrerelease)
                {
                    continue;
                }

                result.Add(new CoreReleaseAsset
                {
                    TagName = release.TagName,
                    AssetName = asset.Name,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    Prerelease = release.Prerelease,
                });
            }
        }

        return result;
    }

    /// <summary>拉取 GitHub Releases（失败返回 null）。</summary>
    public async Task<List<GitHubRelease>?> FetchReleasesAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
            request.Headers.UserAgent.ParseAdd(UserAgents[Random.Shared.Next(UserAgents.Length)]);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonDefaults.Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 下载并安装内核。命中 <c>easytier/cache/&lt;fileName&gt;</c> 时直接解压，不重复下载。
    /// </summary>
    /// <returns>成功返回 true。</returns>
    public async Task<bool> DownloadAndInstallAsync(string downloadUrl, string fileName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        Directory.CreateDirectory(_paths.CacheDir);
        var cacheFile = Path.Combine(_paths.CacheDir, fileName);

        if (File.Exists(cacheFile))
        {
            return await Task.Run(() => Extract(cacheFile), ct).ConfigureAwait(false);
        }

        var tempFile = Path.Combine(_paths.EasytierDir, fileName);

        try
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            using var response = await _http
                .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var total = response.Content.Headers.ContentLength ?? 0;
            long received = 0;

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    received += read;
                    Progress?.Invoke(this, (received, total));
                }
            }

            var extracted = await Task.Run(() => Extract(tempFile), ct).ConfigureAwait(false);
            if (!extracted)
            {
                return false;
            }

            // 安装成功后把 zip 转存到 cache，供下次切换复用
            try
            {
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }

                File.Move(tempFile, cacheFile);
            }
            catch
            {
                // 转存失败不影响内核已就位
                TryDelete(tempFile);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempFile);
            throw;
        }
        catch
        {
            TryDelete(tempFile);
            return false;
        }
    }

    /// <summary>
    /// 解压 zip 到 <c>easytier/</c>。旧版 <c>unzip</c> 只取文件名（忽略目录层级），这里保持一致，
    /// 因为官方包是平铺的 <c>easytier-core.exe</c> + <c>easytier-cli.exe</c>。
    /// </summary>
    public bool Extract(string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                return false;
            }

            Directory.CreateDirectory(_paths.EasytierDir);

            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // 目录项
                }

                var target = Path.Combine(_paths.EasytierDir, entry.Name);
                entry.ExtractToFile(target, overwrite: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取当前内核版本（<c>easytier-core --version</c> 的输出，去掉前缀）。</summary>
    public async Task<string> GetCoreVersionAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_paths.CoreExe))
        {
            return "";
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _paths.CoreExe,
                ArgumentList = { "--version" },
                WorkingDirectory = _paths.Root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                return "";
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            }

            return output.Trim().Replace("easytier-core ", "", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 版本号比较（旧版 <c>versionWeight</c> 规则：major*10000 + minor*100 + patch）。
    /// 返回负数表示 current 更旧。
    /// </summary>
    public static int VersionDifference(string? current, string? latest)
    {
        static int Weight(string? version)
        {
            var text = (version ?? "").Trim().TrimStart('v', 'V');
            var parts = text.Split('.');
            var major = ParsePart(parts, 0);
            var minor = ParsePart(parts, 1);
            var patch = ParsePart(parts, 2);
            return (major * 10000) + (minor * 100) + patch;
        }

        return Weight(current) - Weight(latest);
    }

    private static int ParsePart(string[] parts, int index)
    {
        if (index >= parts.Length)
        {
            return 0;
        }

        var digits = new string(parts[index].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}
