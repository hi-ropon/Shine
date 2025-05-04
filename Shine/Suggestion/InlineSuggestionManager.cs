// ファイル名: InlineSuggestionManager.cs
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Shine.Suggestion
{
    /// <summary>
    /// InlineSuggestionManager：
    /// ① Enter キーでのみ補完要求
    /// ② Output ウィンドウへ raw reply 表示
    /// ③ あらかじめキャレット位置を固定してからリクエスト
    /// ④ IAsyncCompletionBroker 経由でゴーストテキストを表示
    /// </summary>
    internal class InlineSuggestionManager : IDisposable
    {
        private readonly IWpfTextView _view;
        private readonly IChatClientService _chat;
        private readonly CancellationTokenSource _cts = new();
        private static Guid _shineOutputPaneGuid = new("D2E3747C-1234-ABCD-5678-0123456789AB");

        public InlineSuggestionManager(IWpfTextView view, IChatClientService chatClient)
        {
            _view = view;
            _chat = chatClient;
        }

        /// <summary>Enter キー押下時に呼び出し</summary>
        public async Task OnEnterAsync()
        {
            await RequestSuggestionAsync();
        }

        /// <summary>補完リクエスト処理</summary>
        private async Task RequestSuggestionAsync()
        {
            // 1) UI スレッド保証
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // 2) キャレット位置を固定
            var caretPoint = _view.Caret.Position.BufferPosition;
            int position = caretPoint.Position;

            // 3) コンテキスト取得
            var snapshot = _view.TextSnapshot;
            string context = GetContext(snapshot, position);
            if (string.IsNullOrWhiteSpace(context))
                return;

            // 4) AI にリクエスト
            string reply;
            try
            {
                reply = await _chat.GetChatResponseAsync(
$"#Role\nYou are a brilliant pair-programming AI. Continue the code. Return only code, no comment.\n\n#Context\n{context}");
            }
            catch
            {
                return;
            }

            // 5) 重複部分を落としてゴーストテキスト生成
            string suggestion = PostProcess(reply, context);
            await DumpReplyToOutputAsync(suggestion);
            DumpAllKeys(_view);
            if (string.IsNullOrWhiteSpace(suggestion))
                return;

            //// 6) IAsyncCompletionBroker 経由でゴーストテキストを表示
            await Suggestions.ShowAsync(_view, suggestion, position);
        }

        /// <summary>raw AI reply を出力ウィンドウへ</summary>
        private async Task DumpReplyToOutputAsync(string reply)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var outWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outWindow == null)
                return;

            // カスタムペインを作成（既存ならスキップ）
            outWindow.CreatePane(
                ref _shineOutputPaneGuid,
                "Shine Suggestions",
                fInitVisible: 1,
                fClearWithSolution: 0);

            outWindow.GetPane(ref _shineOutputPaneGuid, out IVsOutputWindowPane pane);
            if (pane == null)
                return;

            pane.Activate();
            pane.OutputString($"[Shine @ {DateTime.Now:HH:mm:ss}]\n{reply}\n\n");
        }

        /// <summary>PropertyList の全キーを出力 (デバッグ用)</summary>
        private static void DumpAllKeys(ITextView view)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var outWindow = ServiceProvider.GlobalProvider
                    .GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outWindow == null)
                    return;

                var dbgPaneGuid = VSConstants.GUID_OutWindowDebugPane;
                outWindow.CreatePane(ref dbgPaneGuid, "Shine-Keys", 1, 0);
                outWindow.GetPane(ref dbgPaneGuid, out var pane);
                pane.OutputString("=== PropertyList Keys Start ===\n");
                foreach (var kv in view.Properties.PropertyList)
                {
                    pane.OutputString($"KeyType: {kv.Key.GetType().FullName}\n");
                }
                pane.OutputString("=== PropertyList Keys End ===\n\n");
            });
        }

        /// <summary>カーソル直前最大 120 行をコンテキストとして取得</summary>
        private static string GetContext(ITextSnapshot snap, int caret)
        {
            var line = snap.GetLineFromPosition(caret);
            var sb = new StringBuilder();
            int lines = 0;
            while (line != null && lines < 120)
            {
                sb.Insert(0, line.GetText() + Environment.NewLine);
                if (sb.Length > 4000) break;
                line = line.LineNumber > 0
                    ? snap.GetLineFromLineNumber(line.LineNumber - 1)
                    : null;
                lines++;
            }
            return sb.ToString();
        }

        /// <summary>AI 応答から既存コード重複を除去</summary>
        private static string PostProcess(string ai, string context)
        {
            ai = Regex.Replace(ai.Trim(), @"^```[a-z]*\s*|```$", "", RegexOptions.Multiline).Trim();
            var ctx = context.Split('\n').Select(l => l.TrimEnd()).ToList();
            var aiLines = ai.Split('\n').ToList();
            int i = 0;
            while (i < aiLines.Count && i < ctx.Count &&
                   aiLines[i].Trim() == ctx[ctx.Count - 1 - i].Trim())
            {
                i++;
            }
            return string.Join("\n", aiLines.Skip(i));
        }

        public void Dispose() => _cts.Cancel();
    }
}
