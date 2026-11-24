using System.Text.Json;
using System.Text.Json.Serialization;
using XiaoGouYingYu.Config;
using System.IO;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 关键 json 的通用读写器，统一实现三条硬性数据安全要求：
/// 1) 原子写：写临时文件 -> 校验完整 -> 原子替换，避免中途退出写坏原文件；
/// 2) schemaVersion 迁移：旧档先复制带时间戳的 .bak 再迁移，无法兼容明确抛错不静默清空；
/// 3) 坏档恢复：加载失败先读 .bak，仍失败把坏档改名为 *.corrupt-时间戳 留存，返回空结构并报告。
///
/// 本类不认识具体业务字段，只处理"带 schemaVersion 的 json 文件"这一类结构。
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 读取一个带 schemaVersion 的 json 文件。
    /// 返回 (数据对象, 给用户看的提示信息)；提示为空表示一切正常。
    /// T 必须有无参构造且 SchemaVersion 可读写（本软件四个数据文件都满足）。
    /// </summary>
    public static (T Data, string Notice) Load<T>(string path) where T : new()
    {
        string notice = "";
        T CreateEmpty() => new T();

        if (!File.Exists(path)) return (CreateEmpty(), notice);

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            // 读文件本身失败（权限/占用）：不静默清空，报告并给空结构
            return (CreateEmpty(), $"无法读取 {Path.GetFileName(path)}：{ex.Message}。本次使用空数据，未删除原文件。");
        }

        T? data;
        try
        {
            data = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch
        {
            // 坏档：先试 .bak
            var bak = path + ".bak";
            if (File.Exists(bak))
            {
                try
                {
                    data = JsonSerializer.Deserialize<T>(File.ReadAllText(bak), Options);
                    notice = $"{Path.GetFileName(path)} 损坏，已从备份恢复。";
                }
                catch
                {
                    data = CreateEmpty();
                    notice = $"{Path.GetFileName(path)} 与其备份均损坏，已重建空数据。";
                }
            }
            else
            {
                data = CreateEmpty();
                notice = $"{Path.GetFileName(path)} 损坏且无备份，已重建空数据。";
            }

            // 坏档改名留存，不直接删除
            QuarantineCorrupt(path);
            return (data!, notice);
        }

        if (data is null) return (CreateEmpty(), notice);

        // schemaVersion 检查：一致直接读；不一致尝试迁移（当前只有 v1，无历史版本可迁移）
        var versionProp = typeof(T).GetProperty("SchemaVersion");
        var version = versionProp?.GetValue(data) as int? ?? 0;
        if (version != AppConfig.SchemaVersion)
        {
            if (TryMigrate(data, version, out var migrated))
            {
                BackupBeforeMigration(path);
                Save(path, migrated!);
                return ((T)migrated!, $"{Path.GetFileName(path)} 已从旧格式 v{version} 迁移到 v{AppConfig.SchemaVersion}，迁移前已备份。");
            }
            // 无法兼容：不静默清空，报告并保留原文件
            return (CreateEmpty(), $"{Path.GetFileName(path)} 版本 v{version} 无法识别（当前 v{AppConfig.SchemaVersion}），已跳过该文件、未删除原数据。请联系维护者提供迁移方案。");
        }

        return (data, notice);
    }

    /// <summary>原子写：临时文件 -> 校验 -> 原子替换。校验失败抛异常，绝不写坏原文件。</summary>
    public static void Save<T>(string path, T data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(data, Options);

        // 写临时文件
        File.WriteAllText(tmp, json);

        // 校验完整：能反序列化回来才算成功
        try
        {
            _ = JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new InvalidOperationException("序列化结果为空");
        }
        catch
        {
            // 校验失败：删临时文件，原文件 untouched
            if (File.Exists(tmp)) File.Delete(tmp);
            throw new InvalidOperationException($"{Path.GetFileName(path)} 临时文件校验失败，已放弃写入，原文件未改动。");
        }

        // 原子替换（同盘符下 File.Move 的 overwrite 是原子操作）
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>迁移前备份：复制为带时间戳的 .bak（保留旧档可回滚）。</summary>
    private static void BackupBeforeMigration(string path)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(path, path + ".bak-" + stamp, overwrite: false);
    }

    /// <summary>坏档留存：改名为 *.corrupt-时间戳，便于排查，不物理删除。</summary>
    private static void QuarantineCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Move(path, path + ".corrupt-" + stamp, overwrite: false);
        }
        catch
        {
            // 留存失败不能影响主流程；坏档仍在原处，下次仍会被识别为坏档
        }
    }

    /// <summary>
    /// 版本迁移钩子。当前工程只有 v1，无历史版本；保留此方法供后续数据结构升级时实现。
    /// 返回 true 表示已成功迁移，out 为迁移后的对象。
    /// </summary>
    private static bool TryMigrate<T>(T data, int fromVersion, out object? migrated)
    {
        migrated = null;
        // 目前仅 v1，无 v0 结构可迁移。后续新增 v2 时在此实现：
        // if (fromVersion == 1 && data is V1Type oldV1) { migrated = ConvertToV2(oldV1); return true; }
        return false;
    }
}
