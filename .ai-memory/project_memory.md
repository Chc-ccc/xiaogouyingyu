# 项目长期记忆 · 小狗英语（xiaogouyingyu）

> 本文件是工程交接的权威记忆，供后续模型增量更新时读取。术语定义见 Glossary。

## 项目概况
- 目标：8 岁孩子（译林三上）本机离线英语单词学习软件，C# WPF / net8.0-windows。
- 交付形态：GitHub Actions 云端编译 self-contained 单文件 exe → Inno Setup 打包 per-user 免 UAC 的 Setup.exe。
- 用户无本地开发环境，只做三步：推代码 → 打 tag → Releases 下载 Setup 双击安装。

## Glossary（术语表）
| 术语 | 规范名称 | 定义 | 记录日期 |
|---|---|---|---|
| sidecar | syllables.json | 用户音节修正文件（用户目录，优先级高于内置） | 2026-11-24 |
| 内置音节数据文件 | syllables.builtin.json | 内置校准音节（只读，编译进 exe，按原始英文键） | 2026-11-24 |
| 复合键 | grade+Unit+当前英文主答案 | 进度/音节修正/索引共用的键，避免同形词串味 | 2026-11-24 |
| 更多入口 | 更多 | 低频管理功能收纳处（按使用频率，非家长/孩子身份分区） | 2026-11-24 |
| 字形段 | 音节 | 自然拼读的拼写切分（mo-ther），非音素段、非音标 | 2026-11-24 |
| 三态 | 新词/待复习/已掌握 | 词条复习状态；已掌握仅手动标记 | 2026-11-24 |

## 关键决策记录（Decision Record）
1. **AppConfig 命名**：命名空间 `XiaoGouYingYu.Config` 与类同名会在 using 后产生解析歧义 → 类改名 `AppConfig`，文件名仍 Config.cs。撤销条件：无（除非重命名命名空间）。
2. **AppId 固定**：3E702BA5-0A7E-49E0-B613-F3D2C56675C0，setup.iss 顶部，跨版本不改，否则覆盖升级失效。
3. **配置与数据分层**：Config 只放全局出厂参数；词条/音节/进度/设置/索引全部独立数据文件。撤销条件：无。
4. **WinRT 语音**：Windows.Media.SpeechSynthesis 原生接口，en-US 神经语音优先，禁用 Web Speech API。TFM 必须带 SDK 版本 net8.0-windows10.0.19041.0 才能投影 WinRT。
5. **GBK 转码**：需 CodePagesEncodingProvider（csproj PackageReference + App 启动注册），否则 .NET 8 下 GBK 读取抛异常。
6. **词库口径**：129 条；按主答案分类 112 单词 / 17 短语；5 条括号清理；4 组同形词（class/it/love/for）；0 跳过。审查报告"120 条 / 107-22"是误判，不采信。

## 约定（Convention Capture）
- 关键逻辑（括号解析、归一化、语音降级、四模式状态机、复合键迁移、三态流转）必须写中文注释，说明"做什么/为什么"。
- 所有用户数据路径经 AppPaths（基于 AppConfig.UserDataRoot），禁止第二套路径。
- 用户改动写 settings.json，不回写 Config.cs、不写安装目录。
- 模块按职责拆分（Config/Models/Services/ViewModels/Views），界面与业务逻辑分离。

## 已知问题清单
- 本机无 .NET SDK，未实际编译；需用户/云端 `dotnet build` 最终确认。
- 拼读分段"读成字母名"的听感验证需 Windows 实机跑 tools/SpeechProbe 人工确认（自动仅验证音频流非空）。
- CMU 词典为精简版（覆盖 112 单词类词条），未来导入大量新词时音节推导覆盖率有限，不确定处标"待补充"。

## 下一步
- 用户在有 .NET 8 SDK 环境 `dotnet build src/XiaoGouYingYu.csproj` 验证编译。
- 按 README 第 2 节跑 SpeechProbe 最小验证，通过后推代码、打 v1.0.0 tag 触发 Release。
- 干净环境安装 Setup.exe 首跑确认语音枚举。
