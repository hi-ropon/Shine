using System;
using System.ClientModel;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// Azure OpenAI クライアントサービス
    /// </summary>
    public class AzureOpenAiClientService : IChatClientService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly float _temperature;
        private readonly IChatModelProcessor _processor;

        /// <summary>
        /// Azure OpenAI クライアントサービスのコンストラクタ
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="apiKey"></param>
        /// <param name="deploymentName"></param>
        /// <param name="temperature"></param>
        public AzureOpenAiClientService(string endpoint, string apiKey, string deploymentName, float temperature)
        {
            _client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            _deploymentName = deploymentName;
            _temperature = temperature;

            ChatClient chatClient = _client.GetChatClient(_deploymentName);
            _processor = _deploymentName switch
            {
                "o1-mini" => new O1AzureChatModelProcessor(chatClient),
                _ => new DefaultAzureChatModelProcessor(chatClient)
            };
        }

        /// <summary>
        /// Azure OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            return await _processor.GetChatReplyAsync(userMessage);
        }
    }
}