using System.Threading.Tasks;
namespace Shine
{
    public interface IChatClientService
    {
        Task<string> GetChatResponseAsync(string userMessage);
    }
}