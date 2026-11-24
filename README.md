# EasyTier 游戏联机启动器（原生桌面版）

EasyTier 组网内核的 Windows 原生图形启动器。**功能、界面布局与操作逻辑对齐原 Tauri 版
（`EasytierGame/`），但 UI 不再基于 WebView**，改为 C# .NET 8 原生实现。

提供了**两套并行的 UI 实现**，共用同一套 Core 层：

| 版本 | 项目 | 自包含单文件 exe | 说明 |
|---|---|---|---|
| **WPF 版** | `src/EasytierGame.App` | ~63 MB | 完整实现，已验证 |
| **Avalonia 版** | `src/EasytierGame.Avalonia` | ~43.6 MB | 详见 [docs/AVALONIA.md](docs/AVALONIA.md) |

## 技术栈

| 层 | 技术 |
|---|---|
| Core | 纯 C#，无 UI 依赖，可单测（xUnit） |
| UI（WPF） | WPF（.NET 8），MVVM via `CommunityToolkit.Mvvm` |
| UI（Avalonia） | Avalonia 11.2.3（Fluent 主题 + DataGrid） |
| 托盘 | WPF: `Hardcodet.NotifyIcon.Wpf` / Avalonia: 内置 `TrayIcon` |
| 内核 | 外部进程 `easytier-core.exe` / `easytier-cli.exe`（随包分发） |

仅支持 **Windows 10 / 11**（x64）。Win7 支持已弃用。

## 目录结构

```
EasytierGame.Wpf/
├─ EasytierGame.sln
├─ docs/AVALONIA.md                  # Avalonia 版说明与差异
├─ src/
│  ├─ EasytierGame.Core/           # 与 UI 无关的核心逻辑（两版共用，可单测）
│  │  ├─ AppPaths.cs               # exe 同目录的路径约定
│  │  ├─ Args/ArgsBuilder.cs       # 界面配置 → easytier-core CLI 参数
│  │  ├─ Cli/CliProbe.cs           # 调用 easytier-cli 查询成员/代理/路由
│  │  ├─ Config/                   # config.json、分享码、UI 设置持久化、地址校验
│  │  ├─ Json/                     # 宽松 JSON（camelCase、注释、string|数组）
│  │  ├─ Models/                   # AppState / AppConfig / Member…
│  │  ├─ Processes/                # 内核进程生命周期（含孤儿进程回收）
│  │  ├─ Session/                  # 联机状态机
│  │  ├─ SystemIntegration/        # 任务计划自启、防休眠、网卡、防火墙、ping
│  │  └─ Update/CoreUpdater.cs     # GitHub Releases 查询/下载/解压
│  ├─ EasytierGame.App/            # WPF UI
│  │  ├─ ViewModels/  Views/  Services/  Converters/  Styles/
│  └─ EasytierGame.Avalonia/       # Avalonia UI
│     ├─ ViewModels/  Views/  Controls/
│     ├─ Services/                 # 主题、Toast、确认框、托盘、窗口管理、日志
│     └─ Styles/AppStyles.axaml
├─ tests/EasytierGame.Core.Tests/  # xUnit，227 个用例
├─ assets/easytier/                # 内核与工具（随包分发）
├─ publish.ps1                     # WPF 版绿色 zip
└─ publish-avalonia.ps1            # Avalonia 版绿色 zip
```

## 开发

```powershell
# 编译两版 + 单测
dotnet build EasytierGame.sln
dotnet test  EasytierGame.sln

# 分别运行
dotnet run --project src/EasytierGame.App        # WPF 版
dotnet run --project src/EasytierGame.Avalonia   # Avalonia 版
```

首次运行会从输出目录下的 `easytier/` 读取内核；`csproj` 已配置为自动拷贝
`assets/easytier/`，无需手动复制。

> 两版都声明了 `requireAdministrator`（manifest），需要**提权终端**才能调试运行。

## 发布

```powershell
pwsh ./publish.ps1            # WPF 版  -> release/easytier-game_windows_x86_64_<ver>.zip
pwsh ./publish-avalonia.ps1   # Avalonia 版 -> release/easytier-game_avalonia_windows_x86_64_<ver>.zip
```

输出 `release/easytier-game_windows_x86_64_<version>.zip`，自包含运行时（约 60–80 MB），
解压即用、无需安装 .NET。

## 运行时文件布局（与原版一致）

```
easytier-game.exe
ui-settings.json          # 界面设置（替代原版 localStorage）
easytier/
├─ easytier-core.exe
├─ easytier-cli.exe
├─ wintun.dll / WinDivert64.sys / Packet.dll
├─ config.json            # 存在则覆盖界面配置（原版行为保留）
├─ config_template.json   # 带注释的配置模板
├─ config/                # 外部 .toml/.yaml 配置文件
│  └─ .admin/             # “房间配置管理”快照
├─ cache/                 # 内核 zip 缓存
├─ gameList/              # 游戏封面
├─ icons/                 # 托盘图标
├─ logs/                  # 内核日志
├─ guiLogs/               # 应用日志
└─ tool/                  # ForceBindIP / WinIPBroadcast / nssm 等
```

## 与原版的差异

**行为保持一致**：所有 CLI 参数拼装、`config.json` 的键名与写入规则、分享码编码
（`btoa(encodeURIComponent(JSON))`）、开机自启的任务路径与参数、托盘菜单项、窗口尺寸、
关闭窗口=隐藏到托盘、联机成功判定（`cost == "Local"` 的 ipv4）等。

**有意改进**：
- 内核 pid 落盘，崩溃后下次启动自动清理孤儿 `easytier-core.exe`
- 内核下载全程无 `unwrap`/`expect`，失败只提示不崩溃
- 子窗口靠进程内共享状态刷新，删除原版「隐藏监听窗 + localStorage storage 事件 + 650ms `emitTo` 轮询」
- 删除原版死代码：`utils/service.ts`（nssm 服务自启，无引用）、`composables/netcard.ts`（WinIPBroadcast 全注释）、`composables/icon.ts`（依赖未授权的 ResourcesExtract）

**视觉**：布局、字段、信息结构、交互语义沿用；因为离开 CSS，Element Plus 的具体观感（圆角、
阴影、字体度量）按 WPF 方式重做，非像素级还原。提供亮/暗两套主题（对应原版 `theme` 开关）。

## 免责声明

请勿将本程序与仓库代码用于任何违法用途。
