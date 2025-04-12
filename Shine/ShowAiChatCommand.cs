using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace Shine
{
    /// <summary>
    /// Command handler
    /// </summary>
    public sealed class ShowAiChatCommand
    {
        public const int commandId = 0x0100;
        public static readonly Guid commandSet = new Guid("D1234567-89AB-CDEF-0123-456789ABCDEF");
        private readonly AsyncPackage _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShowAiChatCommand"/> class
        /// </summary>
        /// <param name="package"></param>
        /// <param name="commandService"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private ShowAiChatCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            this._package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(commandSet, commandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Initializes the singleton instance of the command
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            IMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            new ShowAiChatCommand(package, commandService);
        }

        /// <summary>
        /// Executes the command when the menu item is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotSupportedException"></exception>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ToolWindowPane window = _package.FindToolWindow(typeof(ChatToolWindow), 0, true);
            if ((window == null) || (window.Frame == null))
            {
                throw new NotSupportedException("Cannot create Code Assistant Tool Window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
