using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Shine.Suggestion
{
    /// <summary>ProposalCollection を簡潔に生成するヘルパ</summary>
    internal static class Proposals
    {
        public static ProposalCollection Create(ITextView view, string ghostText, int position)
        {
            var span = new SnapshotSpan(new SnapshotPoint(view.TextSnapshot, position), 0);
            var edit = new ProposedEdit(span, ghostText);
            var edits = ImmutableArray.Create(edit);

            // Proposal の生成 (VS2022 のシグネチャに合わせて引数を指定)
            var proposal = new Proposal(
                /* description      */ ghostText,
                /* edits            */ edits,
                /* caret            */ new VirtualSnapshotPoint(span.Start),
                /* completionState  */ (CompletionState?)null,
                /* flags            */ flags: ProposalFlags.SingleTabToAccept | ProposalFlags.ShowCommitHighlight,
                /* commitAction     */ () => true,
                /* proposalId       */ "Shine-Suggestion",
                /* acceptText       */ null,
                /* nextText         */ null,
                /* scope            */ null);

            var list = new List<ProposalBase> { proposal };
            return new ProposalCollection("Shine-Suggestion", list);
        }
    }
}
