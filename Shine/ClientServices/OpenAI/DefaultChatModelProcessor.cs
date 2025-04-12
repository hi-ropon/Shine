using System.Text.Json;
using System.Threading.Tasks;
using System.ClientModel;
using System;
using OpenAI.Chat;

namespace Shine
{
    public class DefaultChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _client;
        private readonly string _model;
        private readonly float _temperature;

        public DefaultChatModelProcessor(ChatClient client, string model, float temperature)
        {
            _client = client;
            _model = model;
            _temperature = temperature;
        }

        public async Task<string> GetChatReplyAsync(string userMessage)
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

        private static string EscapeJson(string input)
        {
            return JsonSerializer.Serialize(input).Trim('\"');
        }
    }
}