// ────────────────────────────────────────────────────────
//  ファイル名: InlineChatSession.cs
//  説明    : 1 TextView につき 1 インスタンスのインラインチャット＆差分プレビュー
//             ・差分ビューの FontSize / FontFamily / ZoomLevel をエディタに合わせる
//             ・差分パネル幅 = Viewport 幅の 90%、高さ = 40%（最小 100px）
//             ・ShineFeatureGate で Suggestion と排他制御
//             ・Backspace / Delete / 方向キー対応 (InlineChatKeyFilter)
//  対応 VS : 2022 以降
// ────────────────────────────────────────────────────────
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Shine
{
    /// <summary>インラインチャットと差分プレビューを管理するクラス（1 TextView = 1 インスタンス）</summary>
    internal sealed class InlineChatSession
    {
        private readonly IWpfTextView _view;
        private readonly IChatClientService _chat;
        private readonly AsyncPackage _pkg;
        private readonly IAdornmentLayer _layer;
        private readonly TextBox _input;
        private readonly Border _panel;
        private readonly Button _sendButton;
        private readonly Button _cancelButton;
        private InlineChatKeyFilter? _keyFilter;
        private readonly IVsTextView _viewAdapter;

        // 差分ビュー関連
        private IWpfDifferenceViewer? _diffViewer;
        private double _diffZoomLevel = 0.5;

        #region ctor
        internal InlineChatSession(IWpfTextView view, IVsTextView viewAdapter,
                                   IChatClientService chat, AsyncPackage pkg)
        {
            _view = view;
            _viewAdapter = viewAdapter;
            _chat = chat;
            _pkg = pkg;
            _layer = _view.GetAdornmentLayer("ShineInlineChat");

            // ───── TextBox ─────
            _input = new TextBox
            {
                Width = 350,
                Height = 24,
                FontFamily = new FontFamily("Cascadia Code"),
                Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x22, 0x22, 0x22)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.SteelBlue,
                AcceptsReturn = true,
                AcceptsTab = false,
                Focusable = true,
            };

            _input.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; Clear(); }
            };
            _input.KeyDown += OnKeyDown;


            // ───── 送信 / キャンセル ─────
            _sendButton = new Button
            {
                Content = "▶ 送信",
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                MinWidth = 60,
            };
            _sendButton.Click += async (s, e) => await SendAsync();

            _cancelButton = new Button
            {
                Content = "✖ キャンセル",
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                MinWidth = 60,
            };
            _cancelButton.Click += (s, e) => Clear();

            // ───── レイアウトパネル ─────
            var panelGrid = new Grid { Background = Brushes.Transparent };
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panelGrid.Children.Add(_input); Grid.SetColumn(_input, 0);
            panelGrid.Children.Add(_sendButton); Grid.SetColumn(_sendButton, 1);
            panelGrid.Children.Add(_cancelButton); Grid.SetColumn(_cancelButton, 2);

            _panel = new Border
            {
                Child = panelGrid,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
            };
        }
        #endregion

        #region Public API
        internal void Toggle()
        {
            if (_layer.Elements.Count > 0) Clear(); else ShowInput();
        }
        #endregion

        #region UI 表示 / 破棄
        private void ShowInput()
        {
            if (!ShineFeatureGate.TryBeginInlineChat()) { System.Media.SystemSounds.Beep.Play(); return; }

            _layer.RemoveAllAdornments();
            var caret = _view.Caret.Position.BufferPosition;
            var span = new SnapshotSpan(caret, 0);
            var geom = _view.TextViewLines.GetMarkerGeometry(span) ?? CreateFallbackGeometry(caret);
            if (geom == null) return;

            Canvas.SetLeft(_panel, geom.Bounds.Left);
            Canvas.SetTop(_panel, geom.Bounds.Bottom + 4);
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, span, null, _panel, null);

            _input.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => { _input.Focus(); Keyboard.Focus(_input); }));

            _keyFilter ??= new InlineChatKeyFilter(_input, _viewAdapter);
        }

        private Geometry? CreateFallbackGeometry(SnapshotPoint caret)
        {
            var lineView = _view.TextViewLines.GetTextViewLineContainingBufferPosition(caret);
            if (lineView != null)
            {
                var ch = lineView.GetCharacterBounds(caret);
                return new RectangleGeometry(new Rect(ch.Left, ch.Top, ch.Width, ch.Height));
            }
            return null;
        }

        private void Clear()
        {
            _layer.RemoveAllAdornments();
            _diffViewer?.Close(); _diffViewer = null;

            if (_keyFilter != null)
            {
                _viewAdapter.RemoveCommandFilter(_keyFilter);
                _keyFilter = null;
            }
            ShineFeatureGate.EndInlineChat();
        }
        #endregion

        #region 送信ロジック
        private async Task SendAsync()
        {
            var prompt = _input.Text;
            if (string.IsNullOrWhiteSpace(prompt)) return;

            _sendButton.IsEnabled = _cancelButton.IsEnabled = _input.IsEnabled = false;

            string reply;
            try
            { 
                reply = await _chat.GetChatResponseAsync(prompt);
            }
            catch (Exception ex)
            {
                ShinePackage.MessageService.ShowError(ex, "AI 応答でエラー"); Clear();
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ShowDiff(reply);
        }

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true; Clear();
            }
        }
        #endregion

        #region 差分ビュー
        private void ShowDiff(string newCode)
        {
            // ─── 差分バッファ作成 ───
            var oldSnap = _view.TextSnapshot;
            var oldCode = oldSnap.GetText();
            var compModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var bufFactory = compModel.GetService<ITextBufferFactoryService>();
            var ctRegistry = compModel.GetService<IContentTypeRegistryService>();
            var diffBufSvc = compModel.GetService<IDifferenceBufferFactoryService>();
            var diffViewFac = compModel.GetService<IWpfDifferenceViewerFactoryService>();

            var contentType = ctRegistry.GetContentType("text");
            var leftBuffer = bufFactory.CreateTextBuffer(oldCode, contentType);
            var rightBuffer = bufFactory.CreateTextBuffer(newCode, contentType);
            var diffBuf = diffBufSvc.CreateDifferenceBuffer(leftBuffer, rightBuffer);

            _diffViewer = diffViewFac.CreateDifferenceView(diffBuf);

            // ─── フォント/ズーム調整 ───
            var defaultProps = _view.FormattedLineSource.DefaultTextProperties;
            double fontSize = defaultProps.FontRenderingEmSize;
            var family = defaultProps.Typeface.FontFamily;

            // ZoomLevel はそのまま合わせる
            foreach (var v in new[] { _diffViewer.LeftView, _diffViewer.RightView, _diffViewer.InlineView })
            {
                if (v == null) continue;

                if (v.VisualElement is FrameworkElement fe)
                {
                    fe.SetValue(Control.FontSizeProperty, fontSize);
                    fe.SetValue(Control.FontFamilyProperty, family);
                }
            }

            // ─── ビジュアル要素取得 & サイズ制限 ───
            var diffCtrl = _diffViewer.VisualElement;
            diffCtrl.MaxWidth = Math.Max(100, _view.ViewportWidth * 1.00);
            diffCtrl.MaxHeight = Math.Max(100, _view.ViewportHeight * 0.40);
            diffCtrl.MinWidth = 100;
            diffCtrl.MinHeight = 100;

            // ─── ボタンバー ───
            var accept = new Button { Content = "✔ Accept", Margin = new Thickness(4) };
            var cancel = new Button { Content = "✖ Cancel", Margin = new Thickness(4) };
            accept.Click += (s, e) => ApplyChanges(newCode);
            cancel.Click += (s, e) => Clear();

            // ─── ズーム操作 ───
            var zoomIn = new Button { Content = "+", Margin = new Thickness(4), MinWidth = 24 };
            var zoomOut = new Button { Content = "-", Margin = new Thickness(4), MinWidth = 24 };
            zoomIn.Click += (_, __) => { _diffZoomLevel += 0.1; ApplyDiffZoom(); };
            zoomOut.Click += (_, __) => { _diffZoomLevel = Math.Max(0.1, _diffZoomLevel - 0.1); ApplyDiffZoom(); };

            var btnBar = new StackPanel { Orientation = Orientation.Horizontal };
            btnBar.Children.Add(accept);
            btnBar.Children.Add(cancel);
            btnBar.Children.Add(zoomOut);
            btnBar.Children.Add(zoomIn);

            // ─── 全体スタックに差分とボタンバーを追加 ───
            var stack = new StackPanel();
            stack.Children.Add(_diffViewer.VisualElement);
            stack.Children.Add(btnBar);

            _layer.RemoveAllAdornments();
            _layer.AddAdornment(
              AdornmentPositioningBehavior.OwnerControlled,
              new SnapshotSpan(_view.Caret.Position.BufferPosition, 0),
              null,
              stack,
              null);

            ApplyDiffZoom();
        }

        /// <summary>
        /// _diffViewer が非 null のとき、各ビューに _diffZoomLevel を適用
        /// </summary>
        private void ApplyDiffZoom()
        {
            if (_diffViewer == null) return;

            // 左右／インラインすべてに適用
            foreach (var view in new[] { _diffViewer.LeftView, _diffViewer.RightView, _diffViewer.InlineView })
            {
                if (view != null)
                {
                    view.ZoomLevel = _diffZoomLevel * _view.ZoomLevel;
                }
            }
        }


        private void ApplyChanges(string newCode)
        {
            using (var edit = _view.TextBuffer.CreateEdit())
            {
                edit.Replace(0, _view.TextSnapshot.Length, newCode);
                edit.Apply();
            }
            Clear();
        }
        #endregion
    }
}
