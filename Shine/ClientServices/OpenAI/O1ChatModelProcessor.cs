using System.Text.Json;
using System.Threading.Tasks;
using System.ClientModel;
using System;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// O1ChatModelProcessor クラスは、OpenAI のチャットモデルを処理するクラスです。
    /// </summary>
    public class O1ChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _client;
        private readonly string _model;

        /// <summary>
        /// O1ChatModelProcessor クラスのコンストラクタ
        /// </summary>
        /// <param name="client"></param>
        /// <param name="model"></param>
        public O1ChatModelProcessor(ChatClient client, string model)
        {
            _client = client;
            _model = model;
        }

        /// <summary>
        /// OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            string json = $@"
{{
    ""model"": ""{_model}"",
    ""temperature"": 1.0,
    ""messages"": [
        {{
            ""role"": ""user"",
            ""content"": ""{EscapeJson(userMessage)}""
        }}
    ]
}}";
            BinaryData input = BinaryData.FromString(json);
            using var content = BinaryContent.Create(input);
            ClientResult result = await _client.CompleteChatAsync(content);
            BinaryData output = result.GetRawResponse().Content;
            using var doc = JsonDocument.Parse(output.ToString());
            string message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return message;
        }

        /// <summary>
        /// JSON 文字列をエスケープする
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string EscapeJson(string input)
        {
            return JsonSerializer.Serialize(input).Trim('\"');
        }
    }
}