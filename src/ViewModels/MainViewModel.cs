using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Models;
using XiaoGouYingYu.Services;
using System.Windows.Controls;

namespace XiaoGouYingYu.ViewModels;

/// <summary>
/// 主视图模型：四模式状态机 + 卡片导航 + 筛选 + 一轮收口 + 答错提示（不公布答案）。
/// 切卡/切模式/切年级单元时先取消当前朗读与高亮定时器，杜绝串音串高亮。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    // ---- 服务 ----
    private readonly WordBankService _bankService = new();
    private readonly SyllabusSidecarStore _sidecar;
    private readonly SyllableService _syllables;
    private readonly ProgressService _progress;
    private readonly SettingsService _settings;
    private readonly WordbankIndexService _index;

    private List<WordBank> _banks = new();
    private List<WordEntry> _queue = new();       // 当前练习队列（已按三步规则过滤/排序）
    private int _queueIndex;
    private WordStatusMap _statusMap = null!;

    private CancellationTokenSource? _highlightCts; // 拼读分段高亮的取消令牌

    // ---- 一轮收口统计 ----
    private int _roundTotal, _roundCorrect, _roundWrong;

    public MainViewModel()
    {
        _sidecar = new SyllabusSidecarStore();
        _syllables = new SyllableService(() => _sidecar.Data, KeyBuilder.Build);
        _bankService.UseSyllableService(_syllables);
        _progress = new ProgressService();
        _settings = new SettingsService();
        _index = new WordbankIndexService();

        // 合并所有服务的启动提示（坏档恢复/迁移等）
        foreach (var n in _sidecar.Notices.Concat(_progress.Notices).Concat(_settings.Notices))
            Notices.Add(n);

        _banks = _bankService.LoadAll();
        RefreshVoiceList();
        RebuildQueue();

        // 命令
        PlayCommand = new RelayCommand(_ => PlayCurrent());
        LookupCommand = new RelayCommand(_ => LookupCurrent());
        NextCommand = new RelayCommand(_ => Next());
        PrevCommand = new RelayCommand(_ => Prev());
        SkipCommand = new RelayCommand(_ => Skip());
        MarkMasteredCommand = new RelayCommand(_ => MarkMastered());
        SubmitAnswerCommand = new RelayCommand(_ => SubmitAnswer());
        SetModeCommand = new RelayCommand(p => SetMode(p));
        ToggleSlowCommand = new RelayCommand(_ => ToggleSlow());

        // 恢复用户选声
        if (!string.IsNullOrEmpty(_settings.SelectedVoiceName))
            SpeechService.Instance.SelectVoice(_settings.SelectedVoiceName);
    }

    // ---- 绑定属性 ----
    public ObservableCollection<string> Notices { get; } = new();
    public ObservableCollection<string> GradeNames { get; } = new();
    public ObservableCollection<string> UnitNames { get; } = new(); // "全部单元" + "Unit N"
    public ObservableCollection<string> ModeNames { get; } = new()
    {
        "整体读音", "拼读", "看中文背英文", "看中文+听音默写"
    };
    public ObservableCollection<VoiceDisplay> Voices { get; } = new();

    public sealed class VoiceDisplay
    {
        public string Name { get; set; } = "";
        public string Display { get; set; } = "";
    }

    private string _selectedGrade = "";
    public string SelectedGrade
    {
        get => _selectedGrade;
        set { if (SetProperty(ref _selectedGrade, value)) { OnGradeChanged(); RebuildQueue(); } }
    }

    private string _selectedUnit = "全部单元";
    public string SelectedUnit
    {
        get => _selectedUnit;
        set { if (SetProperty(ref _selectedUnit, value)) RebuildQueue(); }
    }

    private int _selectedModeIndex;
    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set { if (SetProperty(ref _selectedModeIndex, value)) { CancelPlayback(); OnModeChanged(); } }
    }

    private WordEntry? _current;
    public WordEntry? Current
    {
        get => _current;
        set => SetProperty(ref _current, value);
    }

    // 卡片显示字段（按模式切换，默写模式严格隐藏英文）
    private string _cardEnglish = "";
    public string CardEnglish { get => _cardEnglish; set => SetProperty(ref _cardEnglish, value); }

    private string _cardChinese = "";
    public string CardChinese { get => _cardChinese; set => SetProperty(ref _cardChinese, value); }

    private string _cardSegments = "";
    public string CardSegments { get => _cardSegments; set => SetProperty(ref _cardSegments, value); }

    private int _highlightIndex = -1;
    public int HighlightIndex { get => _highlightIndex; set => SetProperty(ref _highlightIndex, value); }

    private List<string> _segmentList = new();
    public List<string> SegmentList { get => _segmentList; set => SetProperty(ref _segmentList, value); }

    private string _answerInput = "";
    public string AnswerInput { get => _answerInput; set => SetProperty(ref _answerInput, value); }

    private string _hint = "";
    public string Hint { get => _hint; set => SetProperty(ref _hint, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private bool _isAnswerMode;
    public bool IsAnswerMode { get => _isAnswerMode; set => SetProperty(ref _isAnswerMode, value); }

    private bool _isSlow;
    public bool IsSlow
    {
        get => _isSlow;
        set { if (SetProperty(ref _isSlow, value)) { _settings.SlowMode = value; } }
    }

    private string _selectedVoiceName = "";
    public string SelectedVoiceName
    {
        get => _selectedVoiceName;
        set
        {
            if (SetProperty(ref _selectedVoiceName, value))
            {
                SpeechService.Instance.SelectVoice(value);
                _settings.SelectedVoiceName = value;
            }
        }
    }

    private bool _showRoundSummary;
    public bool ShowRoundSummary { get => _showRoundSummary; set => SetProperty(ref _showRoundSummary, value); }
    public string RoundSummaryText => $"练了 {_roundTotal} 条，答对 {_roundCorrect} 条，待复习 {_progress.ActiveCount} 条";

    private bool _shuffle;
    public bool Shuffle
    {
        get => _shuffle;
        set { if (SetProperty(ref _shuffle, value)) { _settings.ShuffleEnabled = value; RebuildQueue(); } }
    }

    // ---- 命令 ----
    public ICommand PlayCommand { get; }
    public ICommand LookupCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand MarkMasteredCommand { get; }
    public ICommand SubmitAnswerCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand ToggleSlowCommand { get; }

    // ---- 初始化 ----
    private void FillGrades()
    {
        GradeNames.Clear();
        foreach (var b in _banks) GradeNames.Add(b.Grade);
        if (!GradeNames.Contains(_selectedGrade) && GradeNames.Count > 0)
            _selectedGrade = GradeNames[0];
        OnPropertyChanged(nameof(SelectedGrade));

        UnitNames.Clear();
        UnitNames.Add("全部单元");
        var grade = _banks.FirstOrDefault(b => b.Grade == _selectedGrade);
        if (grade is not null)
            foreach (var u in grade.Units) UnitNames.Add($"Unit {u}");
        if (!UnitNames.Contains(_selectedUnit)) _selectedUnit = "全部单元";
        OnPropertyChanged(nameof(SelectedUnit));
    }

    private void OnGradeChanged()
    {
        CancelPlayback();
        UnitNames.Clear();
        UnitNames.Add("全部单元");
        var grade = _banks.FirstOrDefault(b => b.Grade == _selectedGrade);
        if (grade is not null)
            foreach (var u in grade.Units) UnitNames.Add($"Unit {u}");
        _selectedUnit = "全部单元";
        OnPropertyChanged(nameof(SelectedUnit));
    }

    private void RefreshVoiceList()
    {
        Voices.Clear();
        foreach (var v in SpeechService.Instance.RefreshVoices())
            Voices.Add(new VoiceDisplay { Name = v.Name, Display = v.Display });
        _selectedVoiceName = SpeechService.Instance.CurrentVoiceDisplay;
        OnPropertyChanged(nameof(SelectedVoiceName));
    }

    // ---- 队列与导航 ----
    private void RebuildQueue()
    {
        // 统一刷新年级/单元下拉（幂等，保留当前有效选择），确保首次加载就有完整单元列表
        FillGrades();
        _statusMap = new WordStatusMap(_progress.AllEntries);
        int unit = ParseUnit(_selectedUnit);
        var scope = _settings.HideSentencesInList
            ? _bankService.Filter(_banks, _selectedGrade, unit, hideSentences: true)
            : (_banks.FirstOrDefault(b => b.Grade == _selectedGrade)?.Entries
                .Where(e => unit == 0 || e.Unit == unit).ToList() ?? new List<WordEntry>());

        var settingsData = new SettingsData
        {
            SkipSentenceDictation = _settings.SkipSentenceDictation,
            ShuffleEnabled = _settings.ShuffleEnabled
        };
        _queue = QuizQueueService.Build(scope, CurrentMode, _statusMap, settingsData);
        _queueIndex = 0;
        ShowRoundSummary = false;
        _roundTotal = _queue.Count; _roundCorrect = 0; _roundWrong = 0;

        UpdateCurrentCard();
    }

    private static int ParseUnit(string s)
    {
        var m = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private LearningMode CurrentMode => (LearningMode)Math.Clamp(_selectedModeIndex, 0, 3);

    private void UpdateCurrentCard()
    {
        CancelPlayback();
        Hint = "";
        AnswerInput = "";
        StatusText = _queue.Count == 0 ? "没有符合条件的词条" : $"{_queueIndex + 1} / {_queue.Count}";

        if (_queue.Count == 0 || _queueIndex >= _queue.Count)
        {
            Current = null;
            CardEnglish = ""; CardChinese = ""; CardSegments = "";
            SegmentList = new List<string>();
            if (_queue.Count > 0) ShowSummary();
            return;
        }

        Current = _queue[_queueIndex];
        ApplyModeToCard();
    }

    /// <summary>按当前模式设置卡片显示。默写/看中文背英文模式严格隐藏英文与音节。</summary>
    private void ApplyModeToCard()
    {
        var e = Current!;
        var (segments, pending) = _syllables.Get(e);
        SegmentList = segments;

        switch (CurrentMode)
        {
            case LearningMode.WholeReading:
                IsAnswerMode = false;
                CardEnglish = e.DisplayEnglish;
                CardChinese = e.Chinese;
                CardSegments = e.IsPhrase ? "" : string.Join(" · ", segments);
                Hint = pending ? "音节待补充" : (string.IsNullOrEmpty(e.AliasHint) ? "" : e.AliasHint);
                break;

            case LearningMode.Spelling:
                IsAnswerMode = false;
                CardEnglish = e.DisplayEnglish;
                CardChinese = e.Chinese;
                CardSegments = string.Join("  ", segments);
                Hint = pending ? "音节待补充，本次整词朗读" : "";
                break;

            case LearningMode.ChineseToEnglish:
                IsAnswerMode = true;
                CardEnglish = "";           // 隐藏英文
                CardChinese = e.Chinese;
                CardSegments = "";           // 隐藏音节（不给提示，提示在答错后给）
                Hint = e.IsPhrase ? "选做 · 输入整句" : "输入英文";
                break;

            case LearningMode.Dictation:
                IsAnswerMode = true;
                CardEnglish = "";           // 严格隐藏英文
                CardChinese = e.Chinese;
                CardSegments = "";           // 默写时隐藏音节
                Hint = e.IsPhrase ? "选做 · 听音默写整句" : "听音默写";
                // 默写模式进入即播放整体读音
                _ = PlayWholeAsync();
                break;
        }
        HighlightIndex = -1;
    }

    // ---- 朗读 ----
    private void PlayCurrent()
    {
        CancelPlayback();
        if (Current is null) return;
        switch (CurrentMode)
        {
            case LearningMode.WholeReading:
            case LearningMode.Dictation:
                _ = PlayWholeAsync();
                break;
            case LearningMode.Spelling:
                _ = PlaySpellingAsync();
                break;
            default:
                // 输入模式点播放：重播整体读音（帮孩子回忆），不显示答案
                _ = PlayWholeAsync();
                break;
        }
    }

    private async Task PlayWholeAsync()
    {
        if (Current is null) return;
        await SpeechService.Instance.SpeakWholeAsync(Current.CurrentEnglish, IsSlow);
    }

    /// <summary>拼读：单词类按字形段逐段读+高亮，短语/句子类按单词逐词读+高亮，段间约 300ms。</summary>
    private async Task PlaySpellingAsync()
    {
        if (Current is null) return;
        var e = Current;
        var segments = SegmentList;
        if (segments.Count == 0) return;

        _highlightCts?.Dispose();
        _highlightCts = new CancellationTokenSource();
        var token = _highlightCts.Token;

        try
        {
            for (int i = 0; i < segments.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                HighlightIndex = i; // 高亮对应字形段/单词

                if (e.SyllablePending && !e.IsPhrase)
                    await SpeechService.Instance.SpeakWholeAsync(e.CurrentEnglish, IsSlow, token); // 待补充退化为整词朗读
                else if (e.IsPhrase)
                    await SpeechService.Instance.SpeakWholeAsync(segments[i], IsSlow, token);
                else
                    await SpeechService.Instance.SpeakSegmentAsync(segments[i], e.CurrentEnglish, IsSlow, token);

                await Task.Delay(AppConfig.SegmentGapMs, token);
            }
            HighlightIndex = -1;
        }
        catch (OperationCanceledException)
        {
            HighlightIndex = -1; // 切卡取消：清除旧高亮
        }
    }

    /// <summary>取消当前朗读与高亮定时器（切卡/切模式/切年级/关窗时调用）。</summary>
    private void CancelPlayback()
    {
        try { _highlightCts?.Cancel(); } catch { }
        SpeechService.Instance.CancelAll();
        HighlightIndex = -1;
    }

    // ---- 答案判定 ----
    private void SubmitAnswer()
    {
        if (Current is null || !IsAnswerMode) return;
        bool correct = NormalizationService.IsCorrect(AnswerInput, Current);
        if (correct)
        {
            _progress.MarkCorrect(Current.Key);
            _roundCorrect++;
            Hint = "答对了！";
            StatusText = $"答对了 · {_queueIndex + 1}/{_queue.Count}";
            AdvanceAfterAnswer();
        }
        else
        {
            _progress.MarkWrong(Current.Key);
            _roundWrong++;
            Hint = BuildWrongHint(Current);
            // 答错保留原输入并全选（由界面 TextBox 处理全选）
            OnPropertyChanged(nameof(AnswerInput));
        }
    }

    /// <summary>答错提示：给可复盘的提示但不公布完整答案（对应四模式答错提示）。</summary>
    private string BuildWrongHint(WordEntry e)
    {
        var (segments, pending) = _syllables.Get(e);
        switch (CurrentMode)
        {
            case LearningMode.ChineseToEnglish:
                if (e.IsPhrase) return $"再想想，按词切分，共 {segments.Count} 词";
                return $"再想想，共 {segments.Count} 段，第一段 {segments[0]}";
            case LearningMode.Dictation:
                if (e.IsPhrase) return $"再想想，按词切分，共 {segments.Count} 词";
                if (pending) return "再想想，听音默写整词";
                return "再想想，分成 " + segments.Count + " 段：" + string.Join(" - ", segments);
            default:
                return "再想想";
        }
    }

    private void AdvanceAfterAnswer() { /* 答对后不自动跳卡，孩子点下一张/标记掌握；保留输入便于复盘 */ }

    // ---- 导航 ----
    private void Next()
    {
        CancelPlayback();
        if (_queueIndex < _queue.Count - 1) _queueIndex++;
        else ShowSummary();
        UpdateCurrentCard();
    }

    private void Prev()
    {
        CancelPlayback();
        if (_queueIndex > 0) _queueIndex--;
        UpdateCurrentCard();
    }

    private void Skip()
    {
        CancelPlayback();
        if (_queueIndex < _queue.Count - 1) _queueIndex++;
        else ShowSummary();
        UpdateCurrentCard();
    }

    private void MarkMastered()
    {
        if (Current is null) return;
        _progress.MarkMastered(Current.Key);
        CancelPlayback();
        RebuildQueue(); // 已掌握默认不进普通队列，重建后跳到下一张
    }

    private void ShowSummary()
    {
        ShowRoundSummary = true;
        OnPropertyChanged(nameof(RoundSummaryText));
    }

    // ---- 模式/设置 ----
    private void SetMode(object? param)
    {
        if (param is int i) { SelectedModeIndex = i; return; }
        if (param is string s)
        {
            // XAML 的 CommandParameter="0" 传进来是字符串，先试数字，再试中文名
            if (int.TryParse(s, out var n)) { SelectedModeIndex = n; return; }
            int idx = s switch
            {
                "整体读音" => 0, "拼读" => 1, "看中文背英文" => 2, "看中文+听音默写" => 3, _ => 0
            };
            SelectedModeIndex = idx;
        }
    }

    private void OnModeChanged()
    {
        if (_queue.Count == 0 || Current is null) return;
        ApplyModeToCard();
    }

    private void ToggleSlow() => IsSlow = !IsSlow;

    private void LookupCurrent()
    {
        if (Current is null) return;
        LookupService.Open(Current.CurrentEnglish, _settings.LookupTemplate);
    }

    // ---- 供「更多」窗口调用的公开方法 ----
    public void RefreshVoices()
    {
        RefreshVoiceList();
        if (!string.IsNullOrEmpty(SelectedVoiceName) && _settings.SelectedVoiceName != SelectedVoiceName)
            _settings.SelectedVoiceName = SelectedVoiceName;
    }

    public string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public string DataCounts =>
        $"词库 {_banks.Sum(b => b.Entries.Count)} 条 · 进度 {_progress.ActiveCount} 条 · 待补充音节 {CountPendingSyllables()} 条";

    private int CountPendingSyllables()
    {
        int n = 0;
        foreach (var b in _banks)
            foreach (var e in b.Entries)
                if (_syllables.IsPending(e)) n++;
        return n;
    }

    /// <summary>供待补充清单显示：年级/Unit/英文。</summary>
    public List<(string Grade, int Unit, string English)> PendingSyllableItems()
    {
        var list = new List<(string, int, string)>();
        foreach (var b in _banks)
            foreach (var e in b.Entries)
                if (_syllables.IsPending(e)) list.Add((e.Grade, e.Unit, e.CurrentEnglish));
        return list;
    }

    public IReadOnlyList<WordBank> Banks => _banks;
    public SettingsService Settings => _settings;
    public ProgressService Progress => _progress;
    public SyllableService Syllables => _syllables;
    public SyllabusSidecarStore Sidecar => _sidecar;
    public WordbankIndexService Index => _index;

    public void ReloadBanks()
    {
        _banks = _bankService.LoadAll();
        FillGrades();
        RebuildQueue();
    }
}
