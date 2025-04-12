using System.Threading.Tasks;

namespace Shine
{
    /// <summary>
    /// WebView2 の主要な機能を抽象化するインターフェース
    /// </summary>
    public interface IWebView2Wrapper
    {
        /// <summary>
        /// WebView2 に HTML を読み込む
        /// </summary>
        /// <param name="html"></param>
        void NavigateToString(string html);

        /// <summary>
        /// WebView2 に JavaScript を実行する
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        Task<string> ExecuteScriptAsync(string script);
    }
}
