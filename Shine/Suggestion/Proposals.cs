// ファイル名: Proposals.cs
using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Shine.Suggestion
{
    /// <summary>ProposalCollection を簡潔に生成するヘルパ</summary>
    internal static class Proposals
    {
        public static ProposalCollection Create(ITextView view, string ghostText, int position)
        {
            var snapshot = view.TextSnapshot;
            // キャレット行の実インデント取得
            var line = snapshot.GetLineFromPosition(position);
            string realIndent = snapshot.GetText(line.Start.Position, position - line.Start.Position);

            // 仮想空白数取得（後段の行だけ手動で使う）
            int vspaces = view.Caret.Position.VirtualSpaces;
            string virtualIndent = new string(' ', vspaces);

            // 生テキストを行ごとに分解
            var lines = ghostText.Replace("\r\n", "\n").Split('\n');

            // 共通先行空白 (minIndent) を除去
            static bool IsIndentChar(char c) => c == ' ' || c == '\t' || c == '\u3000';
            int minIndent = lines
                .Where(l => l.Length > 0)
                .Select(l => l.TakeWhile(IsIndentChar).Count())
                .DefaultIfEmpty(0)
                .Min();
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length >= minIndent)
                    lines[i] = lines[i].Substring(minIndent);

            // 各行に必要なインデントを付与
            var indented = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                {
                    // ← 先頭行はそのまま
                    indented.Add(lines[i]);
                }
                else
                {
                    // 2行目以降だけ実インデント＋仮想空白を付与
                    indented.Add(realIndent + virtualIndent + lines[i]);
                }
            }
            string indentedGhost = string.Join("\n", indented);

            // Proposal の組み立て
            var span = new SnapshotSpan(new SnapshotPoint(snapshot, position), 0);
            var edit = new ProposedEdit(span, indentedGhost);
            var edits = ImmutableArray.Create(edit);

            // VirtualSnapshotPoint はオフセット 0 を指定
            var caretPoint = new VirtualSnapshotPoint(span.Start);

            var proposal = new Proposal(
                /* description      */ ghostText,
                /* edits            */ edits,
                /* caret            */ caretPoint,
                /* completionState  */ (CompletionState?)null,
                /* flags            */ ProposalFlags.SingleTabToAccept | ProposalFlags.ShowCommitHighlight,
                /* commitAction     */ () => true,
                /* proposalId       */ "Shine-Suggestion",
                /* acceptText       */ null,
                /* nextText         */ null,
                /* scope            */ null);

            return new ProposalCollection("Shine-Suggestion", new List<ProposalBase> { proposal });
        }
    }
}
