// --------------- InlineSuggestionCommandFilter.cs ---------------
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shine.Suggestion
{
    internal sealed class InlineSuggestionCommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        private readonly InlineSuggestionManager _manager;
        private IOleCommandTarget _next;

        public InlineSuggestionCommandFilter(IWpfTextView view, InlineSuggestionManager manager)
        {
            _view = view;
            _manager = manager;
        }

        public void Attach(IVsTextView textViewAdapter)
        {
            textViewAdapter.AddCommandFilter(this, out _next);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguid, uint id, uint opt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguid == VSConstants.VSStd2K && id == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                if (_manager.HasActiveSession)
                {
                    _manager.RememberCaret();               // ① キャレット記録
                    var hr = _next.Exec(ref pguid, id, opt, pvaIn, pvaOut); // ② Tab をバブル
                    _manager.RemoveTabAtOriginIfAny();      // ④ 余計な Tab を削除（常に実行）

                    return hr;
                }
            }
            else if (pguid == VSConstants.VSStd2K && id == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                _ = Task.Run(() => _manager.OnEnterAsync());
            }
            return _next.Exec(ref pguid, id, opt, pvaIn, pvaOut);
        }
    }
}
