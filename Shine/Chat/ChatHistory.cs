using System;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.VisualStudio.PlatformUI;
using LangChain.Memory;
using System.Text;
using System.Linq;

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
        private int _historyLimit = 5;

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
            // ChatMessageFormatter を利用してメッセージの HTML スニペットを生成
            string messageBlock = ChatMessageFormatter.FormatMessage(senderName, message, _document.Pipeline, _assistantIconBase64);
            _document.AppendChatMessage(messageBlock);

            // スクリプトを挿入してコピー機能やスクロールを実行
            string script = "<script>addCopyButtons(); scrollToBottom();</script>";
            _document.AppendScript(script);

            _chatHistoryWebView.NavigateToString(_document.GetHtml());
        }

        /// <summary>
        /// チャット履歴を追加する
        /// </summary>
        public void AddConversationMemory(string senderName, string message)
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
        }

        /// <summary>
        /// チャット履歴の量を調整する
        /// </summary>
        public void AdjustConversationMemory()
        {
            if (_historyLimit == 0)
            {
                _conversationMemory.Clear();
                return;
            }

            var messages = _conversationMemory.ChatHistory.Messages.ToList();

            int humanMessageCount = messages.Count(m => (m.Role).ToString() == "Human");

            if (humanMessageCount > _historyLimit)
            {
                int excessPairs = humanMessageCount - _historyLimit;
                int messagesToSkip = 0;
                int humanCount = 0;

                for (int i = 0; i < messages.Count; i++)
                {
                    if ((messages[i].Role).ToString() == "Human")
                    {
                        humanCount++;
                        if (humanCount <= excessPairs)
                        {
                            messagesToSkip = i + 2; // Human + AI で2メッセージ
                            if (i + 1 >= messages.Count || (messages[i + 1].Role).ToString() != "Ai")
                            {
                                // AIメッセージがない場合は1つだけスキップ
                                messagesToSkip = i + 1;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                var newMemory = new ConversationBufferMemory();

                // 残すメッセージを新しいメモリに追加
                for (int i = messagesToSkip; i < messages.Count; i++)
                {
                    if ((messages[i].Role).ToString() == "Human")
                    {
                        newMemory.ChatHistory.AddUserMessage(messages[i].Content);
                    }
                    else if ((messages[i].Role).ToString() == "Ai")
                    {
                        newMemory.ChatHistory.AddAiMessage(messages[i].Content);
                    }
                }

                _conversationMemory = newMemory;
            }
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

            // Human-AI ペアの数なので、オプションの ChatHistoryCount に設定
            _historyLimit = limit / 2;
            AdjustConversationMemory();
        }

        /// <summary>
        /// 会話履歴を整形して返すメソッド
        /// </summary>
        /// <returns></returns>
        public string GetConversationHistory()
        {
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