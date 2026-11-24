using System.Text.Json;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 启动时的内置资源完整性校验：确认 CMU 词典、ARPAbet→IPA 映射、内置音节数据
/// 存在且可解析。缺失或损坏时给明确提示（哪个资源、影响什么），不静默降级、不崩溃；
/// 核心词库与学习功能在资源异常时仍可用。
/// </summary>
public static class ResourceHealthCheck
{
    /// <summary>返回出问题的资源说明列表（空表示全部正常）。</summary>
    public static List<string> CheckAll()
    {
        var problems = new List<string>();

        CheckJson(AppConfig.ResCmuDict, "CMU 发音词典", "单词音节的自动推导与 phoneme 兜底", problems);
        CheckJson(AppConfig.ResArpabetIpa, "ARPAbet→IPA 映射表", "SSML phoneme 内部指定发音", problems);
        CheckJson(AppConfig.ResSyllablesBuiltin, "内置校准音节数据", "初始 129 词的拼读分段", problems);

        if (string.IsNullOrEmpty(ResourceReader.ReadBuiltinWordBank()))
            problems.Add("内置词库「译林三上」：首次播种将无法进行（可从「更多」手动导入词库）");

        return problems;
    }

    private static void CheckJson(string logicalName, string display, string impact, List<string> problems)
    {
        var text = ResourceReader.Read(logicalName);
        if (string.IsNullOrEmpty(text))
        {
            problems.Add($"{display}缺失：{impact}不可用");
            return;
        }
        try
        {
            using var _ = JsonDocument.Parse(text);
        }
        catch
        {
            problems.Add($"{display}损坏（无法解析）：{impact}不可用");
        }
    }
}
