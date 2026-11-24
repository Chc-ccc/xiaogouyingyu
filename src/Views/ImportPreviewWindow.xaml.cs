using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using XiaoGouYingYu.Models;
using XiaoGouYingYu.Services;
using MessageBox = System.Windows.MessageBox;

namespace XiaoGouYingYu;

public partial class ImportPreviewWindow : Window
{
    private ParsePreview _preview;
    private readonly WordBankService _service;
    private string? _utf8Copy;
    private readonly string _sourcePath;
    private PreviewVm _vm;

    public ImportPreviewWindow(ParsePreview preview, string? utf8Copy, WordBankService service, string sourcePath)
    {
        InitializeComponent();
        _preview = preview;
        _service = service;
        _utf8Copy = utf8Copy;
        // 直接用用户选择的原始文件路径，转码时才能找到文件（不能用词库目录同名文件）
        _sourcePath = sourcePath;

        _vm = new PreviewVm(preview);
        DataContext = _vm;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.ValidCount == 0) { MessageBox.Show("没有可入库的词条。", "小狗英语"); return; }
        if (_preview.NeedsTranscode && _utf8Copy is null)
        {
            MessageBox.Show("检测到非 UTF-8 编码，请先点「按 GBK 转码后重试」。", "小狗英语");
            return;
        }
        _service.Commit(_preview, _vm.Overwrite, _utf8Copy);
        DialogResult = true;
        Close();
    }

    private void Transcode_Click(object sender, RoutedEventArgs e)
    {
        if (!_preview.NeedsTranscode) { MessageBox.Show("文件已是 UTF-8，无需转码。", "小狗英语"); return; }

        try
        {
            // 生成 UTF-8 副本（原文件不动），再对新副本重新预览并原地刷新
            _utf8Copy = EncodingHelper.TranscodeToUtf8Copy(_sourcePath);
            _preview = _service.Preview(_utf8Copy, out var secondCopy);
            _utf8Copy = secondCopy ?? _utf8Copy;
            _vm = new PreviewVm(_preview);
            DataContext = _vm;
            MessageBox.Show($"已转码为 UTF-8 副本并重新解析：\n{_utf8Copy}\n请核对后点「确认入库」。", "小狗英语");
        }
        catch (Exception ex)
        {
            MessageBox.Show("转码失败：" + ex.Message, "小狗英语");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class PreviewVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void N(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class Row
        {
            public string Line { get; set; } = "";
            public string English { get; set; } = "";
            public string Chinese { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public ObservableCollection<Row> Rows { get; } = new();
        public int ValidCount { get; }
        public string SummaryLine { get; }
        public string EncodingLine { get; }
        public bool Overwrite { get; set; }

        public PreviewVm(ParsePreview p)
        {
            ValidCount = p.ValidCount;
            SummaryLine = $"年级：{p.Grade} · 识别单元：{p.UnitCount} 个 · 有效词条：{p.ValidCount} 条 · 失败：{p.InvalidCount} 条";
            EncodingLine = p.NeedsTranscode
                ? $"检测到 {p.DetectedEncoding} 编码，请先一键转码为 UTF-8（原文件不动）。"
                : $"编码：{p.DetectedEncoding}";
            foreach (var e in p.Entries)
                Rows.Add(new Row { Line = e.Unit.ToString(), English = e.CurrentEnglish, Chinese = e.Chinese, Status = "可入库" });
            foreach (var f in p.FailedLines)
                Rows.Add(new Row { Line = f.LineNumber.ToString(), English = f.RawEnglish, Chinese = f.RawChinese, Status = "失败：" + f.ErrorReason });
            if (p.PendingBracketItems.Count > 0)
                Rows.Add(new Row { Line = "-", English = string.Join(", ", p.PendingBracketItems), Status = "待确认括号（不自动判对）" });
        }
    }
}
