// ファイル名: TriggerSuggestionCommand.cs
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Shine.Suggestion
{
    internal sealed class TriggerSuggestionCommand
    {
        // PkgCmdIDList.triggerSuggestionCommand は uint 型なので int へキャスト
        public const int commandId = (int)PkgCmdIDList.triggerSuggestionCommand;
        public static readonly Guid commandSet = GuidList.guidShinePackageCmdSet;

        private readonly AsyncPackage _package;

        private TriggerSuggestionCommand(AsyncPackage package, OleMenuCommandService mcs)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            var cmdId = new CommandID(commandSet, commandId);
            var cmd = new OleMenuCommand(ExecuteAsync, cmdId);

            // チャット稼働中・サジェスチョン稼働中は無効化
            cmd.BeforeQueryStatus += (s, e) =>
            {
                var c = (OleMenuCommand)s;
                c.Visible = true;
                c.Enabled = !ShineFeatureGate.IsInlineChatActive &&
                            !ShineFeatureGate.IsSuggestionRunning;
            };

            mcs.AddCommand(cmd);
        }

        /// <summary>パッケージから呼び出される初期化。</summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            _ = new TriggerSuggestionCommand(package, mcs!);
        }

        /// <summary>コマンド本体。排他フラグを立て非同期でサジェスチョンを実行。</summary>
        private async void ExecuteAsync(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!ShineFeatureGate.TryBeginSuggestion())
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            try
            {
                // ① アクティブな VS テキストビューを取得
                var textMgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
                if (textMgr == null) return;

                textMgr.GetActiveView(1, null, out IVsTextView vsTextView);
                if (vsTextView == null) return;

                // ② WPF テキストビューへ変換
                var compModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                var adapterSvc = compModel.GetService<IVsEditorAdaptersFactoryService>();
                var wpfView = adapterSvc.GetWpfTextView(vsTextView);
                if (wpfView == null) return;

                // ③ プロパティからマネージャーを取得しサジェスチョン実行
                if (wpfView.Properties.TryGetProperty<InlineSuggestionManager>(
                        "Shine.InlineSuggestionManager", out var manager))
                {
                    if (!manager.IsBusy)
                        await manager.OnEnterAsync();
                }
            }
            finally
            {
                ShineFeatureGate.EndSuggestion();
            }
        }
    }
}
