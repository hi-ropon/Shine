using System.Text.Json;
using System.Threading.Tasks;
using System.ClientModel;
using System;
using OpenAI.Chat;

namespace Shine
{
    public class O3ChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _client;
        private readonly string _model;
        private readonly string _reasoningEffort;

        public O3ChatModelProcessor(ChatClient client, string model, string reasoningEffort)
        {
            _client = client;
            _model = model;
            _reasoningEffort = reasoningEffort;
        }

        public async Task<string> GetChatReplyAsync(string userMessage)
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

        private static string EscapeJson(string input)
        {
            return JsonSerializer.Serialize(input).Trim('\"');
        }
    }
}