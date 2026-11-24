using System.Text.RegularExpressions;
using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 括号注释通用解析 + 主答案/别名拆分。对当前和未来所有词库通用，不枚举当前例子。
///
/// 规则（对应 <word_bank_rules> 第一条）：
/// 1) 主答案 = 第一个括号前部分并 trim；卡片不显示原样括号串。
/// 2) 扫描每对括号，先剥离标记前缀（AmE/BrE/pl.，大小写不敏感），剩余是"别名本体"。
/// 3) AmE/BrE/无标记别名 -> 本体进"可判对别名数组"（与去句末标点后本体相同的除外，如 Mr.）。
/// 4) pl. 复数提示 -> 本体仅展示提示，不判对。
/// 5) 分号/嵌套/无法识别 -> 整体列入"待确认项"，不猜。
/// </summary>
public static class BracketParser
{
    // 标记前缀：AmE / BrE / pl.（后面跟空白），大小写不敏感
    private static readonly Regex MarkerPrefix =
        new(@"^(AmE|BrE|pl\.)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class ParsedBracket
    {
        public string Content { get; init; } = "";
        public string Marker { get; init; } = "";   // "AmE" / "BrE" / "pl." / ""
        public string Body { get; init; } = "";     // 剥离标记后的别名本体
        public bool Recognized { get; init; }
    }

    /// <summary>解析一条英文：返回主答案；out 可判对别名、仅展示别名、提示文案、待确认项。</summary>
    public static string Parse(
        string rawEnglish,
        out List<string> aliases,
        out List<string> displayOnly,
        out string hint,
        out List<string> pendingConfirmation)
    {
        aliases = new List<string>();
        displayOnly = new List<string>();
        hint = "";
        pendingConfirmation = new List<string>();

        if (string.IsNullOrEmpty(rawEnglish)) return "";

        // 主答案：第一个左括号之前的部分（用 Regex 找第一个 '('）
        int firstParen = rawEnglish.IndexOf('(');
        string mainAnswer = (firstParen >= 0 ? rawEnglish[..firstParen] : rawEnglish).Trim();

        if (firstParen < 0) return mainAnswer; // 无括号，直接返回

        // 逐个解析括号（支持一对，也支持多对；嵌套按最外层整体处理）
        var brackets = ExtractBrackets(rawEnglish);
        foreach (var b in brackets)
        {
            if (!b.Recognized || b.Body.Contains(';') || b.Body.Contains('('))
            {
                // 分号/嵌套/无法识别：待确认，不猜
                pendingConfirmation.Add($"({b.Content})");
                continue;
            }

            switch (b.Marker.ToLowerInvariant())
            {
                case "pl.":
                    // 复数仅提示，不判对
                    displayOnly.Add(b.Body);
                    break;

                case "ame":
                case "bre":
                    // 英美拼写差异：加入可判对别名；AmE 时给小字提示
                    AddAliasIfMeaningful(b.Body, mainAnswer, aliases);
                    if (b.Marker.Equals("AmE", StringComparison.OrdinalIgnoreCase))
                        hint = $"美式常写 {b.Body}";
                    break;

                default:
                    // 无标记的人名/称呼别名：加入可判对别名
                    AddAliasIfMeaningful(b.Body, mainAnswer, aliases);
                    break;
            }
        }

        return mainAnswer;
    }

    /// <summary>判断词条分类：先按主答案判定（含空格或句末标点 -> 短语/句子类）。</summary>
    public static WordCategory Classify(string mainAnswer)
        => Regex.IsMatch(mainAnswer, @"[\s.!?。！？]") ? WordCategory.Phrase : WordCategory.Word;

    /// <summary>把别名加入数组；若别名去掉句末标点后与主答案相同（如 Mr. vs Mr），仍加入以确保两者都判对。</summary>
    private static void AddAliasIfMeaningful(string body, string mainAnswer, List<string> aliases)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        var value = body.Trim();
        if (!aliases.Contains(value, StringComparer.OrdinalIgnoreCase))
            aliases.Add(value);
    }

    /// <summary>提取原始英文中的所有括号内容（简单配对，不支持嵌套；嵌套交待确认）。</summary>
    private static List<ParsedBracket> ExtractBrackets(string text)
    {
        var result = new List<ParsedBracket>();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '(') { i++; continue; }
            int close = text.IndexOf(')', i + 1);
            if (close < 0) break;
            string content = text[(i + 1)..close];

            var m = MarkerPrefix.Match(content);
            string marker = m.Success ? m.Groups[1].Value : "";
            string body = (m.Success ? content[m.Length..] : content).Trim();

            bool recognized = !string.IsNullOrEmpty(body)
                              && !content.Contains('(')
                              && !content.Contains(';');

            result.Add(new ParsedBracket
            {
                Content = content,
                Marker = marker,
                Body = body,
                Recognized = recognized
            });
            i = close + 1;
        }
        return result;
    }
}
