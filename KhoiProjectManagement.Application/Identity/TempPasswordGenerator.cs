using System.Security.Cryptography;

namespace KhoiProjectManagement.Application
{
    // Server-generated temp password for admin-created accounts (see
    // UserService.CreateUserWithTempPasswordAsync) - crypto-random, not System.Random, matching the
    // same RandomNumberGenerator convention AuthService already uses for refresh tokens. Excludes
    // visually ambiguous characters (0/O, 1/l/I) since this gets read off an email and typed by hand.
    public static class TempPasswordGenerator
    {
        private const string Lower = "abcdefghjkmnpqrstuvwxyz";
        private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        private const string Digits = "23456789";
        private const string Symbols = "!@#$%&*";
        private const string AllChars = Lower + Upper + Digits + Symbols;

        public static string Generate(int length = 12)
        {
            // Guarantee at least one of each category, then fill the rest randomly, then shuffle -
            // otherwise a purely random draw from AllChars could (rarely) miss a category entirely.
            var chars = new char[length];
            chars[0] = PickFrom(Lower);
            chars[1] = PickFrom(Upper);
            chars[2] = PickFrom(Digits);
            chars[3] = PickFrom(Symbols);

            for (var i = 4; i < length; i++)
            {
                chars[i] = PickFrom(AllChars);
            }

            // Fisher-Yates shuffle so the guaranteed categories aren't always in positions 0-3.
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }

        private static char PickFrom(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
