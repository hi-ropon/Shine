using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    /// <summary>
    /// インラインチャット＋差分プレビュー (1 TextView = 1 インスタンス)
    /// </summary>
    internal sealed class InlineChatSession
    {
        private readonly IWpfTextView _view;
        private readonly IVsTextView _viewAdapter;
        private readonly IChatClientService _chat;
        private readonly AsyncPackage _pkg;
        private readonly IAdornmentLayer _layer;

        private InlineChatKeyFilter? _keyFilter;
        private IWpfDifferenceViewer? _diffViewer;
        private double _diffZoomLevel = 0.5;
        private double _baseViewZoom = 1.0;

        internal InlineChatSession(
            IWpfTextView view,
            IVsTextView viewAdapter,
            IChatClientService chat,
            AsyncPackage pkg)
        {
            _view = view;
            _viewAdapter = viewAdapter;
            _chat = chat;
            _pkg = pkg;
            _layer = _view.GetAdornmentLayer("ShineInlineChat");
        }

        internal void Toggle()
        {
            if (_layer.Elements.Count > 0)
                Clear();
            else
                ShowInput();
        }

        private void ShowInput()
        {
            Clear();
            if (!ShineFeatureGate.TryBeginInlineChat())
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var input = CreateTextBox();
            var sendButton = CreateButton("▶ 送信");
            var cancelButton = CreateButton("✖ キャンセル");

            sendButton.Click += async (_, __) => await SendAsync(input, sendButton, cancelButton);
            cancelButton.Click += (_, __) => Clear();

            var panelGrid = new Grid { Background = Brushes.Transparent };
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panelGrid.Children.Add(input); Grid.SetColumn(input, 0);
            panelGrid.Children.Add(sendButton); Grid.SetColumn(sendButton, 1);
            panelGrid.Children.Add(cancelButton); Grid.SetColumn(cancelButton, 2);

            var panel = new Border
            {
                Child = panelGrid,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4)
            };

            // カーソル位置から Geometry を取得。差分ビュー位置のフォールバック処理で TextBounds を Rect に変換
            var caretPos = _view.Caret.Position.BufferPosition;
            var markerGeom = _view.TextViewLines.GetMarkerGeometry(new SnapshotSpan(caretPos, 0));
            if (markerGeom != null)
            {
                panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(panel, markerGeom.Bounds.Left);
                Canvas.SetTop(panel, markerGeom.Bounds.Bottom + 4);
            }
            else
            {
                var tb = _view.TextViewLines.GetCharacterBounds(caretPos);
                var fallbackRect = new Rect(tb.Left, tb.Top, tb.Width, tb.Height);
                Canvas.SetLeft(panel, fallbackRect.Left);
                Canvas.SetTop(panel, fallbackRect.Bottom + 4);
            }

            _layer.AddAdornment(
                AdornmentPositioningBehavior.OwnerControlled,
                new SnapshotSpan(caretPos, 0),
                null, panel, null);

            _keyFilter = new InlineChatKeyFilter(input, _viewAdapter);

            input.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    input.Focus();
                    input.CaretIndex = input.Text.Length;
                    Keyboard.Focus(input);
                }));
        }

        private static TextBox CreateTextBox() => new()
        {
            Width = 350,
            Height = 24,
            FontFamily = new FontFamily("Cascadia Code"),
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x22, 0x22, 0x22)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.SteelBlue,
            AcceptsReturn = true,
            AcceptsTab = false
        };

        private static Button CreateButton(string caption) => new()
        {
            Content = caption,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            MinWidth = 60
        };

        private void Clear()
        {
            _layer.RemoveAllAdornments();
            if (_diffViewer != null)
            {
                _diffViewer.Close();
                _diffViewer = null;
            }
            if (_keyFilter != null)
            {
                _viewAdapter.RemoveCommandFilter(_keyFilter);
                _keyFilter = null;
            }
            _view.ZoomLevelChanged -= OnViewZoomLevelChanged;
            ShineFeatureGate.EndInlineChat();
        }

        private async Task SendAsync(TextBox input, Button sendBtn, Button cancelBtn)
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;

            sendBtn.IsEnabled = cancelBtn.IsEnabled = input.IsEnabled = false;

            string reply;
            try
            {
                reply = await _chat.GetChatResponseAsync(input.Text);
            }
            catch (Exception ex)
            {
                ShinePackage.MessageService.ShowError(ex, "AI 応答でエラー");
                Clear();
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ShowDiff(reply);
        }

        private void ShowDiff(string newCode)
        {
            var compModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var bufFactory = compModel.GetService<ITextBufferFactoryService>();
            var ctRegistry = compModel.GetService<IContentTypeRegistryService>();
            var diffBufSvc = compModel.GetService<IDifferenceBufferFactoryService>();
            var diffViewFac = compModel.GetService<IWpfDifferenceViewerFactoryService>();

            var contentType = ctRegistry.GetContentType("text");
            var left = bufFactory.CreateTextBuffer(_view.TextSnapshot.GetText(), contentType);
            var right = bufFactory.CreateTextBuffer(newCode, contentType);

            _diffViewer = diffViewFac.CreateDifferenceView(
                diffBufSvc.CreateDifferenceBuffer(left, right));

            var props = _view.FormattedLineSource.DefaultTextProperties;
            foreach (var v in new[] { _diffViewer.LeftView, _diffViewer.RightView, _diffViewer.InlineView })
            {
                if (v?.VisualElement is FrameworkElement fe)
                {
                    fe.SetValue(Control.FontSizeProperty, props.FontRenderingEmSize);
                    fe.SetValue(Control.FontFamilyProperty, props.Typeface.FontFamily);
                }
            }

            var diffCtrl = _diffViewer.VisualElement;
            diffCtrl.MaxWidth = Math.Max(100, _view.ViewportWidth * 1.0);
            diffCtrl.MaxHeight = Math.Max(100, _view.ViewportHeight * 0.4);
            diffCtrl.MinWidth = diffCtrl.MinHeight = 100;

            var btnBar = new StackPanel { Orientation = Orientation.Horizontal };
            btnBar.Children.Add(CreateAcceptButton(newCode));
            btnBar.Children.Add(CreateButton("✖ Cancel"));
            ((Button)btnBar.Children[1]).Click += (_, __) => Clear();
            btnBar.Children.Add(CreateZoomButton("-", () => { _diffZoomLevel = Math.Max(0.1, _diffZoomLevel - 0.1); ApplyDiffZoom(); }));
            btnBar.Children.Add(CreateZoomButton("+", () => { _diffZoomLevel += 0.1; ApplyDiffZoom(); }));

            var stack = new StackPanel();
            stack.Children.Add(diffCtrl);
            stack.Children.Add(btnBar);

            _layer.RemoveAllAdornments();
            _layer.AddAdornment(
                AdornmentPositioningBehavior.OwnerControlled,
                new SnapshotSpan(_view.Caret.Position.BufferPosition, 0),
                null, stack, null);

            _baseViewZoom = _view.ZoomLevel;
            _view.ZoomLevelChanged += OnViewZoomLevelChanged;
            ApplyDiffZoom();
        }

        private Button CreateAcceptButton(string newCode)
        {
            var accept = CreateButton("✔ Accept");
            accept.Click += (_, __) =>
            {
                using var edit = _view.TextBuffer.CreateEdit();
                edit.Replace(0, _view.TextSnapshot.Length, newCode);
                edit.Apply();
                Clear();
            };
            return accept;
        }

        private static Button CreateZoomButton(string caption, Action onClick)
        {
            var btn = new Button { Content = caption, Margin = new Thickness(4), MinWidth = 24 };
            btn.Click += (_, __) => onClick();
            return btn;
        }

        private void OnViewZoomLevelChanged(object? sender, ZoomLevelChangedEventArgs e)
            => ApplyDiffZoom();

        private void ApplyDiffZoom()
        {
            if (_diffViewer == null) return;
            double fixedZoom = _diffZoomLevel * _baseViewZoom;
            foreach (var v in new[] { _diffViewer.LeftView, _diffViewer.RightView, _diffViewer.InlineView })
            {
                if (v != null) v.ZoomLevel = fixedZoom;
            }
        }
    }
}
