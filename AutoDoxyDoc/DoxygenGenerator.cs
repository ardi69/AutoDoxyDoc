using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Doxygen comment generator.
    /// </summary>
    class DoxygenGenerator
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configService">Doxygen configuration service.</param>
        public DoxygenGenerator(DoxygenConfigService configService)
        {
            m_configService = configService;
            m_configService.Config.ConfigChanged += onConfigChanged;
            InitStyle();
        }

        ~DoxygenGenerator()
        {
            m_configService.Config.ConfigChanged -= onConfigChanged;
        }

        /// <summary>
        /// Generates smart indentation for the current line.
        /// </summary>
        /// <param name="curOffset">Char offset on the current line.</param>
        /// <param name="prevLine">Contents of the previous line.</param>
        /// <param name="newOffset">Calculated new char offset on the current line.</param>
        /// <returns>True if smart indentation determined proper indentation. False if not.</returns>
        public bool GenerateIndentation(int curOffset, string prevLine, out int newOffset)
        {
            newOffset = curOffset;
            Match tparamMatch = m_regexTParam.Match(prevLine);
            Match paramMatch = m_regexParam.Match(prevLine);
            Match tagMatch = m_regexTagSection.Match(prevLine);

            if (tparamMatch.Success)
            {
                var commentCapture = tparamMatch.Groups[2];
                int diff = commentCapture.Index - curOffset;

                if (diff > 0)
                {
                    newOffset = commentCapture.Index + 1;
                    return true;
                }
            }
            else if (paramMatch.Success)
            {
                var commentCapture = paramMatch.Groups[3];
                int diff = commentCapture.Index - curOffset;

                if (diff > 0)
                {
                    newOffset = commentCapture.Index + 1;
                    return true;
                }
            }
            else if (tagMatch.Success)
            {
                var commentCapture = tagMatch.Groups[2];
                int diff = commentCapture.Index - curOffset;

                if (diff > 0)
                {
                    newOffset = commentCapture.Index + 1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates a doxygen comment block for the given code element.
        /// </summary>
        /// <param name="spaces">Indentation for the whole comment block</param>
        /// <param name="codeElement">The code element.</param>
        /// <param name="existingComment">Existing comment for the code element. Empty if not found.</param>
        /// <returns>Generated doxygen comment block</returns>
        public string GenerateComment(string spaces, CodeElement codeElement, string existingComment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Parse existing comment.
            ParsedComment parsedComment = ParseComment(existingComment);

            // Start writing a new one.
            StringBuilder sb = new StringBuilder("/*!");

            // Write brief summary.
            WriteBriefComment(sb, spaces, codeElement, parsedComment);

            if (codeElement != null)
            {
                // Write comments for template parameters, if any.
                WriteTemplateParamComments(sb, spaces, codeElement, parsedComment);

                // Write comments for function parameters and return value.
                if (codeElement is CodeFunction)
                {
                    CodeFunction function = codeElement as CodeFunction;
                    WriteParamComments(sb, spaces, function, parsedComment);
                    WriteReturnComment(sb, spaces, function, parsedComment);
                }
            }

            // Write other sections that were in the existing comments.
            foreach (ParsedSection section in parsedComment.TagSections)
            {
                string tagLine = m_indentString + Config.TagChar + section.TagName + " ";
                sb.Append("\r\n" + spaces + " *");
                sb.Append("\r\n" + spaces + " *  " + tagLine);
                AppendComments(sb, section.Comments, spaces, tagLine.Length);
            }

            sb.Append("\r\n" + spaces + " */");
            return sb.ToString();
        }

        /// <summary>
        /// Generates tag start line.
        /// </summary>
        /// <param name="spaces">Indentation for the comment line.</param>
        /// <returns>Generated line string.</returns>
        public string GenerateTagStartLine(string spaces)
        {
            return "\r\n" + spaces + "*  ";
        }

        /// <summary>
        /// Generates a file comment.
        /// </summary>
        /// <param name="fullFilename">Name of the source file.</param>
        /// <param name="selectedLine">The line where to place the caret after comment generation.</param>
        /// <returns>Generated comment block.</returns>
        public string GenerateFileComment(EnvDTE.DTE dte, out int selectedLine)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string filename = dte.ActiveDocument.Name;
            string projectName = dte.ActiveDocument.ProjectItem.ContainingProject.Name;

            // Fetch file comment template from config.
            string fileComment = Config.FileCommentTemplate;

            // Replace format tags.
            DateTime localDate = DateTime.Now;
            fileComment = fileComment.Replace("{FILENAME}", filename);
            fileComment = fileComment.Replace("{PROJECTNAME}", projectName);
            fileComment = fileComment.Replace("{AUTHOR}", System.Environment.UserName);
            fileComment = fileComment.Replace("{YEAR}", localDate.ToString("yyyy"));
            fileComment = fileComment.Replace("{MONTH}", localDate.ToString("MM"));
            fileComment = fileComment.Replace("{DAY}", localDate.ToString("dd"));

            // Generate smart comment.
            string comment = TryGenerateFileDesc(filename);
            fileComment = fileComment.Replace("{SMARTCOMMENT}", comment);

            // Determine the line where to place the caret.
            int selectedIndex = fileComment.IndexOf("{CURSOR}");

            if (selectedIndex == -1)
            {
                selectedLine = fileComment.Count(c => c == '\n');
            }
            else
            {
                selectedLine = fileComment.Substring(0, selectedIndex).Count(c => c == '\n');
            }

            fileComment = fileComment.Replace("{CURSOR}", "");

            return fileComment;
        }

        /// <summary>
        /// Writes a brieft comment about the code element.
        /// </summary>
        /// <param name="sb">String builder</param>
        /// <param name="spaces">Indentation spaces.</param>
        /// <param name="codeElement">Code element.</param>
        /// /// <param name="parsedComment">Parsed existing comment, if found.</param>
        private void WriteBriefComment(StringBuilder sb, string spaces, CodeElement codeElement, ParsedComment parsedComment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Write main comment from existing comments, if found.
            if (parsedComment.BriefComments.Count > 0)
            {
                foreach (string line in parsedComment.BriefComments)
                {
                    sb.Append("\r\n" + spaces + " *  " + line);
                }
            }
            else
            {
                // Write placeholder for main comment.
                sb.Append("\r\n" + spaces + " *  ");

                // Try to determine initial main comment if comment auto-generation is enabled.
                if (Config.SmartComments)
                {
                    sb.Append(TryGenerateBriefDesc(codeElement));
                }
            }
        }

        /// <summary>
        /// Writes comments for function parameters.
        /// </summary>
        /// <param name="sb">String builder to write comments into.</param>
        /// <param name="spaces">Indentation spaces.</param>
        /// <param name="codeElement">Function code element.</param>
        /// <param name="parsedComment">Parsed existing comment, if any.</param>
        private void WriteTemplateParamComments(StringBuilder sb, string spaces, CodeElement codeElement, ParsedComment parsedComment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Parse template parameters from the full function name.
            string[] tparams = ParseTemplateParams(codeElement.FullName);

            if (tparams.Length == 0)
            {
                return;
            }

            // Calculate maximum template parameter name length.
            int maxParamNameLength = 0;

            foreach (string tparamName in tparams)
            {
                maxParamNameLength = Math.Max(maxParamNameLength, tparamName.Length);
            }

            // Create doxygen lines for each parameter.
            sb.Append("\r\n" + spaces + " *");

            foreach (string tparamName in tparams)
            {
                // Check if the existing comment contained this parameter.
                ParsedParam parsedParam = null;

                if (parsedComment.TemplateParameters.ContainsKey(tparamName))
                {
                    parsedParam = parsedComment.TemplateParameters[tparamName];
                }

                string paramAlignSpaces = new string(' ', maxParamNameLength - tparamName.Length + 1);
                string tagLine = m_indentString + Config.TagChar + "tparam " + tparamName + paramAlignSpaces;
                sb.Append("\r\n" + spaces + " *  " + tagLine);

                // Add existing comments.
                if (parsedParam != null)
                {
                    AppendComments(sb, parsedParam.Comments, spaces, tagLine.Length);
                }

                // TODO: Any smart comments possible?
            }
        }

        /// <summary>
        /// Writes comments for function parameters.
        /// </summary>
        /// <param name="sb">String builder to write comments into.</param>
        /// <param name="spaces">Indentation spaces.</param>
        /// <param name="codeElement">Function code element.</param>
        /// <param name="parsedComment">Parsed existing comment, if any.</param>
        private void WriteParamComments(StringBuilder sb, string spaces, CodeFunction function, ParsedComment parsedComment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int maxTypeDirectionLength = 0;
            int maxParamNameLength = 0;

            foreach (CodeElement child in function.Children)
            {
                CodeParameter param = child as CodeParameter;

                if (param != null)
                {
                    // Check if the existing comment contained this parameter.
                    ParsedParam parsedParam = null;

                    if (parsedComment.Parameters.ContainsKey(param.Name))
                    {
                        parsedParam = parsedComment.Parameters[param.Name];
                    }

                    string typeDirName = DirectionToString(GetParamDirection(function, param, parsedParam));
                    maxTypeDirectionLength = Math.Max(maxTypeDirectionLength, typeDirName.Length);
                    maxParamNameLength = Math.Max(maxParamNameLength, param.Name.Length);
                }
            }

            // Create doxygen lines for each parameter.
            if (function.Children.Count > 0)
            {
                sb.Append("\r\n" + spaces + " *");

                foreach (CodeElement child in function.Children)
                {
                    CodeParameter param = child as CodeParameter;

                    if (param != null)
                    {
                        // Check if the existing comment contained this parameter.
                        ParsedParam parsedParam = null;

                        if (parsedComment.Parameters.ContainsKey(param.Name))
                        {
                            parsedParam = parsedComment.Parameters[param.Name];
                        }

                        // Determine type of parameter (in, out or inout).
                        string typeDirName = DirectionToString(GetParamDirection(function, param, parsedParam));
                        string paramAlignSpaces = new string(' ', maxParamNameLength - param.Name.Length + 1);
                        string typeAlignSpaces = new string(' ', maxTypeDirectionLength - typeDirName.Length + 1);
                        string tagLine = m_indentString + Config.TagChar + "param " + typeDirName + typeAlignSpaces + param.Name + paramAlignSpaces;
                        sb.Append("\r\n" + spaces + " *  " + tagLine);

                        // Add existing comments.
                        if (parsedParam != null)
                        {
                            AppendComments(sb, parsedParam.Comments, spaces, tagLine.Length);
                        }
                        else if (Config.SmartComments && function.Children.Count == 1)
                        {
                            sb.Append(TryGenerateParamDesc(function, param));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Writes a comment for the function return value.
        /// </summary>
        /// <param name="sb">String builder.</param>
        /// <param name="spaces">Indentation spaces.</param>
        /// <param name="function">Code function.</param>
        /// <param name="parsedComment">Parsed existing comment, if found.</param>
        private void WriteReturnComment(StringBuilder sb, string spaces, CodeFunction function, ParsedComment parsedComment)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (function.Type.AsString != "void")
            {
                sb.Append("\r\n" + spaces + " *");
                string tagLine = m_indentString + Config.TagChar + "return ";
                sb.Append("\r\n" + spaces + " *  " + tagLine);

                if (parsedComment.Returns != null)
                {
                    AppendComments(sb, parsedComment.Returns.Comments, spaces, tagLine.Length);
                }
                else if (Config.SmartComments)
                {
                    sb.Append(TryGenerateReturnDesc(function));
                }
            }
        }

        private bool IsInput(CodeParameter parameter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool isConst = false;
            bool isRef = false;
            string typeName = parameter.Type.AsString;
            string[] expressions = typeName.Split(' ');

            foreach (var e in expressions)
            {
                if (e == "const")
                {
                    isConst = true;
                    break;
                }
                else if (e == "&" || e == "*")
                {
                    isRef = true;
                }
            }

            if (typeName.EndsWith("&") || typeName.EndsWith("*"))
            {
                isRef = true;
            }

            return (isConst || !isRef);
        }

        /// <summary>
        /// Parses the given Doxygen comment.
        /// </summary>
        /// <param name="comment">The comment to parse.</param>
        /// <returns>Parsed comment structure.</returns>
        private ParsedComment ParseComment(string comment)
        {
            ParsedComment parsedComment = new ParsedComment();

            if (comment.Length > 0)
            {
                string[] lines = comment.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                // Trim leading and trailing whitespace before any parsing.
                for (int i = 0; i < lines.Length; ++i)
                {
                    lines[i] = lines[i].Trim();
                }

                for (int i = 0; i < lines.Length; ++i)
                {
                    // Skip empty lines and comment start/end lines.
                    string line = lines[i];

                    if (line.Length == 0 || line == "*" || line == "/*!" || line == "*/")
                    {
                        continue;
                    }

                    // Check if this is a template parameter line.
                    Match tparamMatch = m_regexTParam.Match(line);

                    if (tparamMatch.Success)
                    {
                        string name = tparamMatch.Groups[1].Value;
                        string firstComment = tparamMatch.Groups[2].Value;

                        if (!parsedComment.TemplateParameters.ContainsKey(name) && firstComment.Length > 0)
                        {
                            ParsedParam param = new ParsedParam();
                            param.Name = name;
                            param.Comments.Add(firstComment);
                            i = ParseExtraComments(lines, i + 1, param.Comments);

                            parsedComment.TemplateParameters.Add(param.Name, param);
                        }
                    }
                    else
                    {
                        // Check if this is a parameter line.
                        Match paramMatch = m_regexParam.Match(line);

                        if (paramMatch.Success)
                        {
                            string name = paramMatch.Groups[2].Value;
                            string firstComment = paramMatch.Groups[3].Value;

                            if (!parsedComment.Parameters.ContainsKey(name))
                            {
                                ParsedParam param = new ParsedParam();
                                param.Name = name;
                                param.Direction = ToDirection(paramMatch.Groups[1].Value);

                                if (firstComment.Length > 0)
                                {
                                    param.Comments.Add(firstComment);
                                    i = ParseExtraComments(lines, i + 1, param.Comments);
                                }

                                parsedComment.Parameters.Add(param.Name, param);
                            }
                        }
                        else
                        {
                            // Otherwise check if it is some other tag.
                            Match sectionMatch = m_regexTagSection.Match(line);

                            if (sectionMatch.Success)
                            {
                                string tagName = sectionMatch.Groups[1].Value;
                                string firstComment = sectionMatch.Groups[2].Value;

                                if (firstComment.Length > 0)
                                {
                                    ParsedSection section = new ParsedSection();
                                    section.TagName = tagName;
                                    section.Comments.Add(firstComment);
                                    i = ParseExtraComments(lines, i + 1, section.Comments);

                                    if (section.TagName == "return" || section.TagName == "returns")
                                    {
                                        parsedComment.Returns = section;
                                    }
                                    else
                                    {
                                        parsedComment.TagSections.Add(section);
                                    }
                                }
                            }
                            else
                            {
                                // If the line doesn't contain any tag, we try to extract text out of it.
                                Match textMatch = m_regexText.Match(line);

                                if (textMatch.Success)
                                {
                                    parsedComment.BriefComments.Add(textMatch.Groups[1].Value);
                                }
                            }
                        }
                    }
                }
            }

            return parsedComment;
        }

        /// <summary>
        /// Determines parameter direction based on the code element and the previously parsed parameter comment line.
        /// </summary>
        /// <param name="function">Parent function.</param>
        /// <param name="param">Code element for the parameter.</param>
        /// <param name="parsedParam">Existing parsed parameter info, if available.</param>
        /// <returns>The parameter direction.</returns>
        private ParamDirection GetParamDirection(CodeFunction function, CodeParameter param, ParsedParam parsedParam)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ParamDirection direction = ParamDirection.In;
            bool isConstructor = (function.FunctionKind & vsCMFunction.vsCMFunctionConstructor) != 0;

            // By default trust the user if an existing direction is found.
            if (parsedParam != null)
            {
                direction = parsedParam.Direction;
            }
            // Constructor assumes inputs only. Otherwise we check the parameter data type to try to determine
            // the appropriate direction.
            else if (!isConstructor && !IsInput(param))
            {
                direction = Config.DefaultNonConstReferenceDirection;
            }

            return direction;
        }

        /// <summary>
        /// Parses extra comments for a section.
        /// </summary>
        /// <param name="lines">The comment lines available for parsing.</param>
        /// <param name="startIndex">The first line to start parsing from.</param>
        /// <param name="comments">Extracted list of comments.</param>
        /// <returns>Index of the first line which was not treated as an extra comment.</returns>
        private int ParseExtraComments(string[] lines, int startIndex, List<string> comments)
        {
            // Parse comment lines until we either come across a new doxygen tag or non-text line.
            int i = startIndex;

            for (; i < lines.Length; ++i)
            {
                // Check for non-text line.
                if (lines[i].Length == 0 || lines[i] == "*" || lines[i] == "*/")
                {
                    break;
                }

                // Check for any doxygen tags.
                Match tagMatch = m_regexTagSection.Match(lines[i]);

                if (tagMatch.Success)
                {
                    break;
                }

                // Check if this is text.
                Match textMatch = m_regexText.Match(lines[i]);

                if (!textMatch.Success)
                {
                    break;
                }

                comments.Add(textMatch.Groups[1].Value);
            }

            return i - 1;
        }

        /// <summary>
        /// Appends a list of comments to the string builder.
        /// </summary>
        /// <param name="sb">String builder</param>
        /// <param name="comments">List of comments to append.</param>
        /// <param name="spaces">Number of spaces for line indentation (before asterisk).</param>
        /// <param name="indentCount">Comment indentation level (after asterisk).</param>
        private void AppendComments(StringBuilder sb, List<string> comments, string spaces, int indentCount)
        {
            if (comments.Count > 0)
            {
                // First comment line as a special case since we don't need to write any indentation.
                sb.Append(comments[0]);

                // Add new lines for rest of the comments.
                string indentString = new string(' ', indentCount);

                for (int i = 1; i < comments.Count; ++i)
                {
                    sb.Append("\r\n" + spaces + " *  " + indentString + comments[i]);
                }
            }
        }

        /// <summary>
        /// Retrieves a readable class name from.
        /// </summary>
        /// <param name="codeClass">Class reference.</param>
        /// <returns>The name of the class as a spaced string.</returns>
        private string GetClassName(string className)
        {
            string objectName = "";
            ThreadHelper.ThrowIfNotOnUIThread();
            string[] parentWords = StringHelper.SplitCamelCase(className);
            bool first = true;

            for (int i = 0; i < parentWords.Length; ++i)
            {
                if (parentWords[i].Length > 1)
                {
                    if (!first)
                    {
                        objectName += " ";
                    }

                    objectName += Unabbreviate(parentWords[i]);
                    first = false;
                }
            }

            return objectName;
        }

        /// <summary>
        /// Tries to generate a summary of a file based on its name.
        /// </summary>
        /// <param name="filename">Name of the file.</param>
        /// <returns>Generated text.</returns>
        private string TryGenerateFileDesc(string filename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string smartComment = "";

            if (Config.SmartComments)
            {
                // Extract extension and base name.
                string ext = Path.GetExtension(filename);
                string baseName = Path.GetFileNameWithoutExtension(filename);

                // Analyze the name.
                string className = GetClassName(baseName);
                string[] words = StringHelper.SplitCamelCase(baseName);

                if (words.Length > 1 && words[0] == "i")
                {
                    className += " interface";
                }

                if (ext == ".h" || ext == ".hpp")
                {
                    smartComment = String.Format(Config.FileCommentIsHeader, className);
                }
                else if (ext == ".c" || ext == ".cpp" || ext == ".cxx")
                {
                    smartComment = String.Format(Config.FileCommentIsSource, className);
                }
                else if (ext == ".inl")
                {
                    smartComment = String.Format(Config.FileCommentIsInline, className);
                }
            }

            return smartComment;
        }

        /// <summary>
        /// Tries to generate brief summary of the code element.
        /// </summary>
        /// <param name="codeElement">Code element for which to generate brief summary.</param>
        /// <returns>Generated text.</returns>
        private string TryGenerateBriefDesc(CodeElement codeElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string desc = "";

            if (codeElement != null && codeElement is CodeFunction)
            {
                CodeFunction function = codeElement as CodeFunction;

                if ((function.FunctionKind & vsCMFunction.vsCMFunctionConstructor) != 0)
                {
                    desc = "Constructor.";
                }
                else if ((function.FunctionKind & vsCMFunction.vsCMFunctionDestructor) != 0)
                {
                    desc = "Destructor.";
                }
                else
                {
                    string[] funcNameWords = StringHelper.SplitCamelCase(function.Name);

                    // Retrieve the parent class's name.
                    string className = "";
                    string owner = "";

                    if (function.Parent is CodeClass)
                    {
                        var codeClass = function.Parent as CodeClass;
                        className = GetClassName(codeClass.Name);
                        owner = className + "'s ";
                    }

                    // Determine if the function is a setter or getter.
                    bool setter = funcNameWords[0] == "set";
                    bool getter = funcNameWords[0] == "get";
                    bool boolGetter = funcNameWords[0] == "is" || funcNameWords[0] == "has";

                    if (getter || setter || boolGetter)
                    {
                        // Generate the proper brief description.
                        if (funcNameWords.Length > 1)
                        {
                            if (getter)
                            {
                                desc = String.Format(Config.BriefGetterDescFormat, UnabbreviateAndJoin(funcNameWords, 1), owner);
                            }
                            else if (setter)
                            {
                                desc = String.Format(Config.BriefSetterDescFormat, UnabbreviateAndJoin(funcNameWords, 1), owner);
                            }
                            else if (boolGetter)
                            {
                                desc = String.Format(Config.BriefBoolGetterDescFormat, UnabbreviateAndJoin(funcNameWords, 1), className + " ", funcNameWords[0]);
                            }
                        }
                    }
                    else if (Config.SmartCommentsForAllFunctions)
                    {
                        // Allow smart comments for single word functions only in case they are class members.
                        // All other functions are supported.
                        if (funcNameWords.Length > 1 || function.Parent is CodeClass)
                        {
                            // We'll use the first word as the verb in the general case.
                            // Determine the third person verb ending for the verb.
                            string verb = StringHelper.GetThirdPersonVerb(Unabbreviate(funcNameWords[0]));

                            // In case a single word function name, we assume the object of the verb is the class itself.
                            string dest;
                            if (funcNameWords.Length == 1)
                            {
                                dest = className;
                            }
                            // Otherwise the object is the part which comes in the next words.
                            else
                            {
                                dest = UnabbreviateAndJoin(funcNameWords, 1);
                            }

                            desc = StringHelper.Capitalize(String.Format("{0} the {1}.", verb, dest));
                        }
                    }
                }
            }

            return desc;
        }

        /// <summary>
        /// Tries to generate parameter description comment.
        /// </summary>
        /// <param name="parent">Parent function.</param>
        /// <param name="param">Parameter for which to generate the comment.</param>
        /// <returns>Generated text.</returns>
        private string TryGenerateParamDesc(CodeFunction parent, CodeParameter param)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string desc = "";

            // If the function is a setter or getter, we can try to derive a description for the parameter.
            bool setter = parent.Name.StartsWith("set");
            bool getter = parent.Name.StartsWith("get");
            bool isBoolean = param.Type.AsString == "bool";
            string[] words = StringHelper.SplitCamelCase(param.Name);

            if (isBoolean)
            {
                desc = StringHelper.Capitalize(String.Format(Config.ParamBooleanFormat, UnabbreviateAndJoin(words)));
            }
            else if (setter)
            {
                desc = StringHelper.Capitalize(String.Format(Config.ParamSetterDescFormat, UnabbreviateAndJoin(words)));
            }
            else if (getter && GetParamDirection(parent, param, null) != ParamDirection.In)
            {
                desc = StringHelper.Capitalize(String.Format(Config.ReturnDescFormat, UnabbreviateAndJoin(words)));
            }

            return desc;
        }

        /// <summary>
        /// Tries to generate comment for return value.
        /// </summary>
        /// <param name="function">Function for which to generate the return value comment.</param>
        /// <returns>Generated text.</returns>
        private string TryGenerateReturnDesc(CodeFunction function)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string desc = "";
            string[] words = StringHelper.SplitCamelCase(function.Name);

            if (words.Length > 1)
            {
                if (words[0] == "get")
                {
                    // Determine if the getter has output function parameters.
                    bool hasOutParams = false;

                    foreach (CodeElement child in function.Children)
                    {
                        CodeParameter param = child as CodeParameter;

                        if (GetParamDirection(function, param, null) != ParamDirection.In)
                        {
                            hasOutParams = true;
                            break;
                        }
                    }

                    // Check for a special case: returns boolean and the parameter is returned in a function parameter.
                    if (function.Type.AsString == "bool" && hasOutParams)
                    {
                        desc = StringHelper.Capitalize(String.Format(Config.ReturnBooleanDescFormat, "successful"));
                    }
                    else
                    {
                        desc = StringHelper.Capitalize(String.Format(Config.ReturnDescFormat, UnabbreviateAndJoin(words, 1)));
                    }
                }
                else if (words[0] == "is")
                {
                    desc = StringHelper.Capitalize(String.Format(Config.ReturnBooleanDescFormat, UnabbreviateAndJoin(words, 1)));
                }
                else if (words[0] == "has")
                {
                    desc = StringHelper.Capitalize(String.Format(Config.ReturnBooleanDescFormat, "has " + UnabbreviateAndJoin(words, 1)));
                }
            }

            return desc;
        }

        /// <summary>
        /// Unabbreviates a word based on a dictionary.
        /// </summary>
        /// <param name="word">Word to unabbreviate.</param>
        /// <returns>Unabbreviated word, or the original if no conversion was found for it.</returns>
        private string Unabbreviate(string word)
        {
            return Config.Abbreviations.Unabbreviate(word);
        }

        /// <summary>
        /// Unabbreviates all words in an array and joins them together with spaces.
        /// </summary>
        /// <param name="words">Words to unabbreviate and join.</param>
        /// <param name="startIndex">Index of the first word to include.</param>
        /// <returns>The joined string.</returns>
        private string UnabbreviateAndJoin(string[] words, int startIndex = 0)
        {
            return String.Join(" ", Array.ConvertAll(words.ToArray(), i => Unabbreviate(i)), startIndex, words.Length - startIndex);
        }

        /// <summary>
        /// Initializes config dependent variables.
        /// </summary>
        private void InitStyle()
        {
            m_indentString = new string(' ', Config.TagIndentation);
            m_regexParam = new Regex(@"\s*\*\s+\" + Config.TagChar + @"param\s*(\[[a-z,]+\])?\s+(\w+)(?:\s+(.*))?$", RegexOptions.Compiled);
            m_regexTParam = new Regex(@"\s*\*\s+\" + Config.TagChar + @"tparam\s+(\w+)(?:\s+(.*))?$", RegexOptions.Compiled);
            m_regexTagSection = new Regex(@"\s*\*\s+\" + Config.TagChar + @"([a-z]+)(?:\s+(.*))?$", RegexOptions.Compiled);
        }

        /// <summary>
        /// Re-initializes config related variables when config has been changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onConfigChanged(object sender, EventArgs e)
        {
            InitStyle();
        }

        /// <summary>
        /// Converts parameter direction to Doxygen string.
        /// </summary>
        /// <param name="dir">Parameter direction.</param>
        /// <returns>Doxygen compatible string.</returns>
        private static string DirectionToString(ParamDirection dir)
        {
            switch (dir)
            {
                case ParamDirection.In:
                default:
                    return "[in]";

                case ParamDirection.Out:
                    return "[out]";

                case ParamDirection.InOut:
                    return "[in,out]";
            }
        }

        /// <summary>
        /// Converts Doxygen string to parameter direction.
        /// </summary>
        /// <param name="val">The string to convert.</param>
        /// <returns>The equivalent parameter direction.</returns>
        private static ParamDirection ToDirection(string val)
        {
            switch (val)
            {
                case "[in]":
                default:
                    return ParamDirection.In;

                case "[out]":
                    return ParamDirection.Out;

                case "[in,out]":
                    return ParamDirection.InOut;
            }
        }

        /// <summary>
        /// Extracts template parameters from a full class/function name.
        /// </summary>
        /// <param name="name">The name to parse.</param>
        /// <returns>Extracted template parameters, if any.</returns>
        private string[] ParseTemplateParams(string name)
        {
            string[] tparams = new string[0];

            if (name.EndsWith(">"))
            {
                int startIndex = name.IndexOf('<');

                if (startIndex >= 0)
                {
                    string substr = name.Substring(startIndex + 1, name.Length - startIndex - 2).Replace(" ", "");
                    tparams = substr.Split(',');
                }
            }

            return tparams;
        }

        /// <summary>
        /// Data for a parsed parameter.
        /// </summary>
        private class ParsedParam
        {
            public string Name { get; set; } = "";
            public ParamDirection Direction { get; set; } = ParamDirection.In;
            public List<string> Comments { get; } = new List<string>();
        }

        /// <summary>
        /// Data for a parsed section.
        /// </summary>
        private class ParsedSection
        {
            public string TagName { get; set; } = "";
            public List<string> Comments { get; } = new List<string>();
        }

        /// <summary>
        /// Data for a parsed Doxygen comment.
        /// </summary>
        private class ParsedComment
        {
            public List<string> BriefComments { get; set; } = new List<string>();
            public Dictionary<string, ParsedParam> TemplateParameters { get; } = new Dictionary<string, ParsedParam>();
            public Dictionary<string, ParsedParam> Parameters { get; } = new Dictionary<string, ParsedParam>();
            public ParsedSection Returns { get; set; }
            public List<ParsedSection> TagSections { get; } = new List<ParsedSection>();
        }

        private DoxygenConfig Config { get { return m_configService.Config; } }

        //! Doxygen style configuration.
        private DoxygenConfigService m_configService;

        //! Indentation for doxygen tags.
        private string m_indentString;

        //! Helper regular expressions.
        private Regex m_regexParam;
        private Regex m_regexTParam;
        private Regex m_regexTagSection;
        private Regex m_regexText = new Regex(@"\s*\*\s+(.+)$", RegexOptions.Compiled);
    }
}
