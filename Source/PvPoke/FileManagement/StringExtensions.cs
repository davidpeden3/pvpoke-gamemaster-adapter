using System;

namespace PvPoke.FileManagement
{
    public static class StringExtensions
    {
        public static string ToUpperFirstCharacter(this string s)
        {
            if (String.IsNullOrEmpty(s))
            {
                return String.Empty;
            }

            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);

            return new String(a);
        }
    }
}