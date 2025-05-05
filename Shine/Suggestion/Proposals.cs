// ファイル名: Proposals.cs
using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
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
            // 1. キャレット行の実インデント取得
            var line = snapshot.GetLineFromPosition(position);
            string realIndent = snapshot.GetText(line.Start.Position, position - line.Start.Position);

            // 2. キャレットの仮想空白数取得
            int vspaces = view.Caret.Position.VirtualSpaces;
            string virtualIndent = new string(' ', vspaces);

            // 3. 生テキストを改行で分割し、先頭行を除いた行の共通先行空白（minIndent）を削除
            var lines = ghostText.Replace("\r\n", "\n").Split('\n');
            // インデントとみなす文字
            static bool IsIndentChar(char c) => c == ' ' || c == '\t' || c == '\u3000';
            // 非空行の先行空白文字数を列挙して最小値を取る
            int minIndent = lines
                .Where(l => l.Length > 0)
                .Select(l => l.TakeWhile(IsIndentChar).Count())
                .DefaultIfEmpty(0)
                .Min();
            // 共通インデントを削除
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length >= minIndent)
                    lines[i] = lines[i].Substring(minIndent);
            }

            // 4. 各行にキャレット位置のインデントをプレフィックス
            //    - 先頭行：仮想空白のみ
            //    - 2行目以降：実インデント＋仮想空白
            var indented = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                    indented.Add(virtualIndent + lines[i]);
                else
                    indented.Add(realIndent + virtualIndent + lines[i]);
            }
            string indentedGhost = string.Join("\n", indented);

            // 5. Proposal の組み立て
            var span = new SnapshotSpan(new SnapshotPoint(snapshot, position), 0);
            var edit = new ProposedEdit(span, indentedGhost);
            var edits = ImmutableArray.Create(edit);

            var proposal = new Proposal(
                /* description      */ ghostText,
                /* edits            */ edits,
                /* caret            */ new VirtualSnapshotPoint(span.Start, vspaces),
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
