using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Shine
{
    public class O1AzureChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;

        public O1AzureChatModelProcessor(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            var completion = await _chatClient.CompleteChatAsync(new UserChatMessage(userMessage));
            return completion.Value.Content[0].Text;
        }
    }
}