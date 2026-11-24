using System.IO;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 路径服务：全软件所有用户数据路径的唯一出口，全部基于 AppConfig.UserDataRoot。
/// 程序安装目录只读，本类绝不向其中写文件。
/// </summary>
public static class AppPaths
{
    public static string Root => AppConfig.UserDataRoot;
    public static string WordBankDir => Path.Combine(Root, AppConfig.WordBankFolderName);
    public static string BackupDir => Path.Combine(Root, AppConfig.BackupFolderName);

    public static string ProgressFile => Path.Combine(Root, AppConfig.ProgressFileName);
    public static string SyllablesFile => Path.Combine(Root, AppConfig.SyllablesFileName);
    public static string WordbankIndexFile => Path.Combine(Root, AppConfig.WordbankIndexFileName);
    public static string SettingsFile => Path.Combine(Root, AppConfig.SettingsFileName);

    /// <summary>幂等创建全部用户目录。已存在则什么都不做。</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(WordBankDir);
        Directory.CreateDirectory(BackupDir);
    }

    /// <summary>列出用户词库目录下全部 .md 文件（不含备份目录）。</summary>
    public static string[] ListWordBankFiles()
        => Directory.Exists(WordBankDir)
            ? Directory.GetFiles(WordBankDir, "*.md", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
}
