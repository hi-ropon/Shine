using System;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Shine.Helpers;

namespace Shine.Helpers
{
    /// <summary>
    /// VS のメッセージボックスをラップするサービス実装
    /// </summary>
    public sealed class MessageService : IMessageService
    {
        private readonly AsyncPackage _package;

        public MessageService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public void OKOnly(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(
                _package,
                message,
                "Shine",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public DialogResult QuestionOKCancel(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int result = VsShellUtilities.ShowMessageBox(
                _package,
                message,
                "Shine",
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            return result == (int)VSConstants.MessageBoxResult.IDOK
                ? DialogResult.OK
                : DialogResult.Cancel;
        }

        public void ShowError(Exception ex, string contextMessage = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // コンテキストメッセージ＋例外メッセージを組み立て
            string display = string.IsNullOrWhiteSpace(contextMessage)
                ? ex.Message
                : $"{contextMessage}\n\n詳細: {ex.Message}";

            VsShellUtilities.ShowMessageBox(
                _package,
                display,
                "Shine - エラー",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
