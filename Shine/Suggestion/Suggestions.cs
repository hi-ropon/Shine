// ────────────────────────────────────────────────
//  ファイル名: Suggestions.cs
//  説明: IntelliCode (VS 17.10+) の内部 API をリフレクションで呼び出し、
//       ゴーストテキスト（インライン補完）を表示するユーティリティ。
// ────────────────────────────────────────────────
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shine.Suggestion
{
    internal static class Suggestions
    {
        // ────────── 反射キャッシュ ──────────
        private static readonly Assembly? _icAsm;
        private static readonly Type? _inlineCompletionsType;
        private static readonly Type? _inlineCompSuggestionType;
        private static readonly FieldInfo? _suggestionMgrField;
        private static readonly MethodInfo? _tryDisplaySuggestionAsync;

        static Suggestions()
        {
            _icAsm = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Microsoft.VisualStudio.IntelliCode")
                     ?? Assembly.Load("Microsoft.VisualStudio.IntelliCode");

            if (_icAsm == null) return;

            _inlineCompletionsType = _icAsm.GetTypes().FirstOrDefault(t => t.Name.EndsWith("InlineCompletionsInstance"));
            _inlineCompSuggestionType = _icAsm.GetTypes().FirstOrDefault(t => t.Name.EndsWith("InlineCompletionSuggestion"));
            if (_inlineCompletionsType == null || _inlineCompSuggestionType == null) return;

            _suggestionMgrField = _inlineCompletionsType.GetField("_suggestionManager", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?? _inlineCompletionsType.GetField("SuggestionManager", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_suggestionMgrField != null)
            {
                var mgrType = _suggestionMgrField.FieldType;
                _tryDisplaySuggestionAsync = mgrType.GetMethod("TryDisplaySuggestionAsync",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // ────────── Public API ──────────
        public static async Task ShowAsync(ITextView view, InlineSuggestionManager manager, string ghostText, int position)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_inlineCompletionsType == null || _inlineCompSuggestionType == null)
            {
                Log("[Suggestions] IntelliCode 型が見つかりません。");
                return;
            }

            var inlineInst = FindInlineInstance(view);
            if (inlineInst == null)
            {
                Log("[Suggestions] InlineCompletionsInstance が取得できません。");
                return;
            }

            // → ① フル提案テキストをプロパティに保存
            view.Properties["Shine.FullProposalText"] = ghostText;

            // ① ProposalCollection 生成（表示拒否を避けるため 3 行・240 文字にトリム）
            ghostText = TrimSuggestion(ghostText);
            if (string.IsNullOrEmpty(ghostText))
            {
                Log("[Suggestions] 空行または改行のみのため提案せず。");
                return;
            }

            // → ③ トリム後提案をプロパティに保存
            view.Properties["Shine.LastProposalText"] = ghostText;

            var proposals = Proposals.Create(view, ghostText, position);
            if (proposals.Proposals.Count == 0)
            {
                Log("[Suggestions] Proposal を生成できませんでした。");
                return;
            }

            // ② InlineCompletionSuggestion 生成
            object inlineSuggestion;
            try
            {
                inlineSuggestion = CreateInlineSuggestion(inlineInst, proposals);
            }
            catch (Exception ex)
            {
                Log($"[Suggestions] InlineCompletionSuggestion 生成失敗: {ex.Message}");
                return;
            }

            // ③ SuggestionManager.TryDisplaySuggestionAsync 呼び出し
            var suggMgr = _suggestionMgrField?.GetValue(inlineInst);
            if (suggMgr == null || _tryDisplaySuggestionAsync == null)
            {
                Log("[Suggestions] SuggestionManager が取得できません。");
                return;
            }

            var taskObj = _tryDisplaySuggestionAsync.Invoke(
                             suggMgr, new object[] { inlineSuggestion, CancellationToken.None });

            if (taskObj is not Task task)
            {
                Log("[Suggestions] TryDisplaySuggestionAsync が Task を返しませんでした。");
                return;
            }

            await task.ConfigureAwait(false);

            // 17.10+ normally returns SuggestionSessionBase or null
            var session = task.GetType().GetProperty("Result")?.GetValue(task);
            if (session is null)
            {
                Log("[Suggestions] IntelliCode が提案を表示しませんでした。(改行・重複・機能設定などが原因)");
                return;
            }

            manager.SetCurrentSession(session);

            view.Properties["Shine.LastProposalText"] = ghostText;

            var prop = proposals.Proposals.First();
            await ((dynamic)session).DisplayProposalAsync(prop, CancellationToken.None);
        }

        // ────────── Helper ──────────
        private static object? FindInlineInstance(ITextView view)
            => view.Properties.PropertyList
                   .FirstOrDefault(kv => kv.Value?.GetType().Name.Contains("InlineCompletionsInstance") == true)
                   .Value;

        /// <summary>
        /// ゴーストテキストを最大3行に制限し、
        /// 末尾に余分な閉じ括弧 '}' がある場合は削除した上で、
        /// 先頭行が条件分岐なら1ブロックステートメントのみ、
        /// それ以外は1ステートメントのみ抽出します。
        /// </summary>
        private static string TrimSuggestion(string text)
        {
            // 1. 行数を最大3行に制限
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            var limitedLines = lines.Take(3).ToList();

            // 2. 限定後に余分な閉じ括弧があれば取り除く
            int openCount = limitedLines.Sum(l => l.Count(c => c == '{'));
            int closeCount = limitedLines.Sum(l => l.Count(c => c == '}'));
            while (limitedLines.Count > 0 && closeCount > openCount)
            {
                var last = limitedLines.Last();
                int lastClose = last.Count(c => c == '}');
                // 行全体が単独の '}' または閉じ括弧が多い行なら削除
                if (last.Trim() == "}" || lastClose > last.Count(c => c == '{'))
                {
                    limitedLines.RemoveAt(limitedLines.Count - 1);
                    closeCount -= lastClose;
                }
                else
                {
                    break;
                }
            }

            var limitedText = string.Join("\n", limitedLines).TrimEnd();

            // 3. 先頭行が条件分岐(if/for/while/foreach/switch)の場合は1ブロックステートメントのみ
            var headerPattern = @"^\s*(if|for|while|do|foreach|switch)\b";
            string result;
            if (Regex.IsMatch(limitedText, headerPattern))
            {
                int braceIndex = limitedText.IndexOf('{');
                if (braceIndex >= 0)
                {
                    // 最初のブレース以降の最初のセミコロンまでを含める
                    int innerSemi = limitedText.IndexOf(';', braceIndex + 1);
                    result = innerSemi >= 0
                        ? limitedText.Substring(0, innerSemi + 1)
                        : limitedText;
                }
                else
                {
                    int semi = limitedText.IndexOf(';');
                    result = semi >= 0 ? limitedText.Substring(0, semi + 1) : limitedText;
                }
            }
            else
            {
                // 4. それ以外は最初のステートメントのみ
                int semi = limitedText.IndexOf(';');
                result = semi >= 0 ? limitedText.Substring(0, semi + 1) : limitedText;
            }

            // 5. 文字数上限 240 字に収める
            return result.Length > 240
                ? result.Substring(0, 240)
                : result;
        }

        // ctor 互換生成
        private static object CreateInlineSuggestion(object inlineInst, ProposalCollection proposals)
        {
            foreach (var ctor in _inlineCompSuggestionType!
                     .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                     .OrderByDescending(c => c.GetParameters().Length))
            {
                var ps = ctor.GetParameters();
                var hasIndex = ps.Length == 3;

                if (ps[0].ParameterType.IsAssignableFrom(typeof(ProposalCollection)))
                {
                    var args = hasIndex
                             ? new object[] { proposals, inlineInst, 0 }
                             : new object[] { proposals, inlineInst };
                    return ctor.Invoke(args);
                }
                if (ps[0].ParameterType.Name.Contains("GenerateResult"))
                {
                    var genRes = CreateGenerateResult(ps[0].ParameterType, proposals);
                    var args = hasIndex
                               ? new object[] { genRes, inlineInst, 0 }
                               : new object[] { genRes, inlineInst };
                    return ctor.Invoke(args);
                }
            }
            throw new InvalidOperationException("対応する InlineCompletionSuggestion ctor が見つかりません。");
        }

        private static object CreateGenerateResult(Type genResType, ProposalCollection proposals)
        {
            foreach (var ctor in genResType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                           .OrderByDescending(c => c.GetParameters()
                                                                    .Any(p => p.ParameterType.IsAssignableFrom(typeof(ProposalCollection)))))
            {
                var ps = ctor.GetParameters();
                var args = ps.Select(p =>
                         p.ParameterType.IsAssignableFrom(typeof(ProposalCollection)) ? proposals :
                         p.ParameterType == typeof(CancellationToken) ? CancellationToken.None :
                         p.ParameterType == typeof(bool) ? (object)false :
                         p.ParameterType.IsPrimitive || p.ParameterType.IsEnum ? Activator.CreateInstance(p.ParameterType)! :
                                                                                         null).ToArray();

                if (args.Any(a => a == null)) continue;

                try
                {
                    var genRes = ctor.Invoke(args);
                    ForceSetMember(genResType, genRes, proposals);
                    return genRes;
                }
                catch { /* 次へ */ }
            }

            var uninit = FormatterServices.GetUninitializedObject(genResType);
            ForceSetMember(genResType, uninit, proposals);
            return uninit;
        }

        private static void ForceSetMember(Type type, object target, object value)
        {
            foreach (var fld in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (fld.FieldType.IsInstanceOfType(value)) { fld.SetValue(target, value); return; }

            foreach (var name in new[] { "Proposals", "LeadProposals", "_proposals" })
            {
                var fld = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fld != null) { fld.SetValue(target, value); return; }

                var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop?.CanWrite == true) { prop.SetValue(target, value); return; }
            }
            Log($"[Suggestions] Proposals 注入に失敗: {type.Name}");
        }

        private static void Log(string msg)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is not IVsOutputWindow ow) return;
                var paneGuid = VSConstants.GUID_OutWindowGeneralPane;
                ow.GetPane(ref paneGuid, out var pane);
                pane?.OutputString(msg + Environment.NewLine);
            });
        }
    }
}
