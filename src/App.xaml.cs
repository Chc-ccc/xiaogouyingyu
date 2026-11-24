using System.IO;
using System.Text;
using System.Windows;
using XiaoGouYingYu.Config;
using XiaoGouYingYu.Services;

namespace XiaoGouYingYu;

/// <summary>
/// 应用入口。只做三件事：装配唯一用户数据根目录、首次播种内置词库、打开主窗口。
/// 程序安装目录（%LOCALAPPDATA%\Programs\小狗英语）全程只读，本类不向其中写任何文件。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 0) 注册 GBK/GB2312 代码页提供程序：.NET Core 起默认不加载，
        //    不注册则 EncodingHelper 的 GBK 转码会抛异常。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 1) 建立用户数据根目录与子目录（幂等：已存在则什么都不做）
        AppPaths.EnsureDirectories();

        // 2) 首次播种：仅当用户目录还没有任何词库 md 时，才把内置「译林三上」复制进去。
        //    已有副本时一律以用户副本为准，升级/重装都不覆盖、不回滚。
        SeedService.SeedBuiltinWordBankIfEmpty();

        // 3) 启动校验内置只读资源（CMU 词典 / ARPAbet 映射 / 内置音节），缺失给明确提示但不崩溃。
        var missing = ResourceHealthCheck.CheckAll();
        if (missing.Count > 0)
        {
            MessageBox.Show(
                "以下内置资源缺失或损坏，相关发音功能可能不可用：\n" + string.Join("\n", missing)
                + "\n\n学习、词库与进度功能仍可正常使用。",
                "小狗英语 - 资源提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 4) 打开主窗口
        var main = new MainWindow();
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 关闭窗口时取消一切正在进行的语音合成与高亮定时器，避免退出后残留线程
        SpeechService.Instance.CancelAll();
        base.OnExit(e);
    }
}
