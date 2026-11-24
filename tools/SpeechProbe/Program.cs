using Windows.Media.SpeechSynthesis;

// TTS 前置最小验证：以单文件形态确认两件事，输出明确的 PASS/FAIL，供交付前记录。
// ① 枚举 en-US 声音，确认能看到 Natural / Neural 神经语音
// ② 合成单个字形段（mo / ther），确认不是空流（不抛异常、有音频字节）

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== 小狗英语 · TTS 前置最小验证 ===");
Console.WriteLine("形态: net8.0-windows self-contained 单文件（与发布载荷一致）");

int pass = 0, fail = 0;

// ---- 验证 1：枚举 en-US + 神经语音 ----
Console.WriteLine("\n[1] 枚举系统声音（en-US）...");
var allVoices = SpeechSynthesizer.AllVoices;
var enUs = allVoices.Where(v => v.Language.StartsWith("en-US", StringComparison.OrdinalIgnoreCase)).ToList();
foreach (var v in enUs)
    Console.WriteLine($"  - {v.DisplayName} ({v.Language})");

string[] neuralKeys = { "Natural", "Neural", "神经" };
var neural = enUs.Where(v => neuralKeys.Any(k => v.DisplayName.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();
if (enUs.Count == 0)
{
    Console.WriteLine("  [FAIL] 未找到任何 en-US 语音包。请到 Windows 设置添加 English (United States) 语音。");
    fail++;
}
else if (neural.Count == 0)
{
    Console.WriteLine("  [WARN] 有 en-US 语音但无 Natural/Neural 神经语音；可退回普通 en-US，但建议安装自然语音。");
    pass++; // 有普通 en-US 即可继续（降级链允许），神经语音缺失记为告警
}
else
{
    Console.WriteLine($"  [PASS] 找到 {neural.Count} 个神经语音：" + string.Join(", ", neural.Select(v => v.DisplayName)));
    pass++;
}

// ---- 验证 2：单段朗读（mo / ther）不抛异常且有音频 ----
Console.WriteLine("\n[2] 合成单字形段（mo / ther）...");
using var synth = new SpeechSynthesizer();
if (enUs.Count > 0)
{
    var preferred = neural.FirstOrDefault() ?? enUs.First();
    synth.Voice = preferred;
    Console.WriteLine($"  使用声音: {preferred.DisplayName}");
    bool segOk = true;
    foreach (var seg in new[] { "mo", "ther" })
    {
        try
        {
            using var stream = await synth.SynthesizeTextToStreamAsync(seg);
            var len = stream.Size;
            if (len <= 0) { Console.WriteLine($"  [{seg}] [FAIL] 音频流为空"); segOk = false; }
            else Console.WriteLine($"  [{seg}] [PASS] 音频流 {len} 字节（需人工听感确认未读成字母名）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{seg}] [FAIL] 合成异常: {ex.Message}");
            segOk = false;
        }
    }
    if (segOk) pass++; else fail++;
}
else
{
    Console.WriteLine("  [SKIP] 无 en-US 语音，跳过单段合成");
}

Console.WriteLine($"\n=== 结果: PASS={pass} / FAIL={fail} ===");
Console.WriteLine("说明：字形段是否读成字母名需人工听感确认（自动仅能验证流非空、无异常）。");
Console.WriteLine("每次修改语音/发布/打包配置后，须按最终打包形态（Setup.exe 安装后）重跑本验证。");
return fail == 0 ? 0 : 1;
