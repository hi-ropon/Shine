using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;
using Markdig;

namespace Shine.Tests
{
    [TestClass]
    public class ChatMessageFormatterTests
    {
        [TestMethod]
        public void FormatMessage_User_ReturnsUserCssClass()
        {
            var pipeline = new MarkdownPipelineBuilder().Build();
            string result = ChatMessageFormatter.FormatMessage("USER", "Hello", pipeline, "dummyIcon");
            // 送信者が "USER" の場合、CSS クラスは "user" になるはず
            StringAssert.Contains(result, "class='message user'");
            StringAssert.Contains(result, "Hello");
        }

        [TestMethod]
        public void FormatMessage_Assistant_ReturnsAssistantCssClassAndIcon()
        {
            var pipeline = new MarkdownPipelineBuilder().Build();
            string result = ChatMessageFormatter.FormatMessage("Assistant", "Response", pipeline, "dummyIcon");
            // 送信者が "Assistant" の場合、CSS クラスは "assistant" となり、アイコン画像も含むはず
            StringAssert.Contains(result, "class='message assistant'");
            StringAssert.Contains(result, "dummyIcon");
            StringAssert.Contains(result, "Response");
        }

        [TestMethod]
        public void FormatMessage_Error_ReturnsErrorCssClass()
        {
            var pipeline = new MarkdownPipelineBuilder().Build();
            string result = ChatMessageFormatter.FormatMessage("ERROR", "Error occurred", pipeline, "dummyIcon");
            StringAssert.Contains(result, "class='message error'");
            StringAssert.Contains(result, "Error occurred");
        }
    }
}
