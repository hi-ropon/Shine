using System.Threading.Tasks;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// Azure OpenAI のチャットモデルを処理するクラス
    /// </summary>
    public class DefaultAzureChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;

        /// <summary>
        /// Azure OpenAI のチャットモデルを処理するクラスのコンストラクタ
        /// </summary>
        /// <param name="chatClient"></param>
        public DefaultAzureChatModelProcessor(ChatClient chatClient)
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
            var completion = await _chatClient.CompleteChatAsync(
                new SystemChatMessage("あなたはプログラミング支援 AI です。"),
                new UserChatMessage(userMessage));
            return completion.Value.Content[0].Text;
        }
    }
}