using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Shine
{
    /// <summary>
    /// GitDiffHelper クラスは、Git リポジトリの差分を取得するためのヘルパークラス
    /// </summary>
    public static class GitDiffHelper
    {
        /// <summary>
        /// 指定された作業ディレクトリで git diff --cached コマンドを実行して、その結果を返す
        /// （※git add 済みの変更点を取得します）
        /// </summary>
        /// <param name="workingDirectory">Git リポジトリのルートパス（作業ディレクトリ）</param>
        /// <returns>git diff の結果（差分テキスト）。差分が無い場合は空文字列。</returns>
        public static string GetGitDiff(string workingDirectory)
        {
            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException("作業ディレクトリが見つかりません: " + workingDirectory);
            }

            // ステージ済みの変更（git add 済み）の差分を取得するため、--cached オプションを使用
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --cached",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // エラー出力のチェック（必要に応じてログ出力）
                string error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error))
                {
                    ShinePackage.MessageService.OKOnly("Git diff error: " + error);
                }
                return output;
            }
        }
    }
}