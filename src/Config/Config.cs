using System;
using System.Collections.Generic;

namespace XiaoGouYingYu.Config;

/// <summary>
/// 【配置中枢】本软件唯一的出厂默认参数文件。
///
/// 设计约定（务必遵守，写给下一个接手的模型看）：
/// 1. 这里「只放全局可配置参数」——查词 URL 模板、慢速语速、口音、界面字号、
///    拼读段间隔、默认声音策略、随机种子等。每一项都写中文注释说明默认值与改动影响。
/// 2. 这里「绝不放」任何用户数据：词条、音节、别名数组、学习进度、词库内容、
///    用户当前设置值一律在数据文件里（词库 md / syllables.json / progress.json /
///    wordbank-index.json / settings.json）。用户界面的改动写 settings.json，不回写本文件。
/// 3. 修改本文件后重新打包即可生效；「改哪里」对照表见 README。
/// </summary>
public static class AppConfig
{
    // ============ 一、用户数据根目录（全软件唯一，不得出现第二套路径） ============

    /// <summary>
    /// 用户数据根目录，默认 %APPDATA%\小狗英语\。
    /// 词库 md、progress.json、syllables.json、wordbank-index.json、settings.json、backup/ 全部在此目录下。
    /// 程序安装目录（%LOCALAPPDATA%\Programs\小狗英语）刻意与之分离：前者只读放程序，后者可写放数据。
    /// 改动影响：改这里等于迁移全部用户数据位置，旧目录数据需人工搬移，普通维护不要动。
    /// </summary>
    public static readonly string UserDataRoot =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        + System.IO.Path.DirectorySeparatorChar + "小狗英语";

    /// <summary>用户数据根目录下的词库子目录名。加词库 = 往此目录放 md 文件。</summary>
    public const string WordBankFolderName = "词库";

    /// <summary>备份包存放的子目录名（位于用户数据根目录下）。</summary>
    public const string BackupFolderName = "backup";

    // ============ 二、数据文件名（命名约定：sidecar=syllables.json，内置=syllables.builtin.json） ============

    public const string ProgressFileName = "progress.json";
    public const string SyllablesFileName = "syllables.json";          // 用户手动修正（sidecar）
    public const string WordbankIndexFileName = "wordbank-index.json"; // 词条索引（原始英文/当前英文）
    public const string SettingsFileName = "settings.json";            // 用户偏好
    public const string SyllablesBuiltinName = "syllables.builtin.json"; // 内置只读，编译进程序集

    /// <summary>当前数据文件 schemaVersion。结构变更时递增并提供迁移路径（迁移前先备份 .bak）。</summary>
    public const int SchemaVersion = 1;

    // ============ 三、查词（用户主动点击才跳转，软件不读取网站内容） ============

    /// <summary>默认查词网站模板，{word} 占位符替换为主答案的 URL 编码串。</summary>
    public const string LookupTemplateDefault = "https://www.iciba.com/word?w={word}";

    /// <summary>
    /// 「更多」入口可选的查词模板。{word} 用主答案整串 URL 编码（句子整句编码、别名不参与）。
    /// 改动影响：新增/删除可选网站，默认模板仍是上面的 Default。
    /// </summary>
    public static readonly List<(string Name, string Template)> LookupTemplates = new()
    {
        ("金山词霸", "https://www.iciba.com/word?w={word}"),
        ("有道", "https://www.youdao.com/result?word={word}&lang=en"),
        ("剑桥词典", "https://dictionary.cambridge.org/search/direct/?datasetsearch=english&q={word}"),
    };

    // ============ 四、语音（WinRT 原生接口，en-US，神经语音优先） ============

    /// <summary>朗读语言固定 en-US。改动影响：改语言会违背"英文用英语声音朗读"硬要求，不要改。</summary>
    public const string SpeechLanguage = "en-US";

    /// <summary>神经语音名称关键字（命中任一即优先选中，大小写不敏感）。</summary>
    public static readonly string[] NeuralVoiceKeywords = { "Natural", "Neural", "神经" };

    /// <summary>正常语速（SSML prosody rate 的倍数，1.0 = 原速）。</summary>
    public const double NormalRate = 1.0;

    /// <summary>慢速语速，0.85 = 85%，落在要求的 80%–90% 区间。改动影响：越小越慢。</summary>
    public const double SlowRate = 0.85;

    /// <summary>拼读分段之间的停顿毫秒数，约 300ms，让孩子听清一段再听下一段。</summary>
    public const int SegmentGapMs = 300;

    /// <summary>SSML 使用的音素字母表：默认 ipa；若当前语音包不接受 ipa 可改其他（见 README）。</summary>
    public const string PhonemeAlphabet = "ipa";

    // ============ 五、界面与交互（面向 8 岁孩子） ============

    /// <summary>界面基础字号（px）。硬要求不小于 18，默认 19 留一点余量。</summary>
    public const double BaseFontSize = 19;

    /// <summary>窗口默认尺寸与最小尺寸（像素）。</summary>
    public const double WindowWidth = 1024;
    public const double WindowHeight = 720;
    public const double WindowMinWidth = 800;
    public const double WindowMinHeight = 600;

    // ============ 六、复习闭环 ============

    /// <summary>
    /// 「待复习」词条连续答对多少次后自动转回「新词」。默认 2。
    /// 连对计数按词条持久化到 progress.json，答错归零，切筛选/重启不清零。
    /// </summary>
    public const int CorrectStreakToReviewOut = 2;

    /// <summary>
    /// 乱序练习的固定随机种子。取固定常量而非时间戳，保证同一批词用同一种子打乱结果可复现、便于复盘。
    /// </summary>
    public const int ShuffleSeed = 20261124;

    /// <summary>「跳过句子类默写」开关默认值：默认开（8 岁孩子默写整句偏难）。</summary>
    public const bool SkipSentenceDictationDefault = true;

    /// <summary>筛选界面「隐藏句子类」开关默认值：默认显示（只影响列表展示，不影响学习模式）。</summary>
    public const bool HideSentencesInListDefault = false;

    // ============ 七、内置只读资源名（与 csproj 的 LogicalName 一致） ============

    public const string ResBuiltinWordBank = "XiaoGouYingYu.Res.译林三上.md";
    public const string ResCmuDict = "XiaoGouYingYu.Res.cmu-dict.json";
    public const string ResArpabetIpa = "XiaoGouYingYu.Res.arpabet-to-ipa.json";
    public const string ResSyllablesBuiltin = "XiaoGouYingYu.Res.syllables.builtin.json";

    /// <summary>待补充音节的显示文案。音节未确定时显示它，词条仍可学、拼读退化为整词朗读。</summary>
    public const string SyllablePending = "待补充";
}
