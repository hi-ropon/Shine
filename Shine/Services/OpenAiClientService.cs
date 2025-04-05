using System;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.ClientModel;
namespace Shine
{
    public class OpenAiClientService : IChatClientService
    {
        private readonly ChatClient _client;
        private readonly string _model;
        private readonly float _temperature;
        private readonly string _apiEndpoint; // 4つ目の引数を保持（未使用の場合もある）
        private readonly string _reasoningEffort;

        /// <summary>
        /// コンストラクター（3パラメータ版）
        /// </summary>
        public OpenAiClientService(string apiKey, string model, float temperature)
        {
            _client = new ChatClient(model, apiKey);
            _model = model;
            _temperature = temperature;
            _reasoningEffort = "high";
        }

        /// <summary>
        /// モデルに応じたチャット応答を取得する統一メソッド
        /// </summary>
        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            if (_model == "o1-mini")
            {
                return await GetO1ChatReplyAsync(userMessage);
            }
            else if (_model == "o3-mini")
            {
                return await GetO3ChatReplyAsync(userMessage);
            }
            else
            {
                return await GetChatReplyAsync(userMessage);
            }
        }

        /// <summary>
        /// コード補完用のプロンプトを送信し、生成されたテキストを取得します。
        /// </summary>
        public async Task<string> GetCompletionAsync(string prompt)
        {
            string json = $@"
{{
    ""model"": ""{_model}"",
    ""temperature"": {_temperature},
    ""max_tokens"": 800,
    ""messages"": [
        {{
            ""role"": ""system"",
            ""content"": ""次のコードの続きを生成してください:""
        }},
        {{
            ""role"": ""user"",
            ""content"": ""{EscapeJson(prompt)}""
        }}
    ]
}}";
            // JSON 文字列から BinaryData を生成
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
        /// チャット API を利用して、ユーザーのメッセージに対する AI の応答を取得します。
        /// </summary>
        public async Task<string> GetChatReplyAsync(string userMessage, string conversationHistory = "")
        {
            string json = $@"
{{
    ""model"": ""{_model}"",
    ""temperature"": {_temperature},
    ""max_tokens"": 800,
    ""messages"": [
        {{
            ""role"": ""system"",
            ""content"": ""あなたはプログラミング支援 AI です。""
        }},
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
        /// o1モデルのチャット API を利用して、ユーザーのメッセージに対する AI の応答を取得します。
        /// </summary>
        public async Task<string> GetO1ChatReplyAsync(string userMessage, string conversationHistory = "")
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
        /// o3モデルのチャット API を利用して、ユーザーのメッセージに対する AI の応答を取得します。
        /// </summary>
        public async Task<string> GetO3ChatReplyAsync(string userMessage, string conversationHistory = "")
        {
            string json = $@"
{{
    ""model"": ""{_model}"",
    ""temperature"": 1.0,
    ""reasoning_effort"": ""{_reasoningEffort}"",
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
        /// JSON 用のエスケープ処理を行います。
        /// </summary>
        private static string EscapeJson(string input)
        {
            // JsonSerializer.Serialize によりエスケープ済み文字列を取得後、先頭と末尾の引用符を除去
            return JsonSerializer.Serialize(input).Trim('\"');
        }
    }
}