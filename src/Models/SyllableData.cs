using System.Collections.Generic;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Models;

/// <summary>
/// 用户音节修正数据（sidecar = syllables.json），优先级高于内置 syllables.builtin.json。
/// 只存用户手动改过的词条；改英文/调单元时按复合键迁移，重写导入同名词库时不清空。
/// </summary>
public class SyllableData
{
    public int SchemaVersion { get; set; } = AppConfig.SchemaVersion;

    /// <summary>活跃修正：复合键 -> 用户手填的字形段数组。</summary>
    public Dictionary<string, List<string>> Entries { get; set; } = new();

    /// <summary>被删除/覆盖词条的旧修正，移入此处备查。</summary>
    public Dictionary<string, List<string>> Retired { get; set; } = new();
}
