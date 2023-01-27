using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Editor for abbreviations.
    /// </summary>
    class AbbreviationsEditor : UITypeEditor
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AbbreviationsEditor()
        {
        }

        /// <summary>
        /// Returns the edit style.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            if (context != null && context.Instance != null)
            {
                return UITypeEditorEditStyle.Modal;
            }

            return UITypeEditorEditStyle.None;
        }

        /// <summary>
        /// Handles the editing of the abbreviations when the user presses the browse button.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="provider"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override object EditValue(System.ComponentModel.ITypeDescriptorContext context,
                                         IServiceProvider provider, object value)
        {
            if (context == null || provider == null || context.Instance == null)
            {
                return base.EditValue(provider, value);
            }

            AbbreviationMap oldAbbreviations = value as AbbreviationMap;
            AbbreviationsEditorDialog dialog = new AbbreviationsEditorDialog();
            dialog.Abbreviations = oldAbbreviations;

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return oldAbbreviations;
            }

            return dialog.Abbreviations;
        }
    }
}
