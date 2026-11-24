using System.Reflection;
using System.Text;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 读取随 exe 内置（编译进程序集）的只读资源：CMU 词典、ARPAbet→IPA 映射、
/// 内置校准音节、内置词库 md。这些是出厂资源，不是用户数据，随程序升级、只读。
/// </summary>
public static class ResourceReader
{
    /// <summary>按 LogicalName 读取嵌入资源文本；找不到返回 null（由调用方给出明确提示）。</summary>
    public static string? Read(string logicalName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(logicalName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string? ReadBuiltinWordBank() => Read(AppConfig.ResBuiltinWordBank);
    public static string? ReadCmuDict() => Read(AppConfig.ResCmuDict);
    public static string? ReadArpabetIpa() => Read(AppConfig.ResArpabetIpa);
    public static string? ReadSyllablesBuiltin() => Read(AppConfig.ResSyllablesBuiltin);
}
