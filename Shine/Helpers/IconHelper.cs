// ファイル名: IconHelper.cs
using System;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Shine.Helpers
{
    public static class IconHelper
    {
        /// <summary>
        /// ファイル拡張子に応じた Visual Studio ネイティブ アイコンを取得します。
        /// </summary>
        /// <param name="filePath">取得したいファイルのパス</param>
        /// <param name="size">論理サイズ（px）</param>
        /// <param name="dpi">DPI</param>
        public static BitmapSource? GetIcon(string filePath, int size = 16, int dpi = 96)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var svc = Package.GetGlobalService(typeof(SVsImageService)) as IVsImageService2;
            if (svc == null || string.IsNullOrEmpty(filePath))
                return null;

            ImageMoniker moniker = svc.GetImageMonikerForFile(filePath);
            if (moniker.Id == 0)    // 未知の拡張子など
                return null;

            var attrs = new ImageAttributes
            {
                StructSize = Marshal.SizeOf<ImageAttributes>(),
                Flags = (uint)_ImageAttributesFlags.IAF_RequiredFlags,
                ImageType = (uint)_UIImageType.IT_Bitmap,
                Format = (uint)_UIDataFormat.DF_WPF,
                LogicalWidth = size,
                LogicalHeight = size,
                Dpi = dpi
            };

            IVsUIObject uiObj = svc.GetImage(moniker, attrs);
            uiObj.get_Data(out object data);

            return data as BitmapSource;
        }
    }
}
