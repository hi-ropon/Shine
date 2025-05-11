using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Shine.Helpers;

namespace Shine
{
    /// <summary>
    /// ソリューション内のコードファイルの探索処理（プロジェクト／プロジェクトアイテムの再帰処理や拡張子チェック）を担当
    /// </summary>
    public static class CodeFile
    {
        /// <summary>
        /// ソリューション内の全プロジェクト（ソリューションフォルダ内のプロジェクトも含む）を取得する
        /// </summary>
        /// <param name="dte">DTE2 インスタンス</param>
        /// <returns>全プロジェクトの IEnumerable</returns>
        private static IEnumerable<Project> GetAllProjects(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in dte.Solution.Projects)
            {
                foreach (Project p in GetProjects(project))
                {
                    yield return p;
                }
            }
        }

        /// <summary>
        /// プロジェクトがソリューションフォルダの場合はその中のプロジェクトも再帰的に取得する
        /// </summary>
        /// <param name="project">対象のプロジェクト</param>
        /// <returns>対象プロジェクトおよび入れ子のプロジェクト</returns>
        private static IEnumerable<Project> GetProjects(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null)
            {
                yield break;
            }
            // ソリューションフォルダの場合は、その中に入っている各プロジェクトを取得する
            if (project.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
            {
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        Project subProj = null;
                        try
                        {
                            subProj = item.SubProject;
                        }
                        catch (COMException)
                        {
                            // 例外が発生した場合は無視して次の項目へ
                        }
                        if (subProj != null)
                        {
                            foreach (Project p in GetProjects(subProj))
                            {
                                yield return p;
                            }
                        }
                    }
                }
            }
            else
            {
                yield return project;
            }
        }

        /// <summary>
        /// ソリューション内のコードファイルを取得する
        /// </summary>
        /// <returns>コードファイルのパス（またはファイル名）のリスト</returns>
        public static List<string> GetCodeFilesInSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var codeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            if (dte?.Solution == null)
            {
                ShinePackage.MessageService.OKOnly("DTE または ソリューションが null です。コードファイルを取得できません。");
                return new List<string>();
            }

            // ソリューション内の全プロジェクト（ネストしたプロジェクトも含む）を走査する
            foreach (var proj in GetAllProjects(dte))
            {
                LogHelper.DebugLog($"プロジェクト '{proj?.Name}' の処理中");
                try
                {
                    codeFiles.UnionWith(GetCodeFilesFromProject(proj));
                    LogHelper.DebugLog($"現在のファイル数： '{codeFiles.Count}'");
                }
                catch (COMException comEx)
                {
                    ShinePackage.MessageService.ShowError(comEx,$"プロジェクト '{proj?.Name}' の処理中に COM エラーが発生しました");
                }
                catch (Exception ex)
                {
                    ShinePackage.MessageService.ShowError(ex, $"プロジェクト '{proj?.Name}' の処理中にエラーが発生しました");
                }
            }

            return codeFiles.ToList();
        }

        /// <summary>
        /// プロジェクト内のコードファイルを取得する
        /// </summary>
        /// <param name="project">対象のプロジェクト</param>
        /// <returns>コードファイルのパス（またはファイル名）の IEnumerable</returns>
        public static IEnumerable<string> GetCodeFilesFromProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<string>();

            if (project == null)
                return result;

            ProjectItems items = null;

            try
            {
                items = project.ProjectItems;
            }
            catch (COMException comEx)
            {
                ShinePackage.MessageService.ShowError(comEx, $"プロジェクト '{project.Name}' の ProjectItems 取得中に COM エラーが発生しました");
                return result;
            }
            catch (Exception ex)
            {
                ShinePackage.MessageService.ShowError(ex, $"プロジェクト '{project.Name}' の処理中にエラーが発生しました");
                return result;
            }

            if (items == null)
                return result;

            foreach (ProjectItem item in items)
            {
                try
                {
                    result.AddRange(GetCodeFilesFromProjectItem(item));
                }
                catch (COMException comEx)
                {
                    ShinePackage.MessageService.ShowError(comEx, $"プロジェクト '{project.Name}' 内の ProjectItem '{item?.Name}' の処理中に COM エラーが発生しました");
                }
                catch (Exception ex)
                {
                    ShinePackage.MessageService.ShowError(ex, $"プロジェクト '{project.Name}' 内の ProjectItem '{item?.Name}' の処理中に COM エラーが発生しました");
                }
            }

            return result;
        }

        /// <summary>
        /// プロジェクトアイテム内のコードファイルを取得する
        /// </summary>
        /// <param name="item">対象の ProjectItem</param>
        /// <returns>コードファイルのパス（またはファイル名）の IEnumerable</returns>
        public static IEnumerable<string> GetCodeFilesFromProjectItem(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<string>();

            if (item == null)
                return result;

            string itemName = null;
            ProjectItems subItems = null;

            try
            {
                itemName = item.Name;
                // プロジェクトアイテム自身がコードファイルであれば追加
                if (IsCodeFile(itemName))
                {
                    result.Add(itemName);
                }

                subItems = item.ProjectItems;
            }
            catch (COMException comEx)
            {
                ShinePackage.MessageService.ShowError(comEx, $"ProjectItem プロパティ（'{itemName ?? "unknown"}' の処理中にエラーが発生しました");
                return result;
            }
            catch (Exception ex)
            {
                ShinePackage.MessageService.ShowError(ex, $"ProjectItem プロパティ（'{itemName ?? "unknown"}' の処理中にエラーが発生しました");
                return result;
            }

            if (subItems != null)
            {
                foreach (ProjectItem subItem in subItems)
                {
                    try
                    {
                        result.AddRange(GetCodeFilesFromProjectItem(subItem));
                    }
                    catch (COMException comEx)
                    {
                        ShinePackage.MessageService.ShowError(comEx, $"サブ ProjectItem '{subItem?.Name}' の処理中にエラーが発生しました");
                    }
                    catch (Exception ex)
                    {
                        ShinePackage.MessageService.ShowError(ex, $"サブ ProjectItem '{subItem?.Name}' の処理中にエラーが発生しました");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 指定されたファイル名がコードファイルかどうかを判定する
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <returns>コードファイルであれば true</returns>
        public static bool IsCodeFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            try
            {
                string extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                return extension == ".cs" ||
                       extension == ".c" ||
                       extension == ".vb" ||
                       extension == ".cpp" ||
                       extension == ".h" ||
                       extension == ".csv" ||
                       extension == ".xml" ||
                       extension == ".json";
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}