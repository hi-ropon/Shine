using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace Shine
{
    /// <summary>
    /// VS のテーマに合わせた文字色の更新など、テーマ関連の処理を担当
    /// </summary>
    public class Theme
    {
        /// <summary>
        /// ツールウィンドウの文字色
        /// </summary>
        public Brush ForegroundBrush { get; private set; }

        /// <summary>
        /// コンストラクター
        /// </summary>
        public Theme()
        {
            UpdateTheme();
        }

        /// <summary>
        /// VS のテーマに合わせて ForegroundBrush を更新する
        /// </summary>
        public void UpdateTheme()
        {
            var drawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            var themedColor = Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);

            double brightness = (0.299 * themedColor.R + 0.587 * themedColor.G + 0.114 * themedColor.B);
            ForegroundBrush = brightness > 128 ? Brushes.White : Brushes.Black;
        }

        // 必要に応じて、特定のコントロールへのテーマ適用処理を追加できます
        public void ApplyThemeToControl(UIElement element)
        {
            // 例：子要素に ForegroundBrush を適用するなど
        }
    }
}
