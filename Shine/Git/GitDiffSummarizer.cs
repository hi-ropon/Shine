using System.Text;
using System.Threading.Tasks;

namespace Shine
{
    /// <summary>
    /// Git diff のテキストを AI（gpt-4o-mini）を用いて要約するクラスです。
    /// </summary>
    public class GitDiffSummarizer
    {
        private readonly IChatClientService _chatClientService;
        private readonly string _workingDirectory;

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="chatClientService">OpenAI/AzureOpenAI クライアントサービス</param>
        /// <param name="workingDirectory">Git リポジトリのルートパス</param>
        public GitDiffSummarizer(IChatClientService chatClientService, string workingDirectory)
        {
            _chatClientService = chatClientService;
            _workingDirectory = workingDirectory;
        }

        /// <summary>
        /// Git diff を取得し、AI による要約を取得する
        /// </summary>
        /// <returns>要約テキスト。差分が無い場合はその旨のメッセージ。</returns>
        public async Task<string> SummarizeDiffAsync()
        {
            // 1. git diff を取得
            string diffText = GitDiffHelper.GetGitDiff(_workingDirectory);
            if (string.IsNullOrWhiteSpace(diffText))
            {
                return "変更された差分はありません。";
            }

            // 2. 要約依頼用のプロンプトを組み立てる
            StringBuilder promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("次のGit diffの内容からコミットメッセージを日本語で作成してください。");
            promptBuilder.AppendLine("コミットメッセージのフォーマットは次の通りです");
            promptBuilder.AppendLine("<type>[optional scope]: <subject>");
            promptBuilder.AppendLine("[optional body]");
            promptBuilder.AppendLine("[optional footer]");
            promptBuilder.AppendLine("---------");
            promptBuilder.AppendLine(diffText);
            promptBuilder.AppendLine("---------");

            string prompt = promptBuilder.ToString();

            // 3. AI へ要約依頼（gpt-4o-miniモデルでの回答を想定）
            string summary = await _chatClientService.GetChatResponseAsync(prompt);
            return summary;
        }
    }
}
