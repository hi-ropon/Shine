// ファイル名: ChatMessageFormatterTests.cs
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
            string iconBase64 = ""; // ユーザー側はアイコン無し

            // Act
            string result = ChatMessageFormatter.FormatMessage(sender, message, pipeline, iconBase64);

            // Assert
            Assert.IsTrue(result.Contains("Hello, <strong>world</strong>!"), "Markdown変換結果が正しく出力されていること");
            Assert.IsTrue(result.Contains("User"), "送信者名が含まれていること");
            Assert.IsTrue(result.Contains("class='message user'"), "CSSクラスが 'user' となっていること");
        }
    }
}
