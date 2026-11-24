namespace XiaoGouYingYu.Models;

/// <summary>
/// 复合键构造与迁移的公共工具。
/// 全软件的进度键、音节修正键、词条索引键都用它生成，保证口径一致。
/// 复合键 = 年级 + Unit + 英文主答案；用 \u001f（单元分隔符）连接，避免英文里出现分隔符导致歧义。
/// </summary>
public static class KeyBuilder
{
    private const char Sep = '\u001f';

    /// <summary>生成复合键。grade 段始终参与，Unit 与英文各占一段。</summary>
    public static string Build(string grade, int unit, string english)
        => string.Join(Sep, grade ?? "", unit.ToString(), english ?? "");

    /// <summary>
    /// 迁移键：只替换变化的那一段——改英文换英文段、调单元换 Unit 段，年级段始终不变。
    /// 「修改英文」与「调整所属单元」共用本方法，避免进度与手动修正丢失。
    /// </summary>
    public static string Migrate(string oldKey, int? newUnit = null, string? newEnglish = null)
    {
        var parts = (oldKey ?? "").Split(Sep);
        if (parts.Length != 3) return oldKey; // 坏键原样返回，交给调用方留档处理
        var grade = parts[0];
        var unit = newUnit?.ToString() ?? parts[1];
        var english = newEnglish ?? parts[2];
        return string.Join(Sep, grade, unit, english);
    }

    /// <summary>取复合键中的年级段（用于迁移时确认年级不变）。</summary>
    public static string GradeOf(string key)
    {
        var parts = (key ?? "").Split(Sep);
        return parts.Length == 3 ? parts[0] : "";
    }
}
