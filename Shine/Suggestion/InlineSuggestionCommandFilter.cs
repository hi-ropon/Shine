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
    /// Tab → VS / Copilot へバブル → 30 ms 待機 → フォールバック  
    /// Enter → AI へ補完リクエスト
    /// </summary>
    internal sealed class InlineSuggestionCommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        private readonly InlineSuggestionManager _manager;
        private IOleCommandTarget? _next;

        internal InlineSuggestionCommandFilter(IWpfTextView view, InlineSuggestionManager manager)
        {
            _view = view;
            _manager = manager;
        }

        public void Attach(IVsTextView adapter)
            => adapter.AddCommandFilter(this, out _next);

        public int QueryStatus(ref Guid pg, uint c, OLECMD[] cmds, IntPtr pTxt)
            => _next!.QueryStatus(ref pg, c, cmds, pTxt);

        public int Exec(ref Guid pg, uint id, uint opt, IntPtr inPtr, IntPtr outPtr)
        {
            /* ───────── Tab ───────── */
            if (pg == VSConstants.VSStd2K &&
                id == (uint)VSConstants.VSStd2KCmdID.TAB &&
                _manager.HasActiveSession)
            {
                _manager.RememberCaret();                     // 押下前をメモ
                var hr = _next!.Exec(ref pg, id, opt, inPtr, outPtr);

                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await Task.Delay(30);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _manager.FallbackInsertIfNeeded();
                });

                return hr;
            }

            /* ───────── Enter ───────── */
            if (pg == VSConstants.VSStd2K &&
                id == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                _ = Task.Run(() => _manager.OnEnterAsync());
            }

            return _next!.Exec(ref pg, id, opt, inPtr, outPtr);
        }
    }
}
