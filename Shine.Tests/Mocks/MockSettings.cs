using System.Windows.Controls;

namespace Shine.Tests.Mocks
{
    public class MockSettings : Settings
    {
        public MockSettings(ComboBox modelComboBox) : base(modelComboBox)
        {
            ChatClientService = new MockChatClientService();
        }

        public override void InitializeSettings()
        {
            // No-op for testing
        }

        public override void UpdateModelComboBox()
        {
            // No-op for testing
        }
    }
}
