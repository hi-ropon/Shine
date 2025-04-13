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

        /// <summary>
        /// WebView2 コントロールをラップするコンストラクタ
        /// </summary>
        /// <param name="webView2"></param>
        public WebView2Wrapper(WebView2 webView2)
        {
            _webView2 = webView2;
        }

        /// <summary>
        /// WebView2 に HTML を読み込む
        /// </summary>
        /// <param name="html"></param>
        public void NavigateToString(string html)
        {
            _webView2.NavigateToString(html);
        }

        /// <summary>
        /// WebView2 に JavaScript を実行する
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public Task<string> ExecuteScriptAsync(string script)
        {
            return _webView2.ExecuteScriptAsync(script);
        }
    }
}
