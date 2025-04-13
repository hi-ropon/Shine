using System.Threading.Tasks;

namespace Shine
{
    /// <summary>
    /// IChatModelProcessor インターフェースは、OpenAI のチャットモデルを処理するためのインターフェース
    /// </summary>
    public interface IChatModelProcessor
    {
        /// <summary>
        /// OpenAI にメッセージを送信し、応答を取得する
        /// </summary>
        /// <param name="userMessage"></param>
        /// <returns></returns>
        Task<string> GetChatReplyAsync(string userMessage);
    }
}