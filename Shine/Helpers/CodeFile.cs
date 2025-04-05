using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Shine
{
    /// <summary>
    ///  ソリューション内のコードファイルの探索処理（プロジェクト／プロジェクトアイテムの再帰処理や拡張子チェック）を担当
    /// </summary>
    public static class CodeFile
    {
        public static List<string> GetCodeFilesInSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var codeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            if (dte?.Solution?.Projects == null)
            {
                System.Diagnostics.Debug.WriteLine("DTE または ソリューションまたはプロジェクトが null です。コードファイルを取得できません。");
                return new List<string>();
            }

            foreach (Project proj in dte.Solution.Projects)
            {
                try
                {
                    codeFiles.UnionWith(GetCodeFilesFromProject(proj));
                }
                catch (COMException comEx)
                {
                    System.Diagnostics.Debug.WriteLine($"プロジェクト '{proj?.Name}' の処理中に COM エラーが発生しました: {comEx.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プロジェクト '{proj?.Name}' の処理中にエラーが発生しました: {ex.Message}");
                }
            }

            return codeFiles.ToList();
        }

        public static IEnumerable<string> GetCodeFilesFromProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<string>();

            if (project == null) return result;

            ProjectItems items = null;

            try
            {
                items = project.ProjectItems;
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"プロジェクト '{project.Name}' の ProjectItems 取得中に COM エラーが発生しました: {comEx.Message}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プロジェクト '{project.Name}' の ProjectItems 取得中にエラーが発生しました: {ex.Message}");
                return result;
            }

            if (items == null) return result;

            foreach (ProjectItem item in items)
            {
                try
                {
                    result.AddRange(GetCodeFilesFromProjectItem(item));
                }
                catch (COMException comEx)
                {
                    System.Diagnostics.Debug.WriteLine($"プロジェクト '{project.Name}' 内の ProjectItem '{item?.Name}' の処理中に COM エラーが発生しました: {comEx.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プロジェクト '{project.Name}' 内の ProjectItem '{item?.Name}' の処理中にエラーが発生しました: {ex.Message}");
                }
            }

            return result;
        }

        public static IEnumerable<string> GetCodeFilesFromProjectItem(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<string>();

            if (item == null) return result;

            string itemName = null;
            ProjectItems subItems = null;

            try
            {
                itemName = item.Name;

                if (IsCodeFile(itemName))
                {
                    result.Add(itemName);
                }
                subItems = item.ProjectItems;
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItem プロパティ（'{itemName ?? "unknown"}'）へのアクセス中に COM エラーが発生しました: {comEx.Message}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItem プロパティ（'{itemName ?? "unknown"}'）へのアクセス中にエラーが発生しました: {ex.Message}");
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
                        System.Diagnostics.Debug.WriteLine($"サブ ProjectItem '{subItem?.Name}' の処理中に COM エラーが発生しました: {comEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"サブ ProjectItem '{subItem?.Name}' の処理中にエラーが発生しました: {ex.Message}");
                    }
                }
            }

            return result;
        }

        public static bool IsCodeFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            try
            {
                string extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                return extension == ".cs" ||
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
