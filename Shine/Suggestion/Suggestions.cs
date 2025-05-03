// --------------- Suggestions.cs ---------------
using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Shine.Suggestion
{
    /// <summary>
    /// 内部 IntelliCode API へリフレクションでアクセスし、
    /// Proposal をゴーストテキストとして表示するユーティリティ
    /// （VS のバージョン変化により動作しなくなる可能性があります）
    /// </summary>
    internal static class Suggestions
    {
        // 非公開型キャッシュ
        private static readonly Type _inlineCompletionsType;
        private static readonly MethodInfo _tryDisplaySuggestionAsync;
        private static readonly MethodInfo _cacheProposal;
        private static readonly FieldInfo _sessionField;
        private static readonly FieldInfo _suggestionMgrField;
        private static readonly Type _inlineCompSuggestionType;

        static Suggestions()
        {
            var asm = Assembly.Load("Microsoft.VisualStudio.IntelliCode");
            _inlineCompletionsType = asm.GetTypes().First(t => t.Name == "InlineCompletionsInstance");
            _inlineCompSuggestionType = asm.GetTypes().First(t => t.Name == "InlineCompletionSuggestion");

            _cacheProposal = _inlineCompletionsType.GetMethod("CacheProposal",
                                        BindingFlags.Instance | BindingFlags.NonPublic);
            _sessionField = _inlineCompletionsType.GetField("Session",
                                        BindingFlags.Instance | BindingFlags.NonPublic);

            _suggestionMgrField = _inlineCompletionsType.GetField("_suggestionManager",
                                        BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?? _inlineCompletionsType.GetField("SuggestionManager",
                                        BindingFlags.Instance | BindingFlags.NonPublic);

            _tryDisplaySuggestionAsync =
                _suggestionMgrField.FieldType.GetMethod("TryDisplaySuggestionAsync");
        }

        /// <summary>指定位置にゴーストテキストを表示</summary>
        public static async Task ShowAsync(ITextView view, string ghostText, int position)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var inlineInst = view.Properties.PropertyList
                               .FirstOrDefault(kv => kv.Key is Type t && t.Name == "InlineCompletionsInstance").Value;
            if (inlineInst == null) return;

            // 既存セッションがあれば閉じる
            var curSession = _sessionField.GetValue(inlineInst);
            if (curSession is not null)
            {
                await ((dynamic)curSession).DismissAsync(0, System.Threading.CancellationToken.None);
            }

            // ProposalCollection を生成
            var proposals = Proposals.Create(view, ghostText, position);

            // Internal InlineCompletionSuggestion を new する
            var ctor = _inlineCompSuggestionType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                                                .First();
            object inlineSuggestion = ctor.GetParameters().Length switch
            {
                2 => ctor.Invoke(new object[] { proposals, inlineInst }),
                _ => ctor.Invoke(new object[] { proposals, inlineInst, 0 })
            };

            // SuggestionManager.TryDisplaySuggestionAsync を実行
            var suggMgr = _suggestionMgrField.GetValue(inlineInst);
            var newSessionTask = (Task)_tryDisplaySuggestionAsync.Invoke(suggMgr, new[] { inlineSuggestion, null });
            await newSessionTask.ConfigureAwait(false);

            // Proposal をキャッシュし、即座に表示
            var prop = proposals.Proposals.First();
            _cacheProposal.Invoke(inlineInst, new object[] { prop });
            var newSession = _sessionField.GetValue(inlineInst);
            if (newSession != null)
            {
                await ((dynamic)newSession).DisplayProposalAsync(prop, System.Threading.CancellationToken.None);
            }
        }
    }
}
