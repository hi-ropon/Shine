using System.Text;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Media;

namespace Shine.Tests.Mocks
{
    public class MockChatHistory : ChatHistory
    {
        private readonly StringBuilder _conversationHistory = new StringBuilder();

        public MockChatHistory(WebView2 chatHistoryWebView, System.Windows.Media.Brush foregroundBrush)
            : base(chatHistoryWebView, foregroundBrush)
        {
        }

        public override void AddChatMessage(string senderName, string message)
        {
            _conversationHistory.AppendLine($"{senderName}: {message}");
        }

        public override string GetConversationHistory()
        {
            return _conversationHistory.ToString();
        }

        public override void ClearHistory()
        {
            _conversationHistory.Clear();
        }
    }
}
