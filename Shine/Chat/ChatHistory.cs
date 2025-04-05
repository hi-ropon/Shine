using System;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.VisualStudio.PlatformUI;

namespace Shine
{
    /// <summary>
    /// WebView2 を利用してチャット履歴を HTML 表示するためのファサードクラス
    /// </summary>
    public class ChatHistory
    {
        private readonly WebView2 _chatHistoryWebView;
        private ChatHistoryDocument _document;
        private Brush _foregroundBrush;
        private readonly string _assistantIconBase64;

        public ChatHistory(WebView2 chatHistoryWebView, Brush foregroundBrush)
        {
            _chatHistoryWebView = chatHistoryWebView ?? throw new ArgumentNullException(nameof(chatHistoryWebView));
            _foregroundBrush = foregroundBrush ?? throw new ArgumentNullException(nameof(foregroundBrush));

            _assistantIconBase64 = ResourceHelper.LoadResourceAsBase64("Shine.Resources.icon.png", typeof(ChatHistory));
            _document = new ChatHistoryDocument(_foregroundBrush, _assistantIconBase64);

            // 初期状態の HTML を WebView2 に読み込む
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャットメッセージを追加し、HTML を更新する
        /// </summary>
        public void AddChatMessage(string senderName, string message)
        {
            // ChatMessageFormatter を利用してメッセージの HTML スニペットを生成
            string messageBlock = ChatMessageFormatter.FormatMessage(senderName, message, _document.Pipeline, _assistantIconBase64);

            _document.AppendChatMessage(messageBlock);

            // スクリプトを挿入してコピー機能やスクロールを実行
            string script = "<script>addCopyButtons(); scrollToBottom();</script>";
            _document.AppendScript(script);

            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャット履歴をクリアする
        /// </summary>
        public void ClearHistory()
        {
            _document.Initialize();
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// テーマ変更時に新しい ForegroundBrush を反映する（body の文字色を更新）
        /// </summary>
        public async Task UpdateForegroundColorAsync(Brush newForegroundBrush)
        {
            if (newForegroundBrush == null) return;
            _foregroundBrush = newForegroundBrush;
            string newColorHex = BrushHelper.ConvertBrushToHex(newForegroundBrush);
            string script = $"document.body.style.color = '{newColorHex}';";
            await _chatHistoryWebView.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// VS の背景色を取得して、WebView2 の背景色を更新する
        /// </summary>
        public async Task UpdateBackgroundColorAsync()
        {
            var bgDrawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var themedBgColor = System.Windows.Media.Color.FromArgb(bgDrawingColor.A, bgDrawingColor.R, bgDrawingColor.G, bgDrawingColor.B);
            string backgroundHex = $"#{themedBgColor.R:X2}{themedBgColor.G:X2}{themedBgColor.B:X2}";

            string script = $"document.body.style.backgroundColor = '{backgroundHex}';";
            await _chatHistoryWebView.ExecuteScriptAsync(script);
        }
    }
}
