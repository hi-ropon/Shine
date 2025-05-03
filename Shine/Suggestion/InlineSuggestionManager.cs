// --------------- InlineSuggestionManager.cs ---------------
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Shine.Suggestion
{
    /// <summary>
    /// ① キーイベント／キャレット停止を検知  
    /// ② コンテキスト（直前数行＋コメント）を抽出  
    /// ③ OpenAI / AzureOpenAI へ補完リクエスト  
    /// ④ Suggestions.ShowAsync(...) でゴーストテキスト表示
    /// </summary>
    internal class InlineSuggestionManager
    {
        private readonly IWpfTextView _view;
        private readonly IChatClientService _chat;
        private readonly CancellationTokenSource _cts = new();
        private readonly TimeSpan _idleDelay = TimeSpan.FromMilliseconds(750);
        private bool _requestPending;

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

        /// <summary>一定時間入力が止まったら呼び出し</summary>
        public void ScheduleIdleRequest()
        {
            if (_requestPending) return;
            _requestPending = true;
            _ = Task.Delay(_idleDelay, _cts.Token).ContinueWith(async t =>
            {
                if (t.IsCanceled) return;
                await RequestSuggestionAsync();
            });
        }

        private async Task RequestSuggestionAsync()
        {
            _requestPending = false;
            var caret = _view.Caret.Position.BufferPosition.Position;
            string context = GetContext(_view.TextSnapshot, caret);
            if (string.IsNullOrWhiteSpace(context)) return;

            string prompt =
$@"#Role
You are a brilliant pair‑programming AI. Continue the code. Return only code, no comment.

#Context
{context}";

            string reply;
            try
            {
                reply = await _chat.GetChatResponseAsync(prompt);
            }
            catch
            {
                return;
            }

            string suggestion = PostProcess(reply, context);
            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                await Suggestions.ShowAsync(_view, suggestion, caret);
            }
        }

        /// <summary>カーソル直前最大 120 行をコンテキストとして取得</summary>
        private static string GetContext(ITextSnapshot snap, int caret)
        {
            var line = snap.GetLineFromPosition(caret);
            var sb = new StringBuilder();
            int lines = 0;
            while (line.LineNumber >= 0 && lines < 120)
            {
                sb.Insert(0, line.GetText() + Environment.NewLine);
                if (sb.Length > 4000) break;      // Token 節約
                line = line.LineNumber > 0 ? snap.GetLineFromLineNumber(line.LineNumber - 1) : null;
                if (line == null) break;
                lines++;
            }
            return sb.ToString();
        }

        /// <summary>AI 応答から既存コード重複を除去</summary>
        private static string PostProcess(string ai, string context)
        {
            ai = Regex.Replace(ai.Trim(), @"^```[a-z]*\s*|```$", "", RegexOptions.Multiline).Trim();
            // 先頭が context と重複している行をドロップ
            var ctxLines = context.Split('\n').Select(l => l.TrimEnd()).ToList();
            var aiLines = ai.Split('\n').ToList();
            int i = 0;
            while (i < aiLines.Count && i < ctxLines.Count && aiLines[i].Trim() == ctxLines[ctxLines.Count - 1 - i].Trim())
                i++;
            return string.Join("\n", aiLines.Skip(i));
        }

        public void Dispose() => _cts.Cancel();
    }
}
