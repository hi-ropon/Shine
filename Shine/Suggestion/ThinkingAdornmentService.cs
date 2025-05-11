using System.Windows.Controls;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.ComponentModelHost;
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

        private static readonly Dictionary<IWpfTextView, AdornmentContext> _state = new();


        /// <summary>初期化（TextView 作成時に呼ばれる）</summary>
        public static void Initialize(IWpfTextView view)
        {
            // レイアウト完了後に一度だけ Show を試す
            view.LayoutChanged += OnLayoutChangedOnce;
        }

        private static void OnLayoutChangedOnce(object? sender, TextViewLayoutChangedEventArgs e)
        {
            if (sender is IWpfTextView view)
            {
                view.LayoutChanged -= OnLayoutChangedOnce;
                Show(view);
            }
        }

        /// <summary>“Thinking…” を表示</summary>
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
                ctx.RemoveMarker();
        }

        /// <summary>ビューごとのアドオン管理</summary>
        private sealed class AdornmentContext
        {
            private readonly IWpfTextView _view;
            private readonly IAdornmentLayer _layer;
            private readonly TextBlock _marker;

            [Import] private IClassificationFormatMapService _formatMapService = null!;
            [Import] private IClassificationTypeRegistryService _typeRegistry = null!;
            internal AdornmentContext(IWpfTextView view)
            {
                _view = view;
                _layer = view.GetAdornmentLayer(layerName);

                var model = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                model.DefaultCompositionService.SatisfyImportsOnce(this);

                // コメントの色とエディタ既定のフォントサイズを取得
                var commentType = _typeRegistry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
                var formatMap = _formatMapService.GetClassificationFormatMap(view);
                var textProps = formatMap.GetTextProperties(commentType);
                var commentBrush = textProps.ForegroundBrush;
                double fontSize = textProps.FontRenderingEmSize;

                // フォントファミリを指定　例: Cascadia Code を使う場合（Fira Code, Consolas）
                var fontFamily = new FontFamily("ＭＳ ゴシック");

                _marker = new TextBlock
                {
                    Text = "Thinking…",
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = commentBrush,
                    Background = null,
                    Opacity = 1.0
                };
            }

            internal void ShowMarker()
            {
                _layer.RemoveAllAdornments();

                var caret = _view.Caret.Position.BufferPosition;
                var span = new SnapshotSpan(caret, 0);

                // 通常のマーカー位置取得を試みる
                var geom = _view.TextViewLines.GetMarkerGeometry(span)
                         ?? CreateFallbackGeometry(caret);

                if (geom == null)
                    return;

                // キャレットの仮想空白まで含めたX座標を計算
                double left = geom.Bounds.Left;
                int vs = _view.Caret.Position.VirtualSpaces;
                if (vs > 0)
                {
                    // フォールバック時に tbWidth を取得できるよう一時保持しておく
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
                    span, null, _marker, null);

                LogHelper.DebugLog($"Thinking marker added at {left},{geom.Bounds.Top}");
            }

            /// <summary>
            /// 通常の GetMarkerGeometry が null を返したときに、
            /// キャレット位置の文字セルを矩形で表して Geometry を返すメソッド
            /// </summary>
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

            internal void RemoveMarker() => _layer.RemoveAllAdornments();
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
            => ThinkingAdornmentService.Initialize(textView);
    }
}
