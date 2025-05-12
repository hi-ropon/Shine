// ファイル名: FileItem.cs
using Shine.Helpers;
using System.Windows.Media;

namespace Shine.Models
{
    public sealed class FileItem
    {
        public string Name { get; }
        public ImageSource Icon { get; }

        public FileItem(string path)
        {
            Name = System.IO.Path.GetFileName(path);
            Icon = IconHelper.GetIcon(path);
        }
    }
}
