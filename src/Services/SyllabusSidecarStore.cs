using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>音节 sidecar（syllables.json）持久化包装：SyllableService 从这里读写用户手动修正。</summary>
public sealed class SyllabusSidecarStore
{
    private SyllableData _data;
    private readonly List<string> _notices = new();

    public SyllabusSidecarStore()
    {
        var (data, notice) = JsonFileStore.Load<SyllableData>(AppPaths.SyllablesFile);
        _data = data;
        if (!string.IsNullOrEmpty(notice)) _notices.Add(notice);
    }

    public IReadOnlyList<string> Notices => _notices;
    public SyllableData Data => _data;

    public void Save() => JsonFileStore.Save(AppPaths.SyllablesFile, _data);

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

    /// <summary>复合键迁移（改英文/调单元共用），只替换变化段，撞名不覆盖。</summary>
    public (bool Ok, string Message) MigrateKey(string oldKey, int? newUnit, string? newEnglish)
    {
        if (!_data.Entries.ContainsKey(oldKey)) return (true, "无修正记录");
        var newKey = KeyBuilder.Migrate(oldKey, newUnit, newEnglish);
        if (newKey == oldKey) return (true, "键未变化");
        if (_data.Entries.ContainsKey(newKey)) return (false, "目标已有同名词条修正，禁止覆盖");
        var v = _data.Entries[oldKey];
        _data.Entries.Remove(oldKey);
        _data.Entries[newKey] = v;
        Save();
        return (true, "音节修正键已迁移");
    }

    public int ActiveCount => _data.Entries.Count;
}
