using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 词条索引服务（wordbank-index.json）：持久化「原始英文 / 当前英文」。
/// 原始英文一经入库永不改写——即使改英文、调单元、重新导入都保留，否则内置音节表的原始键查询会失效。
/// 重新导入同名词库时按索引里的旧「当前英文」匹配并更新当前英文、保留原始英文；匹配不到才按新词条处理。
/// </summary>
public sealed class WordbankIndexService
{
    private WordbankIndexData _data;

    public WordbankIndexService()
    {
        var (data, notice) = JsonFileStore.Load<WordbankIndexData>(AppPaths.WordbankIndexFile);
        _data = data;
        if (!string.IsNullOrEmpty(notice)) System.Diagnostics.Debug.WriteLine(notice);
    }

    /// <summary>入库新词条：原始英文=当前英文=主答案。</summary>
    public void AddNew(WordEntry entry)
    {
        var key = entry.Key;
        if (_data.Entries.ContainsKey(key)) return;
        _data.Entries[key] = new IndexEntry
        {
            OriginalEnglish = entry.OriginalEnglish,
            CurrentEnglish = entry.CurrentEnglish,
            Grade = entry.Grade,
            Unit = entry.Unit,
            Category = entry.Category
        };
        Save();
    }

    /// <summary>按复合键取索引；没有返回 null。</summary>
    public IndexEntry? Get(string key) => _data.Entries.TryGetValue(key, out var v) ? v : null;

    /// <summary>取原始英文（供音节按原始键查）；找不到时回退用当前英文主答案。</summary>
    public string GetOriginalEnglish(string key, string fallback) => Get(key)?.OriginalEnglish ?? fallback;

    /// <summary>
    /// 改英文：迁移索引键，但 OriginalEnglish 保持原值不变（关键——内置音节原始键不失效）。
    /// 撞名检测由调用方在目标单元内先做（本方法不跨单元扫描）。
    /// </summary>
    public (bool Ok, string Message) ChangeEnglish(string oldKey, int unit, string newMainAnswer)
    {
        if (!_data.Entries.TryGetValue(oldKey, out var idx))
            return (true, "无索引记录，跳过迁移");
        var newKey = KeyBuilder.Build(idx.Grade, unit, newMainAnswer);
        if (newKey == oldKey) return (true, "键未变化");
        if (_data.Entries.ContainsKey(newKey)) return (false, "目标单元已有同名词条，禁止覆盖");

        _data.Entries.Remove(oldKey);
        idx.CurrentEnglish = newMainAnswer;
        idx.Unit = unit;
        // idx.OriginalEnglish 保持不变！
        _data.Entries[newKey] = idx;
        Save();
        return (true, "索引已迁移，原始英文保留");
    }

    /// <summary>调整所属单元：迁移键，原始英文不变。</summary>
    public (bool Ok, string Message) ChangeUnit(string oldKey, int newUnit)
    {
        if (!_data.Entries.TryGetValue(oldKey, out var idx))
            return (true, "无索引记录，跳过迁移");
        var newKey = KeyBuilder.Build(idx.Grade, newUnit, idx.CurrentEnglish);
        if (newKey == oldKey) return (true, "键未变化");
        if (_data.Entries.ContainsKey(newKey)) return (false, "目标单元已有同名词条，禁止覆盖");

        _data.Entries.Remove(oldKey);
        idx.Unit = newUnit;
        _data.Entries[newKey] = idx;
        Save();
        return (true, "索引单元已迁移");
    }

    /// <summary>
    /// 覆盖导入时的键对齐：按「年级+Unit+主答案」逐条比对。
    /// 匹配上保留记录；被删除的旧键移入 retired 备查（不物理删除）。
    /// 返回被 retire 的键数量。
    /// </summary>
    public int Reconcile(string grade, HashSet<string> activeKeys)
    {
        int retired = 0;
        var toRetire = _data.Entries.Where(kv => kv.Value.Grade == grade && !activeKeys.Contains(kv.Key)).ToList();
        foreach (var kv in toRetire)
        {
            _data.Retired[kv.Key] = kv.Value;
            _data.Entries.Remove(kv.Key);
            retired++;
        }
        if (retired > 0) Save();
        return retired;
    }

    public void Retire(string key)
    {
        if (_data.Entries.TryGetValue(key, out var v))
        {
            _data.Retired[key] = v;
            _data.Entries.Remove(key);
            Save();
        }
    }

    public bool Restore(string key)
    {
        if (!_data.Retired.TryGetValue(key, out var v)) return false;
        _data.Entries[key] = v;
        _data.Retired.Remove(key);
        Save();
        return true;
    }

    public int ClearRetired()
    {
        int n = _data.Retired.Count;
        _data.Retired.Clear();
        if (n > 0) Save();
        return n;
    }

    public int ActiveCount => _data.Entries.Count;
    public int RetiredCount => _data.Retired.Count;

    private void Save() => JsonFileStore.Save(AppPaths.WordbankIndexFile, _data);
}
