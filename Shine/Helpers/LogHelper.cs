using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace Shine.Helpers
{
    internal class LogHelper
    {
        /* ──────────────────────────────────────────
         *           VS 出力ウィンドウへデバッグ出力
         * ──────────────────────────────────────────*/
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(string msg)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is not IVsOutputWindow ow)
                    return;

                var paneGuid = VSConstants.GUID_OutWindowGeneralPane;   // 「全般」ペイン
                ow.CreatePane(ref paneGuid, "Shine", 1, 0);
                ow.GetPane(ref paneGuid, out var pane);
                pane?.OutputString($"[Shine @ {DateTime.Now:HH:mm:ss}] \n{msg}{Environment.NewLine}");
            });
        }
    }
}
