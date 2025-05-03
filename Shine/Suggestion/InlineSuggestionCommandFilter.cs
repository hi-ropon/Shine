// --------------- InlineSuggestionCommandFilter.cs ---------------
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;
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
            // キャレット移動停止監視
            //_view.Caret.PositionChanged += (_, __) => _manager.ScheduleIdleRequest();
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguid, uint id, uint opt, IntPtr pvaIn, IntPtr pvaOut)
        {
            // Enter キーで確定しつつサジェスト更新要求
            if (pguid == VSConstants.VSStd2K && id == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                _ = Task.Run(() => _manager.OnEnterAsync());
            }
            return _next.Exec(ref pguid, id, opt, pvaIn, pvaOut);
        }
    }
}
