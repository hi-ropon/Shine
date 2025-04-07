// �t�@�C����: ChatHistoryDocumentTests.cs
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
            Assert.IsTrue(html.Contains(messageBlock), "�ǉ��������b�Z�[�W�u���b�N�� HTML ���ɑ��݂��邱��");
            Assert.IsTrue(html.Contains("<script>test();</script>"), "�X�N���v�g�� HTML ���ɒǉ�����Ă��邱��");
        }
    }
}
