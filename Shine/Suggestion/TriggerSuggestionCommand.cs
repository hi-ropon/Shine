// ファイル名: TriggerSuggestionCommand.cs
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Shine.Suggestion
{
    internal sealed class TriggerSuggestionCommand
    {
        // PkgCmdIDList.triggerSuggestionCommand は uint 型なので、int にキャストする
        public const int commandId = (int)PkgCmdIDList.triggerSuggestionCommand;
        public static readonly Guid commandSet = GuidList.guidShinePackageCmdSet;
        private readonly AsyncPackage _package;

        private TriggerSuggestionCommand(AsyncPackage package, OleMenuCommandService mcs)
        {
            _package = package;
            var id = new CommandID(commandSet, commandId);
            var cmd = new OleMenuCommand(Execute, id);
            cmd.BeforeQueryStatus += QueryStatus;
            mcs.AddCommand(cmd);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            // UI スレッドで登録
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs != null)
                new TriggerSuggestionCommand(package, mcs);
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            var cmd = sender as OleMenuCommand;
            // 常に有効・表示
            cmd.Visible = cmd.Enabled = true;
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // SVsTextManager の取得
            var textMgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (textMgr == null)
                return;

            textMgr.GetActiveView(1, null, out IVsTextView vsTextView);
            if (vsTextView == null)
                return;

            // WPF テキストビューに変換
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var adapterService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var wpfView = adapterService.GetWpfTextView(vsTextView);
            if (wpfView == null)
                return;

            // プロパティからマネージャーを取得して実行
            if (wpfView.Properties.TryGetProperty<InlineSuggestionManager>("Shine.InlineSuggestionManager", out var manager))
            {
                if (!manager.IsBusy)
                    _ = manager.OnEnterAsync();
            }
        }

    }
}
