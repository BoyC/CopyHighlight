using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace SelectionCopyFlash
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(CopySelectionFlashCommandHandler))]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [ContentType("text")]
    [ContentType("Output")]
    [ContentType("BuildOutput")]
    [ContentType("BuildOrderOutput")]
    [ContentType("DebugOutput")]
    [ContentType("TestsOutput")]
    internal sealed class CopySelectionFlashCommandHandler : IChainedCommandHandler<CopyCommandArgs>
    {
        private readonly IEditorFormatMapService _formatMapService;

        [ImportingConstructor]
        public CopySelectionFlashCommandHandler(IEditorFormatMapService formatMapService)
        {
            _formatMapService = formatMapService ?? throw new ArgumentNullException(nameof(formatMapService));
        }

        public string DisplayName => "Selection Copy Flash";

        public CommandState GetCommandState(CopyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(CopyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = args?.TextView as IWpfTextView;
            if (view == null)
            {
                nextCommandHandler();
                return;
            }

            var shouldFlash = !view.Selection.IsEmpty;

            nextCommandHandler();

            if (!shouldFlash || view.Selection.IsEmpty)
            {
                return;
            }

            var manager = view.Properties.GetOrCreateSingletonProperty(
                () => new SelectionFlashAdornmentManager(view, _formatMapService));

            manager.Flash();
        }
    }
}
