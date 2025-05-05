// ファイル名: ThinkingAdornmentService.cs
using System;
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

            internal AdornmentContext(IWpfTextView view)
            {
                _view = view;
                _layer = view.GetAdornmentLayer(layerName);

                _marker = new TextBlock
                {
                    Text = "Thinking…",
                    FontSize = 12,
                    Foreground = Brushes.Yellow,
                    Background = Brushes.Black,
                    Opacity = 0.6
                };
            }

            internal void ShowMarker()
            {
                _layer.RemoveAllAdornments();
                var caret = _view.Caret.Position.BufferPosition;
                var span = new SnapshotSpan(caret, 0);
                var geom = _view.TextViewLines.GetMarkerGeometry(span);
                if (geom == null)
                {
                    var lineView = _view.TextViewLines.GetTextViewLineContainingBufferPosition(caret);
                    if (lineView != null)
                    {
                        var tb = lineView.GetCharacterBounds(caret);
                        // ← TextBounds には Bounds プロパティがないので左上＋幅高さで矩形生成
                        var rect = new Rect(tb.Left, tb.Top, tb.Width, tb.Height);  // ← 修正箇所
                        geom = new RectangleGeometry(rect);
                    }
                }
                if (geom == null) return;
                Canvas.SetLeft(_marker, geom.Bounds.Left);
                Canvas.SetTop(_marker, geom.Bounds.Bottom + 2);
                _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, _marker, null);
                Debug.WriteLine("Thinking marker added.");
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
