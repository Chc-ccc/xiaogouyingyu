using System.Text;
using XiaoGouYingYu.Config;
using System.IO;

namespace XiaoGouYingYu.Services;

/// <summary>首次播种服务：仅当词库目录为空时，把内置「译林三上」写入用户词库目录。</summary>
public static class SeedService
{
    /// <summary>
    /// 首次运行播种。硬性：已有任何词库文件则直接沿用、不覆盖、不重复播种、不回滚用户修改。
    /// 安装程序不得预写数据——播种只由本方法（程序首次运行）完成。
    /// </summary>
    public static void SeedBuiltinWordBankIfEmpty()
    {
        var existing = AppPaths.ListWordBankFiles();
        if (existing.Length > 0) return; // 用户目录已有词库（含从旧版升级），直接沿用

        var text = ResourceReader.ReadBuiltinWordBank();
        if (string.IsNullOrEmpty(text)) return; // 资源缺失由启动校验提示，不崩溃

        var target = Path.Combine(AppPaths.WordBankDir, "译林三上.md");
        if (File.Exists(target)) return; // 双保险

        // UTF-8 无 BOM 写入
        File.WriteAllText(target, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// 「更多」入口的「恢复内置词库」：另存为新文件（如 译林三上-内置副本.md），不覆盖现有文件。
    /// </summary>
    public static string RestoreBuiltinAsCopy()
    {
        var text = ResourceReader.ReadBuiltinWordBank();
        if (string.IsNullOrEmpty(text)) return "内置词库资源缺失";

        string name = "译林三上-内置副本.md";
        var target = Path.Combine(AppPaths.WordBankDir, name);
        int i = 2;
        while (File.Exists(target))
        {
            target = Path.Combine(AppPaths.WordBankDir, $"译林三上-内置副本{i}.md");
            i++;
        }
        File.WriteAllText(target, text, new UTF8Encoding(false));
        return target;
    }
}
