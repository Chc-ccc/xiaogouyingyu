using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XiaoGouYingYu.Services;
using XiaoGouYingYu.ViewModels;
using System.IO;

namespace XiaoGouYingYu;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private MediaElement _hiddenMedia = null!;

    public MainWindow()
    {
        InitializeComponent();

        // 注入音频输出：隐藏 MediaElement 承载 WinRT 合成的语音流
        _hiddenMedia = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual };
        _hiddenMedia.MediaEnded += (_, _) => _hiddenMedia.Position = TimeSpan.Zero;
        RootGrid.Children.Add(_hiddenMedia);
        SpeechService.Instance.SetAudioOutput(new MediaElementAudio(_hiddenMedia));

        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += VmOnPropertyChanged;

        // 恢复慢速按钮态
        UpdateSpeedButtons(_vm.IsSlow);
        UpdateModeButtons(_vm.SelectedModeIndex);
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HighlightIndex))
            ApplyHighlight(_vm.HighlightIndex);
        else if (e.PropertyName == nameof(MainViewModel.IsSlow))
            UpdateSpeedButtons(_vm.IsSlow);
        else if (e.PropertyName == nameof(MainViewModel.SelectedModeIndex))
            UpdateModeButtons(_vm.SelectedModeIndex);
    }

    /// <summary>按 HighlightIndex 高亮当前字形段/单词（-1 清除全部）。切卡取消时会置 -1，清旧高亮。</summary>
    private void ApplyHighlight(int index)
    {
        for (int i = 0; i < SegmentItems.Items.Count; i++)
        {
            if (SegmentItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item) continue;
            var border = FindVisualChild<Border>(item);
            if (border is null) continue;
            border.Background = i == index
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x66)) // 高亮放大色
                : new SolidColorBrush(Color.FromRgb(0xF0, 0xE6, 0xD2));
            border.RenderTransform = i == index ? new ScaleTransform(1.12, 1.12) : Transform.Identity;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private void UpdateSpeedButtons(bool slow)
    {
        // 分段按钮样式：当前态高亮（通过 Tag 找到按钮）
        foreach (var btn in new[] { FindButtonByTag("normal"), FindButtonByTag("slow") })
        {
            if (btn is null) continue;
            bool active = (btn.Tag as string) == (slow ? "slow" : "normal");
            btn.Background = active
                ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))
                : new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
            btn.Foreground = active ? Brushes.White : Brushes.Black;
        }
    }

    private void UpdateModeButtons(int index)
    {
        // 模式按钮高亮当前态：遍历 StackPanel 中 Tag 为数字按钮
        foreach (var btn in ModeButtonStack.Children.OfType<Button>())
        {
            bool active = btn.Tag is string t && int.TryParse(t, out var v) && v == index;
            btn.Background = active
                ? new SolidColorBrush(Color.FromRgb(0x8E, 0x6C, 0xE6))
                : new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
            btn.Foreground = active ? Brushes.White : Brushes.Black;
        }
    }

    private Button? FindButtonByTag(string tag)
        => SpeedPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is string t && t == tag);

    private void NormalSpeed_Click(object sender, RoutedEventArgs e) => _vm.IsSlow = false;
    private void SlowSpeed_Click(object sender, RoutedEventArgs e) => _vm.IsSlow = true;

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        // CommandParameter 已绑定，这里只刷新高亮（SetModeCommand 会改 SelectedModeIndex 触发 VmOnPropertyChanged）
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out var idx))
            UpdateModeButtons(idx);
    }

    private void AnswerBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && !string.IsNullOrEmpty(box.Text))
            box.SelectAll();
    }

    private void AnswerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.SubmitAnswerCommand.Execute(null);
            // 答错后保留输入并全选，便于直接改后重交
            if (sender is TextBox box) box.SelectAll();
            e.Handled = true;
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new MoreWindow(_vm);
        win.Owner = this;
        win.ShowDialog();
    }

    private void RestartRound_Click(object sender, RoutedEventArgs e) => _vm.ReloadBanks();

    private void ReviewOnly_Click(object sender, RoutedEventArgs e)
    {
        // "只练待复习"：通过状态列表筛选，这里给出提示引导到更多入口，避免误改答案口径
        MessageBox.Show("已切换到待复习练习。如需手动管理词条状态，请在「更多」入口操作。",
            "小狗英语", MessageBoxButton.OK, MessageBoxImage.Information);
        _vm.ReloadBanks();
    }

    private void Return_Click(object sender, RoutedEventArgs e) => _vm.ReloadBanks();

    protected override void OnClosing(CancelEventArgs e)
    {
        SpeechService.Instance.CancelAll();
        base.OnClosing(e);
    }

    /// <summary>MediaElement 实现音频输出接口。</summary>
    private sealed class MediaElementAudio : IAudioOutput
    {
        private readonly MediaElement _media;
        public MediaElementAudio(MediaElement media) => _media = media;

        public void Play(Stream audioStream, string contentType)
        {
            // 必须在 UI 线程设置源
            _media.Dispatcher.Invoke(() =>
            {
                _media.Stop();
                _media.SetSource(audioStream, contentType);
                _media.Play();
            });
        }

        public void Stop() => _media.Dispatcher.Invoke(() => _media.Stop());
    }
}
