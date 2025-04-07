using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.ML.Tokenizers;

namespace Shine
{
    public partial class ChatToolWindowControl : UserControl
    {
        private IChatClientService _chatClientService;
        private Theme _themeManager;
        private ChatHistory _chatHistoryManager;
        private FileContentProvider _fileContentProvider;
        private Settings _settingsManager;
        private Mention _mentionManager;
        private readonly Tokenizer _tokenizer;

        public ChatToolWindowControl()
        {
            InitializeComponent();

            _themeManager = new Theme();
            _fileContentProvider = new FileContentProvider();
            _settingsManager = new Settings(ModelComboBox);
            _mentionManager = new Mention(InputRichTextBox, MentionPopup, MentionListBox);

            ThemeColor();
            _settingsManager.InitializeSettings();
            _settingsManager.UpdateModelComboBox();

            VSColorTheme.ThemeChanged += OnThemeChanged;
            this.Unloaded += ChatToolWindowControl_Unloaded;
            this.Loaded += ChatToolWindowControl_Loaded;

            _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        }

        private async void ChatToolWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ChatHistoryWebView.CoreWebView2 == null)
            {
                string userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Shine", "WebView2");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await ChatHistoryWebView.EnsureCoreWebView2Async(env);
            }

            if (_chatHistoryManager == null)
            {
                _chatHistoryManager = new ChatHistory(ChatHistoryWebView, _themeManager.ForegroundBrush);

                var options = (AiAssistantOptions)ShinePackage.Instance.GetDialogPage(typeof(AiAssistantOptions));
                _chatHistoryManager.SetHistoryLimit(options.ChatHistoryCount * 2);
            }
        }

        public void InitializeSettings()
        {
            _settingsManager.InitializeSettings();
        }

        public void UpdateModelComboBox()
        {
            _settingsManager.UpdateModelComboBox();
        }

        private void InputRichTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            _mentionManager.OnInputKeyDown(sender, e);
        }

        private void InputRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _mentionManager.OnInputTextChanged(sender, e);
        }

        /// <summary>
        /// 入力されたトークン数を計算して、TokenCountTextBox に表示します。
        /// </summary>
        private void UpdateTokenCount(string token)
        {
            int tokenCount = _tokenizer.CountTokens(token);
            TokenCountTextBox.Text = tokenCount.ToString();
        }

        private void MentionListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _mentionManager.OnMentionListPreviewKeyDown(sender, e);
        }

        private void MentionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _mentionManager.OnMentionListMouseDoubleClick(sender, e);
        }

        private void ThemeColor()
        {
            _themeManager.UpdateTheme();
            if (IncludeOpenFilesCheckBox != null)
            {
                IncludeOpenFilesCheckBox.Foreground = _themeManager.ForegroundBrush;
            }
            if (TokenCountLabel != null)
            {
                TokenCountLabel.Foreground = _themeManager.ForegroundBrush;
            }
            if (TokenCountTextBox != null)
            {
                TokenCountTextBox.Foreground = _themeManager.ForegroundBrush;
            }
        }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                ThemeColor();
                if (_chatHistoryManager != null)
                {
                    await _chatHistoryManager.UpdateForegroundColorAsync(_themeManager.ForegroundBrush);
                    await _chatHistoryManager.UpdateBackgroundColorAsync();
                }
            });
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settingsManager.OnModelComboBoxSelectionChanged(sender, e);
            _settingsManager.InitializeSettings();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatClientService == null)
            {
                _settingsManager.InitializeSettings();
                _chatClientService = _settingsManager.ChatClientService;
                if (_chatClientService == null)
                {
                    MessageBox.Show("AI サービスが初期化されていません。オプションを確認してください。");
                    return;
                }
            }

            ThemeColor();

            string userInput = GetRichTextWithMentions(InputRichTextBox);
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return;
            }

            // ユーザーのメッセージを WebView2 に追加
            _chatHistoryManager.AddChatMessage("User", userInput);
            // 入力欄をクリア
            InputRichTextBox.Document.Blocks.Clear();

            // ローディング表示開始
            LoadingProgressBar.Visibility = Visibility.Visible;

            string processedInput = "";
            string reply = "";
            bool errorOccurred = false;

            try
            {
                processedInput = await Task.Run(() => ProcessUserInputForFileContent(userInput));

                // 会話履歴を取得して、現在の入力に結合する
                string conversationHistory = _chatHistoryManager.GetConversationHistory();
                string fullPrompt = conversationHistory + "\n" + processedInput;
                UpdateTokenCount(fullPrompt);

                reply = await _chatClientService.GetChatResponseAsync(fullPrompt);
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                reply = $"エラーが発生しました:\n{ex.Message}";
                System.Diagnostics.Debug.WriteLine($"AI リクエストまたは処理中にエラーが発生しました: {ex}");
            }
            finally
            {
                // ローディング表示解除
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                _chatHistoryManager.AddChatMessage(errorOccurred ? "Error" : "Assistant", reply);

                // ユーザーの入力を会話メモリに追加
                _chatHistoryManager.AddConversationMemory("User", userInput);
                _chatHistoryManager.AddConversationMemory(errorOccurred ? "Error" : "Assistant", reply);
                _chatHistoryManager.AdjustConversationMemory();
            }
        }

        private string ProcessUserInputForFileContent(string userInput)
        {
            StringBuilder processedInput = new StringBuilder(userInput);

            // @ファイル名 処理
            var matches = Regex.Matches(userInput, @"@([\w\.\-]+)");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string fileName = match.Groups[1].Value;
                string fileContent = _fileContentProvider.GetFileContent(fileName);

                if (!string.IsNullOrEmpty(fileContent))
                {
                    processedInput.AppendLine($"\n--- {fileName} の内容開始 ---");
                    processedInput.AppendLine(fileContent);
                    processedInput.AppendLine($"--- {fileName} の内容終了 ---");
                }
                else
                {
                    processedInput.AppendLine($"\n--- @{fileName} (ファイルが見つからないか、読み込めませんでした) ---");
                }
            }

            bool includeOpenFiles = false;
            try
            {
                includeOpenFiles = ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return IncludeOpenFilesCheckBox != null && IncludeOpenFilesCheckBox.IsChecked == true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IncludeOpenFilesCheckBox の状態取得中にエラーが発生しました: {ex}");
            }

            if (includeOpenFiles)
            {
                string openFilesContent = "";
                try
                {
                    openFilesContent = ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        return GetOpenDocumentsContent();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"オープンドキュメントの内容取得中にエラーが発生しました: {ex}");
                }

                if (!string.IsNullOrEmpty(openFilesContent))
                {
                    processedInput.AppendLine($"\n--- 全オープンファイルの内容開始 ---");
                    processedInput.AppendLine(openFilesContent);
                    processedInput.AppendLine($"--- 全オープンファイルの内容終了 ---");
                }
            }

            return processedInput.ToString();
        }

        private string GetOpenDocumentsContent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            StringBuilder sb = new StringBuilder();

            try
            {
                if (dte?.Documents == null)
                {
                    System.Diagnostics.Debug.WriteLine("GetOpenDocumentsContent で DTE または Documents コレクションが null です。");
                    return "";
                }

                foreach (Document doc in dte.Documents)
                {
                    if (doc != null && !string.IsNullOrEmpty(doc.FullName) && doc.Saved && System.IO.File.Exists(doc.FullName))
                    {
                        string fileName = System.IO.Path.GetFileName(doc.FullName);
                        string content = "";
                        try
                        {
                            content = System.IO.File.ReadAllText(doc.FullName);
                            sb.AppendLine($"--- {fileName} の内容開始 ---");
                            sb.AppendLine(content);
                            sb.AppendLine($"--- {fileName} の内容終了 ---");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"--- {fileName} (読み込みエラー) ---");
                            System.Diagnostics.Debug.WriteLine($"オープンドキュメント '{doc.FullName}' の読み込み中にエラーが発生しました: {ex}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping document '{doc?.Name}' (条件に合致しません)。");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOpenDocumentsContent 内でエラーが発生しました: {ex}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// RichTextBox の内容を走査して、Run のテキストや InlineUIContainer(メンション)内のテキストを結合して返す
        /// </summary>
        private string GetRichTextWithMentions(RichTextBox rtb)
        {
            var sb = new StringBuilder();
            // Document.Blocks を順に処理
            foreach (Block block in rtb.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    // Paragraph 内の Inlines を順に処理
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            // 通常テキスト
                            sb.Append(run.Text);
                        }
                        else if (inline is InlineUIContainer uiContainer)
                        {
                            // メンション等のUI要素
                            if (uiContainer.Child is Border border && border.Child is TextBlock tb)
                            {
                                sb.Append(tb.Text);
                            }
                        }
                    }
                    // Paragraph の終わりで改行を挿入
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.InitializeSettings();
            _settingsManager.UpdateModelComboBox();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _chatHistoryManager.ClearHistory();
        }

        private void ChatToolWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _mentionManager.Dispose();
            VSColorTheme.ThemeChanged -= OnThemeChanged;
        }
    }
}
