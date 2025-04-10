using System;
using System.ClientModel;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;
namespace Shine
{
    public class AzureOpenAiClientService : IChatClientService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly float _temperature;

        public AzureOpenAiClientService(string endpoint, string apiKey, string deploymentName, float temperature)
        {
            // クライアントを初期化
            _client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            _deploymentName = deploymentName;
        }

        /// <summary>
        /// モデルに応じたチャット応答を取得する統一メソッド
        /// </summary>
        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            if (_deploymentName == "o1-mini")
            {
                return await GetO1ChatReplyAsync(userMessage);
            }
            else
            {
                return await GetChatReplyAsync(userMessage);
            }
        }

        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            ChatClient chatClient = _client.GetChatClient(_deploymentName);
            var completion = await chatClient.CompleteChatAsync(
                new SystemChatMessage("あなたはプログラミング支援 AI です。"),
                new UserChatMessage(userMessage));
            // 応答メッセージを取得
            string message = completion.Value.Content[0].Text;
            return message;
        }

        /// <summary>
        /// 推論モデルのチャット API を利用して、ユーザーのメッセージに対する AI の応答を取得します。
        /// </summary>
        public async Task<string> GetO1ChatReplyAsync(string userMessage)
        {
            ChatClient chatClient = _client.GetChatClient(_deploymentName);
            var completion = await chatClient.CompleteChatAsync(
                new UserChatMessage(userMessage));
            // 応答メッセージを取得
            string message = completion.Value.Content[0].Text;
            return message;
        }
    }
}