using System.Collections.Generic;

namespace XiaoGouYingYu.Models;

/// <summary>词库容器：一个 md 文件 = 一个年级/教材册，内部按 ## Unit N 分段。</summary>
public class WordBank
{
    /// <summary>年级/册名（文件名去掉 .md）。</summary>
    public string Grade { get; set; } = "";

    /// <summary>来源 md 文件完整路径（位于用户目录）。</summary>
    public string FilePath { get; set; } = "";

    /// <summary>全部词条（每条带自己的 Unit）。</summary>
    public List<WordEntry> Entries { get; set; } = new();

    /// <summary>本词库包含的单元号（升序）。</summary>
    public List<int> Units
    {
        get
        {
            var set = new SortedSet<int>();
            foreach (var e in Entries) set.Add(e.Unit);
            return new List<int>(set);
        }
    }
}

/// <summary>单个词条的解析结果条目（供导入预览逐行展示/编辑）。</summary>
public class ParsedLine
{
    public int LineNumber { get; set; }
    public string RawEnglish { get; set; } = "";
    public string RawChinese { get; set; } = "";
    public bool IsValid { get; set; }
    public string ErrorReason { get; set; } = "";
    public WordEntry? Entry { get; set; }
}

/// <summary>整个 md 文件的解析预览结果。</summary>
public class ParsePreview
{
    public string FileName { get; set; } = "";
    public string Grade { get; set; } = "";
    public int UnitCount { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }

    /// <summary>无法识别的括号内容等待确认项（不确定不猜）。</summary>
    public List<string> PendingBracketItems { get; set; } = new();

    /// <summary>解析失败行（带行号与原因），不阻断其他行入库。</summary>
    public List<ParsedLine> FailedLines { get; set; } = new();

    /// <summary>成功解析的词条。</summary>
    public List<WordEntry> Entries { get; set; } = new();

    /// <summary>是否为 GBK/GB2312（提示用户一键转码）。</summary>
    public bool NeedsTranscode { get; set; }

    /// <summary>检测到的原始编码说明。</summary>
    public string DetectedEncoding { get; set; } = "";
}
