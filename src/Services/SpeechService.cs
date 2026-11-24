using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 语音服务：C# 原生 Windows.Media.SpeechSynthesis（WinRT）实现，禁用网页 Web Speech API。
///
/// 硬性要求落地：
/// - 枚举 en-US 声音，优先名字含 Natural / Neural / 神经 的神经语音；无则退回普通 en-US；无 en-US 包则明确提示。
/// - 慢速开关：rate = AppConfig.SlowRate(0.85)，正常 = 1.0，通过 SSML prosody rate 实现。
/// - 手动选声由「更多」调用 SelectVoice；失效自动回退默认。
/// - 分段读不准时先用 CMU 音素经 SSML phoneme 内部指定发音（音素不显示），异常/拒绝则静默回退整词朗读。
/// - 竞态取消：切卡/切模式/关窗时 CancelAll，取消正在进行的合成与高亮（高亮定时器由 ViewModel 取消）。
///
/// 播放载体用 WPF MediaElement（由主窗口注入）；WinRT SpeechSynthesisStream 经 AsStream() 转成 .NET Stream。
/// </summary>
public sealed class SpeechService
{
    public static SpeechService Instance { get; } = new();

    private SpeechSynthesizer _synth = new();
    private Windows.Media.SpeechSynthesis.VoiceInformation? _currentVoice;
    private IAudioOutput _audio = NullAudio.Instance;
    private readonly List<VoiceInfo> _voices = new();
    private CancellationTokenSource? _playCts;
    private readonly object _cancelLock = new();

    /// <summary>由主窗口注入音频输出（MediaElement 实现）。</summary>
    public void SetAudioOutput(IAudioOutput audio) => _audio = audio ?? NullAudio.Instance;

    /// <summary>声音信息（界面下拉显示用）。</summary>
    public sealed record VoiceInfo(string Name, string Language, bool IsNeural)
    {
        public string Display => IsNeural ? $"{Name} (Natural)" : Name;
    }

    /// <summary>重新枚举系统声音，筛选 en-US，神经语音优先排序；重枚举保留当前已选（若仍有效）。</summary>
    public IReadOnlyList<VoiceInfo> RefreshVoices()
    {
        _voices.Clear();
        foreach (var v in SpeechSynthesizer.AllVoices)
        {
            if (!v.Language.StartsWith(AppConfig.SpeechLanguage, StringComparison.OrdinalIgnoreCase))
                continue; // 只要 en-US
            bool neural = AppConfig.NeuralVoiceKeywords.Any(k =>
                v.DisplayName.Contains(k, StringComparison.OrdinalIgnoreCase));
            _voices.Add(new VoiceInfo(v.DisplayName, v.Language, neural));
        }
        // 神经语音排前面
        var ordered = _voices.OrderByDescending(x => x.IsNeural).ThenBy(x => x.Name).ToList();
        _voices.Clear();
        _voices.AddRange(ordered);

        // 保留当前选择（若仍有效），否则按降级链选默认
        if (_currentVoice is not null &&
            _voices.Any(x => x.Name == _currentVoice.DisplayName))
            return _voices;

        _currentVoice = null;
        var preferred = _voices.FirstOrDefault(x => x.IsNeural) ?? _voices.FirstOrDefault();
        if (preferred is not null) SelectVoice(preferred.Name);
        return _voices;
    }

    public IReadOnlyList<VoiceInfo> Voices => _voices;

    /// <summary>当前声音显示名（"更多"入口显示）。</summary>
    public string CurrentVoiceDisplay => _currentVoice?.DisplayName ?? "（未找到 en-US 语音，建议安装自然语音）";

    /// <summary>是否有可用的 en-US 声音；无则走"提示安装 + 文本降级"。</summary>
    public bool HasEnUsVoice => _voices.Count > 0;

    /// <summary>手动选声（"更多"入口）；名字无效时回退默认。</summary>
    public void SelectVoice(string displayName)
    {
        var match = _voices.FirstOrDefault(x => x.Name == displayName);
        if (match is null)
        {
            FallbackToDefault();
            return;
        }
        var winRtVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.DisplayName == match.Name);
        if (winRtVoice is null) { FallbackToDefault(); return; }
        _currentVoice = winRtVoice;
        _synth.Voice = winRtVoice;
    }

    private void FallbackToDefault()
    {
        var preferred = _voices.FirstOrDefault(x => x.IsNeural) ?? _voices.FirstOrDefault();
        if (preferred is null) { _currentVoice = null; return; }
        var v = SpeechSynthesizer.AllVoices.FirstOrDefault(x => x.DisplayName == preferred.Name);
        if (v is not null) { _currentVoice = v; _synth.Voice = v; }
    }

    /// <summary>朗读整词/整句。slow=true 用慢速。可被 CancelAll 取消。</summary>
    public async Task SpeakWholeAsync(string text, bool slow, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!HasEnUsVoice) return; // 无包：由界面显示提示，本方法静默不发声（不崩溃）

        var ct = BeginPlay(token);

        try
        {
            var ssml = BuildTextSsml(text, slow);
            await SynthesizeAndPlayAsync(ssml, ct);
        }
        catch (OperationCanceledException) { /* 切卡/关窗取消：正常 */ }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("朗读失败: " + ex.Message); }
    }

    /// <summary>
    /// 朗读一个字形段。先用 CMU 音素经 SSML phoneme 内部指定发音（若该词有音素且映射成功），
    /// phoneme 被拒绝/抛异常时静默回退整词朗读；仍高亮对应字形段（高亮由 ViewModel 负责）。
    /// </summary>
    public async Task SpeakSegmentAsync(string segment, string wholeWord, bool slow, CancellationToken token = default)
    {
        if (!HasEnUsVoice) return;
        var ct = BeginPlay(token);

        var phoneme = TryBuildPhonemeSsml(wholeWord, slow);
        if (phoneme is not null)
        {
            try
            {
                await SynthesizeAndPlayAsync(phoneme, ct);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // phoneme 被引擎拒绝/映射异常：静默回退整词朗读，不崩溃、不静默无声
            }
        }
        await SpeakWholeAsync(wholeWord, slow, ct);
    }

    /// <summary>构造带 phoneme 的 SSML（仅在有可用 IPA 时返回）。</summary>
    private string? TryBuildPhonemeSsml(string word, bool slow)
    {
        var arpabet = CmuDictionary.Lookup(word);
        if (string.IsNullOrEmpty(arpabet)) return null;
        var ipa = ArpabetToIpa.Convert(arpabet);
        if (string.IsNullOrEmpty(ipa)) return null;
        var escaped = System.Security.SecurityElement.Escape(ipa) ?? ipa;
        var rate = slow ? AppConfig.SlowRate : AppConfig.NormalRate;
        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/Synthesis\" xml:lang=\"{AppConfig.SpeechLanguage}\">" +
               $"<voice name=\"{_currentVoice?.DisplayName}\">" +
               $"<prosody rate=\"{rate:0.00}\">" +
               $"<phoneme alphabet=\"{AppConfig.PhonemeAlphabet}\" ph=\"{escaped}\">{System.Security.SecurityElement.Escape(word)}</phoneme>" +
               "</prosody></voice></speak>";
    }

    private static string BuildTextSsml(string text, bool slow)
    {
        var rate = slow ? AppConfig.SlowRate : AppConfig.NormalRate;
        var escaped = System.Security.SecurityElement.Escape(text) ?? text;
        return "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/Synthesis\" " +
               $"xml:lang=\"{AppConfig.SpeechLanguage}\">" +
               "<prosody rate=\"" + rate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\">" +
               escaped + "</prosody></speak>";
    }

    private async Task SynthesizeAndPlayAsync(string ssml, CancellationToken token)
    {
        SpeechSynthesisStream stream;
        try
        {
            stream = await _synth.SynthesizeSsmlToStreamAsync(ssml);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SSML 语法/phoneme 不被接受：若这已是整词朗读，再降级为纯文本合成
            System.Diagnostics.Debug.WriteLine("SSML 合成失败，尝试纯文本: " + ex.Message);
            stream = await _synth.SynthesizeTextToStreamAsync(StripSsmlAndGetText(ssml));
        }
        token.ThrowIfCancellationRequested();

        using (stream)
        {
            var netStream = stream.AsStream();
            _audio.Play(netStream, stream.ContentType);
        }
        await Task.CompletedTask;
    }

    private static string StripSsmlAndGetText(string ssml)
    {
        // 纯文本兜底时把 <phoneme> 的正文取出来朗读，而不是念 IPA
        var m = System.Text.RegularExpressions.Regex.Match(ssml, @"<phoneme[^>]*>(.*?)</phoneme>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
        return System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(ssml, "<[^>]+>", ""));
    }

    /// <summary>开始一次播放：建立可取消令牌并合并调用方传入的 token。返回本次播放的 token。</summary>
    private CancellationToken BeginPlay(CancellationToken externalToken)
    {
        lock (_cancelLock)
        {
            _playCts?.Dispose();
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            return _playCts.Token;
        }
    }

    /// <summary>取消当前朗读（切卡/切模式/关窗/重复点播放时调用）。也停止 MediaElement 播放。</summary>
    public void CancelAll()
    {
        CancellationTokenSource? cts;
        lock (_cancelLock) { cts = _playCts; _playCts = null; }
        try { cts?.Cancel(); } catch { /* 已取消 */ }
        _audio.Stop();
    }
}

/// <summary>音频输出抽象：界面与业务逻辑分离，主窗口用 MediaElement 实现。</summary>
public interface IAudioOutput
{
    void Play(Stream audioStream, string contentType);
    void Stop();
}

/// <summary>空实现：无界面或单元测试时使用（静默）。</summary>
internal sealed class NullAudio : IAudioOutput
{
    public static NullAudio Instance { get; } = new();
    public void Play(Stream audioStream, string contentType) { }
    public void Stop() { }
}
