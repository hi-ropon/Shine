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
            // ���M�҂� "USER" �̏ꍇ�ACSS �N���X�� "user" �ɂȂ�͂�
            StringAssert.Contains(result, "class='message user'");
            StringAssert.Contains(result, "Hello");
        }

        [TestMethod]
        public void FormatMessage_Assistant_ReturnsAssistantCssClassAndIcon()
        {
            var pipeline = new MarkdownPipelineBuilder().Build();
            string result = ChatMessageFormatter.FormatMessage("Assistant", "Response", pipeline, "dummyIcon");
            // ���M�҂� "Assistant" �̏ꍇ�ACSS �N���X�� "assistant" �ƂȂ�A�A�C�R���摜���܂ނ͂�
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
