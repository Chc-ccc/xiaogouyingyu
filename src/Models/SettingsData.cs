using XiaoGouYingYu.Config;
namespace XiaoGouYingYu.Models;

/// <summary>
/// 用户偏好设置（settings.json），只存用户在界面上改动过的值。
/// 与 AppConfig.cs 出厂默认值分层：读取顺序 settings.json 用户值 -> AppConfig.cs 默认值；
/// 写回只写 settings.json，不修改安装目录、不回写 AppConfig.cs。
/// </summary>
public class SettingsData
{
    public int SchemaVersion { get; set; } = AppConfig.SchemaVersion;

    /// <summary>慢速开关状态。cards 顶部控制条切换后持久保存。</summary>
    public bool SlowMode { get; set; } = false;

    /// <summary>用户在「更多」入口手动选中的声音名称；空串表示用默认策略（神经语音优先）。</summary>
    public string SelectedVoiceName { get; set; } = "";

    /// <summary>用户改过的查词 URL 模板；空串表示用 AppConfig.LookupTemplateDefault。</summary>
    public string LookupTemplate { get; set; } = "";

    /// <summary>「跳过句子类默写」开关（只作用于默写模式）。</summary>
    public bool SkipSentenceDictation { get; set; } = AppConfig.SkipSentenceDictationDefault;

    /// <summary>筛选列表「隐藏句子类」开关（只作用于列表展示）。与上一个开关作用域分离，互不覆盖。</summary>
    public bool HideSentencesInList { get; set; } = AppConfig.HideSentencesInListDefault;

    /// <summary>是否乱序练习。</summary>
    public bool ShuffleEnabled { get; set; } = false;
}
