using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Collection of abbreviations.
    /// </summary>
    [TypeConverter(typeof(AbbreviationMapConverter))]
    [SettingsSerializeAs(SettingsSerializeAs.String)]
    public class AbbreviationMap
    {
        /// <summary>
        /// Abbreviations as key-value pairs.
        /// </summary>
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public AbbreviationMap()
        {
        }

        /// <summary>
        /// Adds a new abbreviation to the collection.
        /// </summary>
        /// <param name="abbreviation">Abbreviated word.</param>
        /// <param name="unabbreviated">Equivalent unabbreviated word.</param>
        public void Add(string abbreviation, string unabbreviated)
        {
            Values.Add(abbreviation, unabbreviated);
        }

        /// <summary>
        /// Checks if the collection contains the given abbreviation.
        /// </summary>
        /// <param name="abbreviation">Abbreviation to check.</param>
        /// <returns>True if found. Otherwise false.</returns>
        public bool Contains(string abbreviation)
        {
            return Values.ContainsKey(abbreviation);
        }

        /// <summary>
        /// Unabbreviates the given word using the collection.
        /// </summary>
        /// <param name="abbreviation">The word to unabbreviate.</param>
        /// <returns>Unabbreviated word, or the original word if the abbreviation was not found.</returns>
        public string Unabbreviate(string abbreviation)
        {
            if (Values.ContainsKey(abbreviation))
            {
                return Values[abbreviation];
            }

            return abbreviation;
        }
    }
}
