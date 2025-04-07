using System.Threading.Tasks;

namespace Shine.Tests.Mocks
{
    public class MockChatClientService : IChatClientService
    {
        public string PredefinedResponse { get; set; } = "Mock response";

        public Task<string> GetChatResponseAsync(string userMessage)
        {
            return Task.FromResult(PredefinedResponse);
        }
    }
}
