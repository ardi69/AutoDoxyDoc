using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Type converter for abbreviation map.
    /// </summary>
    class AbbreviationMapConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, System.Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            else
            {
                return base.CanConvertFrom(context, sourceType);
            }
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
                                         object value, Type destinationType)
        {
            if ((destinationType == typeof(string)) && (value.GetType() == typeof(AbbreviationMap)))
            {
                AbbreviationMap abbreviations = value as AbbreviationMap;
                string str = String.Join(";", Array.ConvertAll(abbreviations.Values.ToArray(), i => i.Key + "," + i.Value));
                return str;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context,
                                           CultureInfo culture, object value)
        {
            if (value != null && value is string)
            {
                AbbreviationMap abbreviations = new AbbreviationMap();
                string str = value as string;
                string[] entries = str.Split(';');

                foreach (string e in entries)
                {
                    string[] parts = e.Split(',');

                    if (parts.Length == 2)
                    {
                        abbreviations.Add(parts[0], parts[1]);
                    }
                }

                return abbreviations;
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
