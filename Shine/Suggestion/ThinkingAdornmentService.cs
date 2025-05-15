using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.StandardClassification;
using Shine.Helpers;

namespace Shine.Suggestion
{
    /// <summary>
    /// カーソル付近に "Thinking..." を表示／非表示するサービス
    /// </summary>
    internal static class ThinkingAdornmentService
    {
        public const string layerName = "ShineThinkingAdornment";

        private static readonly Dictionary<IWpfTextView, AdornmentContext> _state =
            new Dictionary<IWpfTextView, AdornmentContext>();

        /// <summary>
        /// TextView 生成時の初期化  
        /// （ビューのライフサイクルにフックするだけで *Thinking…* は出さない）
        /// </summary>
        public static void Initialize(IWpfTextView view)
        {
            if (!_state.ContainsKey(view))
            {
                _state[view] = new AdornmentContext(view);
            }
        }

        /// <summary>*Thinking…* を表示</summary>
        public static void Show(IWpfTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_state.TryGetValue(view, out var ctx))
            {
                ctx = new AdornmentContext(view);
                _state[view] = ctx;
            }
            ctx.ShowMarker();
        }

        /// <summary>表示をクリア</summary>
        public static void Hide(IWpfTextView view)
        {
            if (_state.TryGetValue(view, out var ctx))
            {
                ctx.RemoveMarker();
            }
        }

        /// <summary>ビューごとのアドオン管理</summary>
        private sealed class AdornmentContext
        {
            private readonly IWpfTextView _view;
            private readonly IAdornmentLayer _layer;
            private readonly TextBlock _marker;

            [Import]
            private IClassificationFormatMapService _formatMapService = null!;

            [Import]
            private IClassificationTypeRegistryService _typeRegistry = null!;

            internal AdornmentContext(IWpfTextView view)
            {
                _view = view;
                _layer = view.GetAdornmentLayer(layerName);

                var model = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                model.DefaultCompositionService.SatisfyImportsOnce(this);

                var commentType = _typeRegistry.GetClassificationType(
                    PredefinedClassificationTypeNames.Comment);
                var formatMap = _formatMapService.GetClassificationFormatMap(view);
                var textProps = formatMap.GetTextProperties(commentType);

                _marker = new TextBlock
                {
                    Text = "Thinking…",
                    FontFamily = new FontFamily("ＭＳ ゴシック"),
                    FontSize = textProps.FontRenderingEmSize,
                    Foreground = textProps.ForegroundBrush,
                    Background = null,
                    Opacity = 1.0
                };
            }

            internal void ShowMarker()
            {
                _layer.RemoveAllAdornments();

                var caret = _view.Caret.Position.BufferPosition;
                var span = new SnapshotSpan(caret, 0);
                var geom = _view.TextViewLines.GetMarkerGeometry(span)
                           ?? CreateFallbackGeometry(caret);

                if (geom == null)
                {
                    return;
                }

                double left = geom.Bounds.Left;
                int vs = _view.Caret.Position.VirtualSpaces;

                if (vs > 0)
                {
                    var lineView = _view.TextViewLines.GetTextViewLineContainingBufferPosition(caret);
                    if (lineView != null)
                    {
                        var tb = lineView.GetCharacterBounds(caret);
                        left += vs * tb.Width;
                    }
                }

                Canvas.SetLeft(_marker, left);
                Canvas.SetTop(_marker, geom.Bounds.Top);

                _layer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    span,
                    null,
                    _marker,
                    null);

                LogHelper.DebugLog($"Thinking marker added at {left},{geom.Bounds.Top}");
            }

            private Geometry CreateFallbackGeometry(SnapshotPoint caret)
            {
                var lineView = _view.TextViewLines.GetTextViewLineContainingBufferPosition(caret);
                if (lineView != null)
                {
                    var tb = lineView.GetCharacterBounds(caret);
                    var rect = new Rect(tb.Left, tb.Top, tb.Width, tb.Height);
                    return new RectangleGeometry(rect);
                }
                return null;
            }

            internal void RemoveMarker()
            {
                _layer.RemoveAllAdornments();
            }
        }
    }

    /// <summary>
    /// テキストビュー生成時に ThinkingAdornmentService を初期化
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code"), TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class ThinkingAdornmentListener : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            ThinkingAdornmentService.Initialize(textView);
        }
    }
}
