using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Services;
using XiaoGouYingYu.ViewModels;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using System.IO;

namespace XiaoGouYingYu;

public partial class MoreWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly MoreVm _vm;
    private readonly BackupService _backup = new();

    public MoreWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _vm = new MoreVm(mainVm);
        DataContext = _vm;
    }

    private void RefreshVoices_Click(object sender, RoutedEventArgs e)
    {
        _mainVm.RefreshVoices();
        _vm.ReloadVoices();
        MessageBox.Show("声音列表已刷新。", "小狗英语");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "词库文件 (*.md)|*.md", Title = "选择要导入的词库 md" };
        if (dlg.ShowDialog() != true) return;

        var preview = new XiaoGouYingYu.Services.WordBankService();
        // 预览需要音节服务：用主 VM 的服务（通过公开的 Syllables）
        preview.UseSyllableService(_mainVm.Syllables);
        var result = preview.Preview(dlg.FileName, out var utf8Copy);
        var win = new ImportPreviewWindow(result, utf8Copy, preview, dlg.FileName);
        win.Owner = this;
        if (win.ShowDialog() == true)
        {
            _mainVm.ReloadBanks();
            _vm.ReloadStats();
            MessageBox.Show("词库已入库。", "小狗英语");
        }
    }

    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        var win = new WordBankManagerWindow(_mainVm);
        win.Owner = this;
        win.ShowDialog();
        _mainVm.ReloadBanks();
        _vm.ReloadStats();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_mainVm.Banks.Count == 0) { MessageBox.Show("暂无词库可导出。", "小狗英语"); return; }
        var dlg = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", FileName = "词库导出.md" };
        if (dlg.ShowDialog() != true) return;
        var svc = new XiaoGouYingYu.Services.WordBankService();
        var all = string.Join("\n\n", _mainVm.Banks.Select(svc.ExportWordBankText));
        System.IO.File.WriteAllText(dlg.FileName, all, new System.Text.UTF8Encoding(false));
        MessageBox.Show("词库已导出（仅词库文字，不含进度/修正/设置）。", "小狗英语");
    }

    private void RestoreBuiltin_Click(object sender, RoutedEventArgs e)
    {
        var path = XiaoGouYingYu.Services.SeedService.RestoreBuiltinAsCopy();
        _mainVm.ReloadBanks();
        MessageBox.Show("已另存内置词库副本（不覆盖现有文件）：\n" + path, "小狗英语");
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _backup.Backup();
            _vm.BackupPath = "备份完成：" + path;
            MessageBox.Show("已备份全部用户数据：\n" + path, "小狗英语");
        }
        catch (System.Exception ex) { MessageBox.Show("备份失败：" + ex.Message, "小狗英语"); }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "备份包 (*.zip)|*.zip", Title = "选择备份包" };
        if (dlg.ShowDialog() != true) return;
        var result = MessageBox.Show("还原会用备份覆盖当前数据（还原前会自动先备份当前数据）。确定继续吗？",
            "小狗英语", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        var (ok, msg) = _backup.Restore(dlg.FileName);
        MessageBox.Show(msg, ok ? "还原完成" : "已中止还原");
        if (ok) { _mainVm.ReloadBanks(); _vm.ReloadStats(); }
    }

    private void ClearRetired_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("清理已删除/被覆盖词条的旧记录？清理后无法再恢复这些记录。",
            "小狗英语", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        int n1 = _mainVm.Progress.ClearRetired();
        int n2 = _mainVm.Sidecar.ClearRetired();
        int n3 = _mainVm.Index.ClearRetired();
        MessageBox.Show($"已清理 retired 记录：进度 {n1} / 音节 {n2} / 索引 {n3} 条。", "小狗英语");
    }

    private void ParentGuide_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "安装自然语音：Windows 设置 → 时间和语言 → 语言和区域 → 添加语言 English (United States) → 展开该语言 → 语音 → 添加带 Natural 的语音（免费，装一次即可）。\n\n" +
            "覆盖更新：到仓库 Releases 页下载最新 Setup.exe，双击、点下一步即可升级，无需卸载；词库、进度、音节修正、偏好设置都在用户目录，不会丢。\n\n" +
            "换 Windows 账户时数据按账户隔离，需用「备份全部数据 / 从备份还原」迁移。",
            "家长说明", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>「更多」窗口的绑定模型。</summary>
    private sealed class MoreVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly MainViewModel _vm;
        public MoreVm(MainViewModel vm)
        {
            _vm = vm;
            ReloadVoices();
            ReloadStats();
            PendingLines = new ObservableCollection<string>(
                _vm.PendingSyllableItems().Select(x => $"{x.Grade} · Unit {x.Unit} · {x.English}"));
        }

        public string VersionLine => "程序版本：" + _vm.CurrentVersion;
        public string DataCounts => _vm.DataCounts;

        public ObservableCollection<MainViewModel.VoiceDisplay> Voices { get; } = new();
        public ObservableCollection<string> PendingLines { get; }

        private string _selectedVoice = "";
        public string SelectedVoiceName
        {
            get => _selectedVoice;
            set { _selectedVoice = value; }
        }

        private string _lookupTemplate = "";
        public string LookupTemplate
        {
            get => _vm.Settings.LookupTemplate;
            set { if (_vm.Settings.LookupTemplate != value) _vm.Settings.LookupTemplate = value; }
        }

        public sealed class LookupOption
        {
            public string Name { get; set; } = "";
            public string Template { get; set; } = "";
        }

        public ObservableCollection<LookupOption> LookupTemplates { get; } =
            new(AppConfig.LookupTemplates.Select(t => new LookupOption { Name = t.Name, Template = t.Template }));

        private string _backupPath = "";
        public string BackupPath { get => _backupPath; set { _backupPath = value; Notify(nameof(BackupPath)); } }

        public string StatCounts
        {
            get
            {
                var keys = _vm.Banks.SelectMany(b => b.Entries).Select(e => e.Key);
                var (n, r, m) = _vm.Progress.CountBy(keys);
                return $"新词 {n} 条 · 待复习 {r} 条 · 已掌握 {m} 条";
            }
        }

        public void ReloadVoices()
        {
            Voices.Clear();
            foreach (var v in _vm.Voices) Voices.Add(v);
            _selectedVoice = _vm.SelectedVoiceName;
            Notify(nameof(Voices));
            Notify(nameof(SelectedVoiceName));
        }

        public void ReloadStats()
        {
            Notify(nameof(StatCounts));
            Notify(nameof(DataCounts));
            Notify(nameof(VersionLine));
        }
    }
}
