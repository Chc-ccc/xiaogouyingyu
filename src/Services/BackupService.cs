using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Models;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 全量备份/还原服务（与"导出词库 md"明确区分：这里含进度/音节修正/索引/设置全部数据）。
/// - 备份包为带时间戳 zip，存用户目录 backup/，附 manifest.json 作为版本权威。
/// - 还原前必须先备份当前数据并校验备份包 schemaVersion，版本不兼容中止，禁止用旧备份覆盖较新记录。
/// </summary>
public sealed class BackupService
{
    private sealed class Manifest
    {
        public string AppVersion { get; set; } = "";
        public int BackupSchemaVersion { get; set; }
        public Dictionary<string, int> FileSchemas { get; set; } = new();
        public List<string> Files { get; set; } = new();
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>备份全部用户数据，返回备份包完整路径（显示在更多入口）。</summary>
    public string Backup()
    {
        Directory.CreateDirectory(AppPaths.BackupDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(AppPaths.BackupDir, $"小狗英语备份-{stamp}.zip");

        // 收集要备份的文件：词库 md + 四个 json（存在才加入）
        var files = new List<string>();
        files.AddRange(AppPaths.ListWordBankFiles());
        foreach (var f in new[] { AppPaths.ProgressFile, AppPaths.SyllablesFile, AppPaths.WordbankIndexFile, AppPaths.SettingsFile })
            if (File.Exists(f)) files.Add(f);

        var manifest = new Manifest
        {
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            BackupSchemaVersion = AppConfig.SchemaVersion,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var f in files)
            {
                zip.CreateEntryFromFile(f, Path.GetFileName(f));

                // 记录每个 json 的 schemaVersion
                var name = Path.GetFileName(f);
                if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(f));
                        if (doc.RootElement.TryGetProperty("schemaVersion", out var sv))
                            manifest.FileSchemas[name] = sv.GetInt32();
                    }
                    catch { /* 坏档也备份，还原时再提示 */ }
                }
                manifest.Files.Add(name);
            }

            // manifest 作为版本权威写入包内
            var entry = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }
        return zipPath;
    }

    /// <summary>
    /// 还原：返回 (是否成功, 说明)。流程——先备份当前数据 -> 校验 manifest 版本 -> 覆盖 -> 提示核对。
    /// 版本不兼容/校验失败时中止，不覆盖现有数据。
    /// </summary>
    public (bool Ok, string Message) Restore(string zipPath)
    {
        if (!File.Exists(zipPath)) return (false, "备份包不存在");

        Manifest manifest;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var mEntry = zip.GetEntry("manifest.json");
            if (mEntry is null) return (false, "备份包缺少 manifest.json，无法校验版本");
            using var reader = new StreamReader(mEntry.Open());
            manifest = JsonSerializer.Deserialize<Manifest>(reader.ReadToEnd()) ?? new Manifest();
        }
        catch (Exception ex) { return (false, $"备份包损坏：{ex.Message}"); }

        // 版本兼容校验：包内 schemaVersion 必须等于当前
        if (manifest.BackupSchemaVersion != AppConfig.SchemaVersion)
            return (false, $"备份包版本 v{manifest.BackupSchemaVersion} 与当前 v{AppConfig.SchemaVersion} 不兼容，已中止还原（未改动现有数据）");

        // 还原前先备份当前数据并校验备份成功
        string safetyZip;
        try
        {
            safetyZip = Backup(); // 复用备份逻辑
            if (!File.Exists(safetyZip)) return (false, "还原前安全备份失败，已中止");
        }
        catch (Exception ex) { return (false, $"还原前安全备份失败：{ex.Message}，已中止"); }

        // 覆盖还原
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName == "manifest.json" || string.IsNullOrEmpty(entry.Name)) continue;
                var isWordBank = entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
                var target = isWordBank
                    ? Path.Combine(AppPaths.WordBankDir, entry.Name)
                    : Path.Combine(AppPaths.Root, entry.Name);
                var extracted = Path.Combine(Path.GetTempPath(), "xgyy-restore-" + Guid.NewGuid().ToString("N"));
                entry.ExtractToFile(extracted);
                File.Move(extracted, target, overwrite: true);
            }
        }
        catch (Exception ex) { return (false, $"还原失败：{ex.Message}。还原前的备份在：{safetyZip}"); }

        return (true, $"还原完成。还原前的备份已存：{safetyZip}\n请到「更多」核对词库数量与进度条数。");
    }

    public string[] ListBackupFiles()
        => Directory.Exists(AppPaths.BackupDir)
            ? Directory.GetFiles(AppPaths.BackupDir, "*.zip").OrderByDescending(f => File.GetCreationTime(f)).ToArray()
            : Array.Empty<string>();
}
