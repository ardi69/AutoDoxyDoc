using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GenerateComment
    {
        [Import]
        public IVsEditorAdaptersFactoryService AdapterService = null;

        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("dc4e4bbc-7b8f-44b5-b27c-2f2d9a599ee9");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateComment"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GenerateComment(AsyncPackage package, OleMenuCommandService commandService, DoxygenConfigService configService)
        {
            m_generator = new DoxygenGenerator(configService);

            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateComment Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in GenerateComment's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            DoxygenConfigService configService = await package.GetServiceAsync((typeof(DoxygenConfigService))) as DoxygenConfigService;
            Instance = new GenerateComment(package, commandService, configService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;

            if (dte == null || dte.ActiveDocument == null)
            {
                return;
            }

            TextSelection ts = dte.ActiveDocument.Selection as TextSelection;

            // Check if we're at the beginning of the document and should generate a file comment.
            if (ts.ActivePoint.Line == 1)
            {
                string fileComment = m_generator.GenerateFileComment(dte, out int selectedLine);
                ts.DeleteLeft(2); // Removing the // part here.
                ts.Insert(fileComment);

                // Move the caret.
                ts.MoveToLineAndOffset(selectedLine + 1, 1);
                ts.EndOfLine();
                return;
            }

            ts.EndOfLine();

            // Scroll down until we find a non-comment line.
            if (!ScrollToCodeStart(ts))
            {
                return;
            }

            // Save the position so that we know where to place the comment.
            ts.StartOfLine();
            var funcPoint = ts.ActivePoint.CreateEditPoint();
            int oldLine = ts.ActivePoint.Line;
            int oldOffset = ts.ActivePoint.LineCharOffset;
            ts.EndOfLine();

            // Determine indentation level.
            string currentLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
            string spaces = currentLine.Replace(currentLine.TrimStart(), "");

            // Search for the associated code element.
            CodeElement codeElement = null;
            FileCodeModel fcm = dte.ActiveDocument.ProjectItem.FileCodeModel;
            if (fcm != null)
            {
                while (codeElement == null)
                {
                    codeElement = fcm.CodeElementFromPoint(ts.ActivePoint, vsCMElement.vsCMElementFunction);

                    if (ts.ActivePoint.AtEndOfDocument)
                    {
                        break;
                    }

                    if (codeElement == null || !(codeElement is CodeFunction))
                    {
                        codeElement = null;
                        ts.LineDown();
                        ts.EndOfLine();
                    }
                }
            }

            // Extract existing comment if found.
            ts.MoveToLineAndOffset(oldLine, oldOffset);
            int startLine = ExtractComment(ts, out string existingDoxyComment);

            // Delete old comment from the text.
            if (startLine >= 0)
            {
                ts.ActivePoint.CreateEditPoint().Delete(funcPoint);
                oldLine = ts.ActivePoint.Line;
                oldOffset = ts.ActivePoint.LineCharOffset;
            }

            // Generate new comment.
            string doxyComment = m_generator.GenerateComment(spaces, codeElement, existingDoxyComment);

            // Write the doxygen comment to the correct position.
            ts.MoveToLineAndOffset(oldLine, oldOffset);
            ts.LineUp();

            // If the upper line is empty, we should go to the start of the line. Otherwise go to the end of the line.
            currentLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);

            if (currentLine.Trim().Length == 0)
            {
                ts.StartOfLine();
            }
            else
            {
                ts.EndOfLine();
            }

            ts.Insert("\r\n" + spaces + doxyComment);

            // If this is a new comment, move to the main comment position immediately.
            if (startLine < 0)
            {
                ts.MoveToLineAndOffset(oldLine, oldOffset);
                ts.LineDown();
                ts.EndOfLine();
            }
        }

        /// <summary>
        /// Extracts comment from the current text selection location.
        /// </summary>
        /// <param name="ts">Text selection.</param>
        /// <param name="comment">Extracted comment</param>
        /// <returns>The start line of the comment. -1, if no comment was found.</returns>
        private int ExtractComment(TextSelection ts, out string comment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            comment = "";
            string curLine = "";
            int startLine = -1;
            int endLine = -1;

            do
            {
                ts.LineUp();
                curLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
                curLine = curLine.TrimStart();

                // Check if we found the beginning of the comment.
                if (curLine.StartsWith("/*!"))
                {
                    startLine = ts.ActivePoint.Line;
                    break;
                }
                // Check for the end of the comment.
                else if (curLine.StartsWith("*/"))
                {
                    endLine = ts.ActivePoint.Line;
                }

            } while (curLine.Length == 0 || curLine.StartsWith("*"));

            if (startLine >= 0 && endLine >= ts.ActivePoint.Line)
            {
                comment = ts.ActivePoint.CreateEditPoint().GetLines(startLine, endLine + 1);
            }

            return startLine;
        }

        /// <summary>
        /// Returns true if the line is empty or a comment.
        /// </summary>
        /// <param name="curLine">The line to check.</param>
        /// <returns>True if empty or comment. Otherwise false.</returns>
        private static bool IsEmptyOrComment(string curLine)
        {
            // This doesn't handle corner cases since lines starting with * could also be code lines.
            // Proper solution should scan for a beginning /*! and ending */ to see if we are truly inside a comment.
            return curLine.Length == 0 || curLine.StartsWith("/*!") || curLine.StartsWith("*") || curLine.StartsWith("//");
        }

        /// <summary>
        /// Scrolls to the beginning of code. Code is searched from below if we are currently on a comment or
        /// empty line. Otherwise the beginning of code is searched from above.
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        private bool ScrollToCodeStart(TextSelection ts)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string curLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
            curLine = curLine.TrimStart();
            bool codeFound = false;

            // If we are currently on a comment or empty line, find the first code line from below.
            if (IsEmptyOrComment(curLine))
            {
                while (!ts.ActivePoint.AtEndOfDocument)
                {
                    if (!IsEmptyOrComment(curLine))
                    {
                        codeFound = true;
                        break;
                    }

                    ts.LineDown();
                    curLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line, ts.ActivePoint.Line + 1);
                    curLine = curLine.TrimStart();
                }
            }
            else
            {
                // Otherwise search from above for the line which begins the code statement.
                while (ts.ActivePoint.Line > 1)
                {
                    // Peek previous line and check if it is new code statement, comment or empty.
                    string prevLine = ts.ActivePoint.CreateEditPoint().GetLines(ts.ActivePoint.Line - 1, ts.ActivePoint.Line);
                    prevLine = prevLine.Trim();

                    if (IsEmptyOrComment(prevLine) || prevLine.EndsWith(";") || prevLine.EndsWith("}"))
                    {
                        codeFound = true;
                        break;
                    }

                    ts.LineUp();
                }

            }

            return codeFound;
        }

        //! Doxygen generator.
        private DoxygenGenerator m_generator;
    }
}
