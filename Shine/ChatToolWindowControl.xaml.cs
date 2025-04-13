using System;
using System.Diagnostics;
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
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Shine
{
    /// <summary>
    /// ChatToolWindowControl.xaml の相互作用ロジック
    /// </summary>
    public partial class ChatToolWindowControl : UserControl
    {
        private IChatClientService _chatClientService;
        private Theme _themeManager;
        private ChatHistory _chatHistoryManager;
        private FileContentProvider _fileContentProvider;
        private Settings _settingsManager;
        private Mention _mentionManager;
        private readonly Tokenizer _tokenizer;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChatToolWindowControl()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(InputRichTextBox, OnPaste);

            _themeManager = new Theme();
            _fileContentProvider = new FileContentProvider();
            _settingsManager = new Settings(ModelComboBox);
            _mentionManager = new Mention(InputRichTextBox, MentionPopup, MentionListBox);

            ThemeColor();
            _settingsManager.InitializeSettings();
            _settingsManager.UpdateModelComboBox();

#if !DEBUG
            EnableButton.Visibility = Visibility.Collapsed;
#endif

            VSColorTheme.ThemeChanged += OnThemeChanged;
            this.Unloaded += ChatToolWindowControl_Unloaded;
            this.Loaded += ChatToolWindowControl_Loaded;

            _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        }

        /// <summary>
        /// RichTextBox にペーストされたデータを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                string text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    // 標準のペースト処理をキャンセルし、プレーンテキストを挿入
                    e.CancelCommand();
                    e.Handled = true;
                    InputRichTextBox.CaretPosition.InsertTextInRun(text);
                }
            }
        }

        /// <summary>
        /// WebView2 の初期化をリトライ機能付きで実行します
        /// </summary>
        /// <param name="webView">対象のWebView2コントロール</param>
        /// <param name="userDataFolder">ユーザーデータの格納先パス</param>
        /// <param name="maxRetries">最大リトライ回数</param>
        /// <returns>初期化成功ならtrue、失敗ならfalse</returns>
        private async Task<bool> InitializeWebView2WithRetryAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView, string userDataFolder, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // ユーザーデータフォルダが存在しない場合は作成
                    if (!Directory.Exists(userDataFolder))
                    {
                        Directory.CreateDirectory(userDataFolder);
                    }
                    Debug.WriteLine($"Attempt {attempt}: ユーザーデータフォルダ '{userDataFolder}' を使用してWebView2環境を生成します。");

                    var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await webView.EnsureCoreWebView2Async(env);
                    Debug.WriteLine("WebView2の初期化に成功しました。");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Attempt {attempt}: WebView2の初期化に失敗しました。エラー内容：{ex.Message}");
                    // エラーがユーザーデータフォルダの不整合やロックが原因と考えられる場合、フォルダの削除を試みる
                    if (attempt < maxRetries)
                    {
                        try
                        {
                            if (Directory.Exists(userDataFolder))
                            {
                                Debug.WriteLine("ユーザーデータフォルダの状態をリセットするため、フォルダを削除します。");
                                Directory.Delete(userDataFolder, true);
                            }
                        }
                        catch (Exception dirEx)
                        {
                            Debug.WriteLine($"Attempt {attempt}: ユーザーデータフォルダの削除に失敗しました。エラー内容：{dirEx.Message}");
                        }
                        await Task.Delay(1000);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// WebView2 の初期化処理を実施します。リトライ機能付きの初期化を行い、初期化失敗時はユーザーへ通知します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ChatToolWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            // WebView2が未初期化の場合にリトライ付き初期化を行う
            if (ChatHistoryWebView.CoreWebView2 == null)
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Shine", "WebView2");

                bool initialized = await InitializeWebView2WithRetryAsync(ChatHistoryWebView, userDataFolder, maxRetries: 3);
                if (!initialized)
                {
                    Debug.WriteLine("複数回の初期化試行にも関わらずWebView2の初期化に失敗しました。");
                    MessageBox.Show("WebView2 の初期化に失敗しました。Visual Studio を再起動するか、システム環境をご確認ください。",
                                    "初期化エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (_chatHistoryManager == null)
            {
                _chatHistoryManager = new ChatHistory(new WebView2Wrapper(ChatHistoryWebView), _themeManager.ForegroundBrush);

                var options = (AiAssistantOptions)ShinePackage.Instance.GetDialogPage(typeof(AiAssistantOptions));
                _chatHistoryManager.SetHistoryLimit(options.ChatHistoryCount * 2);
            }
        }

        /// <summary>
        /// 設定を初期化します
        /// </summary>
        public void InitializeSettings()
        {
            _settingsManager.InitializeSettings();
        }

        /// <summary>
        /// モデルコンボボックスを更新します
        /// </summary>
        public void UpdateModelComboBox()
        {
            _settingsManager.UpdateModelComboBox();
        }

        /// <summary>
        /// RichTextBox の KeyDown イベントを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputRichTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            _mentionManager.OnInputKeyDown(sender, e);
        }

        /// <summary>
        /// RichTextBox の TextChanged イベントを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// MentionListBox の KeyDown イベントを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MentionListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _mentionManager.OnMentionListPreviewKeyDown(sender, e);
        }

        /// <summary>
        /// MentionListBox の MouseDoubleClick イベントを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MentionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _mentionManager.OnMentionListMouseDoubleClick(sender, e);
        }

        /// <summary>
        /// テーマ変更時に、RichTextBox の色を更新します
        /// </summary>
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

        /// <summary>
        /// VS のテーマ変更イベントを処理します
        /// </summary>
        /// <param name="e"></param>
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

        /// <summary>
        /// モデルコンボボックスの選択変更イベントを処理します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settingsManager.OnModelComboBoxSelectionChanged(sender, e);
            _settingsManager.InitializeSettings();
        }

        /// <summary>
        /// 送信ボタンがクリックされた際の処理を行います
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.InitializeSettings();
            _chatClientService = _settingsManager.ChatClientService;
            if (_chatClientService == null)
            {
                MessageBox.Show("AI サービスが初期化されていません。オプションを確認してください。");
                return;
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
                string fullPrompt;

                if (!string.IsNullOrEmpty(conversationHistory))
                {
                    fullPrompt = "#参考情報\n#過去の会話履歴 Start\n" + conversationHistory + "#過去の会話履歴 End\n\n" + processedInput;
                }
                else
                {
                    fullPrompt = processedInput;
                }

                UpdateTokenCount(fullPrompt);

                reply = await _chatClientService.GetChatResponseAsync(fullPrompt);
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                reply = $"エラーが発生しました:\n{ex.Message}";
                Debug.WriteLine($"AI リクエストまたは処理中にエラーが発生しました: {ex}");
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

        /// <summary>
        /// ユーザーの入力を処理して、ファイルの内容を取得します
        /// </summary>
        /// <param name="userInput"></param>
        /// <returns></returns>
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
                Debug.WriteLine($"IncludeOpenFilesCheckBox の状態取得中にエラーが発生しました: {ex}");
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
                    Debug.WriteLine($"オープンドキュメントの内容取得中にエラーが発生しました: {ex}");
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

        /// <summary>
        /// オープンドキュメントの内容を取得します
        /// </summary>
        /// <returns></returns>
        private string GetOpenDocumentsContent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            StringBuilder sb = new StringBuilder();

            try
            {
                if (dte?.Documents == null)
                {
                    Debug.WriteLine("GetOpenDocumentsContent で DTE または Documents コレクションが null です。");
                    return "";
                }

                foreach (Document doc in dte.Documents)
                {
                    if (doc != null && !string.IsNullOrEmpty(doc.FullName) && doc.Saved && File.Exists(doc.FullName))
                    {
                        string fileName = Path.GetFileName(doc.FullName);
                        string content = "";
                        try
                        {
                            content = File.ReadAllText(doc.FullName);
                            sb.AppendLine($"--- {fileName} の内容開始 ---");
                            sb.AppendLine(content);
                            sb.AppendLine($"--- {fileName} の内容終了 ---");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"--- {fileName} (読み込みエラー) ---");
                            Debug.WriteLine($"オープンドキュメント '{doc.FullName}' の読み込み中にエラーが発生しました: {ex}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping document '{doc?.Name}' (条件に合致しません)。");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOpenDocumentsContent 内でエラーが発生しました: {ex}");
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

        /// <summary>
        /// EnableButton が押された際に、Azure プロバイダーの場合、設定されている API キー、エンドポイント、モデル名を ChatHistory に表示します。
        /// </summary>
        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.InitializeSettings();
            _settingsManager.UpdateModelComboBox();

            var package = ShinePackage.Instance;
            if (package == null)
            {
                MessageBox.Show("Package がロードされていません。");
                return;
            }
            AiAssistantOptions options = null;
            try
            {
                options = (AiAssistantOptions)package.GetDialogPage(typeof(AiAssistantOptions));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting DialogPage in EnableButton: {ex}");
                return;
            }

            // プロバイダーが AzureOpenAI の場合のみ、設定情報を ChatHistory に表示
            if (options != null && options.Provider == OpenAiProvider.AzureOpenAI)
            {
                string settingsMessage = $"【Azure OpenAI 設定】\nエンドポイント: {options.AzureOpenAIEndpoint}\nAPI キー: {options.AzureOpenAIApiKey}\nモデル: {options.AzureDeploymentName}";
                if (_chatHistoryManager != null)
                {
                    _chatHistoryManager.AddChatMessage("System", settingsMessage);
                }
            }
        }

        /// <summary>
        /// クリアボタンが押された際に、チャット履歴をクリアします
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _chatHistoryManager.ClearHistory();
        }

        /// <summary>
        /// Git の差分を要約します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SummarizeDiffButton_Click(object sender, RoutedEventArgs e)
        {
            string solutionPath = "";
            try
            {
                // DTE2 を取得してソリューションのパスを取得
                DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (dte?.Solution?.FullName == null)
                {
                    MessageBox.Show("ソリューションが開かれていません。");
                    return;
                }
                solutionPath = Path.GetDirectoryName(dte.Solution.FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("ソリューション情報の取得中にエラーが発生しました: " + ex.Message);
                return;
            }

            // AiAssistantOptions からプロバイダー情報等を取得
            AiAssistantOptions options = null;
            try
            {
                options = (AiAssistantOptions)ShinePackage.Instance.GetDialogPage(typeof(AiAssistantOptions));
            }
            catch (Exception ex)
            {
                MessageBox.Show("オプションの取得中にエラーが発生しました: " + ex.Message);
                return;
            }
            if (options == null)
            {
                MessageBox.Show("オプションが取得できませんでした。");
                return;
            }

            IChatClientService chatService = null;
            try
            {
                if (options.Provider == OpenAiProvider.OpenAI)
                {
                    chatService = new OpenAiClientService(options.OpenAIApiKey, "gpt-4o-mini", options.Temperature);
                }
                else if (options.Provider == OpenAiProvider.AzureOpenAI)
                {
                    chatService = new AzureOpenAiClientService(options.AzureOpenAIEndpoint, options.AzureOpenAIApiKey, "gpt-4o-mini", options.Temperature);
                }
                else
                {
                    MessageBox.Show("未サポートのプロバイダーです。");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("AIサービスの初期化中にエラーが発生しました: " + ex.Message);
                return;
            }
            if (chatService == null)
            {
                MessageBox.Show("AI サービスの初期化に失敗しました。オプションを確認してください。");
                return;
            }

            LoadingProgressBar.Visibility = Visibility.Visible;
            string diffSummary = string.Empty;
            try
            {
                GitDiffSummarizer diffSummarizer = new GitDiffSummarizer(chatService, solutionPath);
                diffSummary = await diffSummarizer.SummarizeDiffAsync();
            }
            catch (System.ComponentModel.Win32Exception win32Ex)
            {
                // Win32Exception は、Gitコマンドが見つからない場合（未インストールなど）に発生することがある
                MessageBox.Show("Gitが見つかりません。Gitがインストールされていることを確認してください。\n詳細: " + win32Ex.Message);
                return;
            }
            catch (DirectoryNotFoundException dnfe)
            {
                MessageBox.Show("作業ディレクトリが見つかりません: " + dnfe.Message);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Git差分の要約中にエラーが発生しました: " + ex.Message);
                return;
            }
            finally
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }

            _chatHistoryManager.AddChatMessage("Assistant", diffSummary);
        }


        /// <summary>
        /// 画像入力ボタンがクリックされた際の処理を行います
        /// </summary>
        private void ImageInputButton_Click(object sender, RoutedEventArgs e)
        {
            // ここに画像入力のロジックを実装します。
            // サンプルとして、メッセージボックスを表示します。
            MessageBox.Show("画像入力機能はまだ実装されていません。", "画像入力", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// ツールウィンドウがアンロードされた際の処理を行います
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatToolWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _mentionManager.Dispose();
            VSColorTheme.ThemeChanged -= OnThemeChanged;
        }
    }
}