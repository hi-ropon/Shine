using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Shine.Suggestion
{
    internal static class ThinkingAdornmentLayerProvider
    {
        /// <summary>
        /// エディタ上の "Thinking..." 表示用アドオンレイヤー定義
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ShineThinkingAdornment")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        internal static AdornmentLayerDefinition _shineThinkingAdornmentLayer = null!;

        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ShineInlineChat")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        internal static AdornmentLayerDefinition _inlineChatLayer = null!;
    }
}
