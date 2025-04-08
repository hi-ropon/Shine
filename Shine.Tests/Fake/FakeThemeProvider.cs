using System.Windows.Media;

namespace Shine.Tests
{
    public class FakeThemeProvider : IThemeProvider
    {
        public System.Windows.Media.Color GetToolWindowBackgroundColor()
        {
            // テストでは任意の固定色（例: White）を返す
            return Colors.White;
        }
    }
}
