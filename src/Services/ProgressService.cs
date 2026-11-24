using XiaoGouYingYu.Models;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 进度服务：三态（新词/待复习/已掌握）+ 连对计数 + 复合键迁移。
///
/// 三态流转（对应 <architecture> 第十二条）：
/// ① 新词答错 -> 待复习；② 待复习答对连续 2 次 -> 转回新词，答错留待复习并归零；
/// ③ 已掌握仅手动退回；④ 连对计数按词条持久化，切筛选/重启不清零。
/// 「已掌握」只能由点「标记掌握」触发，不由答对次数自动判定。
/// </summary>
public sealed class ProgressService
{
    private ProgressData _data;
    private readonly List<string> _notices = new();

    public ProgressService()
    {
        var (data, notice) = JsonFileStore.Load<ProgressData>(AppPaths.ProgressFile);
        _data = data;
        if (!string.IsNullOrEmpty(notice)) _notices.Add(notice);
    }

    public IReadOnlyList<string> Notices => _notices;

    /// <summary>取某词进度（不存在则按新词）。</summary>
    public ProgressEntry Get(string key)
        => _data.Entries.TryGetValue(key, out var p) ? p : new ProgressEntry { Status = WordStatus.New };

    public WordStatus GetStatus(string key) => Get(key).Status;

    /// <summary>答对：待复习连对+1，达到阈值转回新词并归零；其他态不改变（已掌握不自动流转）。</summary>
    public void MarkCorrect(string key)
    {
        if (!_data.Entries.TryGetValue(key, out var p))
        {
            // 新词答对：无记录即可，不落库（避免数据膨胀）
            return;
        }
        if (p.Status != WordStatus.Review) return;
        p.CorrectStreak++;
        p.LastPracticed = DateTime.Now;
        if (p.CorrectStreak >= AppConfig.CorrectStreakToReviewOut)
        {
            p.Status = WordStatus.New;
            p.CorrectStreak = 0;
        }
        Save();
    }

    /// <summary>答错：新词/待复习 -> 待复习并归零；已掌握不因答错自动退回。</summary>
    public void MarkWrong(string key)
    {
        if (!_data.Entries.TryGetValue(key, out var p))
        {
            _data.Entries[key] = new ProgressEntry
            {
                Status = WordStatus.Review,
                CorrectStreak = 0,
                LastPracticed = DateTime.Now
            };
            Save();
            return;
        }
        if (p.Status == WordStatus.Mastered) return; // 已掌握不自动退回
        p.Status = WordStatus.Review;
        p.CorrectStreak = 0;
        p.LastPracticed = DateTime.Now;
        Save();
    }

    /// <summary>点「标记掌握」：置为已掌握并归零连对。</summary>
    public void MarkMastered(string key)
    {
        var p = _data.Entries.TryGetValue(key, out var existing) ? existing : new ProgressEntry();
        p.Status = WordStatus.Mastered;
        p.CorrectStreak = 0;
        p.LastPracticed = DateTime.Now;
        _data.Entries[key] = p;
        Save();
    }

    /// <summary>手动设置状态（更多入口的状态列表操作），连对归零。</summary>
    public void SetStatus(string key, WordStatus status)
    {
        var p = _data.Entries.TryGetValue(key, out var existing) ? existing : new ProgressEntry();
        p.Status = status;
        p.CorrectStreak = 0;
        _data.Entries[key] = p;
        Save();
    }

    /// <summary>ensureEntry：为词条建立新词记录（导入/播种后调用，便于统计口径一致）。</summary>
    public void EnsureEntry(string key)
    {
        if (!_data.Entries.ContainsKey(key))
            _data.Entries[key] = new ProgressEntry { Status = WordStatus.New };
    }

    /// <summary>
    /// 复合键迁移：「修改英文」与「调整所属单元」共用——只替换变化的那一段，年级不变。
    /// 撞名不覆盖：若 newKey 已存在则拒绝迁移（返回 false），由 UI 提示用户。
    /// </summary>
    public (bool Ok, string Message) MigrateKey(string oldKey, int? newUnit, string? newEnglish)
    {
        if (!_data.Entries.ContainsKey(oldKey)) return (true, "无需迁移：无进度记录");
        var newKey = KeyBuilder.Migrate(oldKey, newUnit, newEnglish);
        if (newKey == oldKey) return (true, "键未变化");
        if (_data.Entries.ContainsKey(newKey)) return (false, "目标位置已有同名词条记录，禁止覆盖");

        var entry = _data.Entries[oldKey];
        _data.Entries.Remove(oldKey);
        _data.Entries[newKey] = entry;
        Save();
        return (true, "进度键已迁移");
    }

    /// <summary>删除词条：进度移入 retired 区（不物理删除，便于误删恢复）。</summary>
    public void Retire(string key)
    {
        if (_data.Entries.TryGetValue(key, out var p))
        {
            _data.Retired[key] = p;
            _data.Entries.Remove(key);
            Save();
        }
    }

    /// <summary>恢复 retired 记录。</summary>
    public bool Restore(string key)
    {
        if (!_data.Retired.TryGetValue(key, out var p)) return false;
        _data.Entries[key] = p;
        _data.Retired.Remove(key);
        Save();
        return true;
    }

    /// <summary>清理 retired（更多入口显式调用）。</summary>
    public int ClearRetired()
    {
        int n = _data.Retired.Count;
        _data.Retired.Clear();
        if (n > 0) Save();
        return n;
    }

    /// <summary>统计（按当前筛选范围由调用方传入 key 集合）。</summary>
    public (int New, int Review, int Mastered) CountBy(IEnumerable<string> keys)
    {
        int n = 0, r = 0, m = 0;
        foreach (var k in keys)
        {
            switch (GetStatus(k))
            {
                case WordStatus.New: n++; break;
                case WordStatus.Review: r++; break;
                case WordStatus.Mastered: m++; break;
            }
        }
        return (n, r, m);
    }

    public int ActiveCount => _data.Entries.Count;
    public int RetiredCount => _data.Retired.Count;
    public IReadOnlyDictionary<string, ProgressEntry> AllEntries => _data.Entries;

    private void Save() => JsonFileStore.Save(AppPaths.ProgressFile, _data);
}
