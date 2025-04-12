using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace Shine
{
    [Guid("C2345678-90AB-CDEF-0123-456789ABCDEF")] // ※適宜 GUID を生成してください
    public class ChatToolWindow : ToolWindowPane
    {
        /// <summary>
        /// コンストラクター
        /// </summary>
        public ChatToolWindow() : base(null)
        {
            this.Caption = "Shine(Code Assistant Tool)";
            this.Content = new ChatToolWindowControl();
        }
    }
}
