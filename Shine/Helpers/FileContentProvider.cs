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
                    System.Diagnostics.Debug.WriteLine($"IO Error reading file '{filePath}': {ioEx.Message}");
                    return null;
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Access Error reading file '{filePath}': {uaEx.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected Error reading file '{filePath}': {ex}");
                    return null;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"File not found for '{fileName}'. Searched path: '{filePath}'");
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
                System.Diagnostics.Debug.WriteLine($"COM error iterating projects in FindFileInProject: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error iterating projects in FindFileInProject: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"COM error iterating ProjectItems: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error iterating ProjectItems: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"Warning: Found relative path '{filePath}' for item '{item.Name}'.");
                            return filePath;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        System.Diagnostics.Debug.WriteLine($"FileNames[1] not available for item '{item.Name}'.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting FileNames for item '{item.Name}': {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"COM error processing ProjectItem '{item?.Name}': {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing ProjectItem '{item?.Name}': {ex.Message}");
            }
            return null;
        }
    }
}