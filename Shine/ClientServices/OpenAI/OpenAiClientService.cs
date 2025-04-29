using System.Threading.Tasks;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// OpenAiClientService クラスは、OpenAI のチャットモデルを処理するクラスです
    /// </summary>
    public class OpenAiClientService : IChatClientService
    {
        private readonly ChatClient _client;
        private readonly string _model;
        private readonly float _temperature;
        private readonly string _reasoningEffort;
        private readonly IChatModelProcessor _processor;

        /// <summary>
        /// OpenAiClientService クラスのコンストラクタ
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="model"></param>
        /// <param name="temperature"></param>
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
                "o4-mini" => new O4ChatModelProcessor(_client, _model, _reasoningEffort),
                _ => new DefaultChatModelProcessor(_client, _model, _temperature)
            };
        }

        /// <summary>
        /// OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            return await _processor.GetChatReplyAsync(userMessage);
        }
    }
}