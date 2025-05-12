using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Shell;
using Shine.Helpers;
using Shine.Models;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Timer = System.Threading.Timer;

namespace Shine
{
    /// <summary>
    /// 入力中のテキストエリアにおいて「@」を利用したメンションの表示・挿入処理を管理します。
    /// </summary>
    public class Mention : IDisposable
    {
        private readonly RichTextBox _inputRichTextBox;
        private readonly System.Windows.Controls.Primitives.Popup _mentionPopup;
        private readonly ListBox _mentionListBox;
        private List<string> _codeFileList;
        private bool _isMentionMode = false;
        private bool _isProcessingKeyDown = false;
        private bool _isUpdatingText = false;
        private Timer _mentionDebounceTimer;
        private CancellationTokenSource _mentionCts;
        private const int _mentionDebounceTime = 500;

        /// <summary>
        /// コンストラクタ。指定された UI コンポーネントを利用してメンション機能を初期化します。
        /// </summary>
        /// <param name="inputRichTextBox">対象の入力リッチテキストボックス</param>
        /// <param name="mentionPopup">メンション候補を表示するポップアップ</param>
        /// <param name="mentionListBox">メンション候補のリストを管理する ListBox</param>
        public Mention(RichTextBox inputRichTextBox, System.Windows.Controls.Primitives.Popup mentionPopup, ListBox mentionListBox)
        {
            _inputRichTextBox = inputRichTextBox;
            _mentionPopup = mentionPopup;
            _mentionListBox = mentionListBox;
        }

        /// <summary>
        /// キーが押下された際の処理を行います。特に Enter キーの場合、メンション候補を挿入します。
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">キーイベントの引数</param>
        public void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (_mentionPopup.IsOpen && _mentionListBox.HasItems && e.Key == Key.Enter)
            {
                if (_mentionListBox.SelectedItem is FileItem item)
                {
                    _mentionDebounceTimer?.Dispose();
                    _mentionCts?.Cancel();
                    _mentionCts?.Dispose();
                    _mentionCts = null;

                    InsertMention(item.Name);
                    e.Handled = true;
                }
                else
                {
                    _mentionPopup.IsOpen = false;
                    _isMentionMode = false;
                    e.Handled = true;
                }
                return;
            }

            if (_isProcessingKeyDown) return;

            try
            {
                _isProcessingKeyDown = true;
            }
            finally
            {
                _isProcessingKeyDown = false;
            }
        }

        /// <summary>
        /// テキスト変更イベント時にコールされ、キャレット位置付近の「@」を検出してメンション候補を更新・表示します。
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">テキスト変更イベントの引数</param>
        public void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;

            // キャレット位置までのテキストを取得
            TextPointer caretPosition = _inputRichTextBox.CaretPosition;
            int caretOffset = GetTextOffset(_inputRichTextBox.Document.ContentStart, caretPosition);
            string fullText = new TextRange(_inputRichTextBox.Document.ContentStart, _inputRichTextBox.Document.ContentEnd).Text;

            if (caretOffset > fullText.Length) caretOffset = fullText.Length;

            string textUpToCaret = fullText.Substring(0, caretOffset);
            int atIndex = textUpToCaret.LastIndexOf('@');

            _mentionDebounceTimer?.Dispose();
            _mentionCts?.Cancel();
            _mentionCts?.Dispose();
            _mentionCts = null;

            if (atIndex >= 0)
            {
                _isMentionMode = true;
                string query = textUpToCaret.Substring(atIndex + 1);

                _mentionCts = new CancellationTokenSource();
                var token = _mentionCts.Token;

                _mentionDebounceTimer = new Timer(async _ =>
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                        if (token.IsCancellationRequested) return;

                        bool shouldScanSolution = _codeFileList == null ||
                                                  !_isMentionMode ||
                                                  (_codeFileList.Count == 0);

                        if (shouldScanSolution)
                        {
                            LogHelper.DebugLog("Scanning solution for files...");
                            _codeFileList = CodeFile.GetCodeFilesInSolution();
                            // 走査結果をアルファベット順にソートしてキャッシュ
                            _codeFileList.Sort(StringComparer.OrdinalIgnoreCase);
                            LogHelper.DebugLog($"Found {_codeFileList.Count} files.");
                        }

                        FilterAndShowMentionList(query);
                    }
                    catch (OperationCanceledException opeCancelEx)
                    {
                        ShinePackage.MessageService.ShowError(opeCancelEx, "Mention update cancelled.");
                    }
                    catch (Exception ex)
                    {
                        ShinePackage.MessageService.ShowError(ex, $"Error updating mention list");
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _mentionPopup.IsOpen = false;
                        _isMentionMode = false;
                    }
                }, null, _mentionDebounceTime, Timeout.Infinite);
            }
            else
            {
                _isMentionMode = false;
                _mentionPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// メンション候補リストをフィルタ＆表示（アルファベット順ソート付き）
        /// </summary>
        private void FilterAndShowMentionList(string query)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_codeFileList == null)
            {
                _mentionPopup.IsOpen = false;
                return;
            }

            // クエリに応じてフィルタ
            var filteredList = string.IsNullOrWhiteSpace(query)
                ? new List<string>(_codeFileList)
                : _codeFileList.FindAll(f => f != null &&
                                             f.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            // ここでアルファベット順にソート
            filteredList.Sort(StringComparer.OrdinalIgnoreCase);

            _mentionListBox.ItemsSource = filteredList;

            if (filteredList.Count > 0)
            {
                _mentionListBox.SelectedIndex = 0;
                RepositionMentionPopup();
                _mentionPopup.IsOpen = true;
                Keyboard.Focus(_mentionListBox);
            }
            else
            {
                _mentionPopup.IsOpen = false;
            }

            var filtered = string.IsNullOrWhiteSpace(query)
            ? _codeFileList
            : _codeFileList.FindAll(f => f.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            var items = new List<FileItem>();
            foreach (var path in filtered) items.Add(new FileItem(path));

            _mentionListBox.ItemsSource = items;
        }

        /// <summary>
        /// メンションポップアップの位置を入力リッチテキストボックスの下側に設定します。
        /// </summary>
        private void RepositionMentionPopup()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _mentionPopup.PlacementTarget = _inputRichTextBox;
            _mentionPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _mentionPopup.HorizontalOffset = 0;
            _mentionPopup.VerticalOffset = 2;
        }

        /// <summary>
        /// メンションリスト内でキーが押下されたときの処理を行います。Enter および Escape キーに対する挙動を管理します。
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">キーイベントの引数</param>
        public void OnMentionListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_mentionListBox.SelectedItem is FileItem item)
                {
                    _mentionDebounceTimer?.Dispose();
                    _mentionCts?.Cancel();
                    _mentionCts?.Dispose();
                    _mentionCts = null;

                    InsertMention(item.Name);
                    e.Handled = true;
                }

                return;
            }
            else if (e.Key == Key.Escape)
            {
                _mentionDebounceTimer?.Dispose();
                _mentionCts?.Cancel();
                _mentionCts?.Dispose();
                _mentionCts = null;

                _mentionPopup.IsOpen = false;
                _isMentionMode = false;
                e.Handled = true;
                Keyboard.Focus(_inputRichTextBox);
            }
        }

        /// <summary>
        /// メンションリスト内でダブルクリックされた際に、クリックされた項目を挿入します。
        /// </summary>
        /// <param name="sender">イベントの送信元</param>
        /// <param name="e">マウスボタンイベントの引数</param>
        public void OnMentionListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_mentionListBox.SelectedItem is FileItem item)
            {
                _mentionDebounceTimer?.Dispose();
                _mentionCts?.Cancel();
                _mentionCts?.Dispose();
                _mentionCts = null;

                InsertMention(item.Name);
            }
        }

        /// <summary>
        /// 指定されたファイル名を使用して、入力中のメンション（「@」以降の文字）を削除し、
        /// インラインにアイコン要素（Border 内にテキストを表示）を挿入します。
        /// </summary>
        /// <param name="fileName">挿入するメンション文字列（ファイル名等）</param>
        public void InsertMention(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            _isUpdatingText = true;

            try
            {
                // 現在のキャレット位置を取得
                TextPointer caretPos = _inputRichTextBox.CaretPosition;
                // 直近の「@」の位置を逆方向に走査して特定
                TextPointer mentionStart = FindLastAtSymbol(caretPos);

                if (mentionStart != null)
                {
                    // 「@」からキャレット位置までのテキストを削除
                    TextRange rangeToReplace = new TextRange(mentionStart, caretPos);
                    rangeToReplace.Text = string.Empty;

                    // アイコン表示用の UI 要素（Border 内にテキスト表示の例）
                    var mentionElement = new Border
                    {
                        Background = Brushes.LightBlue,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(2),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "@" + fileName,
                            Foreground = Brushes.DarkBlue,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };

                    // インラインUI要素として挿入
                    var inlineUI = new InlineUIContainer(mentionElement, mentionStart)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                        // （必要であれば TextBottom や TextBaseline に変えてみてください）
                    };

                    // 挿入後、キャレットを移動
                    _inputRichTextBox.CaretPosition = inlineUI.ElementEnd;
                    _inputRichTextBox.CaretPosition.InsertTextInRun(" ");
                }
            }
            finally
            {
                _isUpdatingText = false;
                _mentionPopup.IsOpen = false;
                _isMentionMode = false;
                Keyboard.Focus(_inputRichTextBox);
            }
        }

        /// <summary>
        /// キャレット位置から逆方向に走査し、直近の「@」がある位置の TextPointer を返します。
        /// </summary>
        /// <param name="caret">現在のキャレット位置</param>
        /// <returns>「@」の位置を示す TextPointer。見つからなければ null を返す。</returns>
        private TextPointer FindLastAtSymbol(TextPointer caret)
        {
            TextPointer pointer = caret;
            while (pointer != null)
            {
                TextPointer prev = pointer.GetNextInsertionPosition(LogicalDirection.Backward);
                if (prev == null)
                    break;
                var textRange = new TextRange(prev, pointer);
                if (textRange.Text.Contains("@"))
                {
                    int index = textRange.Text.LastIndexOf("@");
                    return prev.GetPositionAtOffset(index);
                }
                pointer = prev;
            }
            return null;
        }

        /// <summary>
        /// テキストレンジの開始位置から指定されたポインターまでの文字数を返します。
        /// </summary>
        /// <param name="start">テキストレンジの開始位置</param>
        /// <param name="pointer">対象の TextPointer</param>
        /// <returns>開始位置からのオフセット</returns>
        private int GetTextOffset(TextPointer start, TextPointer pointer)
        {
            return new TextRange(start, pointer).Text.Length;
        }

        /// <summary>
        /// マネージリソースの解放を行います。
        /// </summary>
        public void Dispose()
        {
            _mentionDebounceTimer?.Dispose();
            _mentionCts?.Cancel();
            _mentionCts?.Dispose();
        }
    }
}