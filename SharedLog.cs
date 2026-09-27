using System;
using System.IO;
using System.Text;

namespace RockeyPasswordTester
{
    // A single CSV log shared by several dongle workers. Each worker buffers its rows and flushes a
    // block at a time, so the cross-worker lock is taken only once per ~100 rows (the RY_SEED calls,
    // not the file, are the bottleneck). Writes the header once if the file is new/empty; otherwise it
    // appends, so an existing mappings file is continued rather than overwritten.
    internal sealed class LogSink : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();
        private bool _disposed;

        public LogSink(string path)
        {
            bool existed = File.Exists(path) && new FileInfo(path).Length > 0;
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            if (!existed)
            {
                _writer.WriteLine(SeedUtil.CsvHeader);
                _writer.Flush();
            }
        }

        public void WriteBlock(string block)
        {
            if (string.IsNullOrEmpty(block)) return;
            lock (_lock)
            {
                if (_disposed) return;
                _writer.Write(block);
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                try { _writer.Flush(); } catch { }
                try { _writer.Dispose(); } catch { }
            }
        }
    }
}
