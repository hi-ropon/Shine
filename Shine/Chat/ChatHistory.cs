using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace Shine
{
    /// <summary>
    /// 会話メッセージを表すモデル
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// WebView2 を利用してチャット履歴を HTML 表示するためのファサードクラス
    /// </summary>
    public class ChatHistory
    {
        private readonly IWebView2Wrapper _chatHistoryWebView;
        private ChatHistoryDocument _document;
        private Brush _foregroundBrush;
        private string _assistantIconBase64;

        // 独自実装の会話メモリ
        private readonly List<ChatMessage> _conversationMemory = new List<ChatMessage>();

        private int _historyLimit = 5;
        private IThemeProvider _themeProvider;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChatHistory(IWebView2Wrapper chatHistoryWebView, Brush foregroundBrush)
        {
            _chatHistoryWebView = chatHistoryWebView ?? throw new ArgumentNullException(nameof(chatHistoryWebView));
            _foregroundBrush = foregroundBrush ?? throw new ArgumentNullException(nameof(foregroundBrush));

            _assistantIconBase64 = ResourceHelper.LoadResourceAsBase64("Shine.Resources.icon.png", typeof(ChatHistory));
            _document = new ChatHistoryDocument(_foregroundBrush, _assistantIconBase64);

            // 初期状態の HTML を WebView2 に読み込む
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// コンストラクタ（テスト用: ChatHistoryDocument と IThemeProvider を注入可能）
        /// </summary>
        public ChatHistory(
            IWebView2Wrapper chatHistoryWebView,
            Brush foregroundBrush,
            ChatHistoryDocument document,
            IThemeProvider themeProvider)
        {
            _chatHistoryWebView = chatHistoryWebView ?? throw new ArgumentNullException(nameof(chatHistoryWebView));
            _foregroundBrush = foregroundBrush ?? throw new ArgumentNullException(nameof(foregroundBrush));
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

            // テスト用ではアイコンは外部から注入されるので空文字
            _assistantIconBase64 = string.Empty;

            // 初期状態の HTML を WebView2 に読み込む
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャットメッセージを追加し、HTML を更新する
        /// </summary>
        public void AddChatMessage(string senderName, string message)
        {
            // HTML 表示部分
            string messageBlock = ChatMessageFormatter.FormatMessage(
                senderName, message, _document.Pipeline, _assistantIconBase64);
            _document.AppendChatMessage(messageBlock);
            _document.AppendScript("<script>addCopyButtons(); scrollToBottom();</script>");
            _chatHistoryWebView.NavigateToString(_document.GetHtml());

            // メモリにも追加
            AddConversationMemory(senderName, message);
            AdjustConversationMemory();
        }

        /// <summary>
        /// 会話メモリにメッセージを追加する
        /// </summary>
        public void AddConversationMemory(string senderName, string message)
        {
            string role = senderName.Equals("User", StringComparison.OrdinalIgnoreCase) ? "Human"
                        : senderName.Equals("Assistant", StringComparison.OrdinalIgnoreCase) ? "Ai"
                        : "Ai";
            _conversationMemory.Add(new ChatMessage(role, message));
        }

        /// <summary>
        /// 会話メモリの件数を制限する
        /// </summary>
        public void AdjustConversationMemory()
        {
            int humanCount = _conversationMemory.Count(m => m.Role == "Human");
            if (humanCount <= _historyLimit) return;

            int removeCount = humanCount - _historyLimit;
            int humanSeen = 0;
            int idxToKeep = 0;

            for (int i = 0; i < _conversationMemory.Count; i++)
            {
                if (_conversationMemory[i].Role == "Human")
                {
                    humanSeen++;
                    if (humanSeen > removeCount)
                    {
                        idxToKeep = i;
                        break;
                    }
                }
            }

            _conversationMemory.RemoveRange(0, idxToKeep);
        }

        /// <summary>
        /// 全メモリをクリアする
        /// </summary>
        public void ClearHistory()
        {
            _conversationMemory.Clear();
            _document.Initialize();
            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// 履歴の上限数を設定する（オプションからの値を反映）
        /// </summary>
        public void SetHistoryLimit(int limit)
        {
            _document.MaxChatHistoryCount = limit;
            _historyLimit = Math.Max(0, limit / 2);
            AdjustConversationMemory();
        }

        /// <summary>
        /// 会話履歴を整形して返す（AI に渡す用）
        /// </summary>
        public string GetConversationHistory()
        {
            var sb = new StringBuilder();
            foreach (var msg in _conversationMemory)
            {
                sb.AppendLine($"{msg.Role}: {msg.Content}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// テーマ変更時にテキスト色を更新
        /// </summary>
        public async Task UpdateForegroundColorAsync(Brush newBrush)
        {
            if (newBrush == null) return;
            _foregroundBrush = newBrush;
            string hex = BrushHelper.ConvertBrushToHex(newBrush);
            await _chatHistoryWebView.ExecuteScriptAsync(
                $"document.body.style.color = '{hex}';");
        }

        /// <summary>
        /// VS テーマ背景色を WebView2 に反映
        /// </summary>
        public async Task UpdateBackgroundColorAsync()
        {
            var bg = VSColorTheme.GetThemedColor(
                EnvironmentColors.ToolWindowBackgroundColorKey);
            var color = System.Windows.Media.Color.FromArgb(
                bg.A, bg.R, bg.G, bg.B);
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            await _chatHistoryWebView.ExecuteScriptAsync(
                $"document.body.style.backgroundColor = '{hex}';");
        }
    }
}