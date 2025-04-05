using System;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Shine
{
    /// <summary>
    /// 指定されたファイル名に対応するファイルパスの探索およびファイル内容の読み込みを担当
    /// </summary>
    public class FileContentProvider
    {
        public string GetFileContent(string fileName)
        {
            // UIスレッドでなければUIスレッドに切り替える
            if (!ThreadHelper.CheckAccess())
            {
                return ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    return GetFileContent(fileName);
                });
            }

            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            if (dte?.Solution == null) return null;

            string filePath = FindFileInProject(dte.Solution, fileName);

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    return File.ReadAllText(filePath);
                }
                catch (IOException ioEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ファイル '{filePath}' の読み込み中にIOエラーが発生しました: {ioEx.Message}");
                    return null;
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ファイル '{filePath}' の読み込み中にアクセスエラーが発生しました: {uaEx.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ファイル '{filePath}' の読み込み中に予期しないエラーが発生しました: {ex}");
                    return null;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ファイル '{fileName}' が見つかりません。検索したパス: '{filePath}'");
                return null;
            }
        }

        public string FindFileInProject(Solution solution, string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (solution?.Projects == null) return null;

            try
            {
                foreach (Project project in solution.Projects)
                {
                    string path = FindFileInProjectItems(project?.ProjectItems, fileName);

                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"FindFileInProjectでプロジェクトを反復処理中にCOMエラーが発生しました: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindFileInProjectでプロジェクトを反復処理中にエラーが発生しました: {ex.Message}");
            }
            return null;
        }

        private string FindFileInProjectItems(ProjectItems items, string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (items == null) return null;

            try
            {
                foreach (ProjectItem item in items)
                {
                    string path = GetFilePathFromProjectItemRecursive(item, fileName);

                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItemsの反復処理中にCOMエラーが発生しました: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItemsの反復処理中にエラーが発生しました: {ex.Message}");
            }
            return null;
        }

        private string GetFilePathFromProjectItemRecursive(ProjectItem item, string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (item == null) return null;

            try
            {
                short fileCount = 0;
                try { fileCount = item.FileCount; } catch { }

                if (fileCount > 0 && string.Equals(item.Name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string filePath = item.FileNames[1];

                        if (Path.IsPathRooted(filePath))
                        {
                            return filePath;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"警告: アイテム '{item.Name}' に相対パス '{filePath}' が見つかりました。");
                            return filePath;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        System.Diagnostics.Debug.WriteLine($"アイテム '{item.Name}' のFileNames[1]が利用できません。");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"アイテム '{item.Name}' のFileNames取得中にエラーが発生しました: {ex.Message}");
                        return null;
                    }
                }

                string pathInSubItems = FindFileInProjectItems(item.ProjectItems, fileName);

                if (!string.IsNullOrEmpty(pathInSubItems))
                {
                    return pathInSubItems;
                }

                if (item.SubProject != null)
                {
                    string pathInSubProject = FindFileInProjectItems(item.SubProject.ProjectItems, fileName);
                    if (!string.IsNullOrEmpty(pathInSubProject))
                    {
                        return pathInSubProject;
                    }
                }
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItem '{item?.Name}' の処理中にCOMエラーが発生しました: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProjectItem '{item?.Name}' の処理中にエラーが発生しました: {ex.Message}");
            }
            return null;
        }
    }
}