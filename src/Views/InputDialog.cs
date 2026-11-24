using System.Windows;
using System.Windows.Controls;

namespace XiaoGouYingYu;

/// <summary>通用单行输入对话框（纯代码定义，无 XAML），供词库管理改英文/中文/单元/音节/新增用。</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _box = new() { FontSize = 18, Height = 38, Margin = new Thickness(0, 8, 0, 0) };

    public string Value => _box.Text;

    public InputDialog(string title, string label, string initial)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.WhiteSmoke;

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 18 });
        _box.Text = initial;
        panel.Children.Add(_box);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "确定", Width = 90, Height = 38, FontSize = 17, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "取消", Width = 90, Height = 38, FontSize = 17, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => { _box.Focus(); _box.SelectAll(); };
    }
}
