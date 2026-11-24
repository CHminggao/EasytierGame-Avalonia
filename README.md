# EasyTier 游戏联机启动器（Avalonia 版）

EasyTier 组网内核的 Windows 原生图形启动器。UI 基于 **Avalonia 11**，
**不依赖 WebView**，也不依赖 .NET 运行时安装（自包含发布、解压即用）。

功能、界面布局与操作逻辑对齐原 Tauri 版（`EasytierGame/`）。

## 技术栈

| 层 | 技术 |
|---|---|
| UI | Avalonia 11.2.3（Fluent 主题 + `Avalonia.Controls.DataGrid`），MVVM via `CommunityToolkit.Mvvm` |
| Core | 纯 C#，无 UI 依赖，可单测（xUnit） |
| 托盘 | Avalonia 内置 `TrayIcon`（Windows 后端为 Shell_NotifyIcon） |
| 内核 | 外部进程 `easytier-core.exe` / `easytier-cli.exe`（随包分发） |

- 目标框架：`net8.0-windows`（Core 使用 Win32 P/Invoke，且内核为 Windows 可执行文件）
- 系统要求：**Windows 10 / 11（x64）**，Win7 不支持
- 程序通过 manifest 声明 `requireAdministrator`，启动时会请求管理员权限
  （防火墙开关、网卡跃点、内核替换都需要提权）

## 目录结构

```
EasytierGame.Wpf/
├─ EasytierGame.sln
├─ src/
│  ├─ EasytierGame.Core/           # 与 UI 无关的核心逻辑（可单测）
│  │  ├─ AppPaths.cs               # exe 同目录的路径约定
│  │  ├─ Args/ArgsBuilder.cs       # 界面配置 → easytier-core CLI 参数
│  │  ├─ Cli/CliProbe.cs           # 调用 easytier-cli 查询成员/代理/路由
│  │  ├─ Cli/MemberDisplay.cs      # 成员列表展示映射（NAT1-4、Local→本机）
│  │  ├─ Config/                   # config.json、分享码、UI 设置持久化、地址校验
│  │  ├─ Json/                     # 宽松 JSON（camelCase、注释、string|数组）
│  │  ├─ Models/                   # AppState / AppConfig / Member…
│  │  ├─ Processes/                # 内核进程生命周期（含孤儿进程回收）
│  │  ├─ Session/                  # 联机状态机
│  │  ├─ SystemIntegration/        # 任务计划自启、防休眠、网卡、防火墙、ping
│  │  ├─ Update/CoreUpdater.cs     # GitHub Releases 查询/下载/解压
│  │  └─ Utils/                    # 文件名合法性等
│  └─ EasytierGame.Avalonia/       # Avalonia UI（根命名空间 EasytierGame.Desktop）
│     ├─ Controls/                 # 服务器地址输入组件（带协议校验）
│     ├─ Converters/
│     ├─ Services/                 # 主题、Toast、确认框、托盘、窗口管理、日志
│     ├─ Styles/AppStyles.axaml
│     ├─ ViewModels/
│     └─ Views/                    # 14 个窗口/对话框
├─ tests/EasytierGame.Core.Tests/  # Core 层 xUnit 单测
├─ assets/easytier/                # 内核与工具（随包分发）
└─ publish-avalonia.ps1            # 打出绿色 zip
```

> 注意：项目根目录名为 `EasytierGame.Wpf` 是历史遗留（早期为 WPF 实现），
> 代码已全部为 Avalonia。

## 开发

```powershell
dotnet build EasytierGame.sln
dotnet test  EasytierGame.sln
dotnet run --project src/EasytierGame.Avalonia
```

首次运行会从输出目录下的 `easytier/` 读取内核；`csproj` 已配置为自动拷贝
`assets/easytier/`，无需手动复制。

> manifest 声明了 `requireAdministrator`，调试运行时需要**提权终端**
> （否则会因 UAC 无法启动）。

### 排查问题

Avalonia 默认不会把界面事件里的异常写到 stderr。程序会把未处理异常与
Avalonia 自身的 Warning 级以上日志写到：

```
easytier/guiLogs/avalonia.log
```

## 发布

### 本地打包

```powershell
pwsh ./publish-avalonia.ps1
```

输出 `release/easytier-game_avalonia_windows_x86_64_<version>.zip`：

- 自包含单文件，**解压即用**，用户无需安装 .NET 运行时
- 单文件 exe 约 **43.6 MB**，整包 zip 约 **53 MB**
- 打包前会强校验 `easytier-core.exe` / `easytier-cli.exe` / `wintun.dll` /
  `config_template.json` 是否齐全，缺失即构建失败（避免静默产出跑不起来的包）

### 打标签自动发布（GitHub Actions）

`.github/workflows/release.yml`：**推送标签后自动编译、跑测试、创建 GitHub Release**。

```bash
git tag v1.5.6
git push origin v1.5.6
```

流程：

1. Windows runner 上 `dotnet restore / build / test`（**测试不通过则不发布**）
2. 调 `publish-avalonia.ps1` 打包绿色 zip，版本号取自标签（`v1.5.6` → `1.5.6`）
3. 先上传 zip 为构建产物（即使发布失败也能拿到），再创建 Release 并附加 zip

细节：

- 触发标签：`v*` 或纯数字开头（`1.5.6` 均可）
- 也支持在 Actions 页面 **手动触发**（可指定版本号与是否预发布）
- 同一标签不会并发重复运行
- 创建 Release 需要 `contents: write` 权限（已在 workflow 内声明）

## 运行时文件布局

```
easytier-game.exe
ui-settings.json          # 界面设置
easytier/
├─ easytier-core.exe
├─ easytier-cli.exe
├─ wintun.dll / WinDivert64.sys / Packet.dll
├─ config.json            # 存在则覆盖界面配置
├─ config_template.json   # 带注释的配置模板
├─ config/                # 外部 .toml/.yaml 配置文件
│  └─ .admin/             # “房间配置管理”快照
├─ cache/                 # 内核 zip 缓存
├─ icons/                 # 托盘图标
├─ logs/                  # 内核日志
├─ guiLogs/               # 应用日志（含 avalonia.log）
└─ tool/                  # 随包工具
```

## 功能

**联机**

- 服务器地址：支持多个，带协议头（`tcp://`、`udp://` 等）静态校验，不符合协议规范的地址直接拒绝
- 房间名 / 房间密码 / 成员名（必填）/ 虚拟 IPv4（动态或固定）/ IPv6 双栈
- 一键启动、停止、重新联机；状态标签按状态变色（联机成功为绿底白字）

**高级选项**

- 50+ 内核开关：P2P 协议优先、UDP 广播中继、KCP/QUIC 代理、端口白名单、自定义监听、
  端口转发、网卡名与跃点、smoltcp、出口节点、加密开关、多线程与线程数、压缩算法、
  UPnP、公网 IPv6、私有模式、Relay 数据转发等
- 多线程数仅在勾选「开启多线程」时才传给内核（默认 2，内核约定的最小值）

**其他窗口**

- 成员列表：成员名、方式（本机/P2P/Relay）、虚拟 IP、延迟、丢包、NAT 类型（映射为 NAT1-4）、
  版本、隧道协议、代理状态、收发量、连接地址；支持横向滚动
- 联机日志：按关键字着色、自动滚底、复制
- 子网代理：CIDR 配置 + 路由表
- 自建服务器：端口、白名单、私有模式
- 内核管理：从 GitHub Releases 查看/下载/切换内核，支持加速地址与本地缓存
- 配置分享 / 导入、房间配置管理、外部配置文件启动、IPv6 编辑
- 增强工具：网卡跃点查询与修改、防火墙状态与一键关闭、Ping 测试

**系统集成**

- 托盘：左键显隐、右键菜单（显示/隐藏、联机/断开、重新联机、主题切换、退出）
- 关闭窗口 = 收进托盘，只有托盘「退出」才结束进程
- 开机自启（Windows 任务计划，`--task-auto-start` 启动时隐藏窗口）
- 防止系统休眠
- 单实例运行

## 与原 Tauri 版的差异

**行为保持一致**：CLI 参数拼装、`config.json` 的键名与写入规则、分享码编码
（`btoa(encodeURIComponent(JSON))`）、开机自启的任务路径与参数、托盘菜单项、窗口尺寸、
关闭窗口=收进托盘、联机成功判定（`cost == "Local"` 的 ipv4）等。

**有意改进**

- 内核 pid 落盘，崩溃后下次启动自动清理孤儿 `easytier-core.exe`
- 内核下载失败只提示不崩溃（原实现会 panic）
- 子窗口靠进程内共享状态刷新，去掉了原版「隐藏监听窗 + localStorage storage 事件 + 650ms 轮询」
- 所有模态框改为异步，避免 UI 线程同步等待导致的卡死
- 删除原版死代码：`utils/service.ts`、`composables/netcard.ts`、`composables/icon.ts`

**视觉**：布局、字段、信息结构、交互语义沿用原版；因离开 CSS，Element Plus 的具体观感
（圆角、阴影、字体度量）按 Avalonia Fluent 主题重做，非像素级还原。支持亮 / 暗主题切换。

## 免责声明

请勿将本程序与仓库代码用于任何违法用途。由此产生的一切后果，仓库所有者与参与开发的人员不承担任何责任。
