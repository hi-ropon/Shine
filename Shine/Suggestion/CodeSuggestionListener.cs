// ファイル名: CodeSuggestionListener.cs

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Shine.Suggestion
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CodeSuggestionListener : IVsTextViewCreationListener
    {
        [Import] internal IVsEditorAdaptersFactoryService _adapter = null!;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            // ① UI スレッドであることをチェック
            ThreadHelper.ThrowIfNotOnUIThread();

            // ② adapter が取れていなければ抜ける
            if (_adapter == null)
                return;

            // ③ WPF テキストビューを取得（null ならコードエディタ以外）
            IWpfTextView view = _adapter.GetWpfTextView(textViewAdapter);
            if (view == null)
                return;

            // ④ オプションから ChatClientService を生成
            var chatSvc = ChatClientServiceFactory.CreateFromOptions();
            if (chatSvc == null)
                return;

            // ⑤ マネージャー／フィルタを添付
            var manager = new InlineSuggestionManager(view, chatSvc);
            var filter = new InlineSuggestionCommandFilter(view, manager);
            filter.Attach(textViewAdapter);
        }
    }
}
