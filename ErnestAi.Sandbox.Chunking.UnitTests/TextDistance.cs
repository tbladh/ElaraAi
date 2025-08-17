using System;
using System.Collections.Generic;
using System.Linq;

namespace ErnestAi.Sandbox.Chunking.UnitTests
{
    internal static class TextDistance
    {
        // Async-first policy is N/A for pure CPU helpers; provide sync only
        public static double CharacterErrorRate(string reference, string hypothesis)
        {
            reference ??= string.Empty;
            hypothesis ??= string.Empty;
            var refChars = reference.ToCharArray();
            var hypChars = hypothesis.ToCharArray();
            int d = Levenshtein(refChars, hypChars);
            return refChars.Length == 0 ? (hypChars.Length == 0 ? 0.0 : 1.0) : (double)d / refChars.Length;
        }

        public static double WordErrorRate(string reference, string hypothesis)
        {
            var refWords = Tokenize(reference);
            var hypWords = Tokenize(hypothesis);
            int d = Levenshtein(refWords, hypWords);
            return refWords.Length == 0 ? (hypWords.Length == 0 ? 0.0 : 1.0) : (double)d / refWords.Length;
        }

        private static string[] Tokenize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim().ToLowerInvariant())
                       .ToArray();
        }

        private static int Levenshtein<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) where T : IEquatable<T>
        {
            int n = a.Count, m = b.Count;
            if (n == 0) return m;
            if (m == 0) return n;
            var prev = new int[m + 1];
            var cur = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;
            for (int i = 1; i <= n; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1].Equals(b[j - 1]) ? 0 : 1;
                    cur[j] = Math.Min(
                        Math.Min(cur[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                Array.Copy(cur, prev, m + 1);
            }
            return prev[m];
        }

        private static int Levenshtein(char[] a, char[] b)
        {
            int n = a.Length, m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;
            var prev = new int[m + 1];
            var cur = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;
            for (int i = 1; i <= n; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(
                        Math.Min(cur[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                Array.Copy(cur, prev, m + 1);
            }
            return prev[m];
        }
    }
}
