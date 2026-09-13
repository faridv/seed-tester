using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace RockeyPasswordTester
{
    // Streaming scanner for the (20 GB+) brute-force CSV logs. Finds every seed in a range that is
    // "missing" - either logged with an error/exception password, or not present at all (a gap).
    //
    // It never loads the file into memory. Instead it makes ONE forward pass over the bytes and sets
    // one bit per seed that has a VALID password. A byte-level parser reads only the first field
    // (the seed) and the status field of each line, so it keeps up with disk throughput. After the
    // pass, every UNSET bit in the range is a seed that needs re-testing - this single bitmap covers
    // both error rows and gaps at once.
    //
    // Memory: one bit per seed in the range. e.g. 11111111->22222222 is ~286M seeds = ~36 MB. Even
    // the full 32-bit space is ~512 MB, which a .NET byte[] handles.
    internal static class LogScanner
    {
        // Extracts a seed range from a filename such as "....\11111111_22222222.csv"
        // -> start = 0x11111111, stopExclusive = 0x22222222.
        public static bool TryParseRangeFromFileName(string path, out uint start, out uint stopExclusive)
        {
            start = 0;
            stopExclusive = 0;
            string name = Path.GetFileNameWithoutExtension(path);
            var m = Regex.Match(name, "([0-9A-Fa-f]{8})[_\\-]([0-9A-Fa-f]{8})");
            if (!m.Success) return false;
            start = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            stopExclusive = uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return stopExclusive > start;
        }

        public sealed class ScanResult
        {
            public byte[] Bitmap = Array.Empty<byte>();
            public uint Start;
            public uint StopExclusive;
            public long RowsScanned;
            public long ValidSeedsSeen;   // rows with a valid (SUCCESS) password inside the range
            public long MissingCount;     // seeds in the range with no valid password (errors + gaps)

            public long RangeCount => (long)StopExclusive - Start;
        }

        // Builds the presence bitmap. `progress(bytesRead, totalBytes)` is called periodically so the
        // UI can show how far the scan has got through the file.
        public static ScanResult Scan(string logPath, uint start, uint stopExclusive, CancellationToken ct, Action<long, long>? progress)
        {
            if (stopExclusive <= start) throw new ArgumentException("Stop seed must be greater than start seed.");

            long range = (long)stopExclusive - start;
            long bitmapBytes = (range + 7) / 8;
            if (bitmapBytes > int.MaxValue - 64)
                throw new InvalidOperationException("Seed range is too large to scan in one pass. Narrow the start/stop range.");

            var result = new ScanResult
            {
                Start = start,
                StopExclusive = stopExclusive,
                Bitmap = new byte[bitmapBytes]
            };
            byte[] bitmap = result.Bitmap;

            const int ReadSize = 1 << 22; // 4 MB reads
            byte[] buffer = new byte[ReadSize];
            // Carry buffer for a line that straddles a read boundary. Log lines are ~90 bytes; 64 KB is
            // far more than any real line but bounds worst-case memory if the file is malformed.
            byte[] carry = new byte[1 << 16];
            int carryLen = 0;

            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);
            long total = fs.Length;
            long readSoFar = 0;
            long lastProgress = 0;

            int n;
            while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                readSoFar += n;

                int lineStart = 0;
                for (int i = 0; i < n; i++)
                {
                    if (buffer[i] != (byte)'\n') continue;

                    if (carryLen > 0)
                    {
                        // Complete the straddling line: carry + buffer[lineStart..i].
                        int add = i - lineStart;
                        if (carryLen + add <= carry.Length)
                        {
                            Array.Copy(buffer, lineStart, carry, carryLen, add);
                            ProcessLine(carry, 0, carryLen + add, start, stopExclusive, bitmap, result);
                        }
                        carryLen = 0;
                    }
                    else
                    {
                        ProcessLine(buffer, lineStart, i - lineStart, start, stopExclusive, bitmap, result);
                    }
                    lineStart = i + 1;
                }

                // Stash the trailing partial line (no newline yet) for the next read.
                int rem = n - lineStart;
                if (rem > 0)
                {
                    if (carryLen + rem <= carry.Length)
                    {
                        Array.Copy(buffer, lineStart, carry, carryLen, rem);
                        carryLen += rem;
                    }
                    else
                    {
                        // Pathologically long line - drop the carry so we resync at the next newline.
                        carryLen = 0;
                    }
                }

                if (progress != null && readSoFar - lastProgress >= (1L << 28)) // every ~256 MB
                {
                    progress(readSoFar, total);
                    lastProgress = readSoFar;
                }
            }

            // Trailing line with no final newline.
            if (carryLen > 0)
                ProcessLine(carry, 0, carryLen, start, stopExclusive, bitmap, result);

            progress?.Invoke(total, total);

            // Count the misses now so the caller can show a total up front.
            long missing = 0;
            for (uint v = start; v < stopExclusive; v++)
            {
                long idx = v - start;
                if ((bitmap[(int)(idx >> 3)] & (1 << (int)(idx & 7))) == 0) missing++;
                if (v == uint.MaxValue) break; // guard against wrap when stopExclusive == 0 (never, but safe)
            }
            result.MissingCount = missing;
            return result;
        }

        // Parses one CSV line (a byte span) and, if it is a valid-password row for a seed inside the
        // range, sets that seed's bit. Reads only field 0 (seed) and field 6 (status).
        private static void ProcessLine(byte[] buf, int off, int len, uint start, uint stopExclusive, byte[] bitmap, ScanResult result)
        {
            if (len <= 0) return;
            int end = off + len;

            int p = off;
            if (buf[p] == (byte)'"') p++;

            // Read up to 8 hex digits for the seed.
            uint seed = 0;
            int hexCount = 0;
            while (p < end && hexCount < 9)
            {
                int d = HexVal(buf[p]);
                if (d < 0) break;
                seed = (seed << 4) | (uint)d;
                hexCount++;
                p++;
            }
            if (hexCount != 8) return; // header line or non-8-hex seed: ignore

            // The char after the seed must be a delimiter (closing quote or comma), else it isn't a seed.
            if (p >= end) return;
            byte after = buf[p];
            if (after != (byte)'"' && after != (byte)',') return;

            result.RowsScanned++;

            if (seed < start || seed >= stopExclusive) return;

            // Find field 6 (Status) by counting the field-separating commas from the line start.
            int commas = 0;
            int statusStart = -1;
            for (int k = off; k < end; k++)
            {
                if (buf[k] == (byte)',')
                {
                    commas++;
                    if (commas == 6) { statusStart = k + 1; break; }
                }
            }
            // Status "SUCCESS" begins with 'S'; "ERROR_x" and "EXCEPTION" begin with 'E'. A single
            // byte tells success from failure.
            bool success = statusStart >= 0 && statusStart < end && (buf[statusStart] == (byte)'S' || buf[statusStart] == (byte)'s');
            if (!success) return;

            long idx = (long)seed - start;
            int byteIdx = (int)(idx >> 3);
            int bit = 1 << (int)(idx & 7);
            if ((bitmap[byteIdx] & bit) == 0)
            {
                bitmap[byteIdx] |= (byte)bit;
                result.ValidSeedsSeen++;
            }
        }

        private static int HexVal(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9') return b - (byte)'0';
            if (b >= (byte)'A' && b <= (byte)'F') return b - (byte)'A' + 10;
            if (b >= (byte)'a' && b <= (byte)'f') return b - (byte)'a' + 10;
            return -1;
        }

        // Folds another CSV's valid rows into an existing scan's bitmap (e.g. a previously produced
        // .repaired.csv), so re-running Repair doesn't re-test seeds that were already filled in.
        // Recomputes MissingCount afterwards. Missing/other files are ignored.
        public static void MarkPresentFrom(ScanResult scan, string path, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            const int ReadSize = 1 << 22;
            byte[] buffer = new byte[ReadSize];
            byte[] carry = new byte[1 << 16];
            int carryLen = 0;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan))
            {
                int n;
                while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int lineStart = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (buffer[i] != (byte)'\n') continue;
                        if (carryLen > 0)
                        {
                            int add = i - lineStart;
                            if (carryLen + add <= carry.Length)
                            {
                                Array.Copy(buffer, lineStart, carry, carryLen, add);
                                ProcessLine(carry, 0, carryLen + add, scan.Start, scan.StopExclusive, scan.Bitmap, scan);
                            }
                            carryLen = 0;
                        }
                        else ProcessLine(buffer, lineStart, i - lineStart, scan.Start, scan.StopExclusive, scan.Bitmap, scan);
                        lineStart = i + 1;
                    }
                    int rem = n - lineStart;
                    if (rem > 0)
                    {
                        if (carryLen + rem <= carry.Length) { Array.Copy(buffer, lineStart, carry, carryLen, rem); carryLen += rem; }
                        else carryLen = 0;
                    }
                }
                if (carryLen > 0) ProcessLine(carry, 0, carryLen, scan.Start, scan.StopExclusive, scan.Bitmap, scan);
            }

            long missing = 0;
            byte[] bitmap = scan.Bitmap;
            for (uint v = scan.Start; v < scan.StopExclusive; v++)
            {
                long idx = (long)v - scan.Start;
                if ((bitmap[(int)(idx >> 3)] & (1 << (int)(idx & 7))) == 0) missing++;
                if (v == uint.MaxValue) break;
            }
            scan.MissingCount = missing;
        }

        // Enumerates the missing seeds (bit unset) as 8-hex strings, in ascending order.
        public static IEnumerable<string> EnumerateMissing(ScanResult scan)
        {
            byte[] bitmap = scan.Bitmap;
            for (uint v = scan.Start; v < scan.StopExclusive; v++)
            {
                long idx = (long)v - scan.Start;
                if ((bitmap[(int)(idx >> 3)] & (1 << (int)(idx & 7))) == 0)
                    yield return v.ToString("X8", CultureInfo.InvariantCulture);
                if (v == uint.MaxValue) break;
            }
        }
    }
}
