using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace AutoDoxyDoc
{
    public partial class AbbreviationsEditorDialog : Form
    {
        /// <summary>
        /// The abbreviations.
        /// </summary>
        public AbbreviationMap Abbreviations
        {
            get
            {
                AbbreviationMap abbreviations = new AbbreviationMap();

                foreach (AbbreviationEntry entry in m_entries)
                {
                    // Remove duplicates and empty ones.
                    if (entry.Abbreviation != "" && !abbreviations.Contains(entry.Abbreviation))
                    {
                        abbreviations.Add(entry.Abbreviation, entry.Unabbreviated);
                    }
                }

                return abbreviations;
            }

            set
            {
                foreach (KeyValuePair<string, string> abbreviation in value.Values)
                {
                    m_entries.Add(new AbbreviationEntry(abbreviation.Key, abbreviation.Value));
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public AbbreviationsEditorDialog()
        {
            InitializeComponent();
            dataGridView.DataSource = m_entries;
        }

        /// <summary>
        /// Adds new empty entry to the table.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddEntry(object sender, EventArgs e)
        {
            AbbreviationEntry entry = new AbbreviationEntry("", "");
            m_entries.Add(entry);
            dataGridView.CurrentCell = dataGridView.Rows[m_entries.Count - 1].Cells[0];
            dataGridView.BeginEdit(true);
        }

        /// <summary>
        /// Deletes currently selected entries from the table.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteSelectedEntries(object sender, EventArgs e)
        {
            foreach (DataGridViewCell cell in dataGridView.SelectedCells)
            {
                DataGridViewRow row = cell.OwningRow;

                if (dataGridView.CurrentRow == row)
                {
                    dataGridView.CurrentCell = null;
                }

                if (!row.IsNewRow)
                {
                    dataGridView.Rows.Remove(row);
                }
            }
        }

        /// <summary>
        /// Handles key shortcuts for editing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Insert)
            {
                AddEntry(this, new EventArgs());
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedEntries(this, new EventArgs());
            }
        }

        //! Abbreviation entries.
        private BindingList<AbbreviationEntry> m_entries = new BindingList<AbbreviationEntry>();
    }
}
