using System;
using System.IO;
using System.Reflection;

namespace Shine
{
    /// <summary>
    /// リソースに関するヘルパークラス
    /// </summary>
    public static class ResourceHelper
    {
        /// <summary>
        /// 指定したリソースパスからアイコンを読み込み、Base64 文字列に変換して返す
        /// </summary>
        public static string LoadResourceAsBase64(string resourcePath, Type typeInAssembly)
        {
            try
            {
                var assembly = typeInAssembly.Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (stream == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch(Exception ex)
            {
                ShinePackage.MessageService.ShowError(ex);
                return null;
            }
        }
    }
}