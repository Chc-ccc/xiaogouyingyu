# 小狗英语 · 维护文档

本机离线可运行的 Windows 儿童英语单词学习软件（C# WPF，面向 8 岁孩子，译林三上起步）。
核心学习功能全程离线；中文释义取自词库 md，不联网、不调大模型、不消耗 token。

> 本文只写工程事实，供家长与后续接手的模型阅读。换模型增量更新说明见第 10 节。

---

## 1. 项目文件树与每个文件作用

```
xiaogouyingyu/
├── README.md                         本维护文档
├── .gitignore                        忽略构建产物
├── src/                              程序源码（编译为 self-contained 单文件 exe）
│   ├── XiaoGouYingYu.csproj          项目文件：net8.0-windows、版本号唯一真源、单文件发布、嵌入资源
│   ├── App.xaml                      应用资源：全局字体、bool 可见性转换器
│   ├── App.xaml.cs                   入口：注册编码/建目录/首次播种/资源校验/打开主窗口；退出取消朗读
│   ├── AssemblyInfo.cs               WPF 线程模型声明
│   ├── Config/Config.cs              【配置中枢】全部出厂默认参数（类名 AppConfig，逐项中文注释）
│   ├── Converters/BoolConverters.cs  bool <-> Visibility 转换器
│   ├── Models/
│   │   ├── Enums.cs                  四模式/三态/词条分类枚举
│   │   ├── WordEntry.cs              词条模型（原始/当前英文、别名、字形段、复合键）
│   │   ├── KeyBuilder.cs             复合键构造与迁移（改英文/调单元共用）
│   │   ├── ProgressData.cs           progress.json 数据结构（三态+连对+retired）
│   │   ├── SyllableData.cs           syllables.json（sidecar）数据结构
│   │   ├── WordbankIndexData.cs      wordbank-index.json 数据结构（原始/当前英文）
│   │   ├── SettingsData.cs           settings.json 数据结构（用户偏好）
│   │   └── ParseModels.cs            词库容器与导入预览数据结构
│   ├── Services/
│   │   ├── AppPaths.cs               用户数据路径唯一出口（全部基于 AppConfig.UserDataRoot）
│   │   ├── JsonFileStore.cs          原子写/schemaVersion 迁移/坏档留存的通用 json 读写
│   │   ├── ResourceReader.cs         读取嵌入的只读资源（CMU/映射/内置音节/内置词库）
│   │   ├── ResourceHealthCheck.cs    启动校验内置资源完整性
│   │   ├── SeedService.cs            首次播种与"恢复内置词库"副本
│   │   ├── EncodingHelper.cs         UTF-8 检测与 GBK 一键转码
│   │   ├── BracketParser.cs          括号通用解析（标记前缀剥离、主答案+别名/仅提示/待确认）
│   │   ├── NormalizationService.cs   默写判定七步归一化（唯一权威）
│   │   ├── CmuDictionary.cs          CMU 词典查询
│   │   ├── ArpabetToIpa.cs           ARPAbet -> IPA 转换（phoneme 内部用）
│   │   ├── SyllableService.cs        字形段取值链 sidecar→内置→CMU→待补充
│   │   ├── SpeechService.cs          WinRT 原生语音：枚举/神经优先/慢速/phoneme 兜底/竞态取消
│   │   ├── SettingsService.cs        settings.json 读写（用户值与默认值分层）
│   │   ├── ProgressService.cs        三态流转/连对/复合键迁移/retired/统计
│   │   ├── WordbankIndexService.cs   原始英文持久化/键对齐/retired
│   │   ├── SyllabusSidecarStore.cs   音节 sidecar 持久化/迁移/retired
│   │   ├── WordBankService.cs        md 解析/导入预览/入库/导出/筛选
│   │   ├── WordBankManager.cs        词库增删改调单元 + 三处复合键迁移协调
│   │   ├── QuizQueueService.cs       练习队列三步规则/固定种子乱序
│   │   ├── LookupService.cs          查词 URL 拼装与浏览器打开（只读取主答案，不抓取网页）
│   │   └── BackupService.cs          全量备份/还原（zip + manifest 版本权威 + 还原前备份）
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs          INotifyPropertyChanged + RelayCommand
│   │   └── MainViewModel.cs          四模式状态机/卡片导航/筛选/一轮收口/答错提示/切卡取消
│   └── Views/
│       ├── MainWindow.xaml/.cs        主界面（大字、顶部控制条、卡片、答案区、导航）
│       ├── MoreWindow.xaml/.cs        "更多"入口（选声/统计/备份/词库/待补充/版本核对/说明）
│       ├── ImportPreviewWindow.xaml/.cs 导入预览（行号/原因/覆盖/GBK 转码）
│       ├── WordBankManagerWindow.xaml/.cs 词库管理（增删改调单元/改音节/状态）
│       └── InputDialog.cs             通用输入对话框
├── data/                              内置只读资源（编译进 exe，不是用户数据）
│   ├── 译林三上.md                    内置初始词库 129 条（## Unit 1–8）
│   ├── syllables.builtin.json         内置校准音节 112 条（字形段，按原始英文键）
│   ├── cmu-dict.json                  精简 CMU 发音词典（ARPAbet，phoneme 兜底）
│   ├── arpabet-to-ipa.json            ARPAbet→IPA 映射表
│   └── README.md                      数据来源与许可说明
├── .github/
│   ├── workflows/build.yml            GitHub Actions：setup-dotnet→publish 单文件→ISCC→tag 发 Release
│   └── installer/setup.iss            Inno Setup：per-user 免 UAC、固定 AppId、卸载保留数据
├── scripts/build-local.ps1            本地兜底打包脚本（需本机已装 SDK+Inno，家长不用）
├── tools/SpeechProbe/                 TTS 前置最小验证程序（单文件形态枚举语音+单段朗读）
│   ├── SpeechProbe.csproj
│   └── Program.cs
└── docs/
    ├── 家长安装与使用说明.md
    └── 家长安装自然语音说明.md
```

---

## 2. 运行与打包步骤

**家长使用（三步）**：打开仓库 Releases 页 → 下载最新 `小狗英语Setup-x.y.z.exe` → 双击、点"下一步"完成安装或覆盖更新，全程无管理员弹窗。
程序不自动更新；每次新版到 Releases 下载新安装包双击覆盖即可，词库/进度/音节修正/偏好设置都在用户目录，不丢、无需卸载。

**开发者打包**：推送代码到 `main/master`，GitHub Actions（windows-latest）自动：
1. `actions/setup-dotnet` 装 .NET 8 SDK；
2. `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true ...` 生成单文件 exe；
3. 直接调用 runner 预装的 `iscc` 编译 `setup.iss`（找不到才下载官方安装包兜底）生成 Setup.exe；
4. 上传 Artifacts；仅推送 `v*` tag 时创建 Release 并附安装包。

本地兜底：本机已装 SDK+Inno 时运行 `powershell -ExecutionPolicy Bypass -File scripts/build-local.ps1`。

**TTS 前置最小验证**（改语音/发布/打包配置后必做，对应提示词硬要求）：
```powershell
dotnet run --project tools/SpeechProbe/SpeechProbe.csproj
```
以最终单文件形态跑两件事：①枚举 en-US 声音确认有 Natural/Neural；②朗读 `mo`/`ther` 确认无异常且音频流非空。两项通过后才能铺开四模式。每次出 Release 前，还应把 Setup.exe 安装到干净环境首跑一次，确认安装形态下语音枚举与单段朗读仍正常。

---

## 3. 配置项逐项对照表（配置文件：`src/Config/Config.cs`，类名 AppConfig）

| 配置项 | 默认值 | 所在文件 | 修改影响 |
|---|---|---|---|
| UserDataRoot | `%APPDATA%\小狗英语\` | Config.cs | 全部用户数据根目录；改它等于迁移数据，普通维护勿动 |
| WordBankFolderName | 词库 | Config.cs | 词库子目录名 |
| BackupFolderName | backup | Config.cs | 备份包目录 |
| Progress/Syllables/WordbankIndex/Settings 文件名 | 各自 json | Config.cs | 数据文件名，改动需同步迁移旧档 |
| SchemaVersion | 1 | Config.cs | 数据结构版本；结构变更时递增并提供迁移路径 |
| LookupTemplateDefault | 金山词霸 URL | Config.cs | 默认查词网站 |
| LookupTemplates | 金山/有道/剑桥 | Config.cs | "更多"可选查词网站 |
| SpeechLanguage | en-US | Config.cs | 朗读语言；改它违背硬要求，勿动 |
| NeuralVoiceKeywords | Natural/Neural/神经 | Config.cs | 神经语音识别关键字 |
| NormalRate / SlowRate | 1.0 / 0.85 | Config.cs | 正常/慢速语速（慢速落在 80–90%） |
| SegmentGapMs | 300 | Config.cs | 拼读段间隔毫秒 |
| PhonemeAlphabet | ipa | Config.cs | SSML phoneme 字母表 |
| BaseFontSize | 19 | Config.cs | 界面基础字号（≥18） |
| WindowWidth/Height/Min* | 1024/720/800/600 | Config.cs | 窗口尺寸 |
| CorrectStreakToReviewOut | 2 | Config.cs | 待复习连对多少次转回新词 |
| ShuffleSeed | 20261124 | Config.cs | 乱序固定种子（可复现，不按时间戳） |
| SkipSentenceDictationDefault | true | Config.cs | 默认跳过句子类默写 |
| HideSentencesInListDefault | false | Config.cs | 默认隐藏句子类列表开关 |
| SyllablePending | 待补充 | Config.cs | 音节不确定时的显示文案 |

用户在界面的改动（慢速、选声、查词 URL、两个开关、乱序）写 `settings.json`，不回写 Config.cs。

---

## 4. 如何新增词库（md 格式与导入步骤）

1. 按教材册命名文件：`译林三下.md`、`译林四上.md`……文件名即"年级/册名"。
2. 文件内部用 `## Unit N` 分段（兼容 `## 第N单元`、`## Unit N 标题`）。
3. 每段用两列表格 `| 单词 | 中文 |`，第二行 `|---|---|`。中文原样填，不用填音节。
4. 统一 UTF-8 编码（推荐无 BOM）。英文列首尾空白会被 trim；括号别名按规则解析。
5. 主界面 →「更多」→「导入词库」→选 md →预览（显示年级/单元数/词条数、失败行列号）→确认入库。
6. 新增年级无需改代码、无需重新打包；主界面年级下拉自动出现。

音节不用家长填：内置 112 词已校准，新词由 CMU 三步推导，不确定标"待补充"仍可学，家长可在「词库管理→改音节」手填（写 sidecar，升级不覆盖）。

---

## 5. 常见修改"改哪里"对照表

| 想改什么 | 改哪里 |
|---|---|
| 查词网站 | Config.cs 的 LookupTemplateDefault / LookupTemplates；用户运行时改在「更多」写 settings.json |
| 慢速语速 | Config.cs 的 SlowRate |
| 拼读段间隔 | Config.cs 的 SegmentGapMs |
| 界面字号 | Config.cs 的 BaseFontSize（仍需 ≥18） |
| 待复习连对转出次数 | Config.cs 的 CorrectStreakToReviewOut |
| 乱序种子 | Config.cs 的 ShuffleSeed |
| 神经语音关键字 | Config.cs 的 NeuralVoiceKeywords |
| 版本号 | csproj 的 `<Version>`（唯一真源，tag/安装包/界面都对齐它） |
| AppId | setup.iss 顶部 `MyAppId`（一旦生成永不改） |
| 默写判对规则 | Services/NormalizationService.cs（七步归一化） |
| 括号别名解析 | Services/BracketParser.cs |
| 音节取值链 | Services/SyllableService.cs |
| 语音枚举/降级 | Services/SpeechService.cs |
| 三态流转 | Services/ProgressService.cs |
| 安装包目录/权限 | .github/installer/setup.iss |

---

## 6. 换模型增量更新说明（重要）

流程：①先备份当前源码；②把 README + 需要修改的源文件一起发给新模型；③明确指令"只改 X 处、不要重写整个项目"；④改完用同一 GitHub Actions 工作流重新生成安装包；⑤按第 12 节交付前自检清单验收。

**为什么不要重写**：重写会丢失已验证过的修复、破坏模块边界、增加回归风险。本工程经过多轮审查闭环（129 条口径、112/17 分类、七步归一化、复合键迁移、三态流转等），每个约定都写在注释/README 里，只改单点即可安全落地。

**技术决策摘要（不得擅自改回旧方案）**：
1. 打包：C# WPF + GitHub Actions 云端出 self-contained 单文件 exe（PublishSingleFile=true 不变）→ Inno Setup 打包 per-user 免 UAC 的 Setup.exe，AppId 固定、双击覆盖升级、卸载保留用户数据。原 Tauri/Electron/WebView2 方案已废弃。
2. 语音：原生 WinRT Windows.Media.SpeechSynthesis，en-US 神经语音优先，禁用网页 Web Speech API。
3. 音节：字形段 + CMU 三步推导 + 三段式回退；内置校准音节独立数据文件，不写进两列词库 md。
4. 数据：复合键（年级+Unit+英文）+ sidecar 分层，改英文/调单元共用迁移；进度独立 progress.json。
5. 交付：源码写入工作区成为真实文件。

---

## 7. 默写判对归一化示例表（七步权威定义见 Services/NormalizationService.cs）

| 孩子输入 | 主答案/别名 | 判定 | 原因 |
|---|---|---|---|
| ` Mother ` | mother | 对 | ①去首尾空白 |
| `MOther` | mother | 对 | ⑥转小写 |
| `mother.` | mother | 对 | ④去句末标点 |
| `mother！` | mother | 对 | ④去句末标点（全角！） |
| `Yes, please` | Yes, please. | 对 | ④去句末标点、保留句中标点逗号 |
| `You're welcome` | You're welcome. | 对 | 保留句中撇号 |
| `You’re welcome` | You're welcome. | 对 | ③弯引号归一为直引号 |
| `mother‑in‑law` | mother-in-law | 对 | ⑤短破折号归一为半角 `-` |
| `mother in law` | mother-in-law | 错 | ⑤连字符不删除，空格版不算对 |
| `mum` | mum(mom) | 对 | 主答案命中 |
| `mom` | mum(mom) | 对 | ⑦别名命中（AmE） |
| `Mr.` | Mr(AmE Mr.) | 对 | 别名 Mr. 去句末标点后与主答案相同 |
| `children` | child(pl. children) | 错 | pl. 复数仅提示、不判对 |
| `母亲` | mother（中文"母亲"） | 错 | 中文释义不参与判定 |

> Unicode 空白（U+00A0、制表符、全角空格 U+3000）折叠为普通空格、内部连续空白合并为一个；中文释义中的顿号逗号不参与判定。

---

## 8. 带括号词条清理对照表（5 条）

| 原始英文 | 主答案 | 别名（可判对） | 仅提示（不判对） | 卡片小字 |
|---|---|---|---|---|
| `Mr (AmE Mr.)` | Mr | Mr. | — | — |
| `mum (AmE mom)` | mum | mom | — | 美式常写 mom |
| `grandfather (grandpa)` | grandfather | grandpa | — | — |
| `grandmother (grandma)` | grandmother | grandma | — | — |
| `child (pl. children)` | child | — | children | — |

分类按主答案判定（先剥括号）：单词类 112 条 / 短语·句子类 17 条。若按未清洗原始字串统计会得到 107/22，那是把上述 5 条因括号内空格误判为短语类，属错误口径。

---

## 9. 数据分层与用户数据说明

**程序安装目录** `%LOCALAPPDATA%\Programs\小狗英语`：只放程序本体与只读资源，运行时不写入。

**用户数据根目录**（唯一）`%APPDATA%\小狗英语\`：
| 文件 | 作用 | 说明 |
|---|---|---|
| 词库 md | 家长可见的 `英文｜中文` 两列 | 首次播种后以用户副本为准，升级不覆盖 |
| `syllables.builtin.json` | 内置校准音节（112 条） | 随 exe 内置只读，不占用户目录存储（在程序集内） |
| `syllables.json` | 用户音节修正（sidecar） | 优先级高于内置；重新导入同名词库不清空 |
| `wordbank-index.json` | 词条索引 | 持久化"原始英文/当前英文"，改英文不丢原始键 |
| `progress.json` | 学习进度（三态+连对） | 独立文件，不塞进 sidecar/索引 |
| `settings.json` | 用户偏好 | 用户改动写这里，不回写 Config.cs |
| `backup/` | 备份包 zip | 带时间戳，升级不覆盖、卸载不删 |

**首次播种与升级不覆盖**：首次运行且词库目录为空时，程序把内置「译林三上」复制进去；此后以用户副本为准，程序升级/重装不覆盖、不回滚用户修改。安装包不在用户目录预写任何数据。安装程序从不扫描/删除用户磁盘上的旧绿色 exe；装好新版后可手动删除旧 exe 及其文件夹，删除旧程序不影响用户数据。

**schemaVersion 与迁移**：四个 json 都带 `schemaVersion`。结构变更时递增并提供迁移路径；读取旧档先比对版本，一致直接读，可迁移则先复制带时间戳 `.bak` 再迁移，无法兼容明确提示，不静默清空。

**原子写与坏档恢复**：关键 json 采用"写临时文件→校验→原子替换"。加载失败先读 `.bak`，仍失败把坏档改名 `*.corrupt-时间戳` 留存，重建空结构并提示，不静默清空。

**全量备份/还原**：「更多」→「备份全部数据」生成带时间戳 zip 存 `backup/` 并显示完整路径，包内 `manifest.json` 为版本权威，记录程序版本与各文件 schemaVersion；还原前先备份当前数据并校验版本，不兼容中止，禁止用旧备份覆盖较新记录。与"导出词库 md"（仅词库文字）明确区分。

**多 Windows 账户**：per-user 安装下 `%LOCALAPPDATA%` 与用户数据按 Windows 账户隔离，换账户看不到前一个账户的数据属正常；需要迁移时用「更多」的全量备份/还原。

---

## 10. 安装包覆盖更新与升级后核对

旧口径"下载绿色 exe / 手动替换 exe / 家长本地装 Inno Setup 打包"全部废弃。现行：下载 Setup.exe → 双击 → 下一步，免 UAC、无需卸载、数据保留。AppId 固定（见 setup.iss 顶部 `MyAppId`，一旦确定不得更换），双击新版即识别为升级。卸载只删程序目录与快捷方式，不删用户目录。

升级后核对：「更多」入口显示程序版本号与数据条数（词库条数、进度条数、待补充音节数），与升级前对比确认数据未丢。版本号唯一来源 csproj `<Version>`，与 Release tag（`v`+版本号）、安装包版本、界面显示四者一致。

---

## 11. CMU 发音词典来源与许可

`data/cmu-dict.json` 的词条音素取自 **CMU Pronouncing Dictionary**（Carnegie Mellon University，http://www.speech.cs.cmu.edu/cgi-bin/cmudict），采用 **BSD 风格许可**：允许以源代码和二进制形式再分发，前提是保留版权声明与免责声明。本工程按该许可内置精简词条（覆盖 112 个单词类词条）。音素仅供 TTS 内部 SSML phoneme 使用，永不显示给孩子。ARPAbet→IPA 映射见 `data/arpabet-to-ipa.json`。

---

## 12. 交付前自检清单

- □129 条逐条覆盖 □U1–U8=13/11/17/9/18/12/31/18 □单词类 112/短语类 17 □括号清理 5 □同形词 4 组（class/it/love/for）□跳过 0
- □四模式齐备 □默写严格隐藏英文 □无音标只拼读/逐词切分 □发音可播放 □神经语音优先 □慢速开关 □更多手动选声 □家长语音说明 □降级链完整
- □三态+连对2次+一轮收口+四模式答错提示（不公布答案）□句子类选做+两个开关作用域分离 □更多入口统计
- □进度复合键+sidecar □改英文/调单元共用迁移且撞名只限目标单元 □retired 可恢复 □首次播种后升级不覆盖
- □括号通用解析（pl.仅提示）□七步归一化+README 示例表 □切卡取消朗读与高亮 □输入框聚焦/回车提交/答错全选
- □md 导入预览/行号/GBK 转码/覆盖不清 sidecar □导出词库与全量备份文案区分 □查词整句编码、别名不参与、只开浏览器
- □数据全在用户目录、安装包不预写 □原子写/坏档留存/schemaVersion 迁移 □备份还原前先备份校验 □更多可核对版本与数据条数
- □AppId 固定、setup.iss 顶部常量、Inno 转义 `AppId={{#MyAppId}}` □per-user 免 UAC □卸载保留用户数据 □ISCC 预装调用缺失兜底 □tag 才发 Release □contents: write
- □配置项集中 Config.cs 且中文注释、只含全局参数、数据不进 Config □关键逻辑中文注释 □模块按职责拆分 □换模型说明齐备
- □WinRT 原生语音、未用 Tauri/Web Speech □音节为字形段、CMU 经映射实测、phoneme 异常回退整词朗读 □内置音节独立、不写两列 md
- □TTS 前置最小验证程序齐备 □启动校验内置资源 □待补充音节可单独列出且拼读退化整词 □运行中可刷新声音 □无跟读评分
- □README 分必备章节+附录、只写工程事实 □多账户说明 □旧绿色 exe 清理说明 □体积以实际构建为准
