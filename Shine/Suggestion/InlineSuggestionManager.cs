// ファイル名: InlineSuggestionManager.cs
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Shine.Suggestion
{
    /// <summary>
    /// ① Enter で AI へ補完要求  
    /// ② Tab フォールバック時はキャレット行のインデントを保ったまま貼り付け
    /// </summary>
    internal sealed class InlineSuggestionManager : IDisposable
    {
        private readonly IWpfTextView _view;
        private readonly IChatClientService _chat;
        private readonly CancellationTokenSource _cts = new();

        private static Guid _paneGuid =
            new Guid("D2E3747C-1234-ABCD-5678-0123456789AB");

        private object? _currentSession;
        private int? _commitOriginPos;      // Tab 押下時の BufferPosition

        /* ───── プロパティ ───── */
        internal bool HasActiveSession => _currentSession != null;
        internal string? LastProposalText
            => _view.Properties.TryGetProperty<string>("Shine.LastProposalText", out var t) ? t : null;

        /* ───── ctor ───── */
        internal InlineSuggestionManager(IWpfTextView view, IChatClientService chat)
        {
            _view = view;
            _chat = chat;
            _view.Caret.PositionChanged += (_, __) => _commitOriginPos = null;   // キャレット移動でフォールバック不要
        }

        /* =====================================================================
                                 フィルタ側から呼ばれるヘルパ
        =====================================================================*/
        internal void RememberCaret() => _commitOriginPos = _view.Caret.Position.BufferPosition;
        internal void SetCurrentSession(object s) => _currentSession = s;

        /* =====================================================================
                                   Enter → AI へ問い合わせ
        =====================================================================*/
        public async Task OnEnterAsync() => await RequestSuggestionAsync();

        private async Task RequestSuggestionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            int caret = _view.Caret.Position.BufferPosition.Position;
            string ctx = GetContext(_view.TextSnapshot, caret);
            if (string.IsNullOrWhiteSpace(ctx)) return;

            string reply;
            try
            {
                reply = await _chat.GetChatResponseAsync(
$"#Role\nYou are a brilliant pair-programming AI. Continue the code. Return only code, no comment.\n\n#Context\n{ctx}");
            }
            catch { return; }

            string suggestion = PostProcess(reply, ctx);
#if DEBUG
            await DumpReplyAsync(suggestion);
#endif
            if (string.IsNullOrWhiteSpace(suggestion)) return;

            await Suggestions.ShowAsync(_view, this, suggestion, caret);
        }

        /* =====================================================================
                              Tab → VS がコミットしなければフォールバック
        =====================================================================*/
        internal void FallbackInsertIfNeeded()
        {
            if (_commitOriginPos is not int origin) return;

            // VS がコミット済みならキャレットは右へ進んでいる
            if (_view.Caret.Position.BufferPosition > origin)
            {
                MoveCaretTo(origin);
                _commitOriginPos = null;
                return;
            }

            var raw = LastProposalText;
            if (string.IsNullOrEmpty(raw)) { _commitOriginPos = null; return; }

            // ── ① 行頭～実インデント文字列
            var snap = _view.TextSnapshot;
            var line = snap.GetLineFromPosition(origin);
            string realIndent = snap.GetText(line.Start.Position, origin - line.Start.Position);

            // ── ② 仮想空白 (Tab で動いた分など) のスペースを生成
            int virtualSpaces = _view.Caret.Position.VirtualSpaces;
            string virtualIndent = new string(' ', virtualSpaces);

            // ── ③ 提案テキストをインデント調整
            string fixedTxt = ReindentWithCaretIndent(raw, realIndent, virtualIndent);

            using (var edit = _view.TextBuffer.CreateEdit())
            {
                edit.Insert(origin, fixedTxt);
                edit.Apply();
            }

            MoveCaretTo(origin);
            RemoveTabIfExists(origin);     // “\t” が残っていれば除去
            _commitOriginPos = null;
        }

        /* =====================================================================
                         ★ インデント調整 (仮想空白対応版) ★
        =====================================================================*/
        private static string ReindentWithCaretIndent(string raw, string realIndent, string virtualIndent)
        {
            var lines = raw.Replace("\r\n", "\n").Split('\n');

            static bool IsIndentChar(char c) => c is ' ' or '\t' or '\u3000';

            int minIndent = lines.Where(l => l.Length > 0)
                                 .Select(l => l.TakeWhile(IsIndentChar).Count())
                                 .DefaultIfEmpty(0)
                                 .Min();

            // 共通先頭インデントを削る
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length >= minIndent)
                    lines[i] = lines[i][minIndent..];

            // 先頭行: 仮想空白ぶんだけ付加（既存インデントは行内に既にある）
            lines[0] = virtualIndent + lines[0];

            // 2 行目以降: 実インデント + 仮想空白
            string fullIndent = realIndent + virtualIndent;
            for (int i = 1; i < lines.Length; i++)
                lines[i] = fullIndent + lines[i];

            return string.Join("\n", lines);
        }

        /* =====================================================================
                                    そのほか小物ヘルパ
        =====================================================================*/
        private void RemoveTabIfExists(int origin)
        {
            var snap = _view.TextSnapshot;
            if (origin < snap.Length && snap.GetText(origin, 1) == "\t")
            {
                using var edit = _view.TextBuffer.CreateEdit();
                edit.Delete(origin, 1);
                edit.Apply();
            }
        }

        private void MoveCaretTo(int pos)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var snap = _view.TextSnapshot;
            pos = Math.Max(0, Math.Min(pos, snap.Length));
            _view.Caret.MoveTo(new SnapshotPoint(snap, pos));
            _view.Caret.EnsureVisible();
        }

        private static string GetContext(ITextSnapshot snap, int caret)
        {
            var sb = new StringBuilder();
            var line = snap.GetLineFromPosition(caret);
            int cnt = 0;
            while (line != null && cnt < 120)
            {
                sb.Insert(0, line.GetText() + Environment.NewLine);
                if (sb.Length > 4000) break;
                line = line.LineNumber > 0 ? snap.GetLineFromLineNumber(line.LineNumber - 1) : null;
                cnt++;
            }
            return sb.ToString();
        }

        private static string PostProcess(string ai, string context)
        {
            ai = Regex.Replace(ai.Trim(), @"^```[a-z]*\s*|```$", "", RegexOptions.Multiline).Trim();

            static string N(string l)
            {
                var s = l.TrimEnd();
                int idx = s.IndexOf("//", StringComparison.Ordinal);
                if (idx >= 0) s = s[..idx];
                return s.Trim();
            }

            var ctxSet = context.Split('\n').Select(N).Where(l => l.Length > 0).ToHashSet();
            var aiLines = ai.Split('\n').ToList();

            while (aiLines.Count > 0 && (aiLines[0].Length == 0 || ctxSet.Contains(N(aiLines[0]))))
                aiLines.RemoveAt(0);

            return string.Join("\n", aiLines);
        }

#if DEBUG
        private static async Task DumpReplyAsync(string text)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is not IVsOutputWindow ow) return;
            ow.CreatePane(ref _paneGuid, "Shine Suggestions", 1, 0);
            ow.GetPane(ref _paneGuid, out var pane);
            pane?.OutputString($"[Shine @ {DateTime.Now:HH:mm:ss}]\n{text}\n\n");
        }
#endif

        public void Dispose() => _cts.Cancel();
    }
}
