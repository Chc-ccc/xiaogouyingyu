using System.Collections.Generic;
using System.Text.Json.Serialization;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Models;

/// <summary>
/// 词条索引数据（wordbank-index.json），由导入生成、随词条管理更新的"派生数据"。
/// 核心作用：持久化「原始英文 / 当前英文」两个字段——原始英文一经入库永不改写，
/// 即使改英文、调单元、重新导入也保留，否则内置音节表的原始键查询会失效。
/// 两列 md 仍是家长可见的规范，本索引是程序派生层，升级不覆盖。
/// </summary>
public class WordbankIndexData
{
    public int SchemaVersion { get; set; } = AppConfig.SchemaVersion;

    /// <summary>活跃词条索引：当前复合键 -> 索引记录。</summary>
    public Dictionary<string, IndexEntry> Entries { get; set; } = new();

    /// <summary>被删除/覆盖词条的旧索引，移入此处备查。</summary>
    public Dictionary<string, IndexEntry> Retired { get; set; } = new();
}

/// <summary>单条词条索引。</summary>
public class IndexEntry
{
    /// <summary>入库时的原始英文（含括号原串），永不改写，供内置音节表按原始键查。</summary>
    public string OriginalEnglish { get; set; } = "";

    /// <summary>当前英文主答案。改英文后更新；重新导入时按旧值匹配再更新。</summary>
    public string CurrentEnglish { get; set; } = "";

    public string Grade { get; set; } = "";
    public int Unit { get; set; }
    public WordCategory Category { get; set; } = WordCategory.Word;
}
