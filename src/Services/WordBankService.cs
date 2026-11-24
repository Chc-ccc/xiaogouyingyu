using System.Text;
using System.Text.RegularExpressions;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 词库服务：md 解析（分单元+两列表格）、导入预览、入库、词库管理（增删改调单元）、
/// 导出、年级/单元筛选。中文直接取 md，不联网、不调大模型、不耗 token。
/// </summary>
public sealed class WordBankService
{
    /// <summary>全量加载用户词库目录下的所有 md（一个文件 = 一个年级）。</summary>
    public List<WordBank> LoadAll()
    {
        var banks = new List<WordBank>();
        foreach (var file in AppPaths.ListWordBankFiles())
        {
            var bank = ParseFileToBank(file);
            if (bank.Entries.Count > 0) banks.Add(bank);
        }
        return banks;
    }

    /// <summary>解析单个 md 为词库（正式入库用的宽松解析，失败行跳过但仍入库其他行）。</summary>
    private WordBank ParseFileToBank(string path)
    {
        var grade = Path.GetFileNameWithoutExtension(path);
        var bank = new WordBank { Grade = grade, FilePath = path };
        int unit = 0;
        int lineNo = 0;
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            lineNo++;
            var um = Regex.Match(line, @"^##\s+(?:Unit\s+(\d+)|第\s*(\d+)\s*单元)(?:\s|$)", RegexOptions.IgnoreCase);
            if (um.Success)
            {
                unit = int.TryParse(um.Groups[1].Success ? um.Groups[1].Value : um.Groups[2].Value, out var u) ? u : 0;
                continue;
            }
            if (unit == 0 || !line.Contains('|')) continue;

            var cols = line.Trim().Trim('|').Split('|');
            if (cols.Length < 2) continue;
            var rawEn = cols[0].Trim();
            var zh = cols[1].Trim();
            // 跳过表头与分隔行
            if (string.IsNullOrEmpty(rawEn) || string.IsNullOrEmpty(zh)) continue;
            if (Regex.IsMatch(rawEn, @"^-{2,}$")) continue;
            if (rawEn == "单词" || rawEn == "英文") continue;

            var entry = BuildEntry(rawEn, zh, grade, unit);
            if (entry is not null) bank.Entries.Add(entry);
        }
        return bank;
    }

    /// <summary>导入预览：逐行解析并给出行号/原因，不入库。</summary>
    public ParsePreview Preview(string sourcePath, out string? utf8CopyPath)
    {
        utf8CopyPath = null;
        var (text, detected, needTranscode) = EncodingHelper.ReadWordBankFile(sourcePath);
        var preview = new ParsePreview
        {
            FileName = Path.GetFileName(sourcePath),
            Grade = Path.GetFileNameWithoutExtension(sourcePath),
            NeedsTranscode = needTranscode,
            DetectedEncoding = detected
        };

        int unit = 0;
        int lineNo = 0;
        var units = new HashSet<int>();
        foreach (var line in text.Split('\n'))
        {
            lineNo++;
            var raw = line.TrimEnd('\r');
            var um = Regex.Match(raw, @"^##\s+(?:Unit\s+(\d+)|第\s*(\d+)\s*单元)(?:\s|$)", RegexOptions.IgnoreCase);
            if (um.Success)
            {
                unit = int.TryParse(um.Groups[1].Success ? um.Groups[1].Value : um.Groups[2].Value, out var u) ? u : 0;
                if (unit > 0) units.Add(unit);
                continue;
            }
            if (!raw.Contains('|')) continue;

            // 全角竖线检测
            if (raw.Contains('｜'))
            {
                preview.FailedLines.Add(new ParsedLine { LineNumber = lineNo, RawEnglish = raw, IsValid = false, ErrorReason = "使用了全角竖线｜，应为半角 |" });
                continue;
            }
            var cols = raw.Trim().Trim('|').Split('|');
            if (cols.Length < 2)
            {
                preview.FailedLines.Add(new ParsedLine { LineNumber = lineNo, RawEnglish = raw, IsValid = false, ErrorReason = "列数不足两列" });
                continue;
            }
            var rawEn = cols[0].Trim();
            var zh = cols[1].Trim();
            if (string.IsNullOrEmpty(rawEn) || string.IsNullOrEmpty(zh))
            {
                preview.FailedLines.Add(new ParsedLine { LineNumber = lineNo, RawEnglish = rawEn, RawChinese = zh, IsValid = false, ErrorReason = "英文或中文为空" });
                continue;
            }
            if (Regex.IsMatch(rawEn, @"^-{2,}$") || rawEn == "单词" || rawEn == "英文") continue; // 表头/分隔行
            if (unit == 0)
            {
                preview.FailedLines.Add(new ParsedLine { LineNumber = lineNo, RawEnglish = rawEn, IsValid = false, ErrorReason = "该行不在任何 ## Unit N 分段下" });
                continue;
            }

            var entry = BuildEntry(rawEn, zh, preview.Grade, unit, preview.PendingBracketItems);
            if (entry is null)
            {
                preview.FailedLines.Add(new ParsedLine { LineNumber = lineNo, RawEnglish = rawEn, IsValid = false, ErrorReason = "无法解析" });
                continue;
            }
            preview.Entries.Add(entry);
        }

        preview.UnitCount = units.Count;
        preview.ValidCount = preview.Entries.Count;
        preview.InvalidCount = preview.FailedLines.Count;
        return preview;
    }

    /// <summary>根据原始英文+中文构造词条：括号解析、分类、音节取值链全走统一流程（新增词条同样调用它）。</summary>
    private WordEntry? BuildEntry(string rawEn, string zh, string grade, int unit, List<string>? pending = null)
    {
        var mainAnswer = BracketParser.Parse(rawEn,
            out var aliases, out var displayOnly, out var hint, out var pendingItems);
        if (string.IsNullOrEmpty(mainAnswer)) return null;
        if (pending is not null) pending.AddRange(pendingItems);

        var entry = new WordEntry
        {
            OriginalEnglish = rawEn,
            CurrentEnglish = mainAnswer,
            Chinese = zh,
            Aliases = aliases,
            DisplayOnlyAliases = displayOnly,
            AliasHint = hint,
            Grade = grade,
            Unit = unit,
            Category = BracketParser.Classify(mainAnswer)
        };

        // 音节初始化：与内置/推导走同一 SyllableService
        var (segments, pendingSyl) = _syllables.Get(entry);
        entry.Syllables = segments;
        entry.SyllablePending = pendingSyl;
        return entry;
    }

    // 音节服务（延迟注入，避免构造顺序问题）
    private SyllableService _syllables = null!;
    public void UseSyllableService(SyllableService svc) => _syllables = svc;

    /// <summary>确认入库：把预览词条写入用户目录对应年级 md（覆盖/追加）。</summary>
    public void Commit(ParsePreview preview, bool overwrite, string? utf8CopyPath = null)
    {
        var source = utf8CopyPath ?? preview.FileName;
        var target = Path.Combine(AppPaths.WordBankDir, Path.GetFileName(source));

        if (File.Exists(target) && !overwrite) return;

        // 按词库原格式导出（## Unit N + 两列），原文中文与括号原串保留
        var lines = new List<string>();
        foreach (var unit in preview.Entries.Select(e => e.Unit).Distinct().OrderBy(x => x))
        {
            lines.Add($"## Unit {unit}");
            lines.Add("");
            lines.Add("| 单词 | 中文 |");
            lines.Add("|---|---|");
            foreach (var e in preview.Entries.Where(e => e.Unit == unit))
                lines.Add($"| {e.OriginalEnglish} | {e.Chinese} |");
            lines.Add("");
        }
        File.WriteAllText(target, string.Join("\n", lines), new UTF8Encoding(false));
    }

    /// <summary>导出当前词库为 md（仅词库文字，不含进度/修正/设置）。</summary>
    public string ExportWordBankText(WordBank bank)
    {
        var lines = new List<string>();
        foreach (var unit in bank.Units)
        {
            lines.Add($"## Unit {unit}");
            lines.Add("");
            lines.Add("| 单词 | 中文 |");
            lines.Add("|---|---|");
            foreach (var e in bank.Entries.Where(e => e.Unit == unit))
                lines.Add($"| {e.OriginalEnglish} | {e.Chinese} |");
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    /// <summary>筛选：按年级、单元（0=全部单元）、隐藏句子类开关过滤列表展示。</summary>
    public List<WordEntry> Filter(List<WordBank> banks, string grade, int unit, bool hideSentences)
    {
        var bank = banks.FirstOrDefault(b => b.Grade == grade);
        if (bank is null) return new List<WordEntry>();
        return bank.Entries
            .Where(e => (unit == 0 || e.Unit == unit))
            .Where(e => !(hideSentences && e.IsPhrase))
            .ToList();
    }
}
