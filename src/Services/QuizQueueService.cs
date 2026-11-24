using XiaoGouYingYu.Models;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 练习队列服务：一轮练习按三步确定（对应 <architecture> 第十二条）。
/// ① 筛选范围：当前年级+单元选择（"全部单元"取该年级全部）；
/// ② 排除规则：已掌握默认不进普通队列；句子类按"跳过句子类默写"开关仅从默写队列排除；
/// ③ 排序：默认词库原顺序，或按 AppConfig.ShuffleSeed 固定种子乱序（同一批可复现）。
/// </summary>
public static class QuizQueueService
{
    /// <summary>构建练习队列。</summary>
    public static List<WordEntry> Build(
        IEnumerable<WordEntry> scope,
        LearningMode mode,
        WordStatusMap statusMap,
        SettingsData settings)
    {
        var list = new List<WordEntry>();
        foreach (var e in scope)
        {
            var status = statusMap.TryGetStatus(e.Key, out var s) ? s : WordStatus.New;
            if (status == WordStatus.Mastered) continue; // 已掌握不进普通队列

            // "跳过句子类默写"只作用于默写模式
            if (mode == LearningMode.Dictation && settings.SkipSentenceDictation && e.IsPhrase)
                continue;

            list.Add(e);
        }

        if (settings.ShuffleEnabled)
        {
            // 固定种子：同一批词打乱结果可复现，便于复盘（不按时间戳）
            var rng = new Random(AppConfig.ShuffleSeed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        return list;
    }
}

/// <summary>把复合键->状态映射包装成便于查询的扩展（避免每层都 new Dictionary）。</summary>
public sealed class WordStatusMap
{
    private readonly Dictionary<string, WordStatus> _map;
    public WordStatusMap(IReadOnlyDictionary<string, ProgressEntry> entries)
    {
        _map = new Dictionary<string, WordStatus>();
        foreach (var kv in entries) _map[kv.Key] = kv.Value.Status;
    }
    public bool TryGetStatus(string key, out WordStatus status) => _map.TryGetValue(key, out status);
}
