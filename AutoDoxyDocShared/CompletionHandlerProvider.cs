using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace AutoDoxyDoc
{
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("C++ Triple Slash Completion Handler")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public class CompletionHandlerProvider : IVsTextViewCreationListener
    {
        [Import]
        public IVsEditorAdaptersFactoryService AdapterService = null;

        [Import]
        public ICompletionBroker CompletionBroker { get; set; }

        [Import]
        public SVsServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// Called when a new text view is created.
        /// </summary>
        /// <param name="textViewAdapter"></param>
        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            try
            {
                var configService = ServiceProvider.GetService(typeof(DoxygenConfigService)) as DoxygenConfigService;

                if (configService == null)
                {
                    return;
                }

                IWpfTextView textView = this.AdapterService.GetWpfTextView(textViewAdapter);
                if (textView == null)
                {
                    return;
                }

                Func<DoxygenCompletionCommandHandler> createCommandHandler = delegate()
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var dte = ServiceProvider.GetService(typeof(DTE)) as DTE;
                    return new DoxygenCompletionCommandHandler(textViewAdapter, textView, this, dte, configService);
                };

                textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
            }
            catch
            {
            }
        }
    }
}