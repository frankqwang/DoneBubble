# DoneBubble

DoneBubble 是一个 Windows 桌面极简事务记录工具。

它只回答一个问题：**今天我已经处理了多少件事情？**

DoneBubble 不管理待办、截止时间或项目。用户想到“这件事处理完了”，点击桌面气泡，选择一个负荷等级，几秒内完成记录。

## 核心体验

### 点击、分类、完成

桌面常驻气泡显示今天已处理的件数和累计负荷分数。点击后展开一张小卡片：

- **轻 · 1 分**：普通回复、小操作
- **中 · 2 分**：需要思考或沟通
- **重 · 3 分**：复杂决策、事故、长时间排查

分类后立即保存并收起气泡。备注可以不填，也可以在分类前补充一句话；不输入文字也能快速记录。

气泡颜色会随负荷从低饱和度的绿色逐渐过渡到红色。每累计 5 分提醒休息，并显示独立的倒计时浮层；默认休息 10 分钟，可用加减按钮按 5 分钟调整。

### 今天的记录

从气泡卡片打开「今日记录」，可以看到按时间倒序排列的事项、分类和分数。误记录可以直接删除。每条记录是独立的，不会被组织成待办列表。

### 低打扰的窗口行为

- 气泡、分类卡片和休息倒计时始终置顶。
- 今日记录、AI 总结和 AI 调试窗口默认不置顶，可以与其他工作窗口切换。
- 气泡和窗口都支持拖动，位置保存在本机。
- 气泡卡片、今日记录、AI 总结和 AI 调试均支持 `Esc` 关闭；关闭详情只隐藏窗口，不会退出程序。
- 点击窗口关闭按钮或 `Alt+F4` 会隐藏到托盘；只有托盘「退出」才会真正退出。

## 可选：本地 AI 总结

AI 总结默认开启，但不会自动替用户记账。用户选择轻、中、重时，DoneBubble 才会把这一段活动作为一个 session 提交给本地模型，生成简短摘要，并将结果绑定到刚保存的那条记录。

活动 session 会动态采样前台工作：应用和窗口标题、窗口切换轨迹、可访问文本控件的短摘录、程序路径、有效活动时长，以及必要时最多 3 张工作窗口截图。应用或主题切换会结束当前 session；只有持续至少 3 分钟的 session 才会在切换时尝试自动分析，较短的 session 不会单独请求 AI，但仍可在用户主动记录时作为上下文使用。约 90 秒无操作也会清空当前 session。用户选择轻、中、重时，会立即结束并总结当时仍在进行的 session，再把结果绑定到这条记录。不会读取全局按键，也不会联网上传数据。

在「今日记录」中，带有 AI 结果的记录会显示「AI总结」入口。打开后可查看：

- AI 生成的简短摘要
- 当时使用的截图证据（如果有）
- 完整提示词
- 模型原始输出

气泡卡片中的「AI 调试」用于手动验证采集流程，可分别执行单次采集、近 30 秒动态采集或截图分析，并查看上下文、提示词和原始 HTTP 响应。调试结果不会自动入账。

默认使用 LM Studio 的 OpenAI 兼容接口：

```text
http://127.0.0.1:1234/v1/chat/completions
```

模型和接口可在 `%LOCALAPPDATA%\\DoneBubble\\settings.json` 中调整。

## 本地优先

所有事务记录、设置和 AI 日志都保存在：

```text
%LOCALAPPDATA%\\DoneBubble\\
├─ donebubble.db       SQLite 记录库
├─ settings.json       窗口位置、开机启动和 AI 配置
└─ ai-sessions.json    与记录关联的 AI 摘要、提示词和原始输出
```

程序不要求登录，不使用云同步，不把业务数据写入 exe 所在目录。SQLite 数据库会在首次启动时自动初始化，旧版本数据库会自动补充所需字段。

托盘只保留三个基础入口：显示 / 隐藏、开机启动、退出。开机启动使用当前用户的 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`，不需要管理员权限。

## 下载和运行

从 [Releases](https://github.com/frankqwang/DoneBubble/releases) 下载 Windows x64 发布包，放到固定目录后运行 `DoneBubble.exe`。自包含版本不要求系统预装 .NET，也不需要 WSL。

支持 Windows 10 和 Windows 11。当前发布包是未签名的单文件程序，首次运行时 Windows SmartScreen 可能提示无法验证发布者；确认来源后即可运行。

## 开发和发布

项目使用 C#、.NET 8 和 WPF，采用轻量 MVVM；托盘使用 .NET 自带 Windows Forms `NotifyIcon`，SQLite 使用 `Microsoft.Data.Sqlite`。

```text
DoneBubble/
├─ App.xaml / App.xaml.cs           应用生命周期、单实例、托盘
├─ MainWindow.xaml / .xaml.cs       悬浮气泡、分类、拖动和焦点
├─ RestWindow.xaml / .xaml.cs       独立休息倒计时
├─ HistoryWindow.xaml / .xaml.cs    今日记录
├─ AiLogWindow.xaml / .xaml.cs      单条 AI 总结详情
├─ DebugWindow.xaml / .xaml.cs      AI 采集调试
├─ Models/                           记录和设置模型
├─ ViewModels/                       主界面状态
├─ Services/                         数据库、设置、启动、采集和 AI
└─ Properties/PublishProfiles/      Windows 发布配置
```

在 Windows 的 .NET 8 SDK 环境中执行：

```powershell
dotnet build
dotnet run
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布文件位于：

```text
bin\\Release\\net8.0-windows\\win-x64\\publish\\DoneBubble.exe
```

WSL 可以通过 `dotnet.exe` 调用 Windows SDK 执行相同命令，但 WPF 窗口和托盘只能在 Windows 桌面环境中运行。

## 当前边界

- 只展示今天的记录，历史数据仍保留在 SQLite，暂未提供日期浏览、编辑和导出。
- 删除记录立即生效，暂无撤销。
- AI 是辅助总结，不保证识别出完整工作内容，也不会自动把候选计入今日数量。
- 截图和可访问文本可能受目标应用权限、窗口类型和隐私保护限制；无法采集时仍可正常手动记录。
- 发布包当前未签名、没有安装器和自动更新，目标架构为 Windows x64。
