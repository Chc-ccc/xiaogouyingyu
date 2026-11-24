using System.Text.Json;

namespace XiaoGouYingYu.Services;

/// <summary>CMU 精简词典查询包装：按小写英文取 ARPAbet 音素串，找不到返回 null（调用方走回退）。</summary>
public static class CmuDictionary
{
    private static readonly Dictionary<string, string> Entries = new();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        var json = ResourceReader.ReadCmuDict();
        if (!string.IsNullOrEmpty(json))
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("entries", out var obj))
                foreach (var p in obj.EnumerateObject())
                    Entries[p.Name] = p.Value.GetString() ?? "";
        }
        _loaded = true;
    }

    public static string? Lookup(string english)
    {
        if (string.IsNullOrWhiteSpace(english)) return null;
        EnsureLoaded();
        // 去掉句末标点与非字母后取键
        var key = new string(english.ToLowerInvariant()
            .TakeWhile(c => char.IsLetter(c) || c == ' ').ToArray()).Trim();
        return Entries.TryGetValue(key, out var v) ? v : null;
    }
}
