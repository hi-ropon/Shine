// ファイル名: CodeSuggestionListener.cs
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Shine.Suggestion
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("code"), TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CodeSuggestionListener : IVsTextViewCreationListener
    {
        [Import] internal IVsEditorAdaptersFactoryService _adapter = null!;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_adapter == null) return;

            var view = _adapter.GetWpfTextView(textViewAdapter);
            if (view == null) return;

            var chatSvc = ChatClientServiceFactory.CreateFromOptions();
            if (chatSvc == null) return;

            var manager = new InlineSuggestionManager(view, chatSvc);
            view.Properties["Shine.InlineSuggestionManager"] = manager;

            var filter = new InlineSuggestionCommandFilter(view, manager);
            filter.Attach(textViewAdapter);
        }
    }
}
