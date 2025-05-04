// --------------- InlineSuggestionCommandFilter.cs ---------------
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Threading.Tasks;

namespace Shine.Suggestion
{
    /// <summary>
    /// エディタに Tab／Enter をフックして
    /// ・Tab なら IntelliCode / Copilot へバブル → 30 ms 待機 → フォールバック
    /// ・Enter なら AI へ補完リクエスト
    /// </summary>
    internal sealed class InlineSuggestionCommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        private readonly InlineSuggestionManager _manager;
        private IOleCommandTarget? _next;

        public InlineSuggestionCommandFilter(IWpfTextView view, InlineSuggestionManager manager)
        {
            _view = view;
            _manager = manager;
        }

        /// <summary>VS から渡されるネイティブ IVsTextView にフィルタを登録</summary>
        public void Attach(IVsTextView textViewAdapter)
            => textViewAdapter.AddCommandFilter(this, out _next);

        /* =================================================================== */
        /*                       IOleCommandTarget 実装                         */
        /* =================================================================== */
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds,
                               OLECMD[] prgCmds, IntPtr pCmdText)
            => _next!.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguid, uint id, uint opt,
                        IntPtr pvaIn, IntPtr pvaOut)
        {
            /* ───────────── Tab ───────────── */
            if (pguid == VSConstants.VSStd2K
                && id == (uint)VSConstants.VSStd2KCmdID.TAB
                && _manager.HasActiveSession)
            {
                _manager.RememberCaret();                         // ① 押下前の位置を記録
                var hr = _next!.Exec(ref pguid, id, opt, pvaIn, pvaOut); // ② そのままバブル

                // ③ 30 ms 後にフォールバック挿入を試みる
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await Task.Delay(30);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _manager.FallbackInsertIfNeeded();            // VS が挿入しなければ自前で
                });

                return hr;
            }

            /* ───────────── Enter ───────────── */
            if (pguid == VSConstants.VSStd2K
                && id == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                _ = Task.Run(() => _manager.OnEnterAsync());
            }

            /* ───────── それ以外 ───────── */
            return _next!.Exec(ref pguid, id, opt, pvaIn, pvaOut);
        }
    }
}
