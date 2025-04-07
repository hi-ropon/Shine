// �t�@�C����: ChatMessageFormatterTests.cs
using System.Net;
using Markdig;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class ChatMessageFormatterTests
    {
        [TestMethod]
        public void FormatMessage_ReturnsHtmlSnippet_ForUserMessage()
        {
            // Arrange
            var pipeline = new MarkdownPipelineBuilder().Build();
            string sender = "User";
            string message = "Hello, **world**!";
            string iconBase64 = ""; // ���[�U�[���̓A�C�R������

            // Act
            string result = ChatMessageFormatter.FormatMessage(sender, message, pipeline, iconBase64);

            // Assert
            Assert.IsTrue(result.Contains("Hello, <strong>world</strong>!"), "Markdown�ϊ����ʂ��������o�͂���Ă��邱��");
            Assert.IsTrue(result.Contains("User"), "���M�Җ����܂܂�Ă��邱��");
            Assert.IsTrue(result.Contains("class='message user'"), "CSS�N���X�� 'user' �ƂȂ��Ă��邱��");
        }
    }
}
