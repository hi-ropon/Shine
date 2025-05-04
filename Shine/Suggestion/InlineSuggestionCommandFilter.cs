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
            // キャレット移動停止監視
            //_view.Caret.PositionChanged += (_, __) => _manager.ScheduleIdleRequest();
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguid, uint id, uint opt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguid == VSConstants.VSStd2K && id == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                if (_manager.HasActiveSession)
                {
                    // Tab 処理前のキャレット位置を記録
                    _manager.RememberCaret();

                    // 1) Tab を下位へバブルさせる
                    var hr = _next.Exec(ref pguid, id, opt, pvaIn, pvaOut);

                    // 2) バッファが変化しなければ自前で挿入
                    _manager.FallbackInsertIfNeeded();

                    return hr;            // 既にバブルさせたのでそのまま返す
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
