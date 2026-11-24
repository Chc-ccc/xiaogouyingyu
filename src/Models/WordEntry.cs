using System.Collections.Generic;
using System.Text.Json.Serialization;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Models;

/// <summary>
/// 词条数据模型。对应词库 md 的一行「英文 | 中文」，加上程序派生字段。
/// 数据文件（wordbank-index.json / 内存词库）承载本类，不写进 AppConfig.cs。
/// </summary>
public class WordEntry
{
    /// <summary>原始英文：入库时的英文串（未剥括号）。一经入库永不改写，供内置音节表按原始键查询。</summary>
    public string OriginalEnglish { get; set; } = "";

    /// <summary>当前英文：主答案（剥掉括号注释后的部分）。改英文后更新此值，OriginalEnglish 不变。</summary>
    public string CurrentEnglish { get; set; } = "";

    /// <summary>中文释义：原样取自词库 md，不联网校验、不翻译。</summary>
    public string Chinese { get; set; } = "";

    /// <summary>可判对的别名数组（AmE/BrE/无标记称呼别名及去标点后与本体不同的项）。</summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>仅展示提示、不参与判对的别名（如 pl. children）。</summary>
    public List<string> DisplayOnlyAliases { get; set; } = new();

    /// <summary>卡片小字提示，如「美式常写 mom」。为空则不显示。</summary>
    public string AliasHint { get; set; } = "";

    /// <summary>字形段音节数组（如 mo / ther）。拼读模式逐段朗读并高亮。待补充时元素为 AppConfig.SyllablePending。</summary>
    public List<string> Syllables { get; set; } = new();

    /// <summary>音节是否待补充（true 时拼读退化为整词朗读并标注）。</summary>
    public bool SyllablePending { get; set; }

    /// <summary>所属年级/教材册名（=词库 md 文件名，如「译林三上」）。复合键第一段。</summary>
    public string Grade { get; set; } = "";

    /// <summary>所属单元号（来自 ## Unit N）。复合键第二段。</summary>
    public int Unit { get; set; }

    /// <summary>词条分类，按主答案判定。单词类 Word / 短语·句子类 Phrase。</summary>
    public WordCategory Category { get; set; } = WordCategory.Word;

    /// <summary>是否为短语/句子类（决定拼读粒度与"选做"标签）。</summary>
    [JsonIgnore]
    public bool IsPhrase => Category == WordCategory.Phrase;

    /// <summary>
    /// 复合键：年级 + Unit + 当前英文主答案。进度、音节修正、索引共用此键，避免同形词串味。
    /// </summary>
    [JsonIgnore]
    public string Key => KeyBuilder.Build(Grade, Unit, CurrentEnglish);

    /// <summary>按主答案展示的英文（带别名小字提示由界面拼接）。</summary>
    [JsonIgnore]
    public string DisplayEnglish => CurrentEnglish;
}
