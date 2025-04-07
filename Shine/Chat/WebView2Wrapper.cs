using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace Shine
{
    /// <summary>
    /// 実際の WebView2 コントロールをラップするクラス
    /// </summary>
    public class WebView2Wrapper : IWebView2Wrapper
    {
        private readonly WebView2 _webView2;

        public WebView2Wrapper(WebView2 webView2)
        {
            _webView2 = webView2;
        }

        public void NavigateToString(string html)
        {
            _webView2.NavigateToString(html);
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            return _webView2.ExecuteScriptAsync(script);
        }
    }
}
