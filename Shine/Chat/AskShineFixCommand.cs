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
                ShowInfo("DTE の取得に失敗しました。");
                return;
            }

            // Error List ウィンドウ → SelectedItems から選択行を取得
            Window errWin = dte.Windows.Item(EnvDTE80.WindowKinds.vsWindowKindErrorList);
            if (errWin?.Object is not EnvDTE80.ErrorList errorList)
            {
                ShowInfo("Error List ウィンドウを取得できませんでした。");
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
                ShowInfo("選択されたエラー項目を取得できませんでした。");
                return;
            }

            string filePath = target.FileName;
            int line = target.Line;
            string message = target.Description;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ShowInfo($"ファイルが見つかりません: {filePath}");
                return;
            }

            /* ─── ソース スニペット抽出（±5 行） ─── */
            var lines = File.ReadAllLines(filePath);
            int start = Math.Max(line - 5, 1);
            int end = Math.Min(line + 4, lines.Length);
            var snippet = new StringBuilder();
            for (int i = start; i <= end; i++)
                snippet.AppendLine($"{i,4}: {lines[i - 1]}");

            /* ─── AI へプロンプト送信 ─── */
            string prompt =
$@"#Role
You are a brilliant C# engineer. Fix the compilation error.

#Error
{message}

#Code
```
{snippet}
```";

            string aiReply;
            try
            {
                var opt = (AiAssistantOptions)ShinePackage.Instance.GetDialogPage(typeof(AiAssistantOptions));
                IChatClientService chat =
                    opt.Provider == OpenAiProvider.OpenAI
                    ? new OpenAiClientService(opt.OpenAIApiKey, opt.OpenAIModelName, opt.Temperature)
                    : new AzureOpenAiClientService(opt.AzureOpenAIEndpoint, opt.AzureOpenAIApiKey,
                                                   opt.AzureDeploymentName, opt.Temperature);

                aiReply = await chat.GetChatResponseAsync(prompt);
            }
            catch (Exception ex)
            {
                aiReply = $"AI との通信で例外が発生しました: {ex.Message}";
            }

            /* ─── Chat ウィンドウへ書き込み ─── */
            var tw = ShinePackage.Instance.FindToolWindow(typeof(ChatToolWindow), 0, true);
            if (tw?.Content is ChatToolWindowControl ctrl)
            {
#if DEBUG
                ctrl.AddChatMessageFromExternal("User", prompt);
#endif
                ctrl.AddChatMessageFromExternal("Assistant", aiReply);
                ((IVsWindowFrame)tw.Frame).Show();
            }
            else
            {
                ShowInfo("ChatToolWindow が取得できませんでした。");
            }
        }

        private void ShowInfo(string msg) =>
            VsShellUtilities.ShowMessageBox(_package, msg, "Shine",
                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
