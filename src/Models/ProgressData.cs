using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Models;

/// <summary>
/// 学习进度数据（独立文件 progress.json，三态 + 连对计数）。
/// 严禁把进度塞进 sidecar 或词条索引——数据分层要求进度独立。
/// 键为复合键（年级+Unit+英文）；retired 区存被删除/覆盖词条的记录，不物理删除，便于误操作恢复。
/// </summary>
public class ProgressData
{
    public int SchemaVersion { get; set; } = AppConfig.SchemaVersion;

    /// <summary>活跃词条进度：复合键 -> 进度记录。</summary>
    public Dictionary<string, ProgressEntry> Entries { get; set; } = new();

    /// <summary>已删除/被覆盖词条的旧进度，移入此处备查，可恢复，不参与当前统计。</summary>
    public Dictionary<string, ProgressEntry> Retired { get; set; } = new();
}

/// <summary>单个词条的进度记录。</summary>
public class ProgressEntry
{
    /// <summary>三态之一。</summary>
    public WordStatus Status { get; set; } = WordStatus.New;

    /// <summary>
    /// 连对计数：只有"待复习"态词条持有；答错归零，连续答对到达阈值（AppConfig.CorrectStreakToReviewOut）转回新词。
    /// 按词条持久化，不随筛选/切卡/重启清零。
    /// </summary>
    public int CorrectStreak { get; set; }

    /// <summary>最近一次练习时间（本地时间），仅用于统计展示，不参与判定。</summary>
    public DateTime LastPracticed { get; set; }

    public ProgressEntry Clone() => new()
    {
        Status = Status,
        CorrectStreak = CorrectStreak,
        LastPracticed = LastPracticed
    };
}
