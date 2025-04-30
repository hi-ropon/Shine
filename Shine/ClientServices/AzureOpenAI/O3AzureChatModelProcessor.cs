using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Shine
{
    /// <summary>
    /// Azure OpenAI のチャットモデルを処理するクラス
    /// </summary>
    public class O3AzureChatModelProcessor : IChatModelProcessor
    {
        private readonly ChatClient _chatClient;
        private readonly ChatCompletionOptions _completionOptions;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public O3AzureChatModelProcessor(ChatClient chatClient)
        {
            _chatClient = chatClient;
            _completionOptions = new ChatCompletionOptions
            {
                ReasoningEffortLevel = "high",
            };
        }

        /// <summary>
        /// Azure OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
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