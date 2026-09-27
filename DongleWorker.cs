using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RockeyPasswordTester
{
    // A source of seeds to test. Per-dongle sources (brute-force sub-range, dictionary slice) are
    // used by a single thread and need no locking; the repair source is shared by all workers and
    // hands out missing seeds under a lock so each seed is tested exactly once.
    internal abstract class SeedSource
    {
        public abstract bool TryGetNext(out string seed);

        public static SeedSource FromEnumerable(IEnumerable<string> seq, bool shared) =>
            new EnumeratorSeedSource(seq, shared);

        private sealed class EnumeratorSeedSource : SeedSource
        {
            private readonly IEnumerator<string> _e;
            private readonly object? _lock;
            public EnumeratorSeedSource(IEnumerable<string> seq, bool shared)
            {
                _e = seq.GetEnumerator();
                _lock = shared ? new object() : null;
            }
            public override bool TryGetNext(out string seed)
            {
                if (_lock != null)
                {
                    lock (_lock)
                    {
                        if (_e.MoveNext()) { seed = _e.Current; return true; }
                        seed = string.Empty; return false;
                    }
                }
                if (_e.MoveNext()) { seed = _e.Current; return true; }
                seed = string.Empty; return false;
            }
        }
    }

    // Drives ONE physical dongle on its own thread: opens it, tests its assigned seeds, logs to its
    // own file, and - crucially - heals itself when the dongle wedges, WITHOUT ever stopping the
    // other workers and WITHOUT touching any USB device other than a Rockey dongle.
    //
    // Recovery ladder when RY_SEED stops responding:
    //   1. a couple of immediate quick retries of the same seed (shrugs off a one-off glitch);
    //   2. soft reopen - close and re-open the SDK handle (fixes most "stuck session" wedges);
    //   3. safe device reset - disable+enable the dongle's own PnP node via UsbReset, then reopen.
    // Only when the whole ladder fails does the worker give up - and even then just this one dongle
    // stops; the rest keep running. This is why an overnight run no longer needs a human to replug.
    internal sealed class DongleWorker
    {
        private const ushort RY_FIND = 1;
        private const ushort RY_FIND_NEXT = 2;
        private const ushort RY_OPEN = 3;
        private const ushort RY_CLOSE = 4;
        private const ushort RY_SEED = 8;

        private const int FIND_RETRY_ATTEMPTS = 5;
        private const int QUICK_RETRIES = 2;         // re-issue RY_SEED on the same seed before logging an error
        private const int WEDGE_THRESHOLD = 5;        // consecutive failing seeds that mean "the dongle wedged"
        private const int SOFT_REOPEN_ROUNDS = 2;    // close+reopen the handle
        private const int DEVICE_RESET_ROUNDS = 2;    // disable+enable the PnP node, then reopen
        private const int HUB_CYCLE_ROUNDS = 2;       // opt-in: power-cycle the dongle's parent hub
        private const int REOPEN_WAIT_MS = 1500;
        private const int DEVICE_RESET_WAIT_MS = 8000;

        public sealed class Config
        {
            public int Index;                    // display index / dongle number
            public uint? TargetHid;              // SDK hardware id to bind to (robust to enumeration order)
            public string? UsbInstanceId;        // Windows PnP instance id for a targeted reset
            public string PortLabel = "port unknown"; // physical USB port, for "which dongle to replug"
            public ushort P1, P2, P3, P4;        // dongle parameters
            public string TargetPassword = string.Empty;
            public SpeedMode Speed = SpeedMode.Balanced;
            public SeedSource Source = null!;
            public string LogPath = string.Empty;      // own log file (per-dongle mode); ignored if SharedLog set
            public LogSink? SharedLog;                  // when set, all workers write to this one file
            // Shared arbiter: RY_SEED takes the READ lock (dongles seed concurrently); FIND/OPEN/CLOSE
            // take the WRITE lock, which briefly pauses every other dongle's seeding so a recovery on
            // one dongle can't knock the others offline (the cascade we saw in the logs).
            public ReaderWriterLockSlim SdkLock = null!;
            public bool SerializeSeedCalls;       // make RY_SEED exclusive too (kills concurrency; use if concurrent seeds corrupt)
            public bool AllowHubCycle;            // opt-in: last-resort power-cycle of the dongle's parent hub
            public bool IsRepair;
            public Action<string> Log = _ => { };
            public Func<bool> IsPaused = () => false;
            public Action<DongleWorker>? OnMatch;
        }

        private readonly Config _cfg;
        private readonly CancellationToken _ct;

        // Live counters read by the UI aggregation timer.
        private long _tested;
        private long _generated;
        private long _verified;
        public long Tested => Volatile.Read(ref _tested);
        public long Generated => Volatile.Read(ref _generated);
        public long Verified => Volatile.Read(ref _verified);

        private volatile string _lastSeed = string.Empty;
        public string LastSeed => _lastSeed;
        private volatile string _status = "Idle";
        public string Status => _status;
        public bool Finished { get; private set; }
        public bool Failed { get; private set; }
        public string FailReason { get; private set; } = string.Empty;
        public string? MatchSeed { get; private set; }
        public string? MatchPassword { get; private set; }
        public int Index => _cfg.Index;

        // Physical identity for "which dongle do I replug?" messages.
        public string Identity => PhysId;
        private string PhysId => _cfg.TargetHid.HasValue
            ? $"dongle #{_cfg.Index} [{_cfg.PortLabel}, HID 0x{_cfg.TargetHid.Value:X8}]"
            : $"dongle #{_cfg.Index} [{_cfg.PortLabel}]";

        // Created inside Run() on the worker's OWN STA thread. The native Rockey SDK object has
        // thread affinity (it expects to be created and called on one STA apartment); constructing it
        // on the UI thread and calling it here would make RY_FIND/RY_OPEN fail.
        private Rockey4SmartClass.Rockey4Smart _r4s = null!;
        private ushort _handle;
        private bool _handleOpen;
        private ushort _chainP1, _chainP2, _chainP3, _chainP4; // RY_SEED input chain (mirrors legacy behaviour)
        private uint _lastGoodSeed = 0x11111111;               // a seed known to work, used to probe after recovery
        private readonly byte[] _buffer = new byte[1024];


        private StreamWriter? _writer;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private int _bufferedLines;
        private bool _flushedFirst;

        private readonly int _yieldInterval;

        public DongleWorker(Config cfg, CancellationToken ct)
        {
            _cfg = cfg;
            _ct = ct;
            _yieldInterval = cfg.Speed == SpeedMode.Fast ? 50 : cfg.Speed == SpeedMode.Balanced ? 100 : 200;
        }

        // Entry point for the worker's dedicated STA thread.
        public void Run()
        {
            try
            {
                // Must be created on THIS STA thread (see field comment), not in the constructor.
                // Exclusive so several workers starting at once don't race the SDK's one-time native init.
                _cfg.SdkLock.EnterWriteLock();
                try { _r4s = new Rockey4SmartClass.Rockey4Smart(); }
                finally { _cfg.SdkLock.ExitWriteLock(); }

                OpenLog();

                _status = "Opening dongle...";
                if (!EnsureOpenWithRecovery())
                {
                    Failed = true;
                    FailReason = "could not open the dongle";
                    _status = "Failed: dongle not opened";
                    _cfg.Log($"[Dongle {_cfg.Index}] Could not open {PhysId} after retries. UNPLUG & REPLUG that dongle. This one is stopped; others continue.");
                    return;
                }

                _cfg.Log($"[Dongle {_cfg.Index}] Connected: {PhysId} (handle 0x{_handle:X4}). Testing...");
                _status = "Testing";
                RunLoop();

                if (_ct.IsCancellationRequested) _status = "Stopped";
                else if (Failed) _status = "Failed: " + FailReason;
                else { Finished = true; _status = "Done"; }
            }
            catch (OperationCanceledException) { _status = "Stopped"; }
            catch (Exception ex)
            {
                Failed = true;
                FailReason = ex.Message;
                _status = "Error: " + ex.Message;
                _cfg.Log($"[Dongle {_cfg.Index}] Error: {ex.Message}");
            }
            finally
            {
                CloseHandle();
                FlushLog();
                try { _writer?.Dispose(); } catch { }
                _writer = null;
                // Release the native SDK object too. A lingering open handle/session keeps the dongle
                // "in use", which makes a subsequent device reset a no-op - the reason a reset only
                // worked after the whole app was closed. Dropping + finalizing this lets an in-app
                // reset actually take.
                try { (_r4s as IDisposable)?.Dispose(); } catch { }
                _r4s = null!;
            }
        }

        private void RunLoop()
        {
            int consecutiveErrors = 0;

            while (!_ct.IsCancellationRequested)
            {
                WaitWhilePaused();
                if (_ct.IsCancellationRequested) break;

                if (!_cfg.Source.TryGetNext(out string seed))
                    break; // range/list/repair set exhausted for this worker

                if (!SeedUtil.TryConvertSeedToUint(seed, out uint seedValue))
                {
                    // A malformed seed (only possible from a hand-made dictionary file). Record it and
                    // move on - it is not a dongle fault.
                    WriteRow(seed, "EXCEPTION", 0, 0, 0, 0, "EXCEPTION", false);
                    Interlocked.Increment(ref _tested);
                    _lastSeed = seed;
                    continue;
                }

                ushort rc = SeedWithQuickRetries(seedValue, out ushort p1, out ushort p2, out ushort p3, out ushort p4);

                if (rc == 0)
                {
                    RecordSuccess(seed, p1, p2, p3, p4);
                    consecutiveErrors = 0;
                    if (MatchSeed != null) break; // target found
                    continue;
                }

                // This seed didn't produce a password. Log it as an error row and keep going - a few
                // scattered errors are normal and get filled in later by Repair. We only treat it as a
                // real wedge once a RUN of consecutive seeds all fail (the dongle has stopped
                // responding), which matches how the tool always behaved.
                WriteRow(seed, "ERROR", 0, 0, 0, 0, $"ERROR_{rc}", false);
                Interlocked.Increment(ref _tested);
                _lastSeed = seed;
                consecutiveErrors++;
                if (consecutiveErrors < WEDGE_THRESHOLD)
                    continue;

                _cfg.Log($"[Dongle {_cfg.Index}] Stopped responding ({consecutiveErrors} seeds in a row failed, last code {rc}) around {seed}. Recovering...");
                _status = "Recovering";
                bool recovered = RecoverDongle();
                if (recovered)
                {
                    _cfg.Log($"[Dongle {_cfg.Index}] Recovered - the dongle is generating passwords again. Continuing.");
                    _status = "Testing";
                    consecutiveErrors = 0;
                    continue;
                }

                Failed = true;
                FailReason = _handleOpen ? "dongle still not generating passwords after recovery" : "dongle did not come back";
                string tip = _cfg.AllowHubCycle ? "" : " Tip: if this dongle is on a dedicated USB hub, enable 'Hub power-cycle on wedge'.";
                _cfg.Log($"[Dongle {_cfg.Index}] Could not recover — UNPLUG & REPLUG {PhysId}. Stopping it only (others keep running). Resume point: {seed}.{tip}");
                break;
            }

            FlushLog();
        }

        private ushort SeedWithQuickRetries(uint seedValue, out ushort p1, out ushort p2, out ushort p3, out ushort p4)
        {
            ushort rc = DoSeed(seedValue, out p1, out p2, out p3, out p4);
            for (int q = 0; rc != 0 && q < QUICK_RETRIES && !_ct.IsCancellationRequested; q++)
            {
                if (_ct.WaitHandle.WaitOne(150)) break;
                rc = DoSeed(seedValue, out p1, out p2, out p3, out p4);
            }
            return rc;
        }

        private void RecordSuccess(string seed, ushort p1, ushort p2, ushort p3, ushort p4)
        {
            string pw = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
            _chainP1 = p1; _chainP2 = p2; _chainP3 = p3; _chainP4 = p4;
            if (SeedUtil.TryConvertSeedToUint(seed, out uint sv)) _lastGoodSeed = sv; // health-probe seed
            WriteRow(seed, pw, p1, p2, p3, p4, "SUCCESS", true);
            long tested = Interlocked.Increment(ref _tested);
            Interlocked.Increment(ref _generated);
            Interlocked.Increment(ref _verified);
            _lastSeed = seed;

            if (!string.IsNullOrEmpty(_cfg.TargetPassword) &&
                pw.Equals(_cfg.TargetPassword, StringComparison.OrdinalIgnoreCase))
            {
                MatchSeed = seed;
                MatchPassword = pw;
                FlushLog();
                _cfg.OnMatch?.Invoke(this);
                return;
            }

            if (tested % _yieldInterval == 0) Thread.Sleep(1);
        }

        // Issues one RY_SEED, mirroring the legacy call exactly (seed in lp2; the previous password is
        // fed back as p1..p4; the new password comes back in p1..p4). Optionally serialised.
        private ushort DoSeed(uint seedValue, out ushort p1, out ushort p2, out ushort p3, out ushort p4)
        {
            if (!_handleOpen) { p1 = p2 = p3 = p4 = 0; return 0xFFFF; }

            uint lp1 = 0, lp2 = seedValue;
            Array.Clear(_buffer, 0, _buffer.Length);
            p1 = _chainP1; p2 = _chainP2; p3 = _chainP3; p4 = _chainP4;
            ushort handle = _handle;

            // Concurrent seeds hold the read lock; a recovery/open (write lock) will pause them.
            bool exclusive = _cfg.SerializeSeedCalls;
            if (exclusive) _cfg.SdkLock.EnterWriteLock(); else _cfg.SdkLock.EnterReadLock();
            try
            {
                return _r4s.Rockey(RY_SEED, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, _buffer);
            }
            finally
            {
                if (exclusive) _cfg.SdkLock.ExitWriteLock(); else _cfg.SdkLock.ExitReadLock();
            }
        }

        // The escalating recovery ladder. Each round reopens (soft), or resets the device node, or
        // (opt-in) power-cycles the parent hub, then probes a KNOWN-GOOD seed to confirm the dongle is
        // really generating passwords again. Probing a known-good seed - not the seed we wedged on -
        // avoids being fooled if that particular seed is what triggers the fault. Returns true as soon
        // as the probe succeeds; false when the whole ladder is exhausted.
        private bool RecoverDongle()
        {
            // Auto-recovery is SOFT ONLY: close and re-open the SDK handle. It NEVER disables a device
            // or power-cycles a hub. Automatic Disable/Enable-PnpDevice was what kept stranding dongles
            // in a Disabled state (Enable can fail with "Generic failure"), which then blocked the SDK
            // for ALL dongles and forced a Windows restart. A hard wedge that a reopen can't fix is
            // reported for a physical replug instead. (The Reset / Cycle-Hub buttons remain for the user
            // to invoke deliberately.)
            for (int round = 0; round < SOFT_REOPEN_ROUNDS + 1; round++)
            {
                if (_ct.IsCancellationRequested) return false;
                _cfg.Log($"[Dongle {_cfg.Index}] Recovery {round + 1}/{SOFT_REOPEN_ROUNDS + 1}: reopening the dongle...");
                bool opened = SoftReopen();
                if (_ct.IsCancellationRequested) return false;
                if (!opened) continue;

                ushort rc = DoSeed(_lastGoodSeed, out _, out _, out _, out _); // health probe (not logged)
                if (rc == 0) return true; // dongle is generating passwords again
            }
            return false;
        }

        private bool SoftReopen()
        {
            CloseHandle();
            if (WaitCancellable(REOPEN_WAIT_MS)) return false;
            return OpenOnce();
        }

        // Initial open, with a couple of soft retries so a run can start through a transient blip.
        // Soft only - never disables the device (see RecoverDongle).
        private bool EnsureOpenWithRecovery()
        {
            if (OpenOnce()) return true;
            for (int round = 0; round < SOFT_REOPEN_ROUNDS; round++)
            {
                if (_ct.IsCancellationRequested) return false;
                if (SoftReopen()) return true;
            }
            return false;
        }

        // One FIND -> (walk to our dongle) -> OPEN attempt. Enumeration and handle lifecycle run under
        // the shared SDK gate because the native FIND cursor is process-global.
        private bool OpenOnce()
        {
            _cfg.SdkLock.EnterWriteLock(); // exclusive: pauses other dongles' seeding during FIND/OPEN
            try
            {
                try
                {
                    ushort handle = 0;
                    uint lp1 = 0, lp2 = 0;
                    ushort a = _cfg.P1, b = _cfg.P2, c = _cfg.P3, d = _cfg.P4;

                    ushort rc = 1;
                    for (int attempt = 0; attempt < FIND_RETRY_ATTEMPTS; attempt++)
                    {
                        if (_ct.IsCancellationRequested) return false;
                        a = _cfg.P1; b = _cfg.P2; c = _cfg.P3; d = _cfg.P4;
                        rc = _r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, _buffer);
                        if (rc == 0) break;
                        if (_ct.WaitHandle.WaitOne(1000)) return false;
                    }
                    if (rc != 0) return false;

                    uint hid = lp1;
                    if (_cfg.TargetHid.HasValue)
                    {
                        int guard = 0;
                        while (hid != _cfg.TargetHid.Value)
                        {
                            if (guard++ >= 64) return false;
                            a = _cfg.P1; b = _cfg.P2; c = _cfg.P3; d = _cfg.P4;
                            rc = _r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, _buffer);
                            if (rc != 0) return false;
                            hid = lp1;
                        }
                    }
                    else
                    {
                        for (int step = 0; step < _cfg.Index; step++)
                        {
                            a = _cfg.P1; b = _cfg.P2; c = _cfg.P3; d = _cfg.P4;
                            rc = _r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, _buffer);
                            if (rc != 0) return false;
                            hid = lp1;
                        }
                    }

                    // RY_OPEN needs the hardware id that RY_FIND / RY_FIND_NEXT left in lp1 (and lp2).
                    // Do NOT zero them here - doing so makes OPEN fail with code 5. Only refresh the
                    // P1..P4 parameters, exactly like the original working code did.
                    a = _cfg.P1; b = _cfg.P2; c = _cfg.P3; d = _cfg.P4;
                    rc = _r4s.Rockey(RY_OPEN, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, _buffer);
                    if (rc != 0) return false;

                    _handle = handle;
                    _handleOpen = true;
                    // Seed the RY_SEED input chain with the post-open parameters, exactly as before.
                    _chainP1 = a; _chainP2 = b; _chainP3 = c; _chainP4 = d;
                    return true;
                }
                catch
                {
                    _handleOpen = false;
                    return false;
                }
            }
            finally { _cfg.SdkLock.ExitWriteLock(); }
        }

        private void CloseHandle()
        {
            if (!_handleOpen) return;
            _cfg.SdkLock.EnterWriteLock();
            try
            {
                try
                {
                    ushort ch = _handle; uint c1 = 0, c2 = 0;
                    ushort a = _cfg.P1, b = _cfg.P2, c = _cfg.P3, d = _cfg.P4;
                    _r4s.Rockey(RY_CLOSE, ref ch, ref c1, ref c2, ref a, ref b, ref c, ref d, _buffer);
                }
                catch { }
                _handleOpen = false;
            }
            finally { _cfg.SdkLock.ExitWriteLock(); }
        }

        private void WaitWhilePaused()
        {
            while (_cfg.IsPaused() && !_ct.IsCancellationRequested)
                _ct.WaitHandle.WaitOne(50);
        }

        // Returns true if cancellation fired during the wait.
        private bool WaitCancellable(int ms) => _ct.WaitHandle.WaitOne(ms);

        // ---- logging ---------------------------------------------------------------------------

        private void OpenLog()
        {
            // Shared-log mode: all workers write to the one LogSink (opened by the form) - nothing to
            // open here. Per-dongle mode: open this worker's own file.
            if (_cfg.SharedLog != null) { _flushedFirst = true; return; }

            bool existed = File.Exists(_cfg.LogPath) && new FileInfo(_cfg.LogPath).Length > 0;
            _writer = new StreamWriter(new FileStream(_cfg.LogPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            if (!existed)
            {
                _writer.WriteLine(SeedUtil.CsvHeader);
                _writer.Flush();
                _flushedFirst = true;
            }
        }

        private void WriteRow(string seed, string passwordStr, ushort p1, ushort p2, ushort p3, ushort p4, string status, bool verifiedOK)
        {
            string row = SeedUtil.FormatLogRow(seed, passwordStr, p1, p2, p3, p4, status, verifiedOK);
            _logBuffer.AppendLine(row);
            _bufferedLines++;
            if (!_flushedFirst || _bufferedLines >= 100 || _logBuffer.Length > 100_000)
                FlushLog();
        }

        private void FlushLog()
        {
            if (_logBuffer.Length == 0) return;
            try
            {
                if (_cfg.SharedLog != null)
                {
                    _cfg.SharedLog.WriteBlock(_logBuffer.ToString());
                }
                else
                {
                    if (_writer == null) return;
                    _writer.Write(_logBuffer.ToString());
                    _writer.Flush();
                }
                _logBuffer.Clear();
                _bufferedLines = 0;
                _flushedFirst = true;
            }
            catch (Exception ex)
            {
                _cfg.Log($"[Dongle {_cfg.Index}] Log write error: {ex.Message}");
            }
        }
    }
}
