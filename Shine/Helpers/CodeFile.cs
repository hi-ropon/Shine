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
                System.Diagnostics.Debug.WriteLine("DTE or Solution or Projects is null. Cannot get code files.");
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
                    System.Diagnostics.Debug.WriteLine($"COM Error processing project '{proj?.Name}': {comEx.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing project '{proj?.Name}': {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"COM Error getting ProjectItems from project '{project.Name}': {comEx.Message}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting ProjectItems from project '{project.Name}': {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"COM Error processing ProjectItem '{item?.Name}' in project '{project.Name}': {comEx.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing ProjectItem '{item?.Name}' in project '{project.Name}': {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"COM Error accessing ProjectItem properties for '{itemName ?? "unknown"}': {comEx.Message}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error accessing ProjectItem properties for '{itemName ?? "unknown"}': {ex.Message}");
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
                        System.Diagnostics.Debug.WriteLine($"COM Error processing sub ProjectItem '{subItem?.Name}': {comEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing sub ProjectItem '{subItem?.Name}': {ex.Message}");
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
                return extension == ".cs" || extension == ".vb" || extension == ".cpp" || extension == ".h";
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
