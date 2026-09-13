using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RockeyPasswordTester
{
    public enum SpeedMode { Fast, Balanced, Slow }
    public enum TestMode { Dictionary, BruteForce, Repair }

    public partial class MainForm : Form
    {
        private const ushort RY_FIND = 1;
        private const ushort RY_FIND_NEXT = 2;

        private CancellationTokenSource? _cts;
        private readonly List<DongleWorker> _workers = new List<DongleWorker>();
        private readonly object _sdkGate = new object();

        private Stopwatch _sw = new Stopwatch();
        private SpeedMode _speedMode = SpeedMode.Balanced;
        private TestMode _testMode = TestMode.Dictionary;
        private volatile bool _isPaused = false;

        private System.Windows.Forms.Timer _uiTimer = null!;
        private long _uiTotal;
        private readonly object _uiTotalLock = new object();
        private DongleWorker? _matchedWorker;

        // Multi-dongle / repair controls, built in code so the generated Designer layout is untouched.
        private GroupBox grpMultiDongle = null!;
        private CheckedListBox clbDongles = null!;
        private Button btnDetectDongles = null!;
        private Label lblDongleDetected = null!;
        private TextBox txtStartSeed = null!;
        private TextBox txtStopSeed = null!;
        private CheckBox chkSerialize = null!;
        private CheckBox chkHubRecover = null!;
        private Button btnResetSafe = null!;
        private Button btnCycleHub = null!;
        private Label lblMultiHint = null!;
        private Button btnModeRepair = null!;

        // One entry in the dongle picker. Index is the SDK enumeration position; Hid binds to the
        // exact dongle regardless of order; UsbInstanceId (best-effort) pairs it with a Windows device
        // for a targeted safe reset.
        private sealed class DongleOption
        {
            public int Index { get; init; }
            public uint? Hid { get; init; }
            public string? UsbInstanceId { get; init; }
            public static DongleOption FirstFound() => new DongleOption { Index = 0, Hid = null };
            public override string ToString() =>
                Hid.HasValue ? $"Dongle #{Index} — HID 0x{Hid.Value:X8}" : "Dongle #0 (first found)";
        }

        public MainForm()
        {
            InitializeComponent();
            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += UiTimer_Tick;
            UiTotal = 0;
            BuildMultiDongleUi();
            SetMode(TestMode.Dictionary); // initial styling + enabled state
        }

        private long UiTotal
        {
            get { lock (_uiTotalLock) { return _uiTotal; } }
            set { lock (_uiTotalLock) { _uiTotal = value; } }
        }

        // ---- UI construction (multi-dongle group + Repair mode button) ---------------------------

        private void BuildMultiDongleUi()
        {
            const int groupTop = 724; // where grpResults currently sits
            const int groupHeight = 152;
            const int shift = groupHeight + 8;

            grpMultiDongle = new GroupBox
            {
                Text = "Multi-Dongle & Recovery (run every checked dongle in parallel; work is split across them)",
                Location = new Point(13, groupTop),
                Size = new Size(730, groupHeight),
                TabStop = false
            };

            var toolTip = new ToolTip();

            var lblDongles = new Label { Text = "Dongles:", AutoSize = true, Location = new Point(8, 24) };
            clbDongles = new CheckedListBox
            {
                Location = new Point(70, 22),
                Size = new Size(250, 62),
                CheckOnClick = true,
                IntegralHeight = false
            };
            toolTip.SetToolTip(clbDongles, "Every checked dongle runs at the same time on its own thread. Click 'Detect Dongles' to fill this list.");
            clbDongles.Items.Add(DongleOption.FirstFound(), true);

            btnDetectDongles = new Button { Text = "Detect Dongles", Location = new Point(332, 22), Size = new Size(120, 26) };
            btnDetectDongles.Click += btnDetectDongles_Click;

            lblDongleDetected = new Label
            {
                Text = "Click 'Detect Dongles' to list connected dongles.",
                AutoSize = false,
                Location = new Point(332, 52),
                Size = new Size(388, 32),
                ForeColor = Color.Gray,
                AutoEllipsis = true
            };

            var lblStartSeed = new Label { Text = "Start seed:", AutoSize = true, Location = new Point(8, 96) };
            txtStartSeed = new TextBox { Location = new Point(78, 93), Size = new Size(84, 23), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip.SetToolTip(txtStartSeed, "Overall 8-hex start of the range (brute-force) or of the log to repair. Empty = beginning. Split evenly across the checked dongles.");

            var lblStopSeed = new Label { Text = "Stop before:", AutoSize = true, Location = new Point(172, 96) };
            txtStopSeed = new TextBox { Location = new Point(250, 93), Size = new Size(84, 23), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip.SetToolTip(txtStopSeed, "Overall 8-hex end of the range (exclusive). Empty = end of the charset space, or parsed from the log filename in Repair mode.");

            chkSerialize = new CheckBox { Text = "Serialize dongle I/O", AutoSize = true, Location = new Point(352, 95) };
            toolTip.SetToolTip(chkSerialize, "Only tick this if multiple dongles produce corrupt results together. It forces one dongle call at a time (slower) in case the SDK isn't thread-safe on this machine.");

            chkHubRecover = new CheckBox { Text = "Hub power-cycle on wedge", AutoSize = true, Location = new Point(510, 95) };
            toolTip.SetToolTip(chkHubRecover,
                "Last-resort recovery for a hard wedge (dongle comes back as 'USB device not recognized'). " +
                "Re-powers the dongle's PARENT HUB, which briefly disconnects EVERY device on that same hub. " +
                "Never cycles a root hub. Safe only if the dongle is on a hub with nothing else you care about " +
                "(ideally a small dedicated USB hub). Off = on a hard wedge the app stops that dongle and asks you to replug.");

            var lblRecovery = new Label { Text = "Recovery:", AutoSize = true, Location = new Point(8, 128) };
            btnResetSafe = new Button { Text = "Reset Dongle (safe)", Location = new Point(78, 123), Size = new Size(150, 26) };
            btnResetSafe.Click += async (s, e) => await ManualSafeResetAsync();
            toolTip.SetToolTip(btnResetSafe, "Disable+enable / restart ONLY the Rockey dongle's own device node. Never touches a USB hub, so it cannot disturb your keyboard, Bluetooth or other devices. Fixes soft wedges.");

            btnCycleHub = new Button { Text = "Cycle Dongle Hub", Location = new Point(234, 123), Size = new Size(150, 26) };
            btnCycleHub.Click += async (s, e) => await ManualHubCycleAsync();
            toolTip.SetToolTip(btnCycleHub, "Power-cycle the dongle's parent hub (re-powers its port). Recovers a hard wedge ('USB device not recognized') that a device reset can't. Lists what's on the hub and asks before cycling. Never cycles a root hub.");

            lblMultiHint = new Label
            {
                Text = "Device reset fixes soft wedges. A hard wedge ('not recognized') needs 'Cycle Dongle Hub' / 'Hub power-cycle' — only when the dongle is on a dedicated hub.",
                AutoSize = false,
                Location = new Point(394, 122),
                Size = new Size(326, 28),
                ForeColor = Color.Gray,
                AutoEllipsis = true
            };

            grpMultiDongle.Controls.AddRange(new Control[]
            {
                lblDongles, clbDongles, btnDetectDongles, lblDongleDetected,
                lblStartSeed, txtStartSeed, lblStopSeed, txtStopSeed, chkSerialize, chkHubRecover,
                lblRecovery, btnResetSafe, btnCycleHub, lblMultiHint
            });
            Controls.Add(grpMultiDongle);

            // A third mode button next to Dictionary / Brute-Force, and relocate the mode caption.
            btnModeRepair = new Button
            {
                Text = "Fix Missing (Repair)",
                Location = new Point(372, 22),
                Size = new Size(175, 27),
                UseVisualStyleBackColor = true
            };
            btnModeRepair.Click += btnModeRepair_Click;
            grpMode.Controls.Add(btnModeRepair);
            lblMode.Location = new Point(556, 27);
            lblMode.AutoEllipsis = true;

            // Push the Results group down and grow the form so nothing overlaps.
            grpResults.Location = new Point(grpResults.Location.X, groupTop + shift);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + shift);
        }

        // ---- Simple UI event handlers -------------------------------------------------------------

        private void btnBrowseSeeds_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Select seed list file" };
            if (ofd.ShowDialog() == DialogResult.OK) txtSeedListFile.Text = ofd.FileName;
        }

        private void btnBrowseLog_Click(object sender, EventArgs e)
        {
            if (_testMode == TestMode.Repair)
            {
                using OpenFileDialog ofd = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", Title = "Select the log file to repair" };
                if (ofd.ShowDialog() == DialogResult.OK) txtLogFile.Text = ofd.FileName;
                AutoFillRepairRange();
                return;
            }
            using SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Select log file location", DefaultExt = "csv", FileName = "password_log.csv" };
            if (sfd.ShowDialog() == DialogResult.OK) txtLogFile.Text = sfd.FileName;
        }

        private void btnModeDictionary_Click(object sender, EventArgs e) => SetMode(TestMode.Dictionary);
        private void btnModeBruteForce_Click(object sender, EventArgs e) => SetMode(TestMode.BruteForce);
        private void btnModeRepair_Click(object? sender, EventArgs e) => SetMode(TestMode.Repair);

        private void SetMode(TestMode mode)
        {
            _testMode = mode;
            StyleModeButton(btnModeDictionary, mode == TestMode.Dictionary);
            StyleModeButton(btnModeBruteForce, mode == TestMode.BruteForce);
            StyleModeButton(btnModeRepair, mode == TestMode.Repair);

            grpSeedList.Enabled = mode == TestMode.Dictionary;
            grpBruteForceSettings.Enabled = mode == TestMode.BruteForce;
            grpTargetPassword.Enabled = mode != TestMode.Repair;

            switch (mode)
            {
                case TestMode.Dictionary:
                    lblMode.Text = "Mode: Dictionary (File)";
                    btnTest.Text = "Start Testing";
                    break;
                case TestMode.BruteForce:
                    lblMode.Text = "Mode: Brute-Force";
                    btnTest.Text = "Start Testing";
                    break;
                case TestMode.Repair:
                    lblMode.Text = "Mode: Repair log";
                    btnTest.Text = "Scan & Fix Missing";
                    AutoFillRepairRange();
                    break;
            }
        }

        private static void StyleModeButton(Button btn, bool active)
        {
            if (active)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
                btn.FlatAppearance.BorderSize = 2;
                btn.Font = new Font(btn.Font, FontStyle.Bold);
            }
            else
            {
                btn.FlatStyle = FlatStyle.Standard;
                btn.FlatAppearance.BorderSize = 0;
                btn.Font = new Font(btn.Font, FontStyle.Regular);
            }
        }

        // In Repair mode, prefill the start/stop range from a "XXXXXXXX_YYYYYYYY.csv" filename.
        private void AutoFillRepairRange()
        {
            if (_testMode != TestMode.Repair) return;
            if (!string.IsNullOrWhiteSpace(txtStartSeed.Text) && !string.IsNullOrWhiteSpace(txtStopSeed.Text)) return;
            if (LogScanner.TryParseRangeFromFileName(txtLogFile.Text, out uint s, out uint e))
            {
                if (string.IsNullOrWhiteSpace(txtStartSeed.Text)) txtStartSeed.Text = s.ToString("X8");
                if (string.IsNullOrWhiteSpace(txtStopSeed.Text)) txtStopSeed.Text = e.ToString("X8");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e) => _cts?.Cancel();

        private void btnPauseResume_Click(object sender, EventArgs e)
        {
            _isPaused = !_isPaused;
            btnPauseResume.Text = _isPaused ? "Resume" : "Pause";
            lblStatus.Text = _isPaused ? "Paused." : "Resuming...";
        }

        private void btnClear_Click(object sender, EventArgs e) => txtResults.Clear();

        private void btnHideResults_Click(object sender, EventArgs e)
        {
            if (grpResults.Visible) { grpResults.Visible = false; Height -= grpResults.Height; }
            else { grpResults.Visible = true; Height += grpResults.Height; }
        }

        // ---- Dongle detection ---------------------------------------------------------------------

        private void GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4)
        {
            p1 = 0x530A; p2 = 0x00FC; p3 = 0xCB51; p4 = 0x8C4E;
            if (ushort.TryParse(txtP1.Text, NumberStyles.HexNumber, null, out ushort v1)) p1 = v1;
            if (ushort.TryParse(txtP2.Text, NumberStyles.HexNumber, null, out ushort v2)) p2 = v2;
            if (ushort.TryParse(txtP3.Text, NumberStyles.HexNumber, null, out ushort v3)) p3 = v3;
            if (ushort.TryParse(txtP4.Text, NumberStyles.HexNumber, null, out ushort v4)) p4 = v4;
        }

        private List<uint> EnumerateSdkDongles(ushort p1, ushort p2, ushort p3, ushort p4)
        {
            var ids = new List<uint>();
            try
            {
                lock (_sdkGate)
                {
                    var r4s = new Rockey4SmartClass.Rockey4Smart();
                    ushort handle = 0; uint lp1 = 0, lp2 = 0; byte[] buffer = new byte[1024];
                    ushort a = p1, b = p2, c = p3, d = p4;
                    ushort rc = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);
                    int guard = 0;
                    while (rc == 0 && guard++ < 64)
                    {
                        ids.Add(lp1);
                        a = p1; b = p2; c = p3; d = p4; lp1 = 0; lp2 = 0;
                        rc = r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);
                    }
                }
            }
            catch { }
            return ids;
        }

        private async void btnDetectDongles_Click(object? sender, EventArgs e)
        {
            btnDetectDongles.Enabled = false;
            lblDongleDetected.ForeColor = Color.Gray;
            lblDongleDetected.Text = "Detecting...";
            try
            {
                GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4);

                List<uint> hids = new List<uint>();
                await RunOnStaThreadAsync(() => { hids = EnumerateSdkDongles(p1, p2, p3, p4); });
                List<string> usb = await Task.Run(() => UsbReset.GetPresentInstanceIds());

                clbDongles.Items.Clear();
                if (hids.Count == 0)
                {
                    clbDongles.Items.Add(DongleOption.FirstFound(), true);
                }
                else
                {
                    for (int i = 0; i < hids.Count; i++)
                    {
                        clbDongles.Items.Add(new DongleOption
                        {
                            Index = i,
                            Hid = hids[i],
                            UsbInstanceId = i < usb.Count ? usb[i] : null
                        }, true); // default: use every detected dongle
                    }
                }

                if (hids.Count > 0)
                {
                    lblDongleDetected.ForeColor = Color.Black;
                    lblDongleDetected.Text = $"Found {hids.Count} dongle(s); Windows sees {usb.Count} USB node(s). All checked dongles run in parallel.";
                }
                else
                {
                    lblDongleDetected.ForeColor = usb.Count > 0 ? Color.DarkOrange : Color.Red;
                    lblDongleDetected.Text = usb.Count > 0
                        ? $"SDK found no dongles but Windows sees {usb.Count} USB node(s). Check the P1-P4 params / drivers."
                        : "No dongles found. Is one plugged in?";
                }
            }
            catch (Exception ex)
            {
                lblDongleDetected.ForeColor = Color.Red;
                lblDongleDetected.Text = "Detect failed: " + ex.Message;
            }
            finally { btnDetectDongles.Enabled = true; }
        }

        private List<DongleOption> GetSelectedDongles()
        {
            var list = new List<DongleOption>();
            foreach (var item in clbDongles.CheckedItems)
                if (item is DongleOption o) list.Add(o);
            if (list.Count == 0) list.Add(DongleOption.FirstFound());
            return list;
        }

        // ---- Manual safe reset --------------------------------------------------------------------

        private async Task ManualSafeResetAsync()
        {
            if (_cts != null)
            {
                MessageBox.Show("A run is in progress. Stop it before resetting a dongle manually.", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            btnResetSafe.Enabled = false;
            btnDetectDongles.Enabled = false;
            try
            {
                AppendResult("— Manual safe reset (dongle node only, never a hub) —");
                var devices = await Task.Run(() => UsbReset.GetPresentRockeyDevices());
                if (devices.Count == 0) { AppendResult("   No Rockey dongle is currently present."); return; }
                foreach (var d in devices) AppendResult($"   {d.InstanceId}  (status: {d.Status})");
                var targets = UsbReset.ChooseResetTargets(null);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await UsbReset.ResetDevicesAsync(targets, cts.Token, AppendResult);
                AppendResult(ok ? "   ✓ Reset issued. Check whether the dongle now responds." : "   ✗ Reset did not complete (see messages above).");
            }
            catch (Exception ex) { AppendResult("   ✗ Reset error: " + ex.Message); }
            finally { btnResetSafe.Enabled = true; btnDetectDongles.Enabled = true; }
        }

        // Manual test of the hub power-cycle: shows what's on the dongle's hub, confirms, then cycles
        // it. Lets the user verify (before relying on it overnight) that cycling recovers the dongle
        // and only affects devices they're OK with. Refuses root hubs.
        private async Task ManualHubCycleAsync()
        {
            if (_cts != null)
            {
                MessageBox.Show("A run is in progress. Stop it before cycling the hub manually.", "Cycle Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            btnResetSafe.Enabled = false;
            btnCycleHub.Enabled = false;
            btnDetectDongles.Enabled = false;
            try
            {
                var (hub, children) = await Task.Run(() =>
                {
                    var devs = UsbReset.GetPresentRockeyDevices();
                    string? id = devs.Count == 1 ? devs[0].InstanceId
                               : devs.FirstOrDefault(d => d.IsHealthy)?.InstanceId
                               ?? devs.FirstOrDefault()?.InstanceId;
                    if (string.IsNullOrEmpty(id)) return ((string?)null, new List<string>());
                    string? h = UsbReset.GetCyclableParentHub(id);
                    return (h, h != null ? UsbReset.DescribeHubChildren(h) : new List<string>());
                });

                if (hub == null)
                {
                    MessageBox.Show("No dongle found on a cyclable hub. Either no dongle is present, or it sits directly on a ROOT hub (which I won't cycle, because that disconnects the whole controller). Move the dongle onto a small dedicated USB hub for safe recovery.",
                        "Cycle Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string list = children.Count == 0 ? "(only the dongle appears to be on it)" : string.Join("\n  • ", children);
                var confirm = MessageBox.Show(
                    $"Power-cycle hub:\n  {hub}\n\nThis briefly disconnects EVERY device on that hub:\n  • {list}\n\nProceed?",
                    "Cycle Dongle Hub", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) { AppendResult("Hub cycle cancelled."); return; }

                AppendResult("— Manual hub power-cycle —");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await UsbReset.CycleHubAsync(hub, cts.Token, AppendResult);
                AppendResult(ok ? "   ✓ Hub cycled. Check whether the dongle now responds (Detect Dongles)." : "   ✗ Hub cycle did not complete (see messages above).");
            }
            catch (Exception ex) { AppendResult("   ✗ Hub cycle error: " + ex.Message); }
            finally { btnResetSafe.Enabled = true; btnCycleHub.Enabled = true; btnDetectDongles.Enabled = true; }
        }

        // ---- Start button: dispatch by mode -------------------------------------------------------

        private async void btnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLogFile.Text))
            {
                MessageBox.Show(_testMode == TestMode.Repair ? "Select the log file to repair." : "Please select a log file location.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_testMode == TestMode.Dictionary && string.IsNullOrWhiteSpace(txtSeedListFile.Text))
            {
                MessageBox.Show("Please select a seed list file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_testMode == TestMode.Repair && !File.Exists(txtLogFile.Text.Trim()))
            {
                MessageBox.Show("The log file to repair does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            _isPaused = false;
            _matchedWorker = null;
            _workers.Clear();
            SetRunningUi(true);

            txtResults.Clear();
            ResetStatLabels();

            _speedMode = radSpeedFast.Checked ? SpeedMode.Fast : radSpeedBalanced.Checked ? SpeedMode.Balanced : SpeedMode.Slow;
            GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4);
            string targetPassword = txtTargetPassword.Text.Trim();
            string logFilePath = Path.GetFullPath(txtLogFile.Text.Trim());
            txtLogFile.Text = logFilePath;
            var dongles = GetSelectedDongles();

            try
            {
                _sw.Restart();
                _uiTimer.Start();

                if (_testMode == TestMode.Repair)
                    await RunRepairAsync(dongles, p1, p2, p3, p4, targetPassword, logFilePath, _cts.Token);
                else
                    await RunTestAsync(dongles, p1, p2, p3, p4, targetPassword, logFilePath, _cts.Token);

                bool cancelled = _cts?.IsCancellationRequested ?? false;
                _sw.Stop();
                _uiTimer.Stop();
                UiTimer_Tick(null, EventArgs.Empty); // final refresh
                FinalizeRunUi(cancelled);
            }
            catch (OperationCanceledException)
            {
                _sw.Stop(); _uiTimer.Stop();
                lblStatus.Text = "Cancelled.";
            }
            catch (Exception ex)
            {
                _sw.Stop(); _uiTimer.Stop();
                MessageBox.Show($"Error: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error occurred.";
            }
            finally
            {
                SetRunningUi(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        // ---- Test run (Dictionary / Brute-force) across N dongles ---------------------------------

        private async Task RunTestAsync(List<DongleOption> dongles, ushort p1, ushort p2, ushort p3, ushort p4, string targetPassword, string logFilePath, CancellationToken ct)
        {
            int n = dongles.Count;
            var configs = new List<DongleWorker.Config>();
            long grandTotal = 0;

            if (_testMode == TestMode.Dictionary)
            {
                lblStatus.Text = "Loading seeds...";
                List<string> seeds = await LoadSeedsAsync(txtSeedListFile.Text);
                if (seeds.Count == 0) { lblStatus.Text = "No seeds to test."; return; }

                for (int i = 0; i < n; i++)
                {
                    int start = (int)((long)seeds.Count * i / n);
                    int end = (int)((long)seeds.Count * (i + 1) / n);
                    string log = DeriveLogPath(logFilePath, i, n);
                    var slice = seeds.GetRange(start, end - start);

                    // Resume: skip past the last seed already in this dongle's log.
                    int resumeFrom = 0;
                    string last = SeedUtil.ReadLastLoggedSeed(log);
                    if (!string.IsNullOrEmpty(last))
                    {
                        int li = slice.FindLastIndex(s => string.Equals(s, last, StringComparison.Ordinal));
                        if (li >= 0) resumeFrom = li + 1;
                    }
                    var remaining = resumeFrom < slice.Count ? slice.GetRange(resumeFrom, slice.Count - resumeFrom) : new List<string>();
                    grandTotal += remaining.Count;
                    configs.Add(MakeConfig(dongles[i], i, n, p1, p2, p3, p4, targetPassword, log,
                        SeedSource.FromEnumerable(remaining, shared: false)));
                }
            }
            else // BruteForce
            {
                string charset = txtCharset.Text;
                int length = (int)nudLength.Value;
                if (string.IsNullOrEmpty(charset)) { lblStatus.Text = "Charset is empty."; return; }
                long space = (long)Math.Pow(charset.Length, length);

                long rangeStart = 0;
                if (!string.IsNullOrEmpty(txtStartSeed.Text.Trim()) && SeedUtil.TryGetCombinationIndex(txtStartSeed.Text.Trim(), charset, length, out long si)) rangeStart = si;
                long rangeStop = space;
                if (!string.IsNullOrEmpty(txtStopSeed.Text.Trim()) && SeedUtil.TryGetCombinationIndex(txtStopSeed.Text.Trim(), charset, length, out long sp)) rangeStop = Math.Min(rangeStop, sp);
                if (chkLimit.Checked) rangeStop = Math.Min(rangeStop, rangeStart + (long)nudLimit.Value);
                if (rangeStop <= rangeStart) { lblStatus.Text = "Stop seed must be after start seed."; return; }

                long total = rangeStop - rangeStart;
                for (int i = 0; i < n; i++)
                {
                    long subStart = rangeStart + total * i / n;
                    long subStop = i == n - 1 ? rangeStop : rangeStart + total * (i + 1) / n;
                    string log = DeriveLogPath(logFilePath, i, n);

                    long resumeStart = subStart;
                    string last = SeedUtil.ReadLastLoggedSeed(log);
                    if (!string.IsNullOrEmpty(last) && SeedUtil.TryGetCombinationIndex(last, charset, length, out long lastIdx))
                    {
                        long next = lastIdx + 1;
                        if (next > resumeStart && next < subStop) resumeStart = next;
                    }
                    grandTotal += Math.Max(0, subStop - resumeStart);
                    var seq = SeedUtil.GenerateCombinations(charset, length, subStop, resumeStart);
                    configs.Add(MakeConfig(dongles[i], i, n, p1, p2, p3, p4, targetPassword, log,
                        SeedSource.FromEnumerable(seq, shared: false)));
                }
            }

            UiTotal = grandTotal;
            lblStatus.Text = $"Running {n} dongle(s) over {grandTotal:N0} seeds...";
            await RunConfigsAsync(configs, ct);
        }

        // ---- Repair run: scan the huge log, then re-test the missing seeds across N dongles --------

        private async Task RunRepairAsync(List<DongleOption> dongles, ushort p1, ushort p2, ushort p3, ushort p4, string targetPassword, string logFilePath, CancellationToken ct)
        {
            uint start, stop;
            string st = txtStartSeed.Text.Trim(), sp = txtStopSeed.Text.Trim();
            if (SeedUtil.TryConvertSeedToUint(st, out uint us) && SeedUtil.TryConvertSeedToUint(sp, out uint ue))
            {
                start = us; stop = ue;
            }
            else if (LogScanner.TryParseRangeFromFileName(logFilePath, out start, out stop))
            {
                txtStartSeed.Text = start.ToString("X8");
                txtStopSeed.Text = stop.ToString("X8");
            }
            else
            {
                MessageBox.Show("Enter the Start seed and Stop before (8 hex each) for the repair range,\nor name the log like 11111111_22222222.csv so the range can be read from it.",
                    "Repair range", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Repair needs a start/stop range.";
                return;
            }
            if (stop <= start) { lblStatus.Text = "Stop seed must be after start seed."; return; }

            int n = dongles.Count;
            AppendResult($"Repair: scanning {Path.GetFileName(logFilePath)} for missing/error seeds in {start:X8}..{stop:X8} (exclusive)...");

            LogScanner.ScanResult scan;
            try
            {
                scan = await Task.Run(() =>
                {
                    var r = LogScanner.Scan(logFilePath, start, stop, ct, (read, tot) =>
                        SafeStatus($"Scanning log... {(tot > 0 ? read * 100.0 / tot : 0):F1}% ({read / (1024 * 1024):N0} MB)"));
                    // Fold any previous repair output back in so we don't re-test what's already fixed.
                    for (int i = 0; i < n; i++) LogScanner.MarkPresentFrom(r, DeriveRepairPath(logFilePath, i, n), ct);
                    return r;
                }, ct);
            }
            catch (OperationCanceledException) { throw; }

            AppendResult($"Scan done: {scan.RowsScanned:N0} rows read, {scan.ValidSeedsSeen:N0} valid seeds present, {scan.MissingCount:N0} missing (errors + gaps).");
            if (scan.MissingCount == 0)
            {
                lblStatus.Text = "Nothing to repair — no missing seeds in range.";
                AppendResult("No missing seeds. Log is complete for this range.");
                return;
            }

            UiTotal = scan.MissingCount;
            // One shared source of missing seeds; every dongle pulls from it so each seed is tested once.
            var shared = SeedSource.FromEnumerable(LogScanner.EnumerateMissing(scan), shared: true);
            var configs = new List<DongleWorker.Config>();
            for (int i = 0; i < n; i++)
            {
                var cfg = MakeConfig(dongles[i], i, n, p1, p2, p3, p4, targetPassword, DeriveRepairPath(logFilePath, i, n), shared);
                cfg.IsRepair = true;
                configs.Add(cfg);
            }

            lblStatus.Text = $"Re-testing {scan.MissingCount:N0} missing seeds on {n} dongle(s)...";
            _sw.Restart(); // measure the re-test rate, not the (much longer) scan
            await RunConfigsAsync(configs, ct);

            long fixedCount = _workers.Sum(w => w.Verified);
            AppendResult($"Repair finished. {fixedCount:N0} seeds re-tested successfully. Output: {Path.GetFileName(DeriveRepairPath(logFilePath, 0, n))}{(n > 1 ? " … (one file per dongle)" : "")}");
        }

        // ---- Worker orchestration -----------------------------------------------------------------

        private DongleWorker.Config MakeConfig(DongleOption d, int pos, int n, ushort p1, ushort p2, ushort p3, ushort p4, string targetPassword, string logPath, SeedSource source)
        {
            return new DongleWorker.Config
            {
                Index = pos,
                TargetHid = d.Hid,
                UsbInstanceId = d.UsbInstanceId,
                P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                TargetPassword = targetPassword,
                Speed = _speedMode,
                Source = source,
                LogPath = logPath,
                SdkGate = _sdkGate,
                SerializeSeedCalls = chkSerialize.Checked,
                AllowHubCycle = chkHubRecover.Checked,
                Log = AppendResult,
                IsPaused = () => _isPaused,
                OnMatch = OnWorkerMatch
            };
        }

        private async Task RunConfigsAsync(List<DongleWorker.Config> configs, CancellationToken ct)
        {
            _workers.Clear();
            var tasks = new List<Task>();
            foreach (var cfg in configs)
            {
                var worker = new DongleWorker(cfg, ct);
                _workers.Add(worker);
                tasks.Add(RunOnStaThreadAsync(worker.Run));
            }
            await Task.WhenAll(tasks);
        }

        private void OnWorkerMatch(DongleWorker w)
        {
            _matchedWorker = w;
            _cts?.Cancel();
        }

        private Task RunOnStaThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object?>();
            var thread = new Thread(() =>
            {
                try { action(); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private async Task<List<string>> LoadSeedsAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var seeds = new List<string>();
                try
                {
                    foreach (string line in File.ReadLines(filePath))
                    {
                        string seed = line.Trim();
                        if (!string.IsNullOrEmpty(seed) && !seed.StartsWith("#")) seeds.Add(seed);
                    }
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((MethodInvoker)(() => MessageBox.Show($"Failed to load seed list: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
                return seeds;
            });
        }

        // password_log.csv -> password_log.d0.csv (only when running more than one dongle).
        private static string DeriveLogPath(string basePath, int pos, int n)
        {
            if (n <= 1) return basePath;
            string dir = Path.GetDirectoryName(basePath) ?? "";
            string name = Path.GetFileNameWithoutExtension(basePath);
            string ext = Path.GetExtension(basePath);
            return Path.Combine(dir, $"{name}.d{pos}{ext}");
        }

        // 11111111_22222222.csv -> 11111111_22222222.repaired.csv (or .repaired.dN.csv for many dongles).
        private static string DeriveRepairPath(string basePath, int pos, int n)
        {
            string dir = Path.GetDirectoryName(basePath) ?? "";
            string name = Path.GetFileNameWithoutExtension(basePath);
            string ext = Path.GetExtension(basePath);
            string suffix = n > 1 ? $".repaired.d{pos}" : ".repaired";
            return Path.Combine(dir, $"{name}{suffix}{ext}");
        }

        // ---- UI state & aggregated stats ----------------------------------------------------------

        private void SetRunningUi(bool running)
        {
            btnTest.Enabled = !running;
            btnCancel.Enabled = running;
            btnPauseResume.Enabled = running;
            btnPauseResume.Text = "Pause";
            btnClear.Enabled = !running;
            grpMultiDongle.Enabled = !running;
            grpMode.Enabled = !running;
        }

        private void ResetStatLabels()
        {
            lblSeedsTested.Text = "0";
            lblMatchesFound.Text = "0";
            lblVerified.Text = "0";
            lblSpeed.Text = "0 seeds/sec";
            lblRemaining.Text = "N/A";
            lblETA.Text = "N/A";
            txtLastSeed.Text = "";
            lblDongleStatus.Text = "Working...";
            lblDongleStatus.ForeColor = Color.DarkOrange;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (_workers.Count == 0) return;
            long total = UiTotal;

            long tested = 0, generated = 0, verified = 0;
            foreach (var w in _workers) { tested += w.Tested; generated += w.Generated; verified += w.Verified; }

            double elapsed = _sw.Elapsed.TotalSeconds;
            double speed = elapsed > 0 ? tested / elapsed : 0;
            long remaining = total > 0 ? Math.Max(0, total - tested) : 0;
            string eta = (speed > 0 && total > 0) ? TimeSpan.FromSeconds(remaining / speed).ToString("c") : "N/A";

            lblSeedsTested.Text = $"{tested:N0}";
            lblMatchesFound.Text = $"{generated:N0}";
            lblVerified.Text = $"{verified:N0}";
            lblSpeed.Text = $"{speed:F1} seeds/sec";
            lblRemaining.Text = total > 0 ? $"{remaining:N0}" : "N/A";
            lblETA.Text = eta;

            string pct = total > 0 ? $" ({tested * 100.0 / total:F1}%)" : "";
            lblStatus.Text = $"{tested:N0}/{(total > 0 ? total.ToString("N0") : "?")} seeds{pct}, {verified:N0} verified — {_workers.Count} dongle(s)";

            // Per-dongle progress (count + a flag when one stops) goes in the status line for many
            // dongles; the Last Seed box keeps its meaning: the last actual seed each dongle tested.
            if (_workers.Count > 1)
            {
                string perDongle = string.Join(" ", _workers.Select(w =>
                    $"D{w.Index}:{w.Tested:N0}{(w.Failed ? "✗" : w.Finished ? "✓" : "")}"));
                lblStatus.Text += "  [" + perDongle + "]";
                txtLastSeed.Text = string.Join("   ", _workers.Select(w =>
                    $"D{w.Index}={(string.IsNullOrEmpty(w.LastSeed) ? "…" : w.LastSeed)}"));
            }
            else
            {
                var w = _workers[0];
                txtLastSeed.Text = string.IsNullOrEmpty(w.LastSeed) ? "…" : w.LastSeed;
            }
        }

        private void FinalizeRunUi(bool cancelled)
        {
            lblHandle.Text = "Closed";
            // Nothing ran (e.g. Repair found no missing seeds, or no seeds to test) - keep the
            // informative status the run already set instead of overwriting it with "Done".
            if (_workers.Count == 0)
            {
                lblDongleStatus.Text = "Idle";
                lblDongleStatus.ForeColor = Color.Gray;
                return;
            }

            long tested = _workers.Sum(w => w.Tested);
            long verified = _workers.Sum(w => w.Verified);
            int failed = _workers.Count(w => w.Failed);

            lblDongleStatus.Text = failed == 0 ? "Idle" : $"{failed} dongle(s) stopped";
            lblDongleStatus.ForeColor = failed == 0 ? Color.Gray : Color.Red;

            if (_matchedWorker != null)
            {
                lblStatus.Text = $"MATCH on dongle {_matchedWorker.Index}: seed {_matchedWorker.MatchSeed} = {_matchedWorker.MatchPassword}";
                MessageBox.Show($"✓✓ FOUND MATCH!\n\nDongle: {_matchedWorker.Index}\nSeed: {_matchedWorker.MatchSeed}\nPassword: {_matchedWorker.MatchPassword}\n\nTested: {tested:N0}\nElapsed: {_sw.Elapsed:hh\\:mm\\:ss}",
                    "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendResult($"✓✓✓✓ MATCH: dongle {_matchedWorker.Index}, seed {_matchedWorker.MatchSeed} = {_matchedWorker.MatchPassword}");
            }
            else
            {
                string verb = cancelled ? "Stopped" : "Done";
                lblStatus.Text = $"{verb}. Tested {tested:N0}, {verified:N0} verified." + (failed > 0 ? $" {failed} dongle(s) stopped — see Results." : "");
            }
        }

        private void AppendResult(string text)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    const int MaxLines = 8000;
                    if (txtResults.Lines.Length > MaxLines)
                        txtResults.Lines = txtResults.Lines.Skip(MaxLines / 2).ToArray();
                    txtResults.AppendText(text + "\r\n");
                    txtResults.ScrollToCaret();
                });
            }
            catch { }
        }

        private void SafeStatus(string text)
        {
            if (!IsHandleCreated) return;
            try { BeginInvoke((MethodInvoker)(() => lblStatus.Text = text)); }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}
