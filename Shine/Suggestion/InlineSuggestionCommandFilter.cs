using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Shine.Helpers;

namespace Shine.Suggestion
{
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

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            // Alt+K（triggerSuggestionCommand）の有効/無効を動的に切替
            if (pguidCmdGroup == GuidList.guidShinePackageCmdSet)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    if (prgCmds[i].cmdID == PkgCmdIDList.triggerSuggestionCommand)
                    {
                        prgCmds[i].cmdf = (uint)(
                            OLECMDF.OLECMDF_SUPPORTED |
                            (_manager.IsBusy ? 0 : OLECMDF.OLECMDF_ENABLED)
                        );
                        return VSConstants.S_OK;
                    }
                }
            }

            // Tab などその他は既存に委譲
            return _next!.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            // ───────── Alt+K ─────────
            if (pguidCmdGroup == GuidList.guidShinePackageCmdSet &&
                nCmdID == PkgCmdIDList.triggerSuggestionCommand)
            {
                if (_manager.IsBusy)
                {
                    LogHelper.DebugLog("Alt+K ignored – suggestion already running.");
                    return VSConstants.S_OK;
                }
                _ = Task.Run(() => _manager.OnEnterAsync());
            }

            // ───────── Tab （フォールバック挿入）─────────
            if (pguidCmdGroup == VSConstants.VSStd2K &&
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB &&
                _manager.HasActiveSession)
            {
                _manager.RememberCaret();
                var hr = _next!.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await Task.Delay(30);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _manager.FallbackInsertIfNeeded();
                });

                return hr;
            }

            // その他は既存に委譲
            return _next!.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }
    }
}
