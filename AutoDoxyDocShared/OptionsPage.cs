using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace AutoDoxyDoc
{
    public class OptionsPage : DialogPage
    {
        [Category("General")]
        [DisplayName("Tags indentation")]
        [Description("Number of extra indentation spaces for Doxygen tag lines.")]
        public int TagIndentation { get; set; } = 4;

        [Category("General")]
        [DisplayName("Tag style")]
        [Description("Tag style to use. JavaDoc uses @param tags while Qt uses \\param tags.")]
        public DoxygenStyle TagStyle { get; set; } = DoxygenStyle.JavaDoc;

        [Category("General")]
        [DisplayName("File comment template")]
        [Description("Template for the file beginning comment.")]
        [Editor(typeof(MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string FileCommentTemplate { get; set; } =
            "/*!\r\n" +
            " *  @file {FILENAME}\r\n" +
            " *  @author {AUTHOR}\r\n" +
            " *  @date {YEAR}-{MONTH}-{DAY}\r\n" +
            " *  @project {PROJECTNAME}\r\n" +
            " *\r\n" +
            " *  {SMARTCOMMENT}{CURSOR}\r\n" +
            " */";

        [Category("Smart Comments")]
        [DisplayName("Smart comments generation")]
        [Description("AutoDoxyDoc tries to generate smart comments for function summary, function parameters and return values automatically.")]
        public bool SmartComments { get; set; } = true;

        [Category("Smart Comments")]
        [DisplayName("Smart comments for all functions")]
        [Description("AutoDoxyDoc tries to generate smart comments extensively for all functions (not only setters and getters).")]
        public bool SmartCommentsForAllFunctions { get; set; } = true;

        [Category("Smart Comments")]
        [DisplayName("Default parameter direction for non-const references")]
        [Description("AutoDoxyDoc chooses this direction as the default one when it cannot determine proper usage from the parameter type.")]
        public ParamDirection DefaultNonConstReferenceDirection { get; set; } = ParamDirection.InOut;

        [Category("Smart Comments")]
        [DisplayName("Abbreviations")]
        [Description("Abbreviations that AutoDoxyDoc will unabbreviate when generating comments.")]
        [EditorAttribute(typeof(AbbreviationsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public AbbreviationMap Abbreviations { get; set; } = new AbbreviationMap();

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Brief summary for setters")]
        [Description("Template for function summary when the function is a setter.")]
        public string BriefSetterDescFormat { get; set; } = "Sets the {1}{0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Brief summary for getters")]
        [Description("Template for brief summary when the function is a getter.")]
        public string BriefGetterDescFormat { get; set; } = "Returns the {1}{0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Brief summary for boolean getters")]
        [Description("Template for brief summary when the function is getter returning a boolean.")]
        public string BriefBoolGetterDescFormat { get; set; } = "Returns true if the {1}{2} {0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Template for setter parameters")]
        [Description("Template for parameters when the function is a setter.")]
        public string ParamSetterDescFormat { get; set; } = "{0} to set.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Template for boolean setter parameters")]
        [Description("Template for boolean function parameters when the function is a setter.")]
        public string ParamBooleanFormat { get; set; } = "If true, {0}. Otherwise not {0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Template for return values")]
        [Description("Template for return values when the function is a getter.")]
        public string ReturnDescFormat { get; set; } = "The {0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("Template for boolean return values")]
        [Description("Template for return values when the function is a getter and returns a boolean.")]
        public string ReturnBooleanDescFormat { get; set; } = "True if {0}. False if not.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("File summary for header files")]
        [Description("File summary for header files (*.h, *.hpp).")]
        public string FileCommentIsHeader { get; set; } = "Declares the {0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("File summary for source files")]
        [Description("File summary for source files (*.c, *.cpp, *.cxx).")]
        public string FileCommentIsSource { get; set; } = "Implements the {0}.";

        [Category("Smart Comments (Advanced)")]
        [DisplayName("File summary for inline files")]
        [Description("File summary for inline files (*.inl).")]
        public string FileCommentIsInline { get; set; } = "Implements the {0}.";

        /// <summary>
        /// Applies the new options to the Doxygen configuration.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply)
            {
                var configService = GetService(typeof(DoxygenConfigService)) as DoxygenConfigService;

                if (configService != null)
                {
                    configService.Config.LoadSettings(this);
                }
            }

            base.OnApply(e);
        }
    }
}
