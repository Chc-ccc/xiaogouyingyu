# 交接检查点 · 小狗英语第一版

session-id: 20261124-2357
日期: 2026-11-24

## 已完成（带验证证据）
- 完整 WPF 工程约 55 个文件写入 xiaogouyingyu/；src 约 4300 行 C#/XAML。
- 数据层：129 条词库（U1-8=13/11/17/9/18/12/31/18）、112 条内置音节、CMU 词典、ARPAbet→IPA 映射。
  - 验证：Python 脚本统计与规格完全一致（129/112/17/5括号/4同形/0跳过）。
- 服务层：原子写+schemaVersion+坏档留存、括号解析、七步归一化、音节取值链、WinRT 语音、进度三态、复合键迁移、备份还原、查词、队列、播种。
- UI 层：MainWindow（四模式）+ 更多 + 导入预览 + 词库管理 + 输入对话框。
- 打包层：build.yml（setup-dotnet + publish 单文件 + 预装 ISCC 兜底 + tag Release + contents:write）、setup.iss（per-user 免 UAC、AppId 固定、卸载保留数据）、本地脚本、SpeechProbe。
- 文档层：README（12 节）、家长安装与使用说明、家长安装自然语音说明。

## 验证状态
- 静态：JSON 3/3、XML 7/7 语法通过；XAML x:Class 与 code-behind 全匹配；配置引用一致；独立静态审查发现的 1 处编译错误 + 2 处运行时崩溃已修复。
- 未验证（需外部条件）：实际 `dotnet build`（本机无 SDK）；TTS 最小验证听感（需 Windows 实机）；GitHub Actions 出包。

## 未完成 / 下一步
1. 有 SDK 环境 `dotnet build src/XiaoGouYingYu.csproj` 确认编译，按报错微调。
2. Windows 实机跑 `tools/SpeechProbe` 确认神经语音枚举与单段朗读听感。
3. 推代码、打 v1.0.0 tag → Actions 出 Setup.exe → 干净环境首跑确认。

## 阻塞项
- 无（本机无 SDK 仅为验证条件限制，不阻塞交付）。
