using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Markdig;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shine;

namespace Shine.Tests
{
    // WebView2 のラッパーのフェイク実装（テスト用）
    public class FakeWebView2Wrapper : IWebView2Wrapper
    {
        public string NavigatedHtml { get; private set; }
        public string ExecutedScript { get; private set; }

        public void NavigateToString(string html)
        {
            NavigatedHtml = html;
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            ExecutedScript = script;
            return Task.FromResult(string.Empty);
        }
    }

    // ChatHistoryDocument のテスト用フェイク実装
    public class FakeChatHistoryDocument : ChatHistoryDocument
    {
        private string _html;

        // Pipeline はテスト上のダミー文字列として利用
        public override MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().Build();

        public override int MaxChatHistoryCount { get; set; }

        public FakeChatHistoryDocument(System.Windows.Media.Brush foregroundBrush, string assistantIconBase64)
            : base(foregroundBrush, assistantIconBase64)
        {
            _html = "<html><body></body></html>";
        }

        public override string GetHtml()
        {
            return _html;
        }

        public override void Initialize()
        {
            _html = "<html><body></body></html>";
        }

        public override void AppendChatMessage(string messageBlock)
        {
            int index = _html.IndexOf("</body>");
            if (index >= 0)
            {
                _html = _html.Insert(index, messageBlock);
            }
            else
            {
                _html += messageBlock;
            }
        }

        public override void AppendScript(string script)
        {
            int index = _html.IndexOf("</body>");
            if (index >= 0)
            {
                _html = _html.Insert(index, script);
            }
            else
            {
                _html += script;
            }
        }
    }

    [TestClass]
    public class ChatHistoryTests
    {
        // ヘルパーメソッド：FakeChatHistoryDocument を利用した ChatHistory を作成する
        private ChatHistory CreateFakeChatHistory(IWebView2Wrapper webView, System.Windows.Media.Brush brush)
        {
            var fakeDocument = new FakeChatHistoryDocument(brush, string.Empty);
            var fakeThemeProvider = new FakeThemeProvider();
            return new ChatHistory(webView, brush, fakeDocument, fakeThemeProvider);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullWebView_ThrowsException()
        {
            var brush = new SolidColorBrush(Colors.Black);
            // WebView2 に null を渡すと例外が発生するはず
            var chatHistory = new ChatHistory(null, brush);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullBrush_ThrowsException()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            // Brush に null を渡すと例外が発生するはず
            var chatHistory = new ChatHistory(fakeWebView, null);
        }

        [TestMethod]
        public void AddChatMessage_UpdatesHtml()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            var brush = new SolidColorBrush(Colors.Black);
            var chatHistory = CreateFakeChatHistory(fakeWebView, brush);

            // 初期 HTML を取得
            string initialHtml = fakeWebView.NavigatedHtml;

            chatHistory.AddChatMessage("User", "Hello World");

            // メッセージ追加後、HTML が更新されているか検証
            Assert.IsNotNull(fakeWebView.NavigatedHtml);
            Assert.AreNotEqual(initialHtml, fakeWebView.NavigatedHtml);
            Assert.IsTrue(fakeWebView.NavigatedHtml.Contains("Hello World"));
        }

        [TestMethod]
        public void AddConversationMemory_AddsMessages()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            var brush = new SolidColorBrush(Colors.Black);
            var chatHistory = CreateFakeChatHistory(fakeWebView, brush);

            chatHistory.AddConversationMemory("User", "User message");
            chatHistory.AddConversationMemory("Assistant", "Assistant message");

            string history = chatHistory.GetConversationHistory();
            Assert.IsTrue(history.Contains("User message"));
            Assert.IsTrue(history.Contains("Assistant message"));
        }

        [TestMethod]
        public void ClearHistory_ClearsConversationMemory()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            var brush = new SolidColorBrush(Colors.Black);
            var chatHistory = CreateFakeChatHistory(fakeWebView, brush);

            chatHistory.AddConversationMemory("User", "User message");
            Assert.IsFalse(string.IsNullOrWhiteSpace(chatHistory.GetConversationHistory()));

            chatHistory.ClearHistory();
            string history = chatHistory.GetConversationHistory();
            Assert.IsTrue(string.IsNullOrWhiteSpace(history));
        }

        [TestMethod]
        public async Task UpdateForegroundColorAsync_ExecutesScript()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            var brush = new SolidColorBrush(Colors.Black);
            var chatHistory = CreateFakeChatHistory(fakeWebView, brush);

            var newBrush = new SolidColorBrush(Colors.Red);
            await chatHistory.UpdateForegroundColorAsync(newBrush);

            Assert.IsNotNull(fakeWebView.ExecutedScript);
            Assert.IsTrue(fakeWebView.ExecutedScript.Contains("document.body.style.color"));
        }
    }
}
