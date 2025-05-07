// ────────────────────────────────────────────────────────
//  ファイル名: Suggestions.cs
//  説明    : Shine のゴーストテキスト（インライン提案）描画ヘルパ
//             IntelliCode の内部 API を Reflection で叩き、
//             3 行・240 文字の制限を完全に解除して全文表示する。
// ────────────────────────────────────────────────────────
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
    /// <summary>
    /// IntelliCode の非公開クラスを使ってフルテキストをゴースト表示するヘルパ
    /// </summary>
    internal static class Suggestions
    {
        // ───────────────────────── 反射キャッシュ ─────────────────────────
        private static readonly Assembly? _icAsm;
        private static readonly Type? _inlineCompInstanceType;
        private static readonly Type? _inlineCompSuggestionType;
        private static readonly FieldInfo? _suggestionMgrField;
        private static readonly MethodInfo? _tryDisplaySuggestionAsync;

        static Suggestions()
        {
            // ① IntelliCode アセンブリをロード
            _icAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Microsoft.VisualStudio.IntelliCode")
                     ?? Assembly.Load("Microsoft.VisualStudio.IntelliCode");

            if (_icAsm == null) return;

            // ② 必要型を解決
            _inlineCompInstanceType = _icAsm.GetTypes().FirstOrDefault(t => t.Name.EndsWith("InlineCompletionsInstance"));
            _inlineCompSuggestionType = _icAsm.GetTypes().FirstOrDefault(t => t.Name.EndsWith("InlineCompletionSuggestion"));
            if (_inlineCompInstanceType == null || _inlineCompSuggestionType == null) return;

            // ③ Manager フィールドと表示メソッド
            _suggestionMgrField = _inlineCompInstanceType.GetField("_suggestionManager",
                                   BindingFlags.Instance | BindingFlags.NonPublic)
                               ?? _inlineCompInstanceType.GetField("SuggestionManager",
                                   BindingFlags.Instance | BindingFlags.NonPublic);

            if (_suggestionMgrField != null)
            {
                var mgrType = _suggestionMgrField.FieldType;
                _tryDisplaySuggestionAsync = mgrType.GetMethod("TryDisplaySuggestionAsync",
                                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // ───────────────────────── Public API ─────────────────────────
        /// <summary>
        /// フルテキストのゴースト提案を表示
        /// </summary>
        public static async Task ShowAsync(ITextView view,
                                           InlineSuggestionManager manager,
                                           string ghostText,
                                           int position)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // IntelliCode がロードできなければ諦める
            if (_inlineCompInstanceType == null || _inlineCompSuggestionType == null)
            {
                LogHelper.DebugLog("[Suggestions] IntelliCode 型が取得できません。");
                return;
            }

            // ① TextView の InlineCompletionsInstance を取得
            var inlineInst = view.Properties.PropertyList
                                 .FirstOrDefault(kv => kv.Value?.GetType().Name
                                                      .Contains("InlineCompletionsInstance") == true).Value;
            if (inlineInst == null)
            {
                LogHelper.DebugLog("[Suggestions] InlineCompletionsInstance がありません。");
                return;
            }

            // ② プロパティに全文を保存（Tab フォールバックで使用）
            view.Properties["Shine.FullProposalText"] = ghostText;

            // ③ 表示用文字列 ― 今回は **制限なし**（3行/240字カットを行わない）
            string displayText = TrimSuggestion(ghostText, limitToThreeLines: false);
            if (string.IsNullOrWhiteSpace(displayText))
            {
                LogHelper.DebugLog("[Suggestions] 空の提案は表示しません。");
                return;
            }
            view.Properties["Shine.LastProposalText"] = displayText;

            // ④ ProposalCollection を生成
            var proposals = Proposals.Create(view, displayText, position);
            if (proposals.Proposals.Count == 0)
            {
                LogHelper.DebugLog("[Suggestions] Proposal 作成に失敗しました。");
                return;
            }

            // ⑤ InlineCompletionSuggestion を生成
            object inlineSuggestion;
            try
            {
                inlineSuggestion = CreateInlineSuggestion(inlineInst, proposals);
            }
            catch (Exception ex)
            {
                LogHelper.DebugLog($"[Suggestions] Suggestion 作成失敗: {ex.Message}");
                return;
            }

            // ⑥ SuggestionManager.TryDisplaySuggestionAsync を呼び出し
            var suggMgr = _suggestionMgrField?.GetValue(inlineInst);
            if (suggMgr == null || _tryDisplaySuggestionAsync == null)
            {
                LogHelper.DebugLog("[Suggestions] SuggestionManager 取得失敗。");
                return;
            }

            var taskObj = _tryDisplaySuggestionAsync.Invoke(
                              suggMgr, new object[] { inlineSuggestion, CancellationToken.None });

            if (taskObj is not Task task)
            {
                LogHelper.DebugLog("[Suggestions] TryDisplaySuggestionAsync の戻り値が Task ではありません。");
                return;
            }

            await task.ConfigureAwait(false);

            // TryDisplaySuggestionAsync は Result プロパティに SuggestionSessionBase を返す
            var session = task.GetType().GetProperty("Result")?.GetValue(task);
            if (session == null)
            {
                LogHelper.DebugLog("[Suggestions] IntelliCode が提案を描画しませんでした。");
                return;
            }

            // セッションを Manager に渡す（Enter/Tab で確定させるため）
            manager.SetCurrentSession(session);

            // 実際にゴーストを描画
            var prop = proposals.Proposals.First();
            await ((dynamic)session).DisplayProposalAsync(prop, CancellationToken.None);
        }

        // ───────────────────────── ユーティリティ ─────────────────────────
        /// <summary>
        /// ゴーストテキストを整形する。  
        /// limitToThreeLines==false のときは行数/文字数をカットしない。  
        /// if/else‑if/else 連鎖は “else” が途切れるまで全部含める。
        /// </summary>
        internal static string TrimSuggestion(string text, bool limitToThreeLines)
        {
            // 1. 改行を統一して行分割
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (limitToThreeLines)
                lines = lines.Take(3).ToList();

            string joined = string.Join("\n", lines).TrimEnd();

            // 2. if/for/while/foreach/switch のヘッダー検出
            const string hdr = @"^\s*(if|for|while|foreach|switch)\b";
            bool isHeader = Regex.IsMatch(joined, hdr);

            // ── フル表示モードなら if 連鎖をまるごと取得
            if (!limitToThreeLines && isHeader)
            {
                int p = 0, len = joined.Length, depth = 0, chainEnd = -1;

                // ① 最初のブロックを走査して閉じ '}' の位置を得る
                for (; p < len; p++)
                {
                    char c = joined[p];
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            chainEnd = p + 1;     // '}' 直後
                            p++;                  // 次から else 検索
                            break;
                        }
                    }
                }

                // ② 連続する else / else if をすべて拾う
                while (chainEnd > 0)
                {
                    // 空白・改行をスキップ
                    while (p < len && char.IsWhiteSpace(joined[p])) p++;

                    if (p >= len || !joined.Substring(p).StartsWith("else"))
                        break;  // 連鎖終了

                    // 「else」トークンを読む
                    int kwStart = p;
                    p += 4; // 'else'
                    while (p < len && char.IsWhiteSpace(joined[p])) p++;

                    // else if … の場合は if ブロックを読む
                    if (p < len && joined.Substring(p).StartsWith("if"))
                    {
                        // 'if' ヘッダーを飛ばし次のブロックへ
                        p += 2;
                        // ヘッダー後のブレースまで送る
                        for (; p < len && joined[p] != '{'; p++) { }
                    }

                    // この時点で joined[p] は '{' であることを期待
                    if (p >= len || joined[p] != '{')
                    {
                        // ブレースが無い (単一文) ケース: 次のセミコロンまで
                        int semi = joined.IndexOf(';', p);
                        chainEnd = (semi >= 0) ? semi + 1 : len;
                        p = chainEnd;
                        continue;
                    }

                    // ブロック波括弧をバランスカウント
                    depth = 0;
                    for (; p < len; p++)
                    {
                        char c = joined[p];
                        if (c == '{') depth++;
                        else if (c == '}')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                chainEnd = p + 1;   // このブロック終端
                                p++;                // 次の else 検索開始
                                break;
                            }
                        }
                    }
                }

                // ③ 連鎖終端までを返却
                if (chainEnd > 0)
                    joined = joined[..chainEnd].TrimEnd();
            }

            // 3. 3行モードのみ 240 文字制限
            if (limitToThreeLines && joined.Length > 240)
                joined = joined[..240];

            return joined;
        }

        /// <summary>InlineCompletionSuggestion インスタンス生成</summary>
        private static object CreateInlineSuggestion(object inlineInst,
                                                     ProposalCollection proposals)
        {
            foreach (var ctor in _inlineCompSuggestionType!
                     .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                     .OrderByDescending(c => c.GetParameters().Length))
            {
                var ps = ctor.GetParameters();
                bool hasIndex = ps.Length == 3;

                if (ps[0].ParameterType.IsAssignableFrom(typeof(ProposalCollection)))
                {
                    var args = hasIndex
                             ? new object[] { proposals, inlineInst, 0 }
                             : new object[] { proposals, inlineInst };
                    return ctor.Invoke(args);
                }
                else if (ps[0].ParameterType.Name.Contains("GenerateResult"))
                {
                    var genRes = CreateGenerateResult(ps[0].ParameterType, proposals);
                    var args = hasIndex
                             ? new object[] { genRes, inlineInst, 0 }
                             : new object[] { genRes, inlineInst };
                    return ctor.Invoke(args);
                }
            }
            throw new InvalidOperationException("InlineCompletionSuggestion の ctor が見つかりません。");
        }

        /// <summary>GenerateResult を強引に生成</summary>
        private static object CreateGenerateResult(Type genResType,
                                                   ProposalCollection proposals)
        {
            // 最初に ctor で行けるか試行
            foreach (var ctor in genResType.GetConstructors(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var ps = ctor.GetParameters();
                var args = ps.Select(p =>
                         p.ParameterType.IsAssignableFrom(typeof(ProposalCollection)) ? proposals :
                         p.ParameterType == typeof(CancellationToken) ? CancellationToken.None :
                         p.ParameterType == typeof(bool) ? (object)false :
                         p.ParameterType.IsPrimitive || p.ParameterType.IsEnum ? Activator.CreateInstance(p.ParameterType)! :
                                                                                       null)
                         .ToArray();
                if (args.Any(a => a is null)) continue;

                try
                {
                    var obj = ctor.Invoke(args);
                    ForceSetProposals(genResType, obj, proposals);
                    return obj;
                }
                catch { /* ctor 不一致 → 次へ */ }
            }

            // どうしても作れない場合は uninitialized + 値セット
            var uninit = FormatterServices.GetUninitializedObject(genResType);
            ForceSetProposals(genResType, uninit, proposals);
            return uninit;
        }

        private static void ForceSetProposals(Type type, object target, ProposalCollection proposals)
        {
            // フィールド優先
            foreach (var fld in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (fld.FieldType.IsAssignableFrom(typeof(ProposalCollection)))
                { fld.SetValue(target, proposals); return; }

            // プロパティ fallback
            var prop = type.GetProperty("Proposals",
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.CanWrite == true)
                prop.SetValue(target, proposals);
        }
    }
}
