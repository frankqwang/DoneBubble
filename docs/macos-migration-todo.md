# DoneBubble macOS 迁移 TODO

## 目标

为 DoneBubble 增加一个原生 macOS 版本：使用 **SwiftUI + Swift** 实现，保证 macOS 的菜单栏、窗口、权限和系统交互符合 macOS 习惯。

Windows（C# + .NET 8 + WPF）和 macOS 不做跨平台 UI，也不互相依赖。两端分别构建、测试和发布；产品逻辑保持一致，但允许平台交互有合理差异。

## 产品范围

macOS 第一版需要支持：

- 菜单栏常驻图标和悬浮气泡
- 今日已处理件数、轻 / 中 / 重负荷分数
- 点击气泡后快速选择分类
- 可选备注；不输入文字也能保存
- 每累计 5 分提醒休息，默认 10 分钟倒计时
- 倒计时加减 5 分钟
- 保存气泡和休息窗口位置
- 今日记录列表、删除误记录
- 记录详情中的 AI 摘要、截图、提示词和原始输出
- 本地 SQLite 数据
- 本地 AI 默认开启，可关闭
- AI 失败不影响手动记录
- 登录项（Login Item）开机启动
- 所有数据保存在用户本地目录，不登录、不云同步

暂不做：任务管理、截止时间、项目、账号、云同步、番茄钟、AI 自动记账、复杂统计。

## 推荐项目结构

创建独立的 Xcode macOS App，不修改 Windows 项目结构：

```text
DoneBubble/
├─ DoneBubble/                 SwiftUI macOS App
│  ├─ DoneBubbleApp.swift
│  ├─ AppState.swift
│  ├─ Models/
│  ├─ Views/
│  ├─ ViewModels/
│  ├─ Services/
│  └─ Resources/
├─ DoneBubbleTests/
└─ docs/
```

优先使用 Apple 系统框架：SwiftUI、AppKit、ScreenCaptureKit、Accessibility API、SQLite（系统库或轻量封装）、ServiceManagement。

## 实施顺序

### 1. 工程和基础状态

- [ ] 创建 macOS 14+ SwiftUI App，确认最低系统版本。
- [ ] 配置 Debug / Release / Archive 构建。
- [ ] 建立 `AppState`，统一管理今日数量、分数、窗口显示状态和休息状态。
- [ ] 定义 `RecordItem`、`ActivitySession`、`AiSummaryLog` 数据模型。
- [ ] 增加统一日期和本地时区处理。

验收：空数据库可以启动，主线程无阻塞，关闭窗口不会退出菜单栏进程。

### 2. 菜单栏和悬浮气泡

- [ ] 使用 `MenuBarExtra` 或 `NSStatusItem` 创建菜单栏入口。
- [ ] 悬浮气泡使用无边框 `NSPanel`，透明背景、圆角、阴影、始终置顶。
- [ ] 气泡显示今日件数和分数，颜色随分数从低饱和绿过渡到红色。
- [ ] 点击气泡展开分类卡片；点击外部、`Esc` 或关闭按钮收起卡片。
- [ ] 支持拖动气泡，保存位置；多显示器变化后自动修正到可见区域。
- [ ] 关闭主窗口只隐藏，菜单栏菜单提供显示 / 隐藏和退出。

验收：点击气泡 → 选择轻 / 中 / 重 → 立即保存并收起，整个流程不需要确认。

### 3. 休息倒计时

- [ ] 每累计 5 分只提醒一次，跨过多个门槛时按当前分数处理。
- [ ] 创建独立置顶休息浮层，默认 10:00，显示大号倒计时。
- [ ] `− 5 分钟` / `+ 5 分钟` 按钮，倒计时期间不抢焦点。
- [ ] 保存结束时间和窗口位置，重启后恢复剩余时间。
- [ ] 到期收起并通过 macOS 通知提醒。

### 4. SQLite 和本地设置

- [ ] 数据目录使用 `Application Support/DoneBubble/`。
- [ ] 建立 `Records` 表：`Id, Content, Category, CreatedAt`。
- [ ] 建立 AI 日志表或本地 JSON 文件，记录 `RecordId, Summary, Prompt, RawResponse, Frames, Error`。
- [ ] 首次启动自动建库；数据库错误显示可理解的错误，不让进程退出。
- [ ] 设置保存气泡位置、休息位置、开机启动、AI 开关、AI endpoint、model。

验收：应用卸载或移动后，用户数据仍位于 Application Support，不写入 App bundle。

### 5. 今日记录与 AI 详情

- [ ] 今日页面采用单窗口布局：记录列表 + 选中记录详情。
- [ ] 小窗口显示上下布局，大窗口显示左右布局；根据窗口尺寸自动切换。
- [ ] 记录按时间倒序，删除前可直接执行，不加入复杂编辑流程。
- [ ] 选中记录显示 AI 摘要、截图、提示词和格式化后的 JSON 原始输出。
- [ ] 详情支持 `Esc` 关闭，窗口可调整大小，统一深色滚动条样式。
- [ ] 记录变化自动刷新，不提供手动刷新按钮。

### 6. 活动采集和 session

- [ ] 使用 Accessibility API 获取前台应用、窗口标题、焦点控件和可访问文本短摘录。
- [ ] 使用 ScreenCaptureKit 获取完整桌面截图；首次使用前引导用户授予屏幕录制权限。
- [ ] 不读取全局按键；密码和敏感控件只保留必要元信息。
- [ ] 动态采样：短 session 频率高，长 session 频率降低。
- [ ] 应用 / 主题切换结束当前 session；持续至少 3 分钟才尝试自动分析；约 90 秒无操作清空 session。
- [ ] 日志保存全部截图；发送视觉模型前缩放最长边约 1280 像素、压缩 JPEG，最多发送 3 张代表帧。

### 7. 本地 AI

- [ ] 默认开启 AI 总结，允许用户在气泡卡片关闭。
- [ ] 记录先保存，AI 在后台执行；AI 超时或失败不影响记录。
- [ ] 使用 OpenAI 兼容接口，默认 `http://127.0.0.1:1234/v1/chat/completions`。
- [ ] 支持文本上下文和多模态截图请求。
- [ ] 请求超时、HTTP 错误、解析失败都写入 AI 日志。
- [ ] 解析 `done / summary / category / confidence`，AI 不直接修改用户分类。
- [ ] 原始 JSON 只做缩进和中文可读化，不做复杂语法高亮。

### 8. 权限和开机启动

- [ ] 首次采集前说明并申请 Screen Recording 权限。
- [ ] 需要文本和窗口信息时申请 Accessibility 权限，并提供打开系统设置的按钮。
- [ ] 权限不足时仍可正常手动记录。
- [ ] 使用 `SMAppService` 注册 Login Item；提供启用 / 关闭状态。
- [ ] 权限和开机启动状态持久化，错误信息可读。

### 9. 测试和发布

- [ ] 单元测试：分数、日期边界、session 分割、JSON 解析、失败回退。
- [ ] UI 测试：气泡记录、Esc、删除、详情切换、窗口缩放。
- [ ] 在单显示器和多显示器测试坐标修正。
- [ ] 测试无屏幕录制权限、无辅助功能权限、AI 服务未启动、AI 超时。
- [ ] 测试 MacBook 睡眠和唤醒后的倒计时恢复。
- [ ] 配置 Developer ID 签名和 notarization，避免“无法验证开发者”提示。
- [ ] 输出 `.app`、`.dmg` 或 `.zip`，在 README 增加 macOS 下载说明。

## 完成标准

- 用户不输入文字也能在 3 秒内完成一次分类记录。
- 手动记录不依赖 AI、网络或任何系统权限。
- AI 请求最长等待时间有限，失败会留下可检查的日志。
- 记录、截图和设置只保存在本机。
- 菜单栏、气泡、休息浮层和详情页符合 macOS 原生交互习惯。
- Release 包可在干净的 macOS 机器上安装运行，并通过签名和 notarization 检查。
