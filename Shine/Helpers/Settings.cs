using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Shine
{
    /// <summary>
    /// Settings クラスは、AI アシスタントの設定を管理します
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// ChatClientService は、OpenAI のチャットモデルを処理するためのサービスです
        /// </summary>
        public IChatClientService ChatClientService { get; private set; }
        private readonly ComboBox _modelComboBox;

        /// <summary>
        /// Settings クラスのコンストラクタ
        /// </summary>
        /// <param name="modelComboBox"></param>
        public Settings(ComboBox modelComboBox)
        {
            _modelComboBox = modelComboBox;
        }

        /// <summary>
        /// InitializeSettings メソッドは、AI アシスタントの設定を初期化します
        /// </summary>
        public void InitializeSettings()
        {
            var package = ShinePackage.Instance;
            if (package == null)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Package がロードされていません。"));
                System.Diagnostics.Debug.WriteLine("Error: AiAssistantPackage.Instance is null in InitializeSettings.");
                return;
            }

            AiAssistantOptions options = null;
           
            try
            {
                options = (AiAssistantOptions)package.GetDialogPage(typeof(AiAssistantOptions));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting DialogPage: {ex}");
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"オプションページの取得に失敗しました: {ex.Message}"));
                return;
            }

            try
            {
                if (options.Provider == OpenAiProvider.OpenAI)
                {
                    if (string.IsNullOrWhiteSpace(options.OpenAIApiKey) || string.IsNullOrWhiteSpace(options.OpenAIModelName))
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: OpenAI API Key or Model Name is empty.");
                    }
                    ChatClientService = new OpenAiClientService(
                        options.OpenAIApiKey,
                        options.OpenAIModelName,
                        options.Temperature);
                }
                else if (options.Provider == OpenAiProvider.AzureOpenAI)
                {
                    if (string.IsNullOrWhiteSpace(options.AzureOpenAIEndpoint) || !Uri.IsWellFormedUriString(options.AzureOpenAIEndpoint, UriKind.Absolute) ||
                        string.IsNullOrWhiteSpace(options.AzureOpenAIApiKey) || string.IsNullOrWhiteSpace(options.AzureDeploymentName))
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Azure OpenAI Endpoint, API Key or Deployment Name is invalid or empty.");
                    }
                    ChatClientService = new AzureOpenAiClientService(
                        options.AzureOpenAIEndpoint,
                        options.AzureOpenAIApiKey,
                        options.AzureDeploymentName,
                        options.Temperature);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show("未サポートのプロバイダーが選択されています。"));
                    ChatClientService = null;
                }
                System.Diagnostics.Debug.WriteLine($"ChatClientService initialized for provider: {options.Provider}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ChatClientService: {ex}");
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"AI クライアントの初期化に失敗しました: {ex.Message}"));
                ChatClientService = null;
            }
        }

        /// <summary>
        /// UpdateModelComboBox メソッドは、モデルのコンボボックスを更新します
        /// </summary>
        public void UpdateModelComboBox()
        {
            var package = ShinePackage.Instance;
            if (package == null)
            {
                System.Diagnostics.Debug.WriteLine("Error: AiAssistantPackage.Instance is null in UpdateModelComboBox.");
                return;
            }

            AiAssistantOptions options = null;
            
            try
            {
                options = (AiAssistantOptions)package.GetDialogPage(typeof(AiAssistantOptions));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting DialogPage in UpdateModelComboBox: {ex}");
                return;
            }

            _modelComboBox.Items.Clear();

            if (options.Provider == OpenAiProvider.OpenAI)
            {
                _modelComboBox.Items.Add("gpt-4o-mini");
                _modelComboBox.Items.Add("gpt-4o");
                _modelComboBox.Items.Add("o1-mini");
                _modelComboBox.Items.Add("o3-mini");
                _modelComboBox.Items.Add("o4-mini");
            }
            else
            {
                _modelComboBox.Items.Add("gpt-4o-mini");
                _modelComboBox.Items.Add("gpt-4o");
                _modelComboBox.Items.Add("o1-mini");
            }

            string selectedModel = options.Provider == OpenAiProvider.OpenAI ? options.OpenAIModelName : options.AzureDeploymentName;
            
            if (selectedModel != null && _modelComboBox.Items.Contains(selectedModel))
            {
                _modelComboBox.SelectedItem = selectedModel;
            }
            else if (_modelComboBox.Items.Count > 0)
            {
                _modelComboBox.SelectedIndex = 0;
                
                if (options.Provider == OpenAiProvider.OpenAI)
                {
                    options.OpenAIModelName = _modelComboBox.Items[0].ToString();
                }
            }
            else
            {
                _modelComboBox.SelectedItem = null;
            }
        }

        /// <summary>
        /// OnModelComboBoxSelectionChanged メソッドは、モデルのコンボボックスの選択が変更されたときに呼び出されます
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnModelComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_modelComboBox.SelectedItem == null) return;

            var package = ShinePackage.Instance;
            
            if (package == null) return;
            
            AiAssistantOptions options = null;
            
            try
            {
                options = (AiAssistantOptions)package.GetDialogPage(typeof(AiAssistantOptions));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting DialogPage in SelectionChanged: {ex}");
                return;
            }

            string selectedModelName = _modelComboBox.SelectedItem.ToString();

            if (options.Provider == OpenAiProvider.OpenAI)
            {
                options.OpenAIModelName = selectedModelName;
            }
            else
            {
                options.AzureDeploymentName = selectedModelName;
            }

            System.Diagnostics.Debug.WriteLine($"Model selection changed to: {selectedModelName} for provider {options.Provider}");
        }
    }
}