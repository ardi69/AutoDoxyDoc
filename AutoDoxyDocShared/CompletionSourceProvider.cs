using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AutoDoxyDoc
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("Code")]
    [Name("Doxygen tag completion")]
    public class CompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal IGlyphService GlyphService { get; set; }

        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// Tries to create a Doxygen completion source.
        /// </summary>
        /// <param name="textBuffer"></param>
        /// <returns></returns>
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            var configService = ServiceProvider.GetService(typeof(DoxygenConfigService)) as DoxygenConfigService;

            if (configService == null)
            {
                return null;
            }

            return new DoxygenCompletionSource(this, textBuffer, configService);
        }
    }
}