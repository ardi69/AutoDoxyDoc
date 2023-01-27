using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoDoxyDoc
{
    class StringHelper
    {
        /// <summary>
        /// Change the first letter of a string to capital.
        /// </summary>
        /// <param name="word">The string to capitalize.</param>
        /// <returns>Capitalized string.</returns>
        public static string Capitalize(string str)
        {
            if (String.IsNullOrEmpty(str))
            {
                return String.Empty;
            }

            return Char.ToUpper(str[0]) + str.Substring(1);
        }

        /// <summary>
        /// Splits a camel case string to individual words. Converts them to lower case too.
        /// </summary>
        /// <param name="str">The string to split.</param>
        /// <returns></returns>
        public static string[] SplitCamelCase(string str)
        {
            string spacedString = Regex.Replace(str, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim().ToLower();
            return spacedString.Split(new char[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Converts the given verb to third person format.
        /// </summary>
        /// <param name="verb">The verb to convert.</param>
        /// <returns>The verb with third person ending.</returns>
        public static string GetThirdPersonVerb(string verb)
        {
            if (verb == "do")
            {
                return "does";
            }
            else if (verb.EndsWith("y"))
            {
                return verb.Substring(0, verb.Length - 1) + "ies";
            }
            else if (Regex.IsMatch(verb, @"[ch|s|sh|x|z]$", RegexOptions.Compiled))
            {
                return verb + "es";
            }
            else
            {
                return verb + "s";
            }
        }
    }
}
