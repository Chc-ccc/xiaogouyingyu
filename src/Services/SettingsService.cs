using XiaoGouYingYu.Models;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 用户偏好设置服务：settings.json 用户值 -> AppConfig.cs 默认值的分层读取，写回只写 settings.json。
/// 慢速开关、手动选声、用户改过的查词 URL 等用户改动全在这里，不回写安装目录、不回写 AppConfig.cs。
/// </summary>
public sealed class SettingsService
{
    private SettingsData _data;
    private readonly List<string> _notices = new();

    public SettingsService()
    {
        var (data, notice) = JsonFileStore.Load<SettingsData>(AppPaths.SettingsFile);
        _data = data;
        if (!string.IsNullOrEmpty(notice)) _notices.Add(notice);
    }

    public IReadOnlyList<string> Notices => _notices;

    public bool SlowMode
    {
        get => _data.SlowMode;
        set { _data.SlowMode = value; Save(); }
    }

    public string SelectedVoiceName
    {
        get => _data.SelectedVoiceName;
        set { _data.SelectedVoiceName = value; Save(); }
    }

    public string LookupTemplate
    {
        get => string.IsNullOrEmpty(_data.LookupTemplate) ? AppConfig.LookupTemplateDefault : _data.LookupTemplate;
        set { _data.LookupTemplate = value; Save(); }
    }

    public bool SkipSentenceDictation
    {
        get => _data.SkipSentenceDictation;
        set { _data.SkipSentenceDictation = value; Save(); }
    }

    public bool HideSentencesInList
    {
        get => _data.HideSentencesInList;
        set { _data.HideSentencesInList = value; Save(); }
    }

    public bool ShuffleEnabled
    {
        get => _data.ShuffleEnabled;
        set { _data.ShuffleEnabled = value; Save(); }
    }

    private void Save() => JsonFileStore.Save(AppPaths.SettingsFile, _data);
}
