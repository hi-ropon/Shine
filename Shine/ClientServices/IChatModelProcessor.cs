using System.Threading.Tasks;

namespace Shine
{
    public interface IChatModelProcessor
    {
        Task<string> GetChatReplyAsync(string userMessage);
    }
}