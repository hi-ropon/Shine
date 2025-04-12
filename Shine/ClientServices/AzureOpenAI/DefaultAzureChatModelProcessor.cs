using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Shine
{
    public class DefaultAzureChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;

        public DefaultAzureChatModelProcessor(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            var completion = await _chatClient.CompleteChatAsync(
                new SystemChatMessage("あなたはプログラミング支援 AI です。"),
                new UserChatMessage(userMessage));
            return completion.Value.Content[0].Text;
        }
    }
}