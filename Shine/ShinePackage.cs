using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Shine;

namespace Shine
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Code Assistant Tool", "Visual Studio AI Code Assistant Extension", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ChatToolWindow))]
    [ProvideOptionPage(typeof(AiAssistantOptions), "Shine(Code Assistant Tool)", "General", 0, 0, true)]
    [Guid("B1234567-89AB-CDEF-0123-456789ABCDEF")] // ※適宜 GUID を生成してください
    public sealed class ShinePackage : AsyncPackage
    {
        public static ShinePackage Instance { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // UIスレッドが必要な初期化は、このメソッドの中で行います
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Instance = this;

            // ツールウィンドウを表示するコマンドを初期化
            await ShowAiChatCommand.InitializeAsync(this);

            // 必要に応じて各モジュールの初期化を実施
            await base.InitializeAsync(cancellationToken, progress);
        }
    }
}
