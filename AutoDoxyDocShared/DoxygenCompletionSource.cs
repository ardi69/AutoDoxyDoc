using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Completion source for Doxygen comments.
    /// </summary>
    public class DoxygenCompletionSource : ICompletionSource
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceProvider"></param>
        /// <param name="textBuffer"></param>
        public DoxygenCompletionSource(CompletionSourceProvider sourceProvider, ITextBuffer textBuffer, DoxygenConfigService configService)
        {
            m_sourceProvider = sourceProvider;
            m_textBuffer = textBuffer;
            m_configService = configService;
            CreateCompletionLists();
            m_configService.Config.ConfigChanged += onConfigChanged;
        }

        /// <summary>
        /// Augments a completion session.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="completionSets"></param>
        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                if (m_disposed)
                {
                    return;
                }

                SnapshotPoint? snapshotPoint = session.GetTriggerPoint(m_textBuffer.CurrentSnapshot);
                if (!snapshotPoint.HasValue)
                {
                    return;
                }

                string text = snapshotPoint.Value.GetContainingLine().GetText();
                if (m_textBuffer.ContentType.TypeName != DoxygenCompletionCommandHandler.CppTypeName)
                {
                    return;
                }

                if (!text.TrimStart().StartsWith("*"))
                {
                    return;
                }

                ITrackingSpan trackingSpan = FindTokenSpanAtPosition(session.GetTriggerPoint(m_textBuffer), session);

                // Check what kind of completion set we need to create.
                List<Completion> compList = null;
                var prevChar = snapshotPoint.Value.Subtract(1).GetChar();

                // Type direction tags.
                if (prevChar == '[')
                {
                    compList = m_compListDir;
                }
                // Generic doxygen tags.
                else if (prevChar == m_configService.Config.TagChar)
                {
                    compList = m_compListTag;
                }

                if (compList != null)
                {
                    var newCompletionSet = new CompletionSet(
                            "TripleSlashCompletionSet",
                            "TripleSlashCompletionSet",
                            trackingSpan,
                            compList,
                            Enumerable.Empty<Completion>());
                    completionSets.Add(newCompletionSet);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Disposes the completion source.
        /// </summary>
        public void Dispose()
        {
            if (!m_disposed)
            {
                m_configService.Config.ConfigChanged -= onConfigChanged;
                GC.SuppressFinalize(this);
                m_disposed = true;
            }
        }

        /// <summary>
        /// Find the token span at the given position.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
        {
            try
            {
                SnapshotPoint currentPoint = session.TextView.Caret.Position.BufferPosition - 1;
                return currentPoint.Snapshot.CreateTrackingSpan(currentPoint, 1, SpanTrackingMode.EdgeInclusive);
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Creates completion lists based on current Doxygen configuration.
        /// </summary>
        private void CreateCompletionLists()
        {
            ImageSource image = null;

            try
            {
                image = this.m_sourceProvider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphKeyword, StandardGlyphItem.GlyphItemPublic);
            }
            catch
            {
            }

            // Create tags list.
            m_compListTag.Clear();
            AddCompletionTag("code", image);
            AddCompletionTag("sa", image);
            AddCompletionTag("see", image);
            AddCompletionTag("include", image);
            AddCompletionTag("li", image);
            AddCompletionTag("param", image);
            AddCompletionTag("tparam", image);
            AddCompletionTag("brief", image);
            AddCompletionTag("throw", image);
            AddCompletionTag("return", image);
            AddCompletionTag("returns", image);
            AddCompletionTag("relates", image);
            AddCompletionTag("remarks", image);
            AddCompletionTag("throw", image);

            // Create directions list.
            m_compListDir.Add(new Completion("[in]", "[in]", string.Empty, image, string.Empty));
            m_compListDir.Add(new Completion("[in,out]", "[in,out]", string.Empty, image, string.Empty));
            m_compListDir.Add(new Completion("[out]", "[out]", string.Empty, image, string.Empty));
        }

        /// <summary>
        /// Adds a completion tag to the tag completion list.
        /// </summary>
        /// <param name="name">Name of the tag without the Doxygen tag character.</param>
        /// <param name="image">Image for the completion listbox.</param>
        private void AddCompletionTag(string name, ImageSource image)
        {
            string tag = m_configService.Config.TagChar + name;
            m_compListTag.Add(new Completion(tag, tag, string.Empty, image, string.Empty));
        }

        /// <summary>
        /// Called when the Doxygen configuration has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onConfigChanged(object sender, EventArgs e)
        {
            CreateCompletionLists();
        }

        //! Completion source provider.
        private CompletionSourceProvider m_sourceProvider;

        //! Text buffer.
        private ITextBuffer m_textBuffer;

        //! Doxygen configuration service.
        private DoxygenConfigService m_configService;

        // List of tag completions.
        private List<Completion> m_compListTag = new List<Completion>();

        // List of parameter direction completions.
        private List<Completion> m_compListDir = new List<Completion>();

        //! If true, the class is disposed.
        private bool m_disposed;
    }
}