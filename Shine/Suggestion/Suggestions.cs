using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Proposals;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Shine.Helpers;
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
                LogHelper.DebugLog("[Suggestions] IntelliCode 型が見つかりません。");
                return;
            }

            var inlineInst = FindInlineInstance(view);
            if (inlineInst == null)
            {
                LogHelper.DebugLog("[Suggestions] InlineCompletionsInstance が取得できません。");
                return;
            }

            // → ① フル提案テキストをプロパティに保存
            view.Properties["Shine.FullProposalText"] = ghostText;

            // ① ゴーストテキスト表示用に 3 行・240 文字へトリム
            var displayText = TrimSuggestion(ghostText, limitToThreeLines: true);
            if (string.IsNullOrEmpty(displayText))
            {
                LogHelper.DebugLog("[Suggestions] 空行または改行のみのため提案せず。");
                return;
            }

            // → ③ トリム済みゴーストテキストをプロパティに保存
            view.Properties["Shine.LastProposalText"] = displayText;

            var proposals = Proposals.Create(view, displayText, position);
            if (proposals.Proposals.Count == 0)
            {
                LogHelper.DebugLog("[Suggestions] Proposal を生成できませんでした。");
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
                LogHelper.DebugLog($"[Suggestions] InlineCompletionSuggestion 生成失敗: {ex.Message}");
                return;
            }

            // ③ SuggestionManager.TryDisplaySuggestionAsync 呼び出し
            var suggMgr = _suggestionMgrField?.GetValue(inlineInst);
            if (suggMgr == null || _tryDisplaySuggestionAsync == null)
            {
                LogHelper.DebugLog("[Suggestions] SuggestionManager が取得できません。");
                return;
            }

            var taskObj = _tryDisplaySuggestionAsync.Invoke(
                             suggMgr, new object[] { inlineSuggestion, CancellationToken.None });

            if (taskObj is not Task task)
            {
                LogHelper.DebugLog("[Suggestions] TryDisplaySuggestionAsync が Task を返しませんでした。");
                return;
            }

            await task.ConfigureAwait(false);

            // 17.10+ normally returns SuggestionSessionBase or null
            var session = task.GetType().GetProperty("Result")?.GetValue(task);
            if (session is null)
            {
                LogHelper.DebugLog("[Suggestions] IntelliCode が提案を表示しませんでした。(改行・重複・機能設定などが原因)");
                return;
            }

            manager.SetCurrentSession(session);

            view.Properties["Shine.LastProposalText"] = displayText;

            var prop = proposals.Proposals.First();
            await ((dynamic)session).DisplayProposalAsync(prop, CancellationToken.None);
        }

        // ────────── Helper ──────────
        private static object? FindInlineInstance(ITextView view)
            => view.Properties.PropertyList
                   .FirstOrDefault(kv => kv.Value?.GetType().Name.Contains("InlineCompletionsInstance") == true)
                   .Value;

        /* ================================================================
               TrimSuggestion(…) ― 3 行制限の有無をパラメータで切替
           ================================================================ */
        internal static string TrimSuggestion(string text) => TrimSuggestion(text, limitToThreeLines: true);

        /// <summary>
        /// ゴーストテキストを最大3行に制限するか選択し、
        /// １ステートメントまたは１ブロックステートメントのみを抽出します。
        /// if-else ブロックがあれば else 部分まで含めて返します。
        /// </summary>
        internal static string TrimSuggestion(string text, bool limitToThreeLines)
        {
            // 1. 改行コード統一→行リスト化
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

            // 1-b. 表示モードのときだけ 3 行に制限
            if (limitToThreeLines)
                lines = lines.Take(3).ToList();

            // 2. 末尾の余分な '}' を除去して括弧のバランスを調整
            int openCount = lines.Sum(l => l.Count(c => c == '{'));
            int closeCount = lines.Sum(l => l.Count(c => c == '}'));
            while (lines.Count > 0 && closeCount > openCount)
            {
                var last = lines[^1];
                int lastClose = last.Count(c => c == '}');
                if (last.Trim() == "}" || lastClose > last.Count(c => c == '{'))
                {
                    lines.RemoveAt(lines.Count - 1);
                    closeCount -= lastClose;
                }
                else break;
            }

            var limitedText = string.Join("\n", lines).TrimEnd();

            // 3. 先頭が if/for/… のヘッダーならブロック全体を、else-ifチェーンがあれば最後まで含める
            const string headerPattern = @"^\s*(if|for|while|do|foreach|switch)\b";
            string result;
            if (Regex.IsMatch(limitedText, headerPattern))
            {
                int braceStart = limitedText.IndexOf('{');
                if (braceStart >= 0)
                {
                    // 最初の if ブロックを閉じる '}' を探す
                    int depth = 0, firstEnd = -1;
                    for (int i = braceStart; i < limitedText.Length; i++)
                    {
                        if (limitedText[i] == '{') depth++;
                        else if (limitedText[i] == '}')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                firstEnd = i;
                                break;
                            }
                        }
                    }

                    if (firstEnd >= 0)
                    {
                        // else-if / else チェーンの終端を探す
                        int chainEnd = firstEnd;
                        int scanPos = firstEnd + 1;
                        while (scanPos < limitedText.Length)
                        {
                            // 先頭位置から "else" または "else if" を正規表現で検索
                            var match = Regex.Match(limitedText.Substring(scanPos), @"^\s*(else(\s+if)?)\b", RegexOptions.Multiline);
                            if (!match.Success) break;

                            int keywordPos = scanPos + match.Index;
                            // ブロック開始の '{' を探す
                            int bracePos = limitedText.IndexOf('{', keywordPos);
                            if (bracePos < 0)
                            {
                                // ブロックなしの else（珍しいパターン）は宣言末尾まで
                                chainEnd = keywordPos + match.Length;
                                scanPos = chainEnd;
                                continue;
                            }
                            // 対応する '}' を探す
                            depth = 0;
                            int thisEnd = -1;
                            for (int i = bracePos; i < limitedText.Length; i++)
                            {
                                if (limitedText[i] == '{') depth++;
                                else if (limitedText[i] == '}')
                                {
                                    depth--;
                                    if (depth == 0)
                                    {
                                        thisEnd = i;
                                        break;
                                    }
                                }
                            }
                            if (thisEnd < 0) break;

                            chainEnd = thisEnd;
                            scanPos = thisEnd + 1;
                        }

                        result = limitedText.Substring(0, chainEnd + 1);
                    }
                    else
                    {
                        // 括弧不整合なら全文返す
                        result = limitedText;
                    }
                }
                else
                {
                    // ブレース無しなら最初のセミコロンまで
                    var semi = limitedText.IndexOf(';');
                    result = semi >= 0 ? limitedText.Substring(0, semi + 1) : limitedText;
                }
            }
            else
            {
                // if 以外は最初のセミコロンまで
                var semi = limitedText.IndexOf(';');
                result = semi >= 0 ? limitedText.Substring(0, semi + 1) : limitedText;
            }

            // 4. 表示モードのときのみ 240 文字に収める
            if (limitToThreeLines && result.Length > 240)
                result = result.Substring(0, 240);

            return result;
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
            LogHelper.DebugLog($"[Suggestions] Proposals 注入に失敗: {type.Name}");
        }
    }
}
