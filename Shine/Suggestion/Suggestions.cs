// ────────────────────────────────────────────────
//  ファイル名: Suggestions.cs
//  説明: IntelliCode (VS 17.10+) の内部 API をリフレクションで呼び出し、
//       ゴーストテキスト（インライン補完）を表示するユーティリティ。
//       ※ 17.9 以前の互換処理は削除済み
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
        public static async Task ShowAsync(ITextView view, string ghostText, int position)
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

            // ① ProposalCollection 生成（表示拒否を避けるため 3 行・240 文字にトリム）
            ghostText = TrimSuggestion(ghostText);
            if (string.IsNullOrEmpty(ghostText))
            {
                Log("[Suggestions] 空行または改行のみのため提案せず。");
                return;
            }

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
                Log("[Suggestions] IntelliCode が提案を表示しませんでした。"
                    +"(改行・重複・機能設定などが原因)");
                return;
            }

            var prop = proposals.Proposals.First();
            await ((dynamic)session).DisplayProposalAsync(prop, CancellationToken.None);
        }

        // ────────── Helper ──────────
        private static object? FindInlineInstance(ITextView view)
            => view.Properties.PropertyList
                   .FirstOrDefault(kv => kv.Value?.GetType().Name.Contains("InlineCompletionsInstance") == true)
                   .Value;

        private static string TrimSuggestion(string text)
        {
            var first = text.Split('\n').FirstOrDefault()?.TrimEnd() ?? "";
            return first.Length > 80 ? first.Substring(0, 80) : first;
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

        private static async Task<bool> GetDisplayedAsync(object taskObj)
        {
            if (taskObj is not Task task) return false;
            await task.ConfigureAwait(false);

            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            return result switch
            {
                bool b => b,
                null => false,
                _ => (bool)(result.GetType().GetProperty("IsDisplayed")?.GetValue(result) ?? false)
            };
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
