using System.Threading.Tasks;
using OpenAI.Chat;

namespace Shine
{
    public class OpenAiClientService : IChatClientService
    {
        private readonly ChatClient _client;
        private readonly string _model;
        private readonly float _temperature;
        private readonly string _reasoningEffort;
        private readonly IChatModelProcessor _processor;

        public OpenAiClientService(string apiKey, string model, float temperature)
        {
            _client = new ChatClient(model, apiKey);
            _model = model;
            _temperature = temperature;
            _reasoningEffort = "high";

            _processor = _model switch
            {
                "o1-mini" => new O1ChatModelProcessor(_client, _model),
                "o3-mini" => new O3ChatModelProcessor(_client, _model, _reasoningEffort),
                _ => new DefaultChatModelProcessor(_client, _model, _temperature)
            };
        }

        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            return await _processor.GetChatReplyAsync(userMessage);
        }
    }
}