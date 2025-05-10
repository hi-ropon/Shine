// ファイル名: InlineChatSession.cs
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
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Shine
{
    /// <summary>1 TextView につき 1 インスタンス。入力＋差分プレビューを管理</summary>
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
        private bool _isImeComposing;
        private InlineChatKeyFilter? _keyFilter;
        private readonly IVsTextView _viewAdapter;

        // 差分ビュー関連
        private IWpfDifferenceViewer _diffViewer;

        internal InlineChatSession(IWpfTextView view, IVsTextView viewAdapter,
                           IChatClientService chat, AsyncPackage pkg)
        {
            _view = view;
            _viewAdapter = viewAdapter;
            _chat = chat;
            _pkg = pkg;
            _layer = _view.GetAdornmentLayer("ShineInlineChat");

            // テキスト入力部分
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

            // PreviewKeyDown でキーを捕捉し、TextBox でのみ処理。エディタへのバブルを防止
            _input.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true; Clear();
                }
                //else if (e.Key == Key.Enter && !_isImeComposing)
                //{
                //    e.Handled = true;
                //    _ = SendAsync();
                //}
            };

            _input.KeyDown += OnKeyDown;

            // IME 確定中フラグの管理
            _input.AddHandler(TextCompositionManager.PreviewTextInputStartEvent,
                new TextCompositionEventHandler((s, e) => _isImeComposing = true));
            _input.AddHandler(TextCompositionManager.TextInputEvent,
                new TextCompositionEventHandler((s, e) => _isImeComposing = false));

            // 送信ボタン
            _sendButton = new Button
            {
                Content = "▶ 送信",
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                MinWidth = 60,
            };
            _sendButton.Click += async (s, e) => await SendAsync();

            // キャンセルボタン
            _cancelButton = new Button
            {
                Content = "✖ キャンセル",
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                MinWidth = 60,
            };
            _cancelButton.Click += (s, e) => Clear();

            // パネルレイアウト用の Grid（列定義のみ）
            var panelGrid = new Grid { Background = Brushes.Transparent };
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // TextBox とボタンを配置
            panelGrid.Children.Add(_input);
            Grid.SetColumn(_input, 0);
            panelGrid.Children.Add(_sendButton);
            Grid.SetColumn(_sendButton, 1);
            panelGrid.Children.Add(_cancelButton);
            Grid.SetColumn(_cancelButton, 2);

            // Grid を包む Border を作成し、角丸＆パディングを設定
            _panel = new Border
            {
                Child = panelGrid,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
            };
        }

        internal void Toggle()
        {
            if (_layer.Elements.Count > 0)
            {
                Clear();
            }
            else
            {
                ShowInput();
            }
        }

        private void ShowInput()
        {
            _layer.RemoveAllAdornments();
            var caret = _view.Caret.Position.BufferPosition;
            var span = new SnapshotSpan(caret, 0);
            var geom = _view.TextViewLines.GetMarkerGeometry(span) ?? CreateFallbackGeometry(caret);
            if (geom == null)
            {
                return;
            }

            Canvas.SetLeft(_panel, geom.Bounds.Left);
            Canvas.SetTop(_panel, geom.Bounds.Bottom + 4);
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, span, null, _panel, null);
            _input.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => { _input.Focus(); Keyboard.Focus(_input); }));
            _keyFilter ??= new InlineChatKeyFilter(_input, _viewAdapter);
        }

        private Geometry CreateFallbackGeometry(SnapshotPoint caret)
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
        }

        /// <summary>送信ボタン / Enter で呼び出す AI リクエスト共通処理</summary>
        private async Task SendAsync()
        {
            //if (_isImeComposing) return;
            var prompt = _input.Text;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            _sendButton.IsEnabled = false; _cancelButton.IsEnabled = false; _input.IsEnabled = false; _input.Text = "Thinking…";
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

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); ShowDiff(reply);
        }

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true; Clear();
            }
            //else if (e.Key == Key.Enter && !_isImeComposing) { e.Handled = true; await SendAsync(); }
        }

        private void ShowDiff(string newCode)
        {
            var oldSnap = _view.TextSnapshot; var oldCode = oldSnap.GetText();
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

            if (_diffViewer.InlineHost.HostControl is FrameworkElement hostFE)
            {
                hostFE.Margin = new Thickness(0);
            }
            
            var diffCtrl = _diffViewer.VisualElement;
            diffCtrl.Height = Math.Min(300, _view.ViewportHeight * 0.6);
            var accept = new Button { Content = "✔ Accept", Margin = new Thickness(4) };
            var cancel = new Button { Content = "✖ Cancel", Margin = new Thickness(4) };
            accept.Click += (s, e) => ApplyChanges(newCode);
            cancel.Click += (s, e) => Clear();
            var btnBar = new StackPanel { Orientation = Orientation.Horizontal };
            btnBar.Children.Add(accept); btnBar.Children.Add(cancel);
            var stack = new StackPanel(); stack.Children.Add(diffCtrl); stack.Children.Add(btnBar);
            _layer.RemoveAllAdornments();
            var caret = _view.Caret.Position.BufferPosition; var span = new SnapshotSpan(caret, 0);
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, span, null, stack, null);
        }

        private void ApplyChanges(string newCode)
        {
            using (var edit = _view.TextBuffer.CreateEdit()) { edit.Replace(0, _view.TextSnapshot.Length, newCode); edit.Apply(); }
            Clear();
        }
    }
}
