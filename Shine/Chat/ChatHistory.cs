using System;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.VisualStudio.PlatformUI;
using LangChain.Memory;
using System.Text;
using LangChain.Schema;
using LangChain.Providers;

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
        private ConversationBufferMemory _conversationMemory;

        public ChatHistory(WebView2 chatHistoryWebView, Brush foregroundBrush)
        {
            _chatHistoryWebView = chatHistoryWebView ?? throw new ArgumentNullException(nameof(chatHistoryWebView));
            _foregroundBrush = foregroundBrush ?? throw new ArgumentNullException(nameof(foregroundBrush));

            _assistantIconBase64 = ResourceHelper.LoadResourceAsBase64("Shine.Resources.icon.png", typeof(ChatHistory));
            _document = new ChatHistoryDocument(_foregroundBrush, _assistantIconBase64);

            _conversationMemory = new ConversationBufferMemory();
            // 初期状態の HTML を WebView2 に読み込む
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャットメッセージを追加し、HTML を更新する
        /// </summary>
        public void AddChatMessage(string senderName, string message)
        {
            if (senderName.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                _conversationMemory.ChatHistory.AddUserMessage(message);
            }
            else if (senderName.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
            {
                _conversationMemory.ChatHistory.AddAiMessage(message);
            }
            else
            {
                _conversationMemory.ChatHistory.AddAiMessage(message);
            }

            // ChatMessageFormatter を利用してメッセージの HTML スニペットを生成
            string messageBlock = ChatMessageFormatter.FormatMessage(senderName, message, _document.Pipeline, _assistantIconBase64);
            _document.AppendChatMessage(messageBlock);

            // スクリプトを挿入してコピー機能やスクロールを実行
            string script = "<script>addCopyButtons(); scrollToBottom();</script>";
            _document.AppendScript(script);

            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャット履歴（および会話メモリ）をクリアする
        /// </summary>
        public void ClearHistory()
        {
            _conversationMemory.Clear();

            // HTML 表示用のドキュメントも初期化
            _document.Initialize();
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// 履歴の上限数を設定する（オプションからの値を反映）
        /// </summary>
        public void SetHistoryLimit(int limit)
        {
            _document.MaxChatHistoryCount = limit;
        }

        // 会話履歴を整形して返すメソッド
        public string GetConversationHistory()
        {
            // TODO:　会話履歴の設定値を取得して、整形する

            StringBuilder sb = new StringBuilder();
            foreach (var msg in _conversationMemory.ChatHistory.Messages)
            {
                sb.AppendLine($"{msg.GetType().Name}: {msg.ToString()}");
            }
            return sb.ToString();
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
