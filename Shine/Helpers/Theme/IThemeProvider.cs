using System.Windows.Media;

namespace Shine
{
    /// <summary>
    /// テーマプロバイダインターフェース
    /// </summary>
    public interface IThemeProvider
    {
        /// <summary>
        /// ツールウィンドウの背景色を取得する
        /// </summary>
        /// <returns></returns>
        Color GetToolWindowBackgroundColor();
    }
}
