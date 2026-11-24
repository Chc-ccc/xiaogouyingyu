using System.Text;

namespace XiaoGouYingYu.Services;

/// <summary>编码检测：词库 md 统一 UTF-8，导入时检测 BOM 与合法性，GBK/GB2312 给一键转码选项。</summary>
public static class EncodingHelper
{
    /// <summary>读取文件文本，返回 (文本, 检测到的编码说明, 是否需要转码)。</summary>
    public static (string Text, string Detected, bool NeedsTranscode) ReadWordBankFile(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return (text, "UTF-8 (BOM)", false);
        }

        // 严格 UTF-8 校验
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            var text = strict.GetString(bytes);
            return (text, "UTF-8", false);
        }
        catch (DecoderFallbackException)
        {
            // 尝试 GBK（在 Windows 中文环境用代码页 936）
            Encoding gbk;
            try { gbk = Encoding.GetEncoding("GBK"); }
            catch { gbk = Encoding.GetEncoding(936); }
            var text = gbk.GetString(bytes);
            return (text, "GBK/GB2312", true);
        }
    }

    /// <summary>按 GBK 转码为 UTF-8 写入用户目录副本；原文件不动。返回新文件路径。</summary>
    public static string TranscodeToUtf8Copy(string sourcePath)
    {
        var (text, _, _) = ReadWordBankFile(sourcePath);
        var dir = Path.GetDirectoryName(sourcePath) ?? AppPaths.WordBankDir;
        var name = Path.GetFileNameWithoutExtension(sourcePath) + "-utf8.md";
        var target = Path.Combine(dir, name);
        int i = 2;
        while (File.Exists(target))
            target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(sourcePath)}-utf8-{i++}.md");
        File.WriteAllText(target, text, new UTF8Encoding(false));
        return target;
    }
}
