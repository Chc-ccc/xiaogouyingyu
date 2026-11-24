using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 音节服务：音节一律指「字形段」（mother -> mo-ther），不是音素段、不是音标。
///
/// 取值链（层级严格，只在其上一层无有效值时生效，不因高层缺一段就丢弃整词其余段）：
/// ① 用户 sidecar 修正值（syllables.json，按当前复合键；若某段被清空/改坏视为无修正继续下一层）
/// ② 内置校准音节（syllables.builtin.json，按 OriginalEnglish 原始键查，永不改写）
/// ③ 按 CMU 词典 / 自然拼读规则现场推导（音素切分 -> 映射回拼写字母段 -> 实测）
/// ④ 仍无把握 -> 待补充（词条仍可学，拼读退化为整词朗读）
///
/// 改英文后的查询顺序：sidecar 新键优先 -> 内置原始英文键（不能用新英文去内置表硬查）。
/// </summary>
public class SyllableService
{
    private readonly Dictionary<string, List<string>> _builtin = new();
    private readonly Dictionary<string, string> _cmu = new();
    private readonly Func<SyllableData> _sidecarProvider;
    private readonly Func<string, int, string, string> _keyBuilder;

    public SyllableService(Func<SyllableData> sidecarProvider, Func<string, int, string, string> keyBuilder)
    {
        _sidecarProvider = sidecarProvider;
        _keyBuilder = keyBuilder;
        LoadBuiltin();
        LoadCmu();
    }

    /// <summary>取某词的字形段数组；返回 (分段, 是否待补充)。</summary>
    public (List<string> Segments, bool Pending) Get(WordEntry entry)
    {
        // 短语/句子类：按单词粒度切分，不拆音节
        if (entry.IsPhrase)
        {
            var words = Regex.Matches(entry.CurrentEnglish, @"[\w']+")
                             .Select(m => m.Value).ToList();
            return words.Count > 0 ? (words, false) : (new List<string> { entry.CurrentEnglish }, false);
        }

        var currentKey = _keyBuilder(entry.Grade, entry.Unit, entry.CurrentEnglish);
        var sidecar = _sidecarProvider();

        // ① 用户 sidecar 修正（当前键）
        if (sidecar.Entries.TryGetValue(currentKey, out var userValue) && IsValid(userValue))
            return (new List<string>(userValue), false);

        // ② 内置校准音节（按原始英文 + 原始单元的键查，永不改写）
        //    改英文后 CurrentEnglish 变了，但 OriginalEnglish 不变，故用原始键查内置表
        string originalKey = _keyBuilder(entry.Grade, entry.Unit, entry.OriginalEnglish);
        if (_builtin.TryGetValue(originalKey, out var builtinValue) && IsValid(builtinValue))
            return (new List<string>(builtinValue), false);

        // ③ 现场推导：CMU 音素切分 -> 映射回拼写字母段
        var derived = DeriveFromCmuOrPhonics(entry.CurrentEnglish);
        if (derived is { Count: > 0 }) return (derived, false);

        // ④ 待补充
        return (new List<string> { AppConfig.SyllablePending }, true);
    }

    /// <summary>该词是否在「更多」入口的待补充清单中。</summary>
    public bool IsPending(WordEntry entry) => Get(entry).Pending;

    /// <summary>保存用户手动修正到 sidecar（由调用方随后 Save）。</summary>
    public void SetUserCorrection(SyllableData sidecar, WordEntry entry, List<string> segments)
    {
        var key = _keyBuilder(entry.Grade, entry.Unit, entry.CurrentEnglish);
        sidecar.Entries[key] = segments;
    }

    /// <summary>
    /// CMU 三步推导：①音素切分 -> ②映射回拼写字母段 -> ③（TTS 实测由语音服务完成）。
    /// 本方法只做前两步的保守推导：只有当能可靠地把音素音节对齐到拼写段时才返回，
    /// 否则返回 null 交"待补充"。绝不把 ARPAbet 音素串直接当字形段显示。
    /// </summary>
    private List<string>? DeriveFromCmuOrPhonics(string english)
    {
        var key = english.ToLowerInvariant();
        if (!_cmu.TryGetValue(key, out var arpabet)) return null;

        // ① 按元音音素切分音素音节（含重音数字）
        var phonemes = arpabet.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vowelSet = new HashSet<string>
        {
            "AA","AE","AH","AO","AW","AY","EH","ER","EY","IH","IY","OW","OY","UH","UW"
        };
        var groups = new List<List<string>>();
        var current = new List<string>();
        foreach (var p in phonemes)
        {
            var baseP = new string(p.Where(char.IsLetter).ToArray());
            current.Add(p);
            if (vowelSet.Contains(baseP))
            {
                groups.Add(current);
                current = new List<string>();
            }
        }
        if (current.Count > 0)
        {
            if (groups.Count > 0) groups[^1].AddRange(current); // 尾辅音并入最后音节
            else return null;
        }

        // ② 映射回拼写字母段：对导入新词，用"元音字母簇"作为保守的拼写切分点。
        //    这是近似的自然拼读回退；只有得到的段数 == 音素音节数时才认为对齐成功。
        var spellingSegments = SplitByVowelClusters(english);
        if (spellingSegments.Count == groups.Count)
            return spellingSegments;

        // 不能可靠对齐 -> 待补充（不臆造）
        return null;
    }

    /// <summary>按元音字母簇把拼写切成字形段（自然拼读回退，保守策略）。</summary>
    private static List<string> SplitByVowelClusters(string word)
    {
        var letters = Regex.Matches(word, @"[A-Za-z]+").Select(m => m.Value).ToList();
        var source = string.Concat(letters);
        if (source.Length == 0) return new List<string>();

        var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u', 'y' };
        var segments = new List<string>();
        var buf = new StringBuilder();
        bool inVowel = false;
        bool seenVowel = false;
        foreach (char c in source)
        {
            bool isVowel = vowels.Contains(char.ToLowerInvariant(c));
            if (seenVowel && isVowel && !inVowel)
            {
                // 新的一段从辅音开始：在元音簇前的辅音处切
                segments.Add(buf.ToString());
                buf.Clear();
            }
            buf.Append(c);
            inVowel = isVowel;
            if (isVowel) seenVowel = true;
        }
        if (buf.Length > 0) segments.Add(buf.ToString());
        return segments;
    }

    /// <summary>校验 sidecar 修正值有效（非空、且不含待补充标记）。</summary>
    private static bool IsValid(List<string>? value)
        => value is { Count: > 0 } && value.All(s => !string.IsNullOrWhiteSpace(s) && s != AppConfig.SyllablePending);

    private void LoadBuiltin()
    {
        var json = ResourceReader.ReadSyllablesBuiltin();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var arr)) return;
            foreach (var item in arr.EnumerateArray())
            {
                var grade = item.GetProperty("grade").GetString() ?? "";
                var unit = item.GetProperty("unit").GetInt32();
                var original = item.GetProperty("originalEnglish").GetString() ?? "";
                var segs = item.GetProperty("syllables").EnumerateArray()
                               .Select(x => x.GetString() ?? "").ToList();
                _builtin[_keyBuilder(grade, unit, original)] = segs;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("内置音节加载失败: " + ex.Message);
        }
    }

    private void LoadCmu()
    {
        var json = ResourceReader.ReadCmuDict();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var obj)) return;
            foreach (var prop in obj.EnumerateObject())
                _cmu[prop.Name] = prop.Value.GetString() ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CMU 词典加载失败: " + ex.Message);
        }
    }
}
