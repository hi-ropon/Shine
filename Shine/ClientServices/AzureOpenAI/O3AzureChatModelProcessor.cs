using System.Threading.Tasks;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// Azure OpenAI のチャットモデルを処理するクラス
    /// </summary>
    public class O3AzureChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;

        /// <summary>
        /// Azure OpenAI のチャットモデルを処理するクラスのコンストラクタ
        /// </summary>
        /// <param name="chatClient"></param>
        public O3AzureChatModelProcessor(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        /// <summary>
        /// Azure OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            var completion = await _chatClient.CompleteChatAsync(new UserChatMessage(userMessage));
            return completion.Value.Content[0].Text;
        }
    }
}