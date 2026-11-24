using System.Diagnostics;
using System.Net;
using System.Web;
using XiaoGouYingYu.Models;
using XiaoGouYingYu.Config;

namespace XiaoGouYingYu.Services;

/// <summary>
/// 查词服务：只负责拼 URL 并用系统默认浏览器打开，绝不读取/抓取/解析网站内容、不上传任何数据。
/// 编码规则：取主答案（去括号）trim 后整串 URL 编码——句子按整句编码、别名不参与。
/// </summary>
public static class LookupService
{
    /// <summary>用默认浏览器打开该词的搜索结果页。template 为空时用默认金山词霸。</summary>
    public static void Open(string mainAnswer, string? template = null)
    {
        if (string.IsNullOrWhiteSpace(mainAnswer)) return;
        var tpl = string.IsNullOrWhiteSpace(template) ? AppConfig.LookupTemplateDefault : template!;
        if (!tpl.Contains("{word}")) tpl = AppConfig.LookupTemplateDefault;

        // 整串编码（句子含空格/标点按整句编码；HttpUtility.UrlEncode 用 + 编码空格，兼容各站）
        var encoded = HttpUtility.UrlEncode(mainAnswer.Trim(), System.Text.Encoding.UTF8);
        var url = tpl.Replace("{word}", encoded, StringComparison.Ordinal);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true // 交给系统默认浏览器
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("打开查词页面失败: " + ex.Message);
        }
    }
}
