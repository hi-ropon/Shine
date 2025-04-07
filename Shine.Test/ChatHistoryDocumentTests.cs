// ファイル名: ChatHistoryDocumentTests.cs
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class ChatHistoryDocumentTests
    {
        [TestMethod]
        public void AppendChatMessage_And_GetHtml_ReturnsExpectedHtml()
        {
            // Arrange
            System.Windows.Media.Brush brush = System.Windows.Media.Brushes.Black;
            string dummyIcon = "dummyBase64";
            ChatHistoryDocument document = new ChatHistoryDocument(brush, dummyIcon);
            string messageBlock = "<div>Test message</div>";
            document.AppendChatMessage(messageBlock);
            document.AppendScript("<script>test();</script>");

            // Act
            string html = document.GetHtml();

            // Assert
            Assert.IsTrue(html.Contains(messageBlock), "追加したメッセージブロックが HTML 内に存在すること");
            Assert.IsTrue(html.Contains("<script>test();</script>"), "スクリプトが HTML 内に追加されていること");
        }
    }
}
