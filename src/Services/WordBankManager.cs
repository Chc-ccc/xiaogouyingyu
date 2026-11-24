using System.Text;
using System.Text.RegularExpressions;
using XiaoGouYingYu.Models;
using System.IO;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 词库管理服务：修改英文/中文、删除、新增、调整所属单元，并协调 progress/sidecar/index
/// 三处复合键迁移（只替换变化段、年级不变、撞名只限目标单元、不覆盖）。
/// 删除/覆盖的旧记录移入各自 retired 区，可恢复。
/// </summary>
public sealed class WordBankManager
{
    private readonly WordBankService _service;
    private readonly ProgressService _progress;
    private readonly SyllabusSidecarStore _sidecar;
    private readonly WordbankIndexService _index;
    private readonly SyllableService _syllables;

    public WordBankManager(WordBankService service, ProgressService progress,
        SyllabusSidecarStore sidecar, WordbankIndexService index, SyllableService syllables)
    {
        _service = service;
        _progress = progress;
        _sidecar = sidecar;
        _index = index;
        _syllables = syllables;
    }

    /// <summary>撞名检测：只在目标年级+目标单元范围内按完整复合键精确比对（不跨单元扫描）。</summary>
    public bool NameConflict(List<WordBank> banks, string grade, int unit, string english, string? excludeKey = null)
    {
        return banks.Where(b => b.Grade == grade)
                    .SelectMany(b => b.Entries)
                    .Any(e => e.Unit == unit
                              && string.Equals(e.CurrentEnglish, english, StringComparison.OrdinalIgnoreCase)
                              && e.Key != excludeKey);
    }

    /// <summary>修改英文：写回 md + 三处键迁移。返回 (成功, 说明)。</summary>
    public (bool Ok, string Message) ChangeEnglish(List<WordBank> banks, WordEntry entry, string newEnglish)
    {
        var grade = entry.Grade; var unit = entry.Unit; var oldKey = entry.Key;
        if (NameConflict(banks, grade, unit, newEnglish, oldKey))
            return (false, "目标单元已有同名词条，禁止覆盖");

        // 1) 三处键迁移（共用 KeyBuilder.Migrate，只换英文段）
        var p = _progress.MigrateKey(oldKey, null, newEnglish);
        var s = _sidecar.MigrateKey(oldKey, null, newEnglish);
        var ix = _index.ChangeEnglish(oldKey, unit, newEnglish);
        if (!p.Ok || !s.Ok || !ix.Ok)
            return (false, $"键迁移失败：{p.Message} / {s.Message} / {ix.Message}");

        // 2) 写回 md（保留 OriginalEnglish 原串不变，只改主答案位置）
        UpdateMd(entry, newEnglish, entry.Chinese);
        return (true, "英文已修改，进度与修正已迁移，原始英文保留");
    }

    /// <summary>调整所属单元：写回 md + 三处键迁移（只换 Unit 段）。</summary>
    public (bool Ok, string Message) ChangeUnit(List<WordBank> banks, WordEntry entry, int newUnit)
    {
        var oldKey = entry.Key; var grade = entry.Grade;
        if (NameConflict(banks, grade, newUnit, entry.CurrentEnglish, oldKey))
            return (false, "目标单元已有同名词条，禁止覆盖");

        var p = _progress.MigrateKey(oldKey, newUnit, null);
        var s = _sidecar.MigrateKey(oldKey, newUnit, null);
        var ix = _index.ChangeUnit(oldKey, newUnit);
        if (!p.Ok || !s.Ok || !ix.Ok)
            return (false, $"键迁移失败：{p.Message} / {s.Message} / {ix.Message}");

        // 写回 md：把该行的 ## Unit 归属改掉（简单做法：重写整个文件，见 MoveEntryUnit）
        MoveEntryUnitInMd(entry, newUnit);
        return (true, "单元已调整，进度与修正已迁移");
    }

    /// <summary>修改中文：只写回 md，不影响键。</summary>
    public void ChangeChinese(WordEntry entry, string newChinese)
        => UpdateMd(entry, entry.CurrentEnglish, newChinese);

    /// <summary>删除词条：三处移入 retired，md 中移除该行。</summary>
    public void Delete(List<WordBank> banks, WordEntry entry)
    {
        var key = entry.Key;
        _progress.Retire(key);
        _sidecar.Retire(key);
        _index.Retire(key);
        RemoveLineFromMd(entry);
    }

    /// <summary>
    /// 新增词条：走与导入完全相同的初始化（括号解析 + 音节推导），原始英文=当前英文。
    /// </summary>
    public WordEntry AddNew(string grade, int unit, string rawEnglish, string chinese)
    {
        var bank = new WordBank { Grade = grade, FilePath = Path.Combine(AppPaths.WordBankDir, grade + ".md") };
        // 复用 WordBankService 的统一入口（通过反射调用私有 BuildEntry 不合适，这里显式重写同一流程）
        var mainAnswer = BracketParser.Parse(rawEnglish,
            out var aliases, out var displayOnly, out var hint, out var pendingBrackets);
        var entry = new WordEntry
        {
            OriginalEnglish = rawEnglish,
            CurrentEnglish = mainAnswer,
            Chinese = chinese,
            Aliases = aliases,
            DisplayOnlyAliases = displayOnly,
            AliasHint = hint,
            Grade = grade,
            Unit = unit,
            Category = BracketParser.Classify(mainAnswer)
        };
        var (segs, pending) = _syllables.Get(entry);
        entry.Syllables = segs;
        entry.SyllablePending = pending;

        // 追加到 md
        AppendLineToMd(entry);
        _index.AddNew(entry);
        _progress.EnsureEntry(entry.Key);
        return entry;
    }

    /// <summary>在 md 中替换某词条的英文/中文（按 OriginalEnglish + 中文定位行）。</summary>
    private void UpdateMd(WordEntry entry, string newEnglishDisplay, string newChinese)
    {
        var path = entry.FilePathOfGrade();
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var cols = lines[i].Trim().Trim('|').Split('|');
            if (cols.Length < 2) continue;
            if (cols[0].Trim() == entry.OriginalEnglish && cols[1].Trim() == entry.Chinese)
            {
                // 主答案改变时需在原串上替换第一个括号前的部分，保留括号注释
                string newRaw = ReplaceMainInRaw(entry.OriginalEnglish, newEnglishDisplay);
                lines[i] = $"| {newRaw} | {newChinese} |";
                break;
            }
        }
        WriteMd(path, lines);
    }

    private void RemoveLineFromMd(WordEntry entry)
    {
        var path = entry.FilePathOfGrade();
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
        lines.RemoveAll(l =>
        {
            var cols = l.Trim().Trim('|').Split('|');
            return cols.Length >= 2 && cols[0].Trim() == entry.OriginalEnglish && cols[1].Trim() == entry.Chinese;
        });
        WriteMd(path, lines);
    }

    private void AppendLineToMd(WordEntry entry)
    {
        var path = Path.Combine(AppPaths.WordBankDir, entry.Grade + ".md");
        var lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8).ToList() : new List<string>();
        // 定位 ## Unit N，插入到该单元末尾（下一个 ## 之前）
        int idx = -1;
        for (int i = 0; i < lines.Count; i++)
            if (Regex.IsMatch(lines[i], $@"^##\s+Unit\s+{entry.Unit}\b")) { idx = i; break; }

        var newLine = $"| {entry.OriginalEnglish} | {entry.Chinese} |";
        if (idx < 0)
        {
            lines.Add("");
            lines.Add($"## Unit {entry.Unit}");
            lines.Add("");
            lines.Add("| 单词 | 中文 |");
            lines.Add("|---|---|");
            lines.Add(newLine);
        }
        else
        {
            int insert = idx + 1;
            while (insert < lines.Count && lines[insert].Trim() != "" && !lines[insert].StartsWith("##")) insert++;
            lines.Insert(insert, newLine);
        }
        WriteMd(path, lines);
    }

    private void MoveEntryUnitInMd(WordEntry entry, int newUnit)
    {
        var path = entry.FilePathOfGrade();
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
        // 从原单元移除
        lines.RemoveAll(l =>
        {
            var cols = l.Trim().Trim('|').Split('|');
            return cols.Length >= 2 && cols[0].Trim() == entry.OriginalEnglish && cols[1].Trim() == entry.Chinese;
        });
        // 追加到新单元
        var newLine = $"| {entry.OriginalEnglish} | {entry.Chinese} |";
        int idx = -1;
        for (int i = 0; i < lines.Count; i++)
            if (Regex.IsMatch(lines[i], $@"^##\s+Unit\s+{newUnit}\b")) { idx = i; break; }
        if (idx < 0)
        {
            lines.Add(""); lines.Add($"## Unit {newUnit}"); lines.Add("");
            lines.Add("| 单词 | 中文 |"); lines.Add("|---|---|"); lines.Add(newLine);
        }
        else
        {
            int insert = idx + 1;
            while (insert < lines.Count && lines[insert].Trim() != "" && !lines[insert].StartsWith("##")) insert++;
            lines.Insert(insert, newLine);
        }
        WriteMd(path, lines);
    }

    /// <summary>替换原始英文串中第一个括号前的主答案，保留括号注释。</summary>
    private static string ReplaceMainInRaw(string raw, string newMain)
    {
        int p = raw.IndexOf('(');
        return p < 0 ? newMain : newMain + raw[p..];
    }

    private static void WriteMd(string path, List<string> lines)
        => File.WriteAllText(path, string.Join("\n", lines), new UTF8Encoding(false));
}

/// <summary>WordEntry 的辅助扩展：由年级定位 md 路径（FilePath 可能为空，故按约定重算）。</summary>
internal static class WordEntryPathExtensions
{
    public static string FilePathOfGrade(this WordEntry e)
        => !string.IsNullOrEmpty(e.FilePath)
            ? e.FilePath
            : Path.Combine(AppPaths.WordBankDir, e.Grade + ".md");
}
