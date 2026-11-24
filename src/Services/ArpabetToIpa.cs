using System.Text;
using System.Text.Json;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// ARPAbet -> IPA 转换：把 CMU 音素（如 HH AH0 DH ER1）转成 SSML phoneme 可用的 IPA 串。
/// 重音数字 1/2 转 IPA 重音符号 ˈ/ˌ，0 不标；AH0/ER0 用弱读值 ə/ɚ。
/// 映射表来自内置资源 arpabet-to-ipa.json；phoneme 内容永不显示给孩子。
/// </summary>
public static class ArpabetToIpa
{
    private static readonly Dictionary<string, string> Segments = new();
    private static readonly Dictionary<string, string> Stress = new();
    private static readonly Dictionary<string, string> Weak = new();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        var json = ResourceReader.ReadArpabetIpa();
        if (!string.IsNullOrEmpty(json))
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("segments", out var seg))
                foreach (var p in seg.EnumerateObject()) Segments[p.Name] = p.Value.GetString() ?? "";
            if (root.TryGetProperty("stressMarks", out var st))
                foreach (var p in st.EnumerateObject()) Stress[p.Name] = p.Value.GetString() ?? "";
            if (root.TryGetProperty("weakVowels", out var wk))
                foreach (var p in wk.EnumerateObject()) Weak[p.Name] = p.Value.GetString() ?? "";
        }
        _loaded = true;
    }

    /// <summary>把 ARPAbet 音素串转 IPA；任一段缺映射返回 null（调用方据此静默回退整词朗读）。</summary>
    public static string? Convert(string arpabet)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(arpabet)) return null;

        var sb = new StringBuilder();
        foreach (var token in arpabet.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // 分离音素本体与末尾重音数字
            var letters = new string(token.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            var digit = token.Length > 0 && char.IsDigit(token[^1]) ? token[^1].ToString() : "0";

            // 弱读元音优先（AH0 -> ə）
            string? ipa;
            if (digit == "0" && Weak.TryGetValue(letters + "0", out var weak)) ipa = weak;
            else if (Segments.TryGetValue(letters, out var seg)) ipa = seg;
            else return null; // 映射缺项

            // 重音符号（0 不标）
            if (Stress.TryGetValue(digit, out var mark) && !string.IsNullOrEmpty(mark))
                sb.Append(mark);
            sb.Append(ipa);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
