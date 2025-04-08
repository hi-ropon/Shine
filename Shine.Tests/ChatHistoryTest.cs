using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Markdig;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shine;

namespace Shine.Tests
{
    // WebView2 �̃��b�p�[�̃t�F�C�N�����i�e�X�g�p�j
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

    // ChatHistoryDocument �̃e�X�g�p�t�F�C�N����
    public class FakeChatHistoryDocument : ChatHistoryDocument
    {
        private string _html;

        // Pipeline �̓e�X�g��̃_�~�[������Ƃ��ė��p
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
        // �w���p�[���\�b�h�FFakeChatHistoryDocument �𗘗p���� ChatHistory ���쐬����
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
            // WebView2 �� null ��n���Ɨ�O����������͂�
            var chatHistory = new ChatHistory(null, brush);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullBrush_ThrowsException()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            // Brush �� null ��n���Ɨ�O����������͂�
            var chatHistory = new ChatHistory(fakeWebView, null);
        }

        [TestMethod]
        public void AddChatMessage_UpdatesHtml()
        {
            var fakeWebView = new FakeWebView2Wrapper();
            var brush = new SolidColorBrush(Colors.Black);
            var chatHistory = CreateFakeChatHistory(fakeWebView, brush);

            // ���� HTML ���擾
            string initialHtml = fakeWebView.NavigatedHtml;

            chatHistory.AddChatMessage("User", "Hello World");

            // ���b�Z�[�W�ǉ���AHTML ���X�V����Ă��邩����
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
