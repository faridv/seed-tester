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
        // Concurrent RY_SEED (read lock) but exclusive FIND/OPEN/CLOSE (write lock), so one dongle's
        // recovery can't knock the others offline mid-seed.
        private readonly ReaderWriterLockSlim _sdkLock = new ReaderWriterLockSlim();
        private LogSink? _sharedLog;

        private Stopwatch _sw = new Stopwatch();
        private SpeedMode _speedMode = SpeedMode.Balanced;
        private TestMode _testMode = TestMode.Dictionary;
        private volatile bool _isPaused = false;

        private System.Windows.Forms.Timer _uiTimer = null!;
        private long _uiTotal;
        private readonly object _uiTotalLock = new object();
        private DongleWorker? _matchedWorker;
        // Running totals from finished workers/cycles (tandem & round-robin use one worker at a time,
        // so completed workers' counts must be carried forward for the aggregate stats).
        private long _baseTested, _baseGenerated, _baseVerified;

        private DongleMode _dongleMode = DongleMode.Tandem;
        private const long ROUND_ROBIN_CHUNK = 1000; // seeds per dongle turn in round-robin

        // Multi-dongle / repair controls, built in code so the generated Designer layout is untouched.
        private GroupBox grpMultiDongle = null!;
        private ListView lvDongles = null!;          // per-dongle panel: checkbox + icon + HID + port + state
        private ImageList _statusIcons = null!;      // colored dots, one per WorkerState
        private RadioButton radTandem = null!, radRoundRobin = null!, radParallel = null!;
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
        private CheckBox chkDemo = null!;

        // One entry in the dongle picker. Index is the SDK enumeration position; Hid binds to the
        // exact dongle regardless of order; UsbInstanceId (best-effort) pairs it with a Windows device
        // for a targeted safe reset.
        private sealed class DongleOption
        {
            public int Index { get; init; }
            public uint? Hid { get; init; }
            public string? UsbInstanceId { get; init; }
            public string? Location { get; init; } // physical port, e.g. "Port_#0004.Hub_#0004"
            public static DongleOption FirstFound() => new DongleOption { Index = 0, Hid = null };
            public string PortLabel => string.IsNullOrEmpty(Location) ? "port unknown" : Location!;
            public override string ToString() =>
                Hid.HasValue
                    ? $"Dongle #{Index} — {PortLabel} — HID 0x{Hid.Value:X8}"
                    : "Dongle #0 (first found)";
        }

        private readonly bool _resumedLaunch;

        public MainForm() : this(false) { }

        public MainForm(bool resumed)
        {
            _resumedLaunch = resumed;
            // A fresh, user-initiated launch clears any leftover auto-restart state (resets the loop
            // guard and drops stale auto-resume settings). A --resumed launch keeps it to continue.
            if (!resumed) { try { if (File.Exists(RunStatePath)) File.Delete(RunStatePath); } catch { } }
            InitializeComponent();
            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += UiTimer_Tick;
            UiTotal = 0;
            AddDemoCheckbox();
            BuildMultiDongleUi();
            SetMode(TestMode.Dictionary); // initial styling + enabled state
            // Self-heal: a previous run's interrupted reset can leave a dongle Disabled in Windows,
            // and it never comes back on its own. Re-enable any such dongle at startup (off the UI
            // thread - it spawns powershell). The app is elevated (manifest) so Enable-PnpDevice works.
            Task.Run(() => UsbReset.EnableDisabledRockeyDevices(AppendResult));
            Shown += MainForm_Shown;
        }

        private async void MainForm_Shown(object? sender, EventArgs e)
        {
            Shown -= MainForm_Shown;
            if (!_resumedLaunch) return;
            // We are the fresh process spawned by an auto-recovery restart. Reload the run settings,
            // reset the dongles from this clean process (no stale handles), then continue the run.
            try
            {
                if (!LoadRunState()) { AppendResult("Auto-restart: no saved run state; idle."); return; }
                AppendResult("Auto-restarted after a wedge. Letting the old instance exit, then resetting dongles...");
                await Task.Delay(2500); // let the previous process fully exit and release its handles

                // Reset from this clean process. Wrapped so a reset hiccup never blocks the resume -
                // the dongles are often already fine and just need the SDK re-initialised (new process).
                try
                {
                    await Task.Run(() => UsbReset.EnableDisabledRockeyDevices(AppendResult));
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    bool ok = await UsbReset.ResetDevicesAsync(UsbReset.GetPresentInstanceIds(), cts.Token, AppendResult);
                    AppendResult(ok ? "Reset done." : "Reset reported an issue; continuing anyway (dongles may already be usable).");
                }
                catch (Exception rex) { AppendResult("Reset step error (continuing): " + rex.Message); }

                await Task.Delay(4000);
                await DetectDonglesAsync();          // enumerate the now-clean dongles
                if (!btnTest.Enabled) { AppendResult("Auto-restart: a run is already active; not starting again."); return; }
                AppendResult("Resuming the run from the log...");
                btnTest_Click(btnTest, EventArgs.Empty); // auto-continue (resumes from the log)
            }
            catch (Exception ex) { AppendResult("Auto-restart error: " + ex.Message); }
        }

        // Default dongle parameters and the "Demo" set, kept in one place.
        private static readonly (string P1, string P2, string P3, string P4) DefaultParams = ("530A", "00FC", "CB51", "8C4E");
        private static readonly (string P1, string P2, string P3, string P4) DemoParams = ("C44C", "C8F8", "CB51", "8C4E");

        // Adds a "Demo" checkbox under the P1-P4 inputs. Checked fills them with the demo password
        // C44C C8F8 CB51 8C4E; unchecked restores the defaults. The P1-P4 group is grown a little and
        // everything below it nudged down so nothing overlaps.
        private void AddDemoCheckbox()
        {
            const int delta = 26;
            int threshold = grpMode.Top; // shift this row and everything below it

            grpDongleParams.Height += delta;
            chkDemo = new CheckBox { Text = "Demo", AutoSize = true, Location = new Point(10, 90) };
            var tt = new ToolTip();
            tt.SetToolTip(chkDemo, "Fill P1-P4 with the demo values C44C C8F8 CB51 8C4E. Uncheck to restore the defaults (530A 00FC CB51 8C4E).");
            chkDemo.CheckedChanged += chkDemo_CheckedChanged;
            grpDongleParams.Controls.Add(chkDemo);

            foreach (Control ctl in Controls)
                if (ctl.Top >= threshold) ctl.Top += delta;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + delta);
        }

        private void chkDemo_CheckedChanged(object? sender, EventArgs e)
        {
            var p = chkDemo.Checked ? DemoParams : DefaultParams;
            txtP1.Text = p.P1; txtP2.Text = p.P2; txtP3.Text = p.P3; txtP4.Text = p.P4;
        }

        private long UiTotal
        {
            get { lock (_uiTotalLock) { return _uiTotal; } }
            set { lock (_uiTotalLock) { _uiTotal = value; } }
        }

        // ---- UI construction (multi-dongle group + Repair mode button) ---------------------------

        private void BuildMultiDongleUi()
        {
            int groupTop = grpResults.Location.Y; // where grpResults currently sits (after any earlier shifts)
            const int groupHeight = 232;
            const int shift = groupHeight + 8;

            grpMultiDongle = new GroupBox
            {
                Text = "Dongles & Recovery",
                Location = new Point(13, groupTop),
                Size = new Size(730, groupHeight),
                TabStop = false
            };

            var toolTip = new ToolTip { AutoPopDelay = 20000 };

            btnDetectDongles = new Button { Text = "Detect Dongles", Location = new Point(8, 20), Size = new Size(120, 26) };
            btnDetectDongles.Click += btnDetectDongles_Click;

            // Mode selector
            var lblMulti = new Label { Text = "Mode:", AutoSize = true, Location = new Point(146, 25) };
            radTandem = new RadioButton { Text = "Tandem", AutoSize = true, Location = new Point(190, 23), Checked = true };
            toolTip.SetToolTip(radTandem, "Use ONE dongle at a time; if it stops responding, automatically switch to the next connected dongle. Most reliable.");
            radRoundRobin = new RadioButton { Text = "Round-robin", AutoSize = true, Location = new Point(270, 23) };
            toolTip.SetToolTip(radRoundRobin, $"Rotate dongles one at a time, {ROUND_ROBIN_CHUNK:N0} seeds each turn. Spreads wear evenly; still one dongle at a time.");
            radParallel = new RadioButton { Text = "Parallel (experimental)", AutoSize = true, Location = new Point(370, 23) };
            toolTip.SetToolTip(radParallel, "Drive all dongles at once. Experimental: the SDK serialises calls, so this currently gives little or no speed-up and can be less stable.");
            radTandem.CheckedChanged += (s, e) => { if (radTandem.Checked) _dongleMode = DongleMode.Tandem; };
            radRoundRobin.CheckedChanged += (s, e) => { if (radRoundRobin.Checked) _dongleMode = DongleMode.RoundRobin; };
            radParallel.CheckedChanged += (s, e) => { if (radParallel.Checked) _dongleMode = DongleMode.Parallel; };

            // Per-dongle status panel: checkbox (use it) + colored icon + HID + port + live state.
            _statusIcons = BuildStatusIcons();
            lvDongles = new ListView
            {
                Location = new Point(8, 52),
                Size = new Size(500, 110),
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                SmallImageList = _statusIcons
            };
            lvDongles.Columns.Add("#", 30);
            lvDongles.Columns.Add("HID", 110);
            lvDongles.Columns.Add("Port", 170);
            lvDongles.Columns.Add("State", 170);
            toolTip.SetToolTip(lvDongles, "Connected dongles. Tick the ones to use. The icon and 'State' column show each dongle live (Ready / Working / Recovering / Stopped / Done).");

            lblDongleDetected = new Label
            {
                Text = "Click 'Detect Dongles' to list connected dongles.",
                AutoSize = false,
                Location = new Point(516, 52),
                Size = new Size(206, 110),
                ForeColor = Color.Gray
            };

            var lblStartSeed = new Label { Text = "Start seed:", AutoSize = true, Location = new Point(8, 174) };
            txtStartSeed = new TextBox { Location = new Point(78, 171), Size = new Size(84, 23), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip.SetToolTip(txtStartSeed, "Brute-force / repair: 8-hex start of the range. Empty = beginning. The run continues from the log's last seed if that is further along.");

            var lblStopSeed = new Label { Text = "Stop before:", AutoSize = true, Location = new Point(172, 174) };
            txtStopSeed = new TextBox { Location = new Point(250, 171), Size = new Size(84, 23), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip.SetToolTip(txtStopSeed, "Brute-force / repair: 8-hex end of the range (exclusive). Empty = end of the charset space, or parsed from the log filename in Repair mode.");

            chkSerialize = new CheckBox { Text = "Serialize I/O", AutoSize = true, Location = new Point(352, 173) };
            toolTip.SetToolTip(chkSerialize, "Parallel mode only: force one dongle call at a time if concurrent access corrupts results. No effect in tandem/round-robin (already one at a time).");

            chkHubRecover = new CheckBox { Text = "Hub power-cycle on wedge", AutoSize = true, Location = new Point(452, 173) };
            toolTip.SetToolTip(chkHubRecover,
                "Last-resort recovery for a hard wedge. Re-powers the dongle's PARENT HUB, briefly disconnecting every device on that hub. " +
                "Never cycles a root hub. Safe only when the dongle is on a dedicated hub.");

            var lblRecovery = new Label { Text = "Recovery:", AutoSize = true, Location = new Point(8, 204) };
            btnResetSafe = new Button { Text = "Reset Dongle", Location = new Point(78, 200), Size = new Size(120, 26) };
            btnResetSafe.Click += async (s, e) => await ManualSafeResetAsync();
            toolTip.SetToolTip(btnResetSafe, "Restart ONLY the Rockey dongle's own device node. Never touches a hub, so it cannot disturb other devices. Fixes soft wedges.");

            btnCycleHub = new Button { Text = "Cycle Dongle Hub", Location = new Point(204, 200), Size = new Size(140, 26) };
            btnCycleHub.Click += async (s, e) => await ManualHubCycleAsync();
            toolTip.SetToolTip(btnCycleHub, "Power-cycle the dongle's parent hub (re-powers its port) to recover a hard wedge a device reset can't. Lists what's on the hub and asks first. Never cycles a root hub.");

            lblMultiHint = new Label
            {
                Text = "Tandem is the most reliable. If a dongle wedges the app recovers automatically (reset → resume), and only asks you to replug if that fails.",
                AutoSize = false,
                Location = new Point(352, 200),
                Size = new Size(370, 28),
                ForeColor = Color.Gray
            };

            grpMultiDongle.Controls.AddRange(new Control[]
            {
                btnDetectDongles, lblMulti, radTandem, radRoundRobin, radParallel,
                lvDongles, lblDongleDetected,
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
                _sdkLock.EnterWriteLock();
                try
                {
                    var r4s = new Rockey4SmartClass.Rockey4Smart();
                    ushort handle = 0; uint lp1 = 0, lp2 = 0; byte[] buffer = new byte[1024];
                    ushort a = p1, b = p2, c = p3, d = p4;
                    ushort rc = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);
                    int guard = 0;
                    while (rc == 0 && guard++ < 64)
                    {
                        ids.Add(lp1);
                        // Keep handle/lp1/lp2 as RY_FIND left them - RY_FIND_NEXT uses them as the
                        // enumeration cursor. Zeroing lp1 here made it only ever return the first dongle
                        // (which is why "Detect" reported 1 when several were connected).
                        a = p1; b = p2; c = p3; d = p4;
                        rc = r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);
                    }
                }
                finally { _sdkLock.ExitWriteLock(); }
            }
            catch { }
            return ids;
        }

        private async void btnDetectDongles_Click(object? sender, EventArgs e) => await DetectDonglesAsync();

        private async Task DetectDonglesAsync()
        {
            btnDetectDongles.Enabled = false;
            lblDongleDetected.ForeColor = Color.Gray;
            lblDongleDetected.Text = "Detecting...";
            try
            {
                GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4);

                List<uint> hids = new List<uint>();
                await RunOnStaThreadAsync(() => { hids = EnumerateSdkDongles(p1, p2, p3, p4); });
                List<UsbReset.RockeyDevice> usb = await Task.Run(() => UsbReset.GetPresentRockeyDevices());

                lvDongles.Items.Clear();
                if (hids.Count == 0)
                {
                    AddDongleRow(DongleOption.FirstFound());
                }
                else
                {
                    for (int i = 0; i < hids.Count; i++)
                    {
                        // Best-effort pairing of the SDK dongle with a Windows USB node (both lists
                        // sorted independently), so we can name a physical port for it.
                        AddDongleRow(new DongleOption
                        {
                            Index = i,
                            Hid = hids[i],
                            UsbInstanceId = i < usb.Count ? usb[i].InstanceId : null,
                            Location = i < usb.Count ? usb[i].Location : null
                        });
                    }
                }

                if (hids.Count > 0)
                {
                    lblDongleDetected.ForeColor = Color.Black;
                    lblDongleDetected.Text = $"Found {hids.Count} dongle(s); Windows sees {usb.Count} USB node(s). Tick the ones to use.";
                }
                else
                {
                    lblDongleDetected.ForeColor = usb.Count > 0 ? Color.DarkOrange : Color.Red;
                    lblDongleDetected.Text = usb.Count > 0
                        ? $"The SDK talked to no dongles, but Windows sees {usb.Count}. They may be wedged — use 'Reset Dongle' or replug them."
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

        // Adds one dongle row to the panel (checked by default), showing HID, port and a Ready icon.
        private void AddDongleRow(DongleOption d)
        {
            var it = new ListViewItem(d.Hid.HasValue ? d.Index.ToString() : "0") { Checked = true, Tag = d, ImageIndex = (int)WorkerState.Ready };
            it.SubItems.Add(d.Hid.HasValue ? $"0x{d.Hid.Value:X8}" : "(first found)");
            it.SubItems.Add(d.PortLabel);
            it.SubItems.Add("Ready");
            lvDongles.Items.Add(it);
        }

        private List<DongleOption> GetSelectedDongles()
        {
            var list = new List<DongleOption>();
            foreach (ListViewItem it in lvDongles.Items)
                if (it.Checked && it.Tag is DongleOption o) list.Add(o);
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

            _baseTested = _baseGenerated = _baseVerified = 0; // cumulative across auto-recovery cycles

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
            const int MAX_AUTO_RECOVERIES = 3; // in-process reset attempts before escalating to a full restart
            int recoveries = 0;

            List<string>? dictSeeds = null;
            if (_testMode == TestMode.Dictionary)
            {
                lblStatus.Text = "Loading seeds...";
                dictSeeds = await LoadSeedsAsync(txtSeedListFile.Text);
                if (dictSeeds.Count == 0) { lblStatus.Text = "No seeds to test."; return; }
            }

            while (!ct.IsCancellationRequested)
            {
                _sharedLog = new LogSink(logFilePath); // one shared log, continued from its last seed
                SeedSource? source = null;
                try
                {
                    var built = BuildTestConfigs(dongles, n, p1, p2, p3, p4, targetPassword, logFilePath, dictSeeds, _sharedLog);
                    source = built.source;
                    UiTotal = _baseTested + built.grandTotal;
                    if (built.grandTotal == 0)
                    {
                        lblStatus.Text = recoveries == 0 ? "Nothing to test — the log already covers this range." : "Done — range complete.";
                        return;
                    }
                    lblStatus.Text = $"{_dongleMode} mode — {built.grandTotal:N0} seeds across {n} dongle(s)...";
                    if (_dongleMode == DongleMode.Parallel)
                        await RunConfigsAsync(built.configs, ct);          // all dongles at once
                    else
                        await RunSequentialAsync(built.configs, _dongleMode, source, ct); // tandem / round-robin
                }
                finally
                {
                    _sharedLog?.Dispose();
                    _sharedLog = null;
                }

                if (ct.IsCancellationRequested) break;
                if (_matchedWorker != null) break;
                if (source != null && source.Exhausted) break; // the whole range is done

                // Work remains but the dongle(s) stopped. Release handles, reset, and resume from the log.
                if (++recoveries > MAX_AUTO_RECOVERIES)
                {
                    AppendResult($"In-app reset didn't clear the wedge after {MAX_AUTO_RECOVERIES} tries. Restarting the app from a clean state to recover...");
                    if (RestartSelfForRecovery())
                    {
                        _uiTimer.Stop();
                        await Task.Delay(500);
                        Environment.Exit(0); // a fresh process is the only sure way to release a stuck SDK session
                    }
                    break;
                }
                AppendResult($"Auto-recovery {recoveries}/{MAX_AUTO_RECOVERIES}: dongles stopped responding. Releasing handles, resetting, and resuming...");
                lblStatus.Text = $"Auto-recovery {recoveries}: resetting dongles and resuming...";
                _baseTested += _workers.Sum(w => w.Tested);
                _baseGenerated += _workers.Sum(w => w.Generated);
                _baseVerified += _workers.Sum(w => w.Verified);
                ReleaseSdkObjects();
                try
                {
                    await UsbReset.ResetDevicesAsync(UsbReset.GetPresentInstanceIds(), ct, AppendResult);
                    await Task.Delay(6000, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { AppendResult("   auto-reset error: " + ex.Message); }
            }
        }

        // Tandem / round-robin: run ONE dongle at a time (no SDK concurrency, so no mutual wedging).
        // Tandem sticks with a dongle until it fails, then moves to the next; round-robin rotates every
        // ROUND_ROBIN_CHUNK seeds. All dongles pull from the one shared source, so the range is covered
        // exactly once and continues seamlessly when the active dongle changes.
        private async Task RunSequentialAsync(List<DongleWorker.Config> configs, DongleMode mode, SeedSource source, CancellationToken ct)
        {
            int n = configs.Count;
            var avail = new bool[n];
            for (int i = 0; i < n; i++) avail[i] = true;
            if (mode == DongleMode.RoundRobin)
                foreach (var c in configs) c.ChunkLimit = ROUND_ROBIN_CHUNK;

            int idx = 0;
            while (!ct.IsCancellationRequested && !source.Exhausted)
            {
                int active = -1;
                for (int k = 0; k < n; k++) { int j = (idx + k) % n; if (avail[j]) { active = j; break; } }
                if (active < 0) break; // every dongle has stopped

                var w = new DongleWorker(configs[active], ct);
                _workers.Clear(); _workers.Add(w);
                await RunOnStaThreadAsync(w.Run);

                // Carry this activation's counts into the running totals, then clear the slot.
                _baseTested += w.Tested; _baseGenerated += w.Generated; _baseVerified += w.Verified;
                _workers.Clear();

                if (w.MatchSeed != null) { _matchedWorker = w; break; }
                if (source.Exhausted) break;
                if (w.Failed)
                {
                    avail[active] = false;
                    int left = avail.Count(a => a);
                    AppendResult($"Dongle #{active} stopped — switching. {left} dongle(s) still available.");
                }
                idx = (active + 1) % n; // tandem: next dongle after a failure; round-robin: rotate each turn
            }
        }

        // Frees the finished workers and their native SDK objects so a device reset can take (a
        // lingering open handle keeps the dongle "in use" - the reason a reset used to need the app
        // to be closed).
        private void ReleaseSdkObjects()
        {
            _workers.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Builds the worker configs for one run attempt around ONE shared seed source and ONE shared
        // log, resuming from that log's last seed. All modes share this; tandem/round-robin just run
        // the configs one at a time. Returns the configs, seeds still to test, and the shared source.
        private (List<DongleWorker.Config> configs, long grandTotal, SeedSource source) BuildTestConfigs(
            List<DongleOption> dongles, int n,
            ushort p1, ushort p2, ushort p3, ushort p4, string targetPassword, string logFilePath,
            List<string>? dictSeeds, LogSink sharedLog)
        {
            var configs = new List<DongleWorker.Config>();
            long grandTotal = 0;
            SeedSource source;

            if (_testMode == TestMode.Dictionary)
            {
                List<string> seeds = dictSeeds ?? new List<string>();
                int resumeFrom = 0;
                string last = SeedUtil.ReadLastLoggedSeed(logFilePath);
                if (!string.IsNullOrEmpty(last))
                {
                    int li = seeds.FindLastIndex(s => string.Equals(s, last, StringComparison.Ordinal));
                    if (li >= 0) resumeFrom = li + 1;
                }
                var remaining = resumeFrom < seeds.Count ? seeds.GetRange(resumeFrom, seeds.Count - resumeFrom) : new List<string>();
                grandTotal = remaining.Count;
                source = SeedSource.FromEnumerable(remaining, shared: true);
            }
            else // BruteForce
            {
                string charset = txtCharset.Text;
                int length = (int)nudLength.Value;
                if (string.IsNullOrEmpty(charset)) return (configs, 0, SeedSource.FromEnumerable(Array.Empty<string>(), true));
                long space = (long)Math.Pow(charset.Length, length);

                long rangeStart = 0;
                if (!string.IsNullOrEmpty(txtStartSeed.Text.Trim()) && SeedUtil.TryGetCombinationIndex(txtStartSeed.Text.Trim(), charset, length, out long si)) rangeStart = si;
                long rangeStop = space;
                if (!string.IsNullOrEmpty(txtStopSeed.Text.Trim()) && SeedUtil.TryGetCombinationIndex(txtStopSeed.Text.Trim(), charset, length, out long sp)) rangeStop = Math.Min(rangeStop, sp);
                if (chkLimit.Checked) rangeStop = Math.Min(rangeStop, rangeStart + (long)nudLimit.Value);
                if (rangeStop <= rangeStart) return (configs, 0, SeedSource.FromEnumerable(Array.Empty<string>(), true));

                long resumeStart = rangeStart;
                string last = SeedUtil.ReadLastLoggedSeed(logFilePath);
                if (!string.IsNullOrEmpty(last) && SeedUtil.TryGetCombinationIndex(last, charset, length, out long lastIdx) && lastIdx + 1 > resumeStart)
                    resumeStart = Math.Min(lastIdx + 1, rangeStop);
                grandTotal = Math.Max(0, rangeStop - resumeStart);
                source = SeedSource.FromEnumerable(SeedUtil.GenerateCombinations(charset, length, rangeStop, resumeStart), shared: true);
            }

            for (int i = 0; i < n; i++)
                configs.Add(MakeConfig(dongles[i], i, n, p1, p2, p3, p4, targetPassword, logFilePath, source, sharedLog));
            return (configs, grandTotal, source);
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
                var cfg = MakeConfig(dongles[i], i, n, p1, p2, p3, p4, targetPassword, DeriveRepairPath(logFilePath, i, n), shared, null);
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

        private DongleWorker.Config MakeConfig(DongleOption d, int pos, int n, ushort p1, ushort p2, ushort p3, ushort p4, string targetPassword, string logPath, SeedSource source, LogSink? sharedLog)
        {
            return new DongleWorker.Config
            {
                Index = pos,
                TargetHid = d.Hid,
                UsbInstanceId = d.UsbInstanceId,
                PortLabel = d.PortLabel,
                P1 = p1, P2 = p2, P3 = p3, P4 = p4,
                TargetPassword = targetPassword,
                Speed = _speedMode,
                Source = source,
                LogPath = logPath,
                SharedLog = sharedLog,
                SdkLock = _sdkLock,
                SerializeSeedCalls = chkSerialize.Checked,
                AllowHubCycle = chkHubRecover.Checked,
                Log = AppendResult,
                IsPaused = () => _isPaused,
                OnMatch = OnWorkerMatch,
                OnState = OnDongleState
            };
        }

        // Called from a worker thread whenever a dongle changes state; updates its row in the panel.
        private void OnDongleState(int index, WorkerState state)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (index >= 0 && index < lvDongles.Items.Count)
                    {
                        var it = lvDongles.Items[index];
                        it.ImageIndex = (int)state;
                        it.SubItems[3].Text = state.ToString();
                    }
                }));
            }
            catch { }
        }

        // One small colored dot per WorkerState (order matches the enum, used as ImageIndex).
        private static ImageList BuildStatusIcons()
        {
            var il = new ImageList { ImageSize = new Size(12, 12), ColorDepth = ColorDepth.Depth32Bit };
            Color[] colors =
            {
                Color.Gray,        // Ready
                Color.RoyalBlue,   // Opening
                Color.SeaGreen,    // Working
                Color.DarkOrange,  // Recovering
                Color.Firebrick,   // Stopped
                Color.MediumTurquoise // Done
            };
            foreach (var c in colors)
            {
                var bmp = new Bitmap(12, 12);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var b = new SolidBrush(c);
                    g.FillEllipse(b, 1, 1, 10, 10);
                }
                il.Images.Add(bmp);
            }
            return il;
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
            _workerTasks = tasks;
            try { await Task.WhenAll(tasks); }
            finally { _workerTasks = null; }
        }

        private List<Task>? _workerTasks;

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
            lblDongleStatus.ForeColor = Color.Green;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (_workers.Count == 0) return;
            long total = UiTotal;

            long tested = _baseTested, generated = _baseGenerated, verified = _baseVerified;
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
            long tested = _baseTested + _workers.Sum(w => w.Tested);
            long verified = _baseVerified + _workers.Sum(w => w.Verified);

            // Truly nothing ran (e.g. Repair found no missing seeds) - keep the status the run set.
            if (tested == 0 && _matchedWorker == null)
            {
                lblDongleStatus.Text = "Idle";
                lblDongleStatus.ForeColor = Color.Gray;
                return;
            }

            var failedWorkers = _workers.Where(w => w.Failed).ToList();
            int failed = failedWorkers.Count;

            lblDongleStatus.Text = failed == 0 ? "Idle" : $"{failed} dongle(s) stopped";
            lblDongleStatus.ForeColor = failed == 0 ? Color.Gray : Color.Red;

            // Spell out exactly which physical dongle(s) to unplug/replug.
            if (failed > 0)
            {
                AppendResult("");
                AppendResult($"⚠ {failed} dongle(s) stopped and need a manual unplug/replug:");
                foreach (var w in failedWorkers) AppendResult($"   → {w.Identity}");
            }

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
            // Timestamp every line so the log reads as a usable timeline. Lines already starting with a
            // '[' timestamp/indent are left as-is.
            string stamped = text.StartsWith("[") || text.StartsWith("   ") || text.Length == 0
                ? text
                : $"[{DateTime.Now:HH:mm:ss}] {text}";
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    const int MaxLines = 8000;
                    if (txtResults.Lines.Length > MaxLines)
                        txtResults.Lines = txtResults.Lines.Skip(MaxLines / 2).ToArray();
                    txtResults.AppendText(stamped + "\r\n");
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

        // ---- Auto-restart recovery (fresh elevated process, no UAC prompt) ------------------------
        // A brand-new process is the only thing guaranteed to fully release a wedged SDK session (the
        // reason a reset used to need the app closed by hand). Because THIS process is already elevated,
        // launching a child inherits the elevated token with no UAC dialog. Run settings are saved so
        // the child resets the dongles and auto-continues from the logs. A windowed counter stops an
        // endless restart loop if a dongle is truly dead.

        private static string RunStatePath => Path.Combine(AppContext.BaseDirectory, "run-state.txt");

        private (int restarts, long t0) ReadRestartCounter()
        {
            int r = 0; long t = 0;
            try
            {
                if (File.Exists(RunStatePath))
                    foreach (var line in File.ReadAllLines(RunStatePath))
                    {
                        if (line.StartsWith("restarts=")) int.TryParse(line.Substring(9), out r);
                        else if (line.StartsWith("restartT0=")) long.TryParse(line.Substring(10), out t);
                    }
            }
            catch { }
            return (r, t);
        }

        private void SaveRunState(int restarts, long t0)
        {
            try
            {
                string speed = radSpeedFast.Checked ? "Fast" : radSpeedSlow.Checked ? "Slow" : "Balanced";
                File.WriteAllLines(RunStatePath, new[]
                {
                    "mode=" + _testMode,
                    "log=" + txtLogFile.Text,
                    "seedFile=" + txtSeedListFile.Text,
                    "start=" + txtStartSeed.Text,
                    "stop=" + txtStopSeed.Text,
                    "charset=" + txtCharset.Text,
                    "length=" + (int)nudLength.Value,
                    "limitOn=" + chkLimit.Checked,
                    "limit=" + (long)nudLimit.Value,
                    "target=" + txtTargetPassword.Text,
                    "p1=" + txtP1.Text, "p2=" + txtP2.Text, "p3=" + txtP3.Text, "p4=" + txtP4.Text,
                    "speed=" + speed,
                    "mode2=" + _dongleMode,
                    "serialize=" + chkSerialize.Checked,
                    "hubRecover=" + chkHubRecover.Checked,
                    "restarts=" + restarts,
                    "restartT0=" + t0,
                });
            }
            catch { }
        }

        private bool LoadRunState()
        {
            try
            {
                if (!File.Exists(RunStatePath)) return false;
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(RunStatePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
                string G(string k) => d.TryGetValue(k, out var v) ? v : "";
                bool B(string k) => string.Equals(G(k), "True", StringComparison.OrdinalIgnoreCase);
                if (G("log").Length > 0) txtLogFile.Text = G("log");
                txtSeedListFile.Text = G("seedFile");
                txtStartSeed.Text = G("start");
                txtStopSeed.Text = G("stop");
                if (G("charset").Length > 0) txtCharset.Text = G("charset");
                if (int.TryParse(G("length"), out int len)) nudLength.Value = Math.Clamp((decimal)len, nudLength.Minimum, nudLength.Maximum);
                chkLimit.Checked = B("limitOn");
                if (long.TryParse(G("limit"), out long lim)) nudLimit.Value = Math.Clamp((decimal)lim, nudLimit.Minimum, nudLimit.Maximum);
                txtTargetPassword.Text = G("target");
                if (G("p1").Length > 0) txtP1.Text = G("p1");
                if (G("p2").Length > 0) txtP2.Text = G("p2");
                if (G("p3").Length > 0) txtP3.Text = G("p3");
                if (G("p4").Length > 0) txtP4.Text = G("p4");
                string sp = G("speed");
                radSpeedFast.Checked = sp == "Fast"; radSpeedSlow.Checked = sp == "Slow";
                radSpeedBalanced.Checked = !radSpeedFast.Checked && !radSpeedSlow.Checked;
                string m2 = G("mode2");
                _dongleMode = m2 == "RoundRobin" ? DongleMode.RoundRobin : m2 == "Parallel" ? DongleMode.Parallel : DongleMode.Tandem;
                radTandem.Checked = _dongleMode == DongleMode.Tandem;
                radRoundRobin.Checked = _dongleMode == DongleMode.RoundRobin;
                radParallel.Checked = _dongleMode == DongleMode.Parallel;
                chkSerialize.Checked = B("serialize");
                chkHubRecover.Checked = B("hubRecover");
                string m = G("mode");
                SetMode(m == "BruteForce" ? TestMode.BruteForce : m == "Repair" ? TestMode.Repair : TestMode.Dictionary);
                return true;
            }
            catch { return false; }
        }

        // Launches a fresh elevated copy to recover from a clean state, then this process should exit.
        // Returns true if the child was launched. A windowed guard prevents an endless restart loop.
        private bool RestartSelfForRecovery()
        {
            const int MAX_RESTARTS = 6;
            long windowTicks = TimeSpan.FromMinutes(30).Ticks;
            var (restarts, t0) = ReadRestartCounter();
            long now = DateTime.UtcNow.Ticks;
            if (t0 == 0 || now - t0 > windowTicks) { restarts = 0; t0 = now; }
            if (restarts >= MAX_RESTARTS)
            {
                AppendResult($"Auto-restart guard: {MAX_RESTARTS} restarts in 30 min without lasting recovery. Not restarting again — unplug/replug the dongles and click Start.");
                return false;
            }
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) { AppendResult("Auto-restart: could not resolve the executable path."); return false; }
            SaveRunState(restarts + 1, t0);
            try
            {
                // UseShellExecute=false: the child inherits this elevated process's token, so no UAC dialog.
                var child = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "--resumed", UseShellExecute = false });
                if (child == null) { AppendResult("Auto-restart: the new instance did not start."); return false; }
                // Confirm the child is still alive shortly after launch. If it crashed immediately we do
                // NOT exit this process - otherwise we'd be left with nothing running (a silent close).
                if (child.WaitForExit(2000))
                {
                    AppendResult($"Auto-restart: the new instance exited immediately (code {child.ExitCode}). Keeping this one open. See crash-log.txt.");
                    return false;
                }
                AppendResult("Auto-restart: fresh instance is up. Handing over.");
                return true;
            }
            catch (Exception ex) { AppendResult("Auto-restart failed to launch: " + ex.Message); return false; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            // Wait for the workers to actually close their dongle handles (RY_CLOSE) before the process
            // exits. Exiting with a handle still open leaks the driver's "in use" session, which then
            // blocks every future FIND/OPEN until a device reset or a Windows restart - the exact
            // "SDK talked to no dongles" wedge. The workers' shutdown does no UI work, so waiting here
            // can't deadlock.
            var tasks = _workerTasks;
            if (tasks != null)
            {
                try { Task.WaitAll(tasks.ToArray(), 6000); } catch { }
            }
            base.OnFormClosing(e);
        }
    }
}
