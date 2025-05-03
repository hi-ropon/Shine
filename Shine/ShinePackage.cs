using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Shine
{
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideBindingPath]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Code Assistant Tool", "Visual Studio AI Code Assistant Extension", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ChatToolWindow))]
    [ProvideOptionPage(typeof(AiAssistantOptions), "Shine(Code Assistant Tool)", "General", 0, 0, true)]
    [Guid("B1234567-89AB-CDEF-0123-456789ABCDEF")] // ※適宜 GUID を生成してください
    public sealed class ShinePackage : AsyncPackage
    {
        /// <summary>
        /// ShinePackage のインスタンスを取得します
        /// </summary>
        public static ShinePackage Instance { get; private set; }

        /// <summary>
        /// このメソッドは、パッケージの初期化を行います
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
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
