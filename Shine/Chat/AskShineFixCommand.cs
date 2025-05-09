// ────────────────────────────────────────────────────────
//  ファイル名: AskShineFixCommand.cs
//  説明    : Error List の右クリックから AI 修正案を提示するコマンド
// ────────────────────────────────────────────────────────
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Shine
{
    /// <summary>「Shine に質問する」コンテキスト メニュー コマンド</summary>
    internal sealed class AskShineFixCommand
    {
        public const int commandId = 0x0102;
        public static readonly Guid commandSet = new Guid("D1234567-89AB-CDEF-0123-456789ABCDEF");

        private readonly AsyncPackage _package;

        private AskShineFixCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(commandSet, commandId);
            var menuItem = new MenuCommand(ExecuteAsync, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            _ = new AskShineFixCommand(package, mcs);
        }

        /// <summary>コンテキスト メニュー コマンド実行</summary>
        private async void ExecuteAsync(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Package.GetGlobalService(typeof(DTE)) is not DTE2 dte)
            {
                ShinePackage.MessageService.OKOnly("DTE の取得に失敗しました。");
                return;
            }

            // Error List ウィンドウ → SelectedItems から選択行を取得
            Window errWin = dte.Windows.Item(EnvDTE80.WindowKinds.vsWindowKindErrorList);
            if (errWin?.Object is not EnvDTE80.ErrorList errorList)
            {
                ShinePackage.MessageService.OKOnly("Error List ウィンドウを取得できませんでした。");
                return;
            }

            // SelectedItems は object(SAFEARRAY) として返るので Array 経由でキャスト
            ErrorItem target = null;
            if (errorList.SelectedItems is Array selArr && selArr.Length > 0)
            {
                target = selArr.GetValue(0) as ErrorItem;
            }
            else if (errorList.ErrorItems.Count > 0)           // フォールバック: 先頭行
            {
                target = errorList.ErrorItems.Item(1);
            }

            if (target == null)
            {
                ShinePackage.MessageService.OKOnly("選択されたエラー項目を取得できませんでした。");
                return;
            }

            string filePath = target.FileName;
            int line = target.Line;
            string message = target.Description;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ShinePackage.MessageService.OKOnly($"ファイルが見つかりません: {filePath}");
                return;
            }

            string[] lines;

            /* ─── ソース スニペット抽出（編集中の内容を優先取得） ─── */
            var vsDoc = dte.Documents
                           .OfType<Document>()
                           .FirstOrDefault(d => d.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (vsDoc != null && vsDoc.Object("TextDocument") is TextDocument textDoc)
            {
                // 未保存のバッファも含めて全文を取得
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                var fullText = editPoint.GetText(textDoc.EndPoint);
                lines = fullText.Replace("\r\n", "\n").Split('\n');
            }
            else if (File.Exists(filePath))
            {
                // フォールバック: ディスク上のファイルを読み込み
                lines = File.ReadAllLines(filePath);
            }
            else
            {
                ShinePackage.MessageService.OKOnly($"ファイルが見つかりません: {filePath}");
                return;
            }

            var snippet = new StringBuilder();
            for (int i = 1; i <= lines.Length; i++)
                snippet.AppendLine($"{i,4}: {lines[i - 1]}");

            /* ─── AI へプロンプト送信 ─── */
            string prompt =
$@"#Role
You are a brilliant code engineer. Fix the compilation error, and briefly explain what and how you fixed.

#Error
{message}

#Code
```
{snippet}
```";

            // ツールウィンドウ取得
            var tw = ShinePackage.Instance.FindToolWindow(typeof(ChatToolWindow), 0, true);
            var ctrl = tw?.Content as ChatToolWindowControl;
            var frame = tw?.Frame as IVsWindowFrame;

            // ローディング表示開始
            if (ctrl != null)
            {
                ctrl.Dispatcher.Invoke(() => ctrl.StartLoading());
                frame?.Show();
            }

            // AI 呼び出し
            string aiReply;
            try
            {
                var opt = (AiAssistantOptions)ShinePackage.Instance
                              .GetDialogPage(typeof(AiAssistantOptions));
                IChatClientService chat = opt.Provider == OpenAiProvider.OpenAI
                    ? new OpenAiClientService(opt.OpenAIApiKey, opt.OpenAIModelName, opt.Temperature)
                    : new AzureOpenAiClientService(opt.AzureOpenAIEndpoint,
                                                   opt.AzureOpenAIApiKey,
                                                   opt.AzureDeploymentName,
                                                   opt.Temperature);

                aiReply = await chat.GetChatResponseAsync(prompt);
            }
            catch (Exception ex)
            {
                aiReply = $"AI との通信で例外が発生しました: {ex.Message}";
            }
            finally
            {
                // ローディング解除
                if (ctrl != null)
                    ctrl.Dispatcher.Invoke(() => ctrl.StopLoading());
            }

            // Chat ウィンドウに結果表示
            if (ctrl != null)
            {
#if DEBUG
                ctrl.AddChatMessageFromExternal("User", prompt);
#endif
                ctrl.AddChatMessageFromExternal("Assistant", aiReply);
            }
            else
            {
                ShinePackage.MessageService.OKOnly("ChatToolWindow が取得できませんでした。");
            }
        }
    }
}
