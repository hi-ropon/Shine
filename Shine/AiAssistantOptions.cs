using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing.Design;

namespace Shine
{
    /// <summary>
    /// OpenAI プロバイダーの種類を定義する列挙体
    /// </summary>
    public enum OpenAiProvider
    {
        OpenAI,
        AzureOpenAI
    }

    /// <summary>
    /// AI アシスタントのオプションを定義するクラス
    /// </summary>
    public class AiAssistantOptions : DialogPage
    {
#if DEBUG
        [Category("Provider")]
        [DisplayName("プロバイダー")]
        [Description("使用するAIプロバイダーを選択します。")]
        public OpenAiProvider Provider { get; set; } = OpenAiProvider.OpenAI;

        // OpenAI 設定項目
        [Category("OpenAI")]
        [DisplayName("API キー")]
        [Description("OpenAI API の API キーを入力します。")]
        public string OpenAIApiKey { get; set; } = "sk-";

        [Category("OpenAI")]
        [DisplayName("モデル名")]
        [Description("利用するモデル名を選択してください。")]
        [Editor(typeof(ModelNameEditor), typeof(UITypeEditor))]
        public string OpenAIModelName { get; set; } = "o4-mini";
#else
        // リリースビルド時は非表示にし、固定で AzureOpenAI を利用
        [Browsable(false)]
        public OpenAiProvider Provider { get; set; } = OpenAiProvider.AzureOpenAI;

        [Browsable(false)]
        public string OpenAIApiKey { get; set; } = "";

        [Browsable(false)]
        public string OpenAIModelName { get; set; } = "";
#endif

#if DEBUG
        [Category("OpenAI")]
#else
        // リリース時は温度設定を Azure OpenAI のカテゴリに変更
        [Category("Azure OpenAI")]
#endif
        [DisplayName("Temperature")]
        [Description("生成テキストの温度設定（0.0～1.0 の値）")]
        public float Temperature { get; set; } = 0.3f;

        // Azure OpenAI 設定項目（常に表示）
        [Category("Azure OpenAI")]
        [DisplayName("エンドポイント")]
        [Description("Azure OpenAI のエンドポイント URL を入力します。")]
        public string AzureOpenAIEndpoint { get; set; } = "https://your-azure-openai-resource.openai.azure.com/";

        [Category("Azure OpenAI")]
        [DisplayName("API キー")]
        [Description("Azure OpenAI の API キーを入力します。")]
        public string AzureOpenAIApiKey { get; set; } = "azure-api-key";

        [Category("Azure OpenAI")]
        [DisplayName("デプロイメント名")]
        [Description("Azure OpenAI でのデプロイメント名を入力します。")]
        [Editor(typeof(ModelNameEditor), typeof(UITypeEditor))]
        public string AzureDeploymentName { get; set; } = "your-deployment-name";

        private int _chatHistoryCount = 3;

        [Category("チャット履歴")]
        [DisplayName("履歴数")]
        [Description("チャット履歴に保持するメッセージ数（0～10）。0の場合、履歴は保持しません。")]
        [DefaultValue(5)]
        [Range(0, 10)]
        public int ChatHistoryCount
        {
            get { return _chatHistoryCount; }
            set { _chatHistoryCount = Math.Max(0, Math.Min(10, value)); }
        }

        [Category("General")]
        [DisplayName("Enable Inline Suggestion")]
        [Description("コード入力中にゴーストテキストで AI サジェスチョンを表示します")]
        public bool EnableInlineSuggestion { get; set; } = true;


        /// <summary>
        /// オプションの適用時に呼び出されるメソッド
        /// </summary>
        /// <param name="e"></param>
        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);

            // リリースビルドの場合、Provider を強制的に AzureOpenAI に設定
#if !DEBUG
            this.Provider = OpenAiProvider.AzureOpenAI;
#endif

            // オプション変更後にツールウィンドウの設定を再初期化
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var window = ShinePackage.Instance.FindToolWindow(typeof(ChatToolWindow), 0, true);
                if (window?.Content is ChatToolWindowControl control)
                {
                    control.InitializeSettings();
                    control.UpdateModelComboBox();
                }
            });
        }
    }
}
