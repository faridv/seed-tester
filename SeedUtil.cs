using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RockeyPasswordTester
{
    // Pure, stateless helpers shared by the UI, the per-dongle workers and the log repair.
    // Kept free of any dongle / UI state so they can be called from any thread.
    internal static class SeedUtil
    {
        // "11111111" (8 hex chars) -> 0x11111111. Rejects anything that is not exactly 8 hex digits.
        public static bool TryConvertSeedToUint(string seed, out uint seedValue)
        {
            seedValue = 0;
            if (seed.Length != 8) return false;
            for (int i = 0; i < seed.Length; i++)
            {
                char c = seed[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
                if (!isHex) return false;
            }
            return uint.TryParse(seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seedValue);
        }

        // 8-hex-digit, upper-case rendering of a 32-bit seed value.
        public static string SeedToHex(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);

        // Index of a seed within a (charset, length) space, e.g. base-16 for the default hex charset.
        // Used by brute-force to turn a "start/stop seed" into an absolute position in the space.
        public static bool TryGetCombinationIndex(string seed, string charset, int length, out long index)
        {
            index = 0;
            if (seed.Length != length || string.IsNullOrEmpty(charset)) return false;

            var charIndexes = new Dictionary<char, int>();
            for (int i = 0; i < charset.Length; i++)
                if (!charIndexes.ContainsKey(charset[i])) charIndexes.Add(charset[i], i);

            for (int i = 0; i < seed.Length; i++)
            {
                if (!charIndexes.TryGetValue(seed[i], out int charIndex)) return false;
                checked { index = (index * charset.Length) + charIndex; }
            }
            return true;
        }

        // Lazily enumerates combinations of `charset` of the given `length`, starting at absolute
        // index `startIndex` and stopping just before `stopExclusive` (an absolute index; null = run
        // to the end of the space).
        public static IEnumerable<string> GenerateCombinations(string charset, int length, long? stopExclusive, long startIndex)
        {
            var indices = new int[length];
            var charsetArray = charset.ToCharArray();
            long count = Math.Max(0, startIndex);
            long workingIndex = count;

            for (int i = length - 1; i >= 0; i--)
            {
                indices[i] = (int)(workingIndex % charsetArray.Length);
                workingIndex /= charsetArray.Length;
            }

            while (true)
            {
                if (stopExclusive.HasValue && count >= stopExclusive.Value) break;

                var combination = new char[length];
                for (int i = 0; i < length; i++) combination[i] = charsetArray[indices[i]];
                yield return new string(combination);

                int pos = length - 1;
                while (pos >= 0)
                {
                    indices[pos]++;
                    if (indices[pos] < charsetArray.Length) break;
                    indices[pos] = 0;
                    pos--;
                }
                if (pos < 0) break;
                count++;
            }
        }

        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return fields;

            var field = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; continue; }
                    inQuotes = !inQuotes;
                    continue;
                }
                if (current == ',' && !inQuotes) { fields.Add(field.ToString().Trim()); field.Clear(); continue; }
                field.Append(current);
            }
            fields.Add(field.ToString().Trim());
            return fields;
        }

        // Reads the last non-empty line of a (possibly huge) file by scanning backwards, so resuming
        // never loads the whole log. Shared with FileShare.ReadWrite so it works while a run is active.
        public static string ReadLastNonEmptyLine(string filePath)
        {
            const int BufferSize = 8192;
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0) return string.Empty;

            var lineBytes = new List<byte>();
            var buffer = new byte[BufferSize];
            long position = stream.Length;
            bool foundContent = false;

            while (position > 0)
            {
                int bytesToRead = (int)Math.Min(BufferSize, position);
                position -= bytesToRead;
                stream.Seek(position, SeekOrigin.Begin);
                int bytesRead = stream.Read(buffer, 0, bytesToRead);

                for (int i = bytesRead - 1; i >= 0; i--)
                {
                    byte current = buffer[i];
                    if (current == '\n' || current == '\r')
                    {
                        if (foundContent)
                        {
                            lineBytes.Reverse();
                            return Encoding.UTF8.GetString(lineBytes.ToArray()).Trim();
                        }
                        continue;
                    }
                    foundContent = true;
                    lineBytes.Add(current);
                }
            }
            lineBytes.Reverse();
            return Encoding.UTF8.GetString(lineBytes.ToArray()).Trim();
        }

        // The last seed logged in `logFilePath`, or "" if the file is missing/empty/header-only.
        public static string ReadLastLoggedSeed(string logFilePath)
        {
            if (!File.Exists(logFilePath)) return string.Empty;
            try
            {
                List<string> fields = ParseCsvLine(ReadLastNonEmptyLine(logFilePath));
                string seed = fields.Count > 0 ? fields[0] : string.Empty;
                if (!string.IsNullOrEmpty(seed) && !seed.Equals("Seed", StringComparison.OrdinalIgnoreCase))
                    return seed;
            }
            catch { /* best effort - treat as no resume point */ }
            return string.Empty;
        }

        public const string CsvHeader = "Seed,Password,Word1,Word2,Word3,Word4,Status,Verified,Timestamp";

        // Formats one log row exactly like the historical format so old and new logs stay uniform.
        public static string FormatLogRow(string seed, string passwordStr, ushort p1, ushort p2, ushort p3, ushort p4, string status, bool verifiedOK)
        {
            string word1 = verifiedOK ? p1.ToString("X4") : "N/A";
            string word2 = verifiedOK ? p2.ToString("X4") : "N/A";
            string word3 = verifiedOK ? p3.ToString("X4") : "N/A";
            string word4 = verifiedOK ? p4.ToString("X4") : "N/A";
            string verifiedStr = verifiedOK ? "YES" : "NO";
            return $"\"{seed}\",\"{passwordStr}\",{word1},{word2},{word3},{word4},{status},{verifiedStr},{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
