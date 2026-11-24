using System.Globalization;
using System.Text;
using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 默写/看中文背英文两类输入模式共用的判定归一化函数（唯一权威七步定义）。
/// 对应 <word_bank_rules> 第二条；README 给出判对示例表。
///
/// 七步：
/// ① 去首尾空白，把所有 Unicode 空白（U+00A0/制表符/全角空格 U+3000 等）折叠为普通空格；
/// ② 内部连续空白合并为单个；
/// ③ 英文弯引号 ‘ ’ “ ” 统一为直引号 ' "；
/// ④ 去除句末标点（. ! ? 。 ！ ？），句中标点（逗号、撇号）保留；
/// ⑤ 连字符 - 与短破折号 – 统一为半角 -，但不删除；
/// ⑥ 整体转小写；
/// ⑦ 与「主答案 + 别名数组」逐项比对，命中任一即判对（中文释义不参与判定）。
/// </summary>
public static class NormalizationService
{
    private static readonly char[] EndPunctuation = { '.', '!', '?', '。', '！', '？' };

    /// <summary>执行七步归一化的前六步，返回可比对的规范串。</summary>
    public static string Normalize(string input)
    {
        if (input is null) return "";

        var sb = new StringBuilder(input.Length);

        // ①② 逐字符处理：所有 Unicode 空白先折叠为单个普通空格，并合并连续空白
        bool lastWasSpace = true; // 初始为 true，顺带吃掉行首空白
        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
                continue;
            }
            lastWasSpace = false;

            // ③ 弯引号 -> 直引号
            string mapped = c switch
            {
                '\u2018' or '\u2019' => "'",  // ‘ ’
                '\u201c' or '\u201d' => "\"", // “ ”
                '\u2013' or '\u2212' or '\uFF0D' => "-", // – − － -> -
                _ => c.ToString()
            };
            sb.Append(mapped);
        }
        // 结尾若为空格则去掉（合并逻辑会在末尾留一个空格）
        if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;

        var result = sb.ToString();

        // ④ 去句末标点（可有多个连续，如 "really?!"）
        result = result.TrimEnd(EndPunctuation).TrimEnd();

        // ⑥ 整体转小写（用当前文化不敏感的小写，避免土耳其语 I 问题）
        result = result.ToLower(CultureInfo.InvariantCulture);

        return result;
    }

    /// <summary>
    /// 第七步：把孩子输入与「主答案 + 别名数组」逐项比对，命中任一即判对。
    /// 中文释义完全不参与判定。
    /// </summary>
    public static bool IsCorrect(string input, WordEntry entry)
    {
        var normalizedInput = Normalize(input);
        if (string.IsNullOrEmpty(normalizedInput)) return false;

        var candidates = new List<string> { entry.CurrentEnglish };
        candidates.AddRange(entry.Aliases);

        foreach (var candidate in candidates)
        {
            if (string.Equals(normalizedInput, Normalize(candidate), StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
