session-id: 20261124-2357

## [23:57] - [项目启动]: 按 v4.1 定稿提示词生成第一版 WPF 工程

- **文件**: 新建工程根目录 xiaogouyingyu/（src、data、.github、scripts、tools、docs、.ai-memory）
- **决策**: 需求已由 515 行规格完整锁定，直接进入执行；所有写入严格限定工作区内，不碰桌面两份提示词文件
- **验证**: 探查确认工作区原为空、本机无 dotnet SDK，验证走静态审查 + 披露未编译限制

## [00:05] - [代码生成]: 完成配置/模型/数据/服务/UI/打包/文档全工程

- **文件**: 约 55 个文件（详见 README 文件树）；src 约 4300 行 C#/XAML
- **决策**:
  - 命名空间 XiaoGouYingYu.Config 与类名 Config 冲突 → 类改名 AppConfig（文件名仍 Config.cs），全量统一引用
  - 配置类只放全局出厂参数；词条/音节/进度/设置全部独立数据文件（md/json），不硬编码进 Config
  - AppId 固定 GUID 3E702BA5-0A7E-49E0-B613-F3D2C56675C0，写入 setup.iss 顶部，跨版本不改
  - 版本号唯一真源 csproj <Version>1.0.0</Version>
- **验证**: 词库统计 129 条 / U1-8=13·11·17·9·18·12·31·18 / 单词112·短语17 / 括号5 / 同形4组 / 内置音节112 全部与规格一致；JSON/XML 语法全通过

## [00:12] - [Bug修复]: 独立静态审查发现并修复 3 处缺陷

- **文件**: MoreWindow.xaml.cs（补 using XiaoGouYingYu.Services）、App.xaml.cs + csproj（注册 GBK CodePages 提供程序 + 包引用）、ImportPreviewWindow.xaml.cs + MoreWindow.xaml.cs（GBK 转码源路径修正 + 原地刷新预览）、MainViewModel.cs（填充年级/单元列表初始化 bug + null 保护）
- **决策**: 用 Explore 子代理做独立只读编译级审查，不自行改码；主 Agent 复核后落码
- **验证**: 修复后复查引用一致性通过；XAML x:Class 与 code-behind 全匹配

## [00:15] - [复盘]: 交付前自检与记忆沉淀

- **文件**: README.md（12 节维护文档）、docs 两份家长说明、.ai-memory/project_memory.md、handoff.md
- **决策**: 本机无 .NET SDK，未做实际编译；27 条自批驳逐条核对，静态层面全部满足
- **验证**: JSON 3/3、XML 7/7 语法通过；C# 大括号平衡（脚本对 URL // 误报已排除）
- **遗留**: 需用户在具备 .NET 8 SDK 环境 `dotnet build` 与 GitHub Actions 云端出包做最终编译验证；TTS 最小验证需在 Windows 实机跑 SpeechProbe
