using System.Threading.Tasks;
using OpenAI.Chat;
using System.Collections.Generic;

namespace Shine
{
    /// <summary>
    /// O4ChatModelProcessor クラスは、OpenAI のチャットモデルを処理するクラスです
    /// </summary>
    public class O4ChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;
        private readonly ChatCompletionOptions _completionOptions;

        /// <summary>
        /// O4ChatModelProcessor クラスのコンストラクタ
        /// </summary>
        /// <param name="client"></param>
        /// <param name="model"></param>
        /// <param name="reasoningEffort"></param>
        public O4ChatModelProcessor(ChatClient chatClient)
        {
            _chatClient = chatClient;
            _completionOptions = new ChatCompletionOptions
            {
                ReasoningEffortLevel = "high",
            };
        }

        /// <summary>
        /// OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        public async Task<string> GetChatReplyAsync(string userMessage)
        {
            string systemMessage =
                "#役割\n" +
                "　あなたは優秀なプログラミング支援 AI です。\n" +
                "プログラム単位でコードブロックで出力し、日本語で回答してください。\n" +
                "各プログラムは先頭と末尾を、\n" +
                "```csharp\n" +
                "```\n" +
                "のように各言語のコードフェンスで囲ってください。";

            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(userMessage),
            };

            var completion = await _chatClient.CompleteChatAsync(messages, _completionOptions);
            return completion.Value.Content[0].Text;
        }
    }
}