using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using XiaoGouYingYu.Models;
using XiaoGouYingYu.Services;
using XiaoGouYingYu.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace XiaoGouYingYu;

public partial class WordBankManagerWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly WordBankService _bankService = new();
    private readonly WordBankManager _manager;
    private readonly ManagerVm _vm = new();
    private List<WordEntry> _allEntries = new();

    public WordBankManagerWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _bankService.UseSyllableService(mainVm.Syllables);
        _manager = new WordBankManager(_bankService, mainVm.Progress, mainVm.Sidecar,
            mainVm.Index, mainVm.Syllables);

        DataContext = _vm;
        foreach (var b in _mainVm.Banks) _vm.Grades.Add(b.Grade);
        if (_vm.Grades.Count > 0) GradeCombo.SelectedIndex = 0;
    }

    private void LoadEntries()
    {
        var grade = GradeCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(grade)) return;
        _allEntries = _mainVm.Banks.FirstOrDefault(b => b.Grade == grade)?.Entries ?? new List<WordEntry>();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        var list = _allEntries.Where(e =>
            string.IsNullOrEmpty(q) || e.CurrentEnglish.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Chinese.Contains(q)).ToList();

        _vm.Rows.Clear();
        foreach (var e in list)
        {
            var (segs, pending) = _mainVm.Syllables.Get(e);
            _vm.Rows.Add(new ManagerVm.Row
            {
                Unit = e.Unit,
                English = e.CurrentEnglish,
                Chinese = e.Chinese,
                Category = e.IsPhrase ? "句子" : "单词",
                Syllables = pending ? "待补充" : string.Join("-", segs),
                Status = _mainVm.Progress.Get(e.Key).Status.ToString(),
                Entry = e
            });
        }
    }

    private WordEntry? SelectedEntry() => (EntryGrid.SelectedItem as ManagerVm.Row)?.Entry;

    private void GradeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadEntries();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ChangeEnglish_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var input = new InputDialog("改英文", "新主答案：", entry.CurrentEnglish);
        if (input.ShowDialog() != true) return;
        var banks = _mainVm.Banks.ToList();
        var (ok, msg) = _manager.ChangeEnglish(banks, entry, input.Value.Trim());
        MessageBox.Show(msg, ok ? "小狗英语" : "禁止覆盖");
        if (ok) { _mainVm.ReloadBanks(); LoadEntries(); }
    }

    private void ChangeChinese_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var input = new InputDialog("改中文", "新中文释义：", entry.Chinese);
        if (input.ShowDialog() != true) return;
        _manager.ChangeChinese(entry, input.Value.Trim());
        MessageBox.Show("中文已修改", "小狗英语");
        _mainVm.ReloadBanks();
        LoadEntries();
    }

    private void ChangeUnit_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var input = new InputDialog("调单元", "新单元号（数字）：", entry.Unit.ToString());
        if (input.ShowDialog() != true) return;
        if (!int.TryParse(input.Value.Trim(), out var unit) || unit <= 0)
        { MessageBox.Show("请输入正整数单元号。", "小狗英语"); return; }
        var (ok, msg) = _manager.ChangeUnit(_mainVm.Banks.ToList(), entry, unit);
        MessageBox.Show(msg, ok ? "小狗英语" : "禁止覆盖");
        if (ok) { _mainVm.ReloadBanks(); LoadEntries(); }
    }

    private void ChangeSyllable_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var (segs, _) = _mainVm.Syllables.Get(entry);
        var input = new InputDialog("改音节", "字形段，用连字符分隔（如 mo-ther）：", string.Join("-", segs));
        if (input.ShowDialog() != true) return;
        var newSegs = input.Value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (newSegs.Count == 0) { MessageBox.Show("至少填一段。", "小狗英语"); return; }
        _mainVm.Syllables.SetUserCorrection(_mainVm.Sidecar.Data, entry, newSegs);
        _mainVm.Sidecar.Save();
        MessageBox.Show("音节修正已保存到用户目录 sidecar，升级不覆盖。", "小狗英语");
        LoadEntries();
    }

    private void MarkReview_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        _mainVm.Progress.SetStatus(entry.Key, WordStatus.Review);
        LoadEntries();
    }

    private void MarkNew_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        _mainVm.Progress.SetStatus(entry.Key, WordStatus.New);
        LoadEntries();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var r = MessageBox.Show($"确定删除「{entry.CurrentEnglish}」？旧进度/修正会移入可恢复记录。",
            "小狗英语", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        _manager.Delete(_mainVm.Banks.ToList(), entry);
        MessageBox.Show("已删除（可从「更多」清理或恢复记录）。", "小狗英语");
        _mainVm.ReloadBanks();
        LoadEntries();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var grade = GradeCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(grade)) return;
        var en = new InputDialog("新增词条", "英文（可带括号别名）：", "");
        if (en.ShowDialog() != true) return;
        var zh = new InputDialog("新增词条", "中文释义：", "");
        if (zh.ShowDialog() != true) return;
        var unit = new InputDialog("新增词条", "单元号（数字）：", "1");
        if (unit.ShowDialog() != true || !int.TryParse(unit.Value.Trim(), out var u) || u <= 0) return;

        _manager.AddNew(grade, u, en.Value.Trim(), zh.Value.Trim());
        MessageBox.Show("词条已新增（走与导入相同的括号解析与音节初始化）。", "小狗英语");
        _mainVm.ReloadBanks();
        LoadEntries();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class ManagerVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<string> Grades { get; } = new();
        public ObservableCollection<Row> Rows { get; } = new();

        public sealed class Row
        {
            public int Unit { get; set; }
            public string English { get; set; } = "";
            public string Chinese { get; set; } = "";
            public string Category { get; set; } = "";
            public string Syllables { get; set; } = "";
            public string Status { get; set; } = "";
            public WordEntry Entry { get; set; } = null!;
        }
    }
}
