using System.Windows.Media;

namespace Shine
{
    public static class BrushHelper
    {
        /// <summary>
        /// Brush を 16進数文字列 (例: #RRGGBB) に変換する
        /// </summary>
        public static string ConvertBrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush solidColorBrush)
            {
                var color = solidColorBrush.Color;
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#000000"; // 変換できない場合は黒
        }
    }
}
