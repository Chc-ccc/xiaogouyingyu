# data 目录说明（内置只读资源，随 exe 打包）

本目录的文件在编译期作为嵌入资源写进程序集（见 csproj 的 `EmbeddedResource` 与 LogicalName），
是**出厂只读资源**，不是用户数据。用户数据一律在 `%APPDATA%\小狗英语\`（见 `Config.UserDataRoot`）。

| 文件 | 作用 |
|---|---|
| `译林三上.md` | 内置初始词库，129 条，按 `## Unit 1–8` 分段。首次运行时由 `SeedService` 复制到用户词库目录；之后以用户副本为准，升级不覆盖。 |
| `syllables.builtin.json` | 内置校准音节（112 个单词类词条的字形段）。按原始英文键（含括号原串）查询，只读、随程序升级。不写进两列词库 md。 |
| `cmu-dict.json` | 精简 CMU 发音词典，覆盖 112 个单词类词条的 ARPAbet 音素串，仅供 TTS 内部 phoneme 兜底。 |
| `arpabet-to-ipa.json` | ARPAbet → IPA 映射表，程序内部转换音素；phoneme 内容永不显示。 |

## 数据来源与许可

- `cmu-dict.json` 的词条音素取自 **CMU Pronouncing Dictionary**（ Carnegie Mellon University，
  项目主页 http://www.speech.cs.cmu.edu/cgi-bin/cmudict ），采用 **BSD 风格许可**：
  允许以源代码和二进制形式再分发，前提是保留版权声明与免责声明。
  本项目按该许可内置精简词条，README 中同样标注。
- `syllables.builtin.json` 的字形段由本项目按自然拼读规则逐词校准，并与 CMU 发音复核。
  不确定的词标「待补充」，不臆造。
- `译林三上.md` 的英文与中文释义由用户提供，程序不自行翻译、不联网校验。
