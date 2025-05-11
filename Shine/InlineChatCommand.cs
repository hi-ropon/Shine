// ファイル名: InlineChatCommand.cs
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text.Editor;

namespace Shine
{
    /// <summary>Alt+L でインラインチャットを起動する</summary>
    internal sealed class InlineChatCommand
    {
        public const int commandId = (int)PkgCmdIDList.inlineChatCommand;
        public static readonly Guid commandSet = GuidList.guidShinePackageCmdSet;
        private readonly AsyncPackage _package;

        private InlineChatCommand(AsyncPackage package, OleMenuCommandService mcs)
        {
            _package = package;
            var cmdId = new CommandID(commandSet, commandId);
            var cmd = new OleMenuCommand(ExecuteAsync, cmdId);

            mcs.AddCommand(cmd);
        }

        /// <summary>パッケージから呼び出される初期化</summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            _ = new InlineChatCommand(package, mcs);
        }

        /// <summary>実行: 現在の IWpfTextView に入力アドーンメントを挿入</summary>
        private async void ExecuteAsync(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // ① アクティブな IWpfTextView を取得
            var compModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var adapter = compModel.GetService<IVsEditorAdaptersFactoryService>();
            var vsTextMgr = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            vsTextMgr.GetActiveView(1, null, out IVsTextView viewAdapter);
            if (viewAdapter == null) return;
            IWpfTextView wpfView = adapter.GetWpfTextView(viewAdapter);
            if (wpfView == null) return;

            // ② 入力アドーンメントを作成（複数起動を避ける）
            if (!wpfView.Properties.TryGetProperty(typeof(InlineChatSession), out InlineChatSession session))
            {
                var chatSvc = ChatClientServiceFactory.CreateFromOptions();
                
                if (chatSvc == null)
                {
                    ShinePackage.MessageService.OKOnly("AI サービスが初期化できません。オプションで API キー等を設定してください。");
                    return;
                }

                session = new InlineChatSession(wpfView, viewAdapter, chatSvc, _package);
                wpfView.Properties.AddProperty(typeof(InlineChatSession), session);
            }
            session.Toggle();   // 既に開いていれば閉じる／閉じていれば開く
        }
    }
}
