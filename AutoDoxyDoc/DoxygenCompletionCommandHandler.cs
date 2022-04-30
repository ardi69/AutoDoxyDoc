using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Command handler for doxygen completions.
    /// </summary>
    public class DoxygenCompletionCommandHandler : IOleCommandTarget
    {
        public const string CppTypeName = "C/C++";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="textViewAdapter"></param>
        /// <param name="textView"></param>
        /// <param name="provider"></param>
        /// <param name="dte"></param>
        public DoxygenCompletionCommandHandler(IVsTextView textViewAdapter, IWpfTextView textView, CompletionHandlerProvider provider, DTE dte, DoxygenConfigService configService)
        {
			m_textView = textView;
            m_provider = provider;
            m_dte = dte;
            m_configService = configService;

            // Add the command to the command chain.
            if (textViewAdapter != null &&
                textView != null &&
                textView.TextBuffer != null &&
                textView.TextBuffer.ContentType.TypeName == CppTypeName)
            {
                textViewAdapter.AddCommandFilter(this, out m_nextCommandHandler);
            }

            m_generator = new DoxygenGenerator(m_configService);

            m_configService.Config.ConfigChanged += onDoxygenConfigChanged;
            onDoxygenConfigChanged(this, EventArgs.Empty);
        }

        ~DoxygenCompletionCommandHandler()
        {
            m_configService.Config.ConfigChanged -= onDoxygenConfigChanged;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return m_nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        /// <summary>
        /// Executes the command handler.
        /// </summary>
        /// <param name="pguidCmdGroup"></param>
        /// <param name="nCmdID"></param>
        /// <param name="nCmdexecopt"></param>
        /// <param name="pvaIn"></param>
        /// <param name="pvaOut"></param>
        /// <returns></returns>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider))
                {
                    return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                uint commandID = nCmdID;
                char typedChar = char.MinValue;

                // Make sure the input is a char before getting it.
                if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
                {
                    typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                }


                if (CheckForCommentTrigger(typedChar))
                {
                    // Check the indentation level at this point.
                    GenerateComment();
                    return VSConstants.S_OK;
                }

                // Check if an auto-completion session is ongoing.
                if (IsAutoCompletionActive())
                {
                    if (TryEndCompletion(typedChar, nCmdID))
                    {
                        return VSConstants.S_OK;
                    }
                }
                else
                {
                    // Add asterisk for comments every time Enter is pressed.
                    if (pguidCmdGroup == VSConstants.VSStd2K)
                    {
                        if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN)
                        {
                            string currentLine = m_textView.TextSnapshot.GetLineFromPosition(
                                    m_textView.Caret.Position.BufferPosition.Position).GetText();

                            int indent = 0;
                            if (IsInsideDoxygenCommentBlock(currentLine, out indent))
                            {
                                NewCommentLine(currentLine, indent);
                                return VSConstants.S_OK;
                            }
                        }
                        else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
                        {
                            if (TrySmartIndent())
                            {
                                return VSConstants.S_OK;
                            }
                        }
                    }
                }

                // Pass along the command so the char is added to the buffer.
                int retVal = m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                // Start auto-completion session for doxygen tags and parameter names.
                if (!IsAutoCompletionActive() && (typedChar == m_configService.Config.TagChar || typedChar == '['))
                {
                    string currentLine = m_textView.TextSnapshot.GetLineFromPosition(
                                m_textView.Caret.Position.BufferPosition.Position).GetText();
                    TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
                    string lineToCursor = currentLine.Substring(0, ts.ActivePoint.DisplayColumn - 2);

                    if (currentLine.TrimStart().StartsWith("*"))
                    {
                        if (TriggerCompletion())
                        {
                            return VSConstants.S_OK;
                        }
                    }
                }
                else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE ||
                         commandID == (uint)VSConstants.VSStd2KCmdID.DELETE ||
                         char.IsLetter(typedChar))
                {
                    if (IsAutoCompletionActive())
                    {
                        m_session.SelectedCompletionSet.SelectBestMatch();
                        m_session.SelectedCompletionSet.Recalculate();
                        return VSConstants.S_OK;
                    }
                }

                return retVal;
            }
            catch
            {
            }

            return VSConstants.E_FAIL;
        }

        /// <summary>
        /// Checks if the comment trigger was just written.
        /// </summary>
        /// <param name="typedChar">Last typed character.</param>
        /// <returns>True if the comment trigger was written. Otherwise false.</returns>
        private bool CheckForCommentTrigger(char typedChar)
        {
            // Check for only those characters which could end either of the trigger words.
            if ((typedChar == '/' || typedChar == '!') && m_dte != null)
            {
                var currentILine = m_textView.TextSnapshot.GetLineFromPosition(m_textView.Caret.Position.BufferPosition.Position);
                int len = m_textView.Caret.Position.BufferPosition.Position - currentILine.Start.Position;
                string currentLine = m_textView.TextSnapshot.GetText(currentILine.Start.Position, len);

                // Check for /// or /*!.
                string trimmed = (currentLine + typedChar).Trim();
                return (trimmed == "///" || trimmed == "/*!");
            }

            return false;
        }

        /// <summary>
        /// Tries to do smart indentation based on the current position of the caret.
        /// </summary>
        /// <returns>If smart indentation was performed. Otherwise false.</returns>
        private bool TrySmartIndent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Try to indent intelligently to correct location based on the previous line.
            TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
            string prevLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line - 1, ts.ActivePoint.Line);
            bool success = m_generator.GenerateIndentation(ts.ActivePoint.LineCharOffset, prevLine, out int newOffset);

            if (success)
            {
                // If we're at the end of the line, we should just move the caret. This ensures that the editor doesn't
                // commit any trailing spaces unless user writes something after the indentation.
                if (ts.ActivePoint.LineCharOffset > ts.ActivePoint.LineLength)
                {
                    ts.MoveToLineAndOffset(ts.ActivePoint.Line, newOffset);
                }
                else
                {
                    // Otherwise add indentation in the middle of the line.
                    ts.Insert(new string(' ', newOffset - ts.ActivePoint.LineCharOffset));
                }
            }

            return success;
        }

        /// <summary>
        /// Creates a new comment line based on the position of the caret and Doxygen configuration.
        /// </summary>
        /// <param name="currentLine">Current line for reference.</param>
        private void NewCommentLine(string currentLine, int indent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;

            // Try to also guess proper indentation level based on the current line.
            int oldLine = ts.ActivePoint.Line;
            int oldOffset = ts.ActivePoint.LineCharOffset;
            int extraIndent = 0;

            // Calculate how many trailing spaces there will be left before the line break point.
            string trimmedLine = currentLine.Substring(0, oldOffset - 1);
            int numTrailingSpaces = trimmedLine.Length - trimmedLine.TrimEnd().Length;

            // At this point we know that we are inside a comment, so we don't need to check for any other cases.
            while (!currentLine.Contains("/*!"))
            {
                if (m_regexTagSection.IsMatch(currentLine))
                {
                    extraIndent = m_configService.Config.TagIndentation;
                    break;
                }

                ts.LineUp();
                currentLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
                currentLine = currentLine.TrimStart();
            }

            // Remove extra spaces from the previous line and add tag start line.
            ts.MoveToLineAndOffset(oldLine, oldOffset);
            ts.DeleteLeft(numTrailingSpaces);

            // TODO: This adds trailing space. Get rid of it similarly to SmartIndent().
            ts.Insert(m_generator.GenerateTagStartLine(new string (' ', indent)) + new string(' ', extraIndent));
        }

        /// <summary>
        /// Generates a Doxygen comment block to the current caret location.
        /// </summary>
        private void GenerateComment()
        {
            var currentILine = m_textView.TextSnapshot.GetLineFromPosition(m_textView.Caret.Position.BufferPosition.Position);
            int len = m_textView.Caret.Position.BufferPosition.Position - currentILine.Start.Position;
            string currentLine = m_textView.TextSnapshot.GetText(currentILine.Start.Position, len);
            string spaces = currentLine.Replace(currentLine.TrimStart(), "");

            ThreadHelper.ThrowIfNotOnUIThread();
            TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;

            // Save current care position.
            int oldLine = ts.ActivePoint.Line;
            int oldOffset = ts.ActivePoint.LineCharOffset;

            // Check if we're at the beginning of the document and should generate a file comment.
            if (oldLine == 1)
            {
                string fileComment = m_generator.GenerateFileComment(m_dte, out int selectedLine);
                ts.DeleteLeft(2); // Removing the // part here.
                ts.Insert(fileComment);

                // Move the caret.
                ts.MoveToLineAndOffset(selectedLine + 1, 1);
                ts.EndOfLine();
                return;
            }

            // Search for the associated code element for which to generate the comment.
            CodeElement codeElement = null;
            ts.LineDown();
            ts.EndOfLine();

            FileCodeModel fcm = m_dte.ActiveDocument.ProjectItem.FileCodeModel;

            if (fcm != null)
            {
                while (codeElement == null)
                {
                    codeElement = fcm.CodeElementFromPoint(ts.ActivePoint, vsCMElement.vsCMElementFunction);

                    if (ts.ActivePoint.AtEndOfDocument)
                    {
                        break;
                    }

                    if (codeElement == null)
                    {
                        ts.LineDown();
                    }
                }
            }

            // Generate the comment and add it to the document.
            string doxyComment = m_generator.GenerateComment(spaces, codeElement, "");
            ts.MoveToLineAndOffset(oldLine, oldOffset);
            ts.DeleteLeft(2); // Removing the // part here.
            ts.Insert(doxyComment);

            // Move caret to the position where the main comment will be written.
            ts.MoveToLineAndOffset(oldLine, oldOffset);
            ts.LineDown();
            ts.EndOfLine();
        }

        /// <summary>
        /// Starts an auto completion session.
        /// </summary>
        /// <returns>True if the completion session was started.</returns>
        private bool TriggerCompletion()
        {
            try
            {
                if (m_session != null)
                {
                    return false;
                }

                // the caret must be in a non-projection location
                SnapshotPoint? caretPoint =
                m_textView.Caret.Position.Point.GetPoint(
                    textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
                if (!caretPoint.HasValue)
                {
                    return false;
                }

                m_session = m_provider.CompletionBroker.CreateCompletionSession(
                    m_textView,
                    caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                    true);

                // subscribe to the Dismissed event on the session
                m_session.Dismissed += this.OnSessionDismissed;
                m_session.Start();
                m_session.SelectedCompletionSet.SelectBestMatch();
                m_session.SelectedCompletionSet.Recalculate();
                return true;
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Tries to end auto completion based on the currently pressed keys.
        /// </summary>
        /// <param name="typedChar">The currently typed character, if any.</param>
        /// <param name="nCmdID">Key command id.</param>
        /// <returns>True if the auto completion committed.</returns>
        private bool TryEndCompletion(char typedChar, uint nCmdID)
        {
            // Dismiss the session on space.
            if (typedChar == ' ')
            {
                m_session.Dismiss();
            }
            // Check for a commit key.
            else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN || nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                // If the selection is fully selected, commit the current session.
                if (m_session.SelectedCompletionSet.SelectionStatus.IsSelected)
                {
                    string selectedCompletion = m_session.SelectedCompletionSet.SelectionStatus.Completion.DisplayText;
                    m_session.Commit();
                    return true;
                }
                else
                {
                    // if there is no selection, dismiss the session
                    m_session.Dismiss();
                }
            }

            return false;
        }

        /// <summary>
        /// Handles auto completion session dismissal.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSessionDismissed(object sender, EventArgs e)
        {
            if (m_session != null)
            {
                m_session.Dismissed -= this.OnSessionDismissed;
                m_session = null;
            }
        }

        /// <summary>
        /// Check if the given parameter is an input.
        /// </summary>
        /// <param name="parameter">The parameter to check.</param>
        /// <returns>True if the parameter is an input. Otherwise false.</returns>
        private bool IsInput(CodeParameter parameter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool isInput = true;
            string typeName = parameter.Type.AsString;
            string[] expressions = typeName.Split(' ');

            foreach (var e in expressions)
            {
                if (e == "const")
                {
                    isInput = true;
                    break;
                }
                else if (e == "&" || e == "*")
                {
                    isInput = false;
                }
            }

            return isInput;
        }

        /// <summary>
        /// Returns true if the auto completion session is active.
        /// </summary>
        /// <returns>True if auto completion is active. Otherwise false.</returns>
        private bool IsAutoCompletionActive()
        {
            return m_session != null && !m_session.IsDismissed;
        }

        private enum CommentStatus
        {
            Yes,
            No,
            Maybe
        }

        private static int FindLastToken(string line, string token)
        {
            int indexToken = line.LastIndexOf(token);

            // Check if the token is inside a string literal.
            if (indexToken != -1)
            {
                // Check if the token is inside a single line comment.
                int indexSingleLineComment = line.IndexOf("//");

                if (indexSingleLineComment != -1 && indexSingleLineComment < indexToken)
                {
                    return -1;
                }

                // Check if the token is inside a string literal.
                string prefix = line.Substring(0, indexToken);
                int quoteCount = prefix.Length - prefix.Replace("\"", "").Length;

                if (quoteCount % 2 == 1)
                {
                    return -1;
                }
            }

            return indexToken;
        }

        private static CommentStatus TestCommentLine(string line)
        {
            // TODO: There are still some corner cases that are not properly checked.
            int indexStart = FindLastToken(line, "/*!");
            int indexEnd = line.LastIndexOf("*/");

            if (indexStart != -1)
            {
                if (indexEnd == -1)
                {
                    return CommentStatus.Yes;
                }
                else if (indexStart < indexEnd)
                {
                    // Line starts and ends a comment.
                    return CommentStatus.No;
                }
                else
                {
                    // Line ends a comment but starts a new one at a later offset.
                    return CommentStatus.Yes;
                }
            }
            else // No starting block found.
            {
                if (indexEnd == -1)
                {
                    // No comment tokens found. Check if the line starts with an asterisk. Then it could be
                    // continuing a doxygen comment.
                    if (line.TrimStart().StartsWith("*"))
                    {
                        return CommentStatus.Maybe;
                    }
                    else
                    {
                        return CommentStatus.No;
                    }
                }
                else
                {
                    // End comment block found, so not inside a comment anymore after this line.
                    return CommentStatus.No;
                }
            }
        }

        /// <summary>
        /// Check if the line is inside a multi-line doxygen comment block.
        /// </summary>
        /// <param name="line">The line to check.</param>
        /// <returns>True if the line is inside a comment.</returns>
        private bool IsInsideDoxygenCommentBlock(string line, out int indent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Peek through the text before this line to see if we are inside a doxygen comment block.
            TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
            int oldLine = ts.ActivePoint.Line;
            int oldOffset = ts.ActivePoint.LineCharOffset;

            int curLineIndex = oldLine;
            string trimmedLine = line.Substring(0, oldOffset - 1);
            string curLine = trimmedLine;

            // Check the current line as a special case. We only want to continue the comment if it follows the
            // asterisk prefix.
            CommentStatus status = TestCommentLine(curLine);

            while (status == CommentStatus.Maybe)
            {
                --curLineIndex;

                // Stop when we reach the beginning of the file.
                if (curLineIndex == 0)
                {
                    break;
                }

                ts.LineUp();
                curLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
                status = TestCommentLine(curLine);
            }

            // Revert the caret position.
            ts.MoveToLineAndOffset(oldLine, oldOffset);

            // We are inside a comment only if we got definitive yes from the loop.
            if (status == CommentStatus.Yes)
            {
                indent = line.IndexOf('*');
                return true;
            }
            else
            {
                indent = 0;
                return false;
            }
        }

        /// <summary>
        /// Called when the Doxygen config has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onDoxygenConfigChanged(object sender, EventArgs e)
        {
            m_regexTagSection = new Regex(@"\*\s+\" + m_configService.Config.TagChar + @"([a-z]+)\s+(.+)$", RegexOptions.Compiled);
        }

        //! Next command handler.
        private IOleCommandTarget m_nextCommandHandler;

        //! Text view to which the handler is attached.
        private IWpfTextView m_textView;

        //! Completion handler provider.
        private CompletionHandlerProvider m_provider;

        //! Auto completion session.
        private ICompletionSession m_session;

        //! DTE handle.
        private DTE m_dte;

        //! Doxygen config.
        DoxygenConfigService m_configService;

        //! Doxygen generator.
        DoxygenGenerator m_generator;

        //! Regex for looking for doxygen sections.
        private Regex m_regexTagSection;
    }
}
