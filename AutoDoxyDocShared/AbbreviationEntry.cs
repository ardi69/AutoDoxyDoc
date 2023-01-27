using System.ComponentModel;

namespace AutoDoxyDoc
{
    public class AbbreviationEntry : INotifyPropertyChanged
    {
        //! Occurs when a property has been changed.
        public event PropertyChangedEventHandler PropertyChanged;

        //! Abbreviation.
        public string Abbreviation
        {
            get { return m_abbreviation; }

            set
            {
                if (m_abbreviation != value)
                {
                    m_abbreviation = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Abbreviation"));
                }
            }
        }

        //! Unabbreviated version.
        public string Unabbreviated
        {
            get { return m_unabbreviated; }

            set
            {
                if (m_unabbreviated != value)
                {
                    m_unabbreviated = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Unabbreviated"));
                }
            }
        }

        public AbbreviationEntry(string abbreviation, string unabbreviated)
        {
            m_abbreviation = abbreviation;
            m_unabbreviated = unabbreviated;
        }

        //! Abbreviation.
        private string m_abbreviation = "";

        //! Unabbreviated version.
        private string m_unabbreviated = "";
    }
}
