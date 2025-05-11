// ファイル名: InlineChatKeyFilter.cs
using System;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Shine
{
    /// <summary>
    /// エディタに奪われたキー操作を TextBox へ転送し、VS 側には流さないフィルター
    /// </summary>
    internal sealed class InlineChatKeyFilter : IOleCommandTarget
    {
        private readonly TextBox _input;
        private readonly IOleCommandTarget _next;

        public InlineChatKeyFilter(TextBox input, IVsTextView viewAdapter)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            ErrorHandler.ThrowOnFailure(viewAdapter.AddCommandFilter(this, out _next));
        }

        #region IOleCommandTarget
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds,
                               OLECMD[] prgCmds, IntPtr pCmdText)
            => _next?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText) ?? VSConstants.S_OK;

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt,
                        IntPtr pvaIn, IntPtr pvaOut)
        {
            // ===== 1) VSStd2K コマンド（編集・カーソル） =====
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.BACKSPACE:
                        EditingCommands.Backspace.Execute(null, _input);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.DELETE:
                        EditingCommands.Delete.Execute(null, _input);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.LEFT:
                        EditingCommands.MoveLeftByCharacter.Execute(null, _input);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.RIGHT:
                        EditingCommands.MoveRightByCharacter.Execute(null, _input);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.UP:
                        EditingCommands.MoveUpByLine.Execute(null, _input);
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.DOWN:
                        EditingCommands.MoveDownByLine.Execute(null, _input);
                        return VSConstants.S_OK;
                }
            }

            // ===== 2) VSStd97 コマンド（古い Delete など） =====
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 &&
                (VSConstants.VSStd97CmdID)nCmdID == VSConstants.VSStd97CmdID.Delete)
            {
                EditingCommands.Delete.Execute(null, _input);
                return VSConstants.S_OK;
            }

            // ===== 3) それ以外は次へ =====
            return _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                   ?? VSConstants.S_OK;
        }
        #endregion
    }
}
