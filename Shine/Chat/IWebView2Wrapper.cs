using System.Threading.Tasks;

namespace Shine
{
    /// <summary>
    /// WebView2 の主要な機能を抽象化するインターフェース
    /// </summary>
    public interface IWebView2Wrapper
    {
        void NavigateToString(string html);
        Task<string> ExecuteScriptAsync(string script);
    }
}
