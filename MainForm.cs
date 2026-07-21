using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RockeyPasswordTester
{
    public enum SpeedMode
    {
        Fast,
        Balanced,
        Slow
    }

    public enum TestMode
    {
        Dictionary,
        BruteForce
    }

    public partial class MainForm : Form
    {
        private const ushort RY_FIND = 1;
        private const ushort RY_FIND_NEXT = 2;
        private const ushort RY_OPEN = 3;
        private const ushort RY_CLOSE = 4;
        private const ushort RY_SEED = 8;
        private const int MAX_CONSECUTIVE_SEED_ERRORS = 3;
        private const string ROCKEY_DONGLE_NAME = "ROCKEY4";
        // Hardware ID of the *physical* Feitian/Rockey4 USB device. The "ROCKEY4" friendly
        // name belongs to a virtual ROOT\USB node the driver installs; disabling that (or a
        // random hub, as the old code did) does nothing to power-cycle the real dongle. To
        // virtually unplug/replug we must disable+enable this device node.
        private const string ROCKEY_HARDWARE_ID = @"USB\VID_096E&PID_0006";
        private const int USB_RESET_RETRY_DELAY_MS = 8000;

        // 0-based index of the dongle this instance should bind to (RY_FIND then N x RY_FIND_NEXT).
        // Lets you run one app instance per dongle. Defaults to the first dongle found. Used only as a
        // fallback when no specific hardware ID was picked (i.e. the user never ran Detect).
        private int _selectedDongleIndex = 0;
        // SDK hardware ID (from RY_FIND/RY_FIND_NEXT lp1) of the dongle this instance should bind to.
        // When set, the run walks RY_FIND_NEXT until it hits THIS id, so selection is robust to the
        // enumeration order rather than a blind index. Null = bind by _selectedDongleIndex instead.
        private uint? _selectedDongleHid = null;
        // Exact Windows PnP InstanceId of the physical dongle this instance opened, e.g.
        // "USB\VID_096E&PID_0006\7&30D00E&0&4". Used to reset ONLY this instance's dongle so a
        // recovery on one instance does not knock the other instances' dongles offline. When
        // null, the reset falls back to matching every ROCKEY_HARDWARE_ID device (single-dongle case).
        private string? _selectedUsbInstanceId = null;
        private readonly object _dongleSelectionLock = new object();

        // The dongle's parent USB hub chain (immediate hub first, up to and including the root hub),
        // captured while the dongle is present. When the dongle wedges it drops OFF the USB bus, so
        // we can't look up its parent then - we power-cycle these remembered hubs instead. Persisted
        // to dongle-hub.txt next to the exe so it survives restarts.
        private readonly List<string> _dongleHubChain = new List<string>();
        // Persisted per dongle index so multiple instances of the same exe (each on its own dongle,
        // possibly a different hub) don't overwrite each other's saved hub.
        private static string HubChainFilePath(int dongleIndex) =>
            Path.Combine(AppContext.BaseDirectory, $"dongle-hub-{dongleIndex}.txt");

        private CancellationTokenSource? _cancellationTokenSource;
        private StreamWriter? _logWriter;
        private long _seedsTested = 0;
        private long _matchesFound = 0;
        private long _verifiedPasswords = 0;
        private Stopwatch _sw = new Stopwatch();
        private SpeedMode _speedMode = SpeedMode.Balanced;
        private TestMode _testMode = TestMode.Dictionary;
        private StringBuilder _logBuffer = new StringBuilder();
        private const int LOG_BUFFER_SIZE = 1000;
        private const int LOG_FLUSH_LINE_COUNT = 100;
        private int _logBufferedLines = 0;
        private bool _hasFlushedFirstLogLine = false;
        private long _totalSeedsToTest = 0;
        private string _lastTestedSeed = string.Empty;
        private bool _isPaused = false;

        private System.Windows.Forms.Timer _uiTimer;
        private long _uiTested;
        private long _uiGenerated;
        private long _uiVerified;
        private long _uiTotal;
        private readonly object _uiTotalLock = new object();
        private readonly object _logLock = new object();

        private sealed class ResumeInfo
        {
            public string LastSeed { get; set; } = string.Empty;
            public bool HasLastSeed => !string.IsNullOrEmpty(LastSeed);
        }

        private delegate void DongleOperation(
            Rockey4SmartClass.Rockey4Smart r4s,
            ushort handle,
            ushort p1,
            ushort p2,
            ushort p3,
            ushort p4,
            CancellationToken cancellationToken);

        // Multi-instance controls, built in code (see BuildMultiInstanceUi) so the existing
        // Designer layout stays untouched.
        private GroupBox grpMultiInstance = null!;
        private ComboBox cmbDongle = null!;
        private Button btnDetectDongles = null!;
        private Label lblDongleDetected = null!;
        private TextBox txtStartSeed = null!;
        private TextBox txtStopSeed = null!;
        private Button btnResetL1 = null!;
        private Button btnResetL2 = null!;

        public MainForm()
        {
            InitializeComponent();
            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 1000; // 1 second, adjust as needed
            _uiTimer.Tick += UiTimer_Tick;
            UiTotal = 0; // Initialize to avoid division by zero
            BuildMultiInstanceUi();
            LoadHubChain();
        }

        // Adds the "Multi-Instance" group (dongle selector + brute-force seed range) below the
        // existing controls, and moves the Results box down to make room. Done programmatically
        // to avoid disturbing the generated Designer file.
        private void BuildMultiInstanceUi()
        {
            const int groupTop = 724;   // where grpResults currently sits
            const int groupHeight = 130;
            const int shift = groupHeight + 8;

            grpMultiInstance = new GroupBox
            {
                Text = "Multi-Instance (select a dongle & seed range for this instance)",
                Location = new System.Drawing.Point(13, groupTop),
                Size = new System.Drawing.Size(730, groupHeight),
                TabStop = false
            };

            var lblDongleIndex = new Label { Text = "Dongle:", AutoSize = true, Location = new System.Drawing.Point(8, 27) };
            cmbDongle = new ComboBox
            {
                Location = new System.Drawing.Point(60, 24),
                Size = new System.Drawing.Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbDongle.SelectedIndexChanged += cmbDongle_SelectedIndexChanged;
            var toolTip = new ToolTip();
            toolTip.SetToolTip(cmbDongle, "The dongle this instance uses. Click 'Detect Dongles' to fill this list, then pick one. Run one app instance per dongle.");
            // Seed with a sensible default so a run works even before Detect is clicked.
            cmbDongle.Items.Add(DongleOption.FirstFound());
            cmbDongle.SelectedIndex = 0;

            btnDetectDongles = new Button
            {
                Text = "Detect Dongles",
                Location = new System.Drawing.Point(268, 23),
                Size = new System.Drawing.Size(120, 25)
            };
            btnDetectDongles.Click += btnDetectDongles_Click;

            lblDongleDetected = new Label
            {
                Text = "Click 'Detect Dongles' to list connected dongles.",
                AutoSize = false,
                Location = new System.Drawing.Point(396, 27),
                Size = new System.Drawing.Size(326, 20),
                ForeColor = System.Drawing.Color.Gray,
                AutoEllipsis = true
            };

            var lblStartSeed = new Label { Text = "Start seed:", AutoSize = true, Location = new System.Drawing.Point(8, 62) };
            txtStartSeed = new TextBox
            {
                Location = new System.Drawing.Point(78, 59),
                Size = new System.Drawing.Size(90, 23),
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 8
            };
            toolTip.SetToolTip(txtStartSeed, "Brute-force only. 8-hex-digit seed to begin at, e.g. 11111111. Leave empty to start at the beginning of the charset range.");

            var lblStopSeed = new Label { Text = "Stop before:", AutoSize = true, Location = new System.Drawing.Point(190, 62) };
            txtStopSeed = new TextBox
            {
                Location = new System.Drawing.Point(268, 59),
                Size = new System.Drawing.Size(90, 23),
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 8
            };
            toolTip.SetToolTip(txtStopSeed, "Brute-force only. Stop when this 8-hex-digit seed is reached (exclusive), e.g. 22222222. Leave empty to run to the end of the range.");

            var lblRangeHint = new Label
            {
                Text = "e.g. instance 1: 11111111 → 22222222,  instance 2: 22222222 → 33333333",
                AutoSize = true,
                Location = new System.Drawing.Point(372, 62),
                ForeColor = System.Drawing.Color.Gray
            };

            // Row 3: manual reset buttons for testing the recovery on demand (no need to wait for a wedge).
            var lblResetTest = new Label { Text = "Test reset:", AutoSize = true, Location = new System.Drawing.Point(8, 99) };
            btnResetL1 = new Button
            {
                Text = "Cycle Hub (L1)",
                Location = new System.Drawing.Point(78, 95),
                Size = new System.Drawing.Size(140, 25)
            };
            btnResetL1.Click += async (s, e) => await ManualResetAsync(1);
            toolTip.SetToolTip(btnResetL1, "Level 1: power-cycle the dongle's immediate parent hub (re-powers the port so a dropped-off dongle re-enumerates).");

            btnResetL2 = new Button
            {
                Text = "Cycle Parent Hub (L2)",
                Location = new System.Drawing.Point(226, 95),
                Size = new System.Drawing.Size(150, 25)
            };
            btnResetL2.Click += async (s, e) => await ManualResetAsync(2);
            toolTip.SetToolTip(btnResetL2, "Level 2: power-cycle the next hub up the chain. Disconnects more devices briefly; use if L1 doesn't recover it.");

            var lblResetHint = new Label
            {
                Text = "Recovery power-cycles the hub (captured on Detect / at run start). App escalates L1 → L2 automatically.",
                AutoSize = true,
                Location = new System.Drawing.Point(384, 99),
                ForeColor = System.Drawing.Color.Gray
            };

            grpMultiInstance.Controls.Add(lblDongleIndex);
            grpMultiInstance.Controls.Add(cmbDongle);
            grpMultiInstance.Controls.Add(btnDetectDongles);
            grpMultiInstance.Controls.Add(lblDongleDetected);
            grpMultiInstance.Controls.Add(lblStartSeed);
            grpMultiInstance.Controls.Add(txtStartSeed);
            grpMultiInstance.Controls.Add(lblStopSeed);
            grpMultiInstance.Controls.Add(txtStopSeed);
            grpMultiInstance.Controls.Add(lblRangeHint);
            grpMultiInstance.Controls.Add(lblResetTest);
            grpMultiInstance.Controls.Add(btnResetL1);
            grpMultiInstance.Controls.Add(btnResetL2);
            grpMultiInstance.Controls.Add(lblResetHint);

            Controls.Add(grpMultiInstance);

            // Push the Results group down and grow the form so nothing overlaps.
            grpResults.Location = new System.Drawing.Point(grpResults.Location.X, groupTop + shift);
            ClientSize = new System.Drawing.Size(ClientSize.Width, ClientSize.Height + shift);
        }

        private long UiTotal
        {
            get
            {
                lock (_uiTotalLock)
                {
                    return _uiTotal;
                }
            }
            set
            {
                lock (_uiTotalLock)
                {
                    _uiTotal = value;
                }
            }
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            // Skip UI updates if total is 0 to avoid division by zero
            long total = UiTotal;
            if (total <= 0)
                return;

            long tested = Volatile.Read(ref _uiTested);
            long generated = Volatile.Read(ref _uiGenerated);
            long verified = Volatile.Read(ref _uiVerified);

            double elapsed = _sw.Elapsed.TotalSeconds;
            double speed = elapsed > 0 ? tested / elapsed : 0;
            long remaining = Math.Max(0, total - tested);
            double etaSeconds = speed > 0 ? remaining / speed : 0;
            string eta = etaSeconds > 0 ? TimeSpan.FromSeconds(etaSeconds).ToString("c") : "N/A";

            lblSeedsTested.Text = $"{tested:N0}";
            lblMatchesFound.Text = $"{generated:N0}";
            lblVerified.Text = $"{verified:N0}";
            lblSpeed.Text = $"{speed:F1} seeds/sec";
            lblRemaining.Text = $"{remaining:N0}";
            lblETA.Text = eta;
            lblStatus.Text = $"Testing... {tested:N0}/{total:N0} seeds, {generated:N0} generated, {verified:N0} verified ({(tested * 100.0 / total):F1}%)";
            if (!string.IsNullOrEmpty(_lastTestedSeed))
            {
                txtLastSeed.Text = _lastTestedSeed;
            }
        }

        private void btnBrowseSeeds_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                ofd.Title = "Select seed list file";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtSeedListFile.Text = ofd.FileName;
                }
            }
        }

        private void btnBrowseLog_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                sfd.Title = "Select log file location";
                sfd.DefaultExt = "csv";
                sfd.FileName = "password_log.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    txtLogFile.Text = sfd.FileName;
                }
            }
        }

        private void btnModeDictionary_Click(object sender, EventArgs e)
        {
            _testMode = TestMode.Dictionary;
            btnModeDictionary.FlatStyle = FlatStyle.Flat;
            btnModeDictionary.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 120, 215);
            btnModeDictionary.FlatAppearance.BorderSize = 2;
            btnModeDictionary.Font = new System.Drawing.Font(btnModeDictionary.Font, System.Drawing.FontStyle.Bold);

            btnModeBruteForce.FlatStyle = FlatStyle.Standard;
            btnModeBruteForce.FlatAppearance.BorderSize = 0;
            btnModeBruteForce.Font = new System.Drawing.Font(btnModeBruteForce.Font, System.Drawing.FontStyle.Regular);

            grpBruteForceSettings.Enabled = false;
            grpSeedList.Enabled = true;
            lblMode.Text = "Mode: Dictionary (File-based)";
        }

        private void btnModeBruteForce_Click(object sender, EventArgs e)
        {
            _testMode = TestMode.BruteForce;
            btnModeBruteForce.FlatStyle = FlatStyle.Flat;
            btnModeBruteForce.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 120, 215);
            btnModeBruteForce.FlatAppearance.BorderSize = 2;
            btnModeBruteForce.Font = new System.Drawing.Font(btnModeBruteForce.Font, System.Drawing.FontStyle.Bold);

            btnModeDictionary.FlatStyle = FlatStyle.Standard;
            btnModeDictionary.FlatAppearance.BorderSize = 0;
            btnModeDictionary.Font = new System.Drawing.Font(btnModeDictionary.Font, System.Drawing.FontStyle.Regular);

            grpBruteForceSettings.Enabled = true;
            grpSeedList.Enabled = false;
            lblMode.Text = "Mode: Brute-Force (Generate on the fly)";
        }

        private async void btnTest_Click(object sender, EventArgs e)
        {
            if (_testMode == TestMode.Dictionary)
            {
                if (string.IsNullOrWhiteSpace(txtSeedListFile.Text))
                {
                    MessageBox.Show("Please select a seed list file.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(txtLogFile.Text))
            {
                MessageBox.Show("Please select a log file location.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            btnTest.Enabled = false;
            btnCancel.Enabled = true;
            btnPauseResume.Enabled = true;
            btnPauseResume.Text = "Pause";
            btnClear.Enabled = false;
            grpMultiInstance.Enabled = false;

            // Bind this run to the selected dongle and capture its hub chain for later recovery.
            // These spawn powershell.exe (Get-PnpDevice), which is slow, so run them OFF the UI thread
            // to keep the app responsive - doing them inline here is what made "Start" freeze for
            // seconds. Done here (not only on Detect) so the reset targets the right dongle/hub even if
            // the user never clicked Detect.
            int selectedIndex = CurrentDongleSelection().Index;
            lblStatus.Text = "Detecting dongle / hub...";
            await Task.Run(() =>
            {
                ResolveSelectedDongle(GetPhysicalDongleInstanceIds(), selectedIndex);
                LoadHubChain();          // this index's last-known hub, in case the dongle is already absent
                CaptureDongleHubChain(); // refresh while it's present (it drops off the bus when wedged)
            });

            txtResults.Clear();
            _seedsTested = 0;
            _matchesFound = 0;
            _verifiedPasswords = 0;
            _lastTestedSeed = string.Empty;
            lock (_logLock)
            {
                _logBuffer.Clear();
                _logBufferedLines = 0;
                _hasFlushedFirstLogLine = false;
            }
            lblSeedsTested.Text = "0";
            lblMatchesFound.Text = "0";
            lblVerified.Text = "0";
            lblSpeed.Text = "0 seeds/sec";
            lblRemaining.Text = "N/A";
            lblETA.Text = "N/A";
            txtLastSeed.Text = "";

            try
            {
                _speedMode = radSpeedFast.Checked ? SpeedMode.Fast :
                             radSpeedBalanced.Checked ? SpeedMode.Balanced : SpeedMode.Slow;

                string targetPassword = txtTargetPassword.Text.Trim();
                string logFilePath = ResolveLogFilePath(txtLogFile.Text);
                txtLogFile.Text = logFilePath;

                ResumeInfo resumeInfo = LoadResumeInfo(logFilePath);
                bool isResume = resumeInfo.HasLastSeed;
                if (isResume)
                {
                    _lastTestedSeed = resumeInfo.LastSeed;
                    txtLastSeed.Text = _lastTestedSeed;
                }

                _logWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                bool headerWritten = File.Exists(logFilePath) && new FileInfo(logFilePath).Length > 0;

                if (!headerWritten)
                {
                    _logWriter.WriteLine("Seed,Password,Word1,Word2,Word3,Word4,Status,Verified,Timestamp");
                    _logWriter.Flush();
                }

                List<string> seeds = new List<string>();
                if (_testMode == TestMode.Dictionary)
                {
                    seeds = await LoadSeedsAsync(txtSeedListFile.Text);
                    if (isResume)
                    {
                        lblStatus.Text = $"Resume mode: Last logged seed is {resumeInfo.LastSeed}. Loading remaining...";
                    }
                    else
                    {
                        lblStatus.Text = $"Loaded {seeds.Count} seeds. Testing...";
                    }
                }
                else
                {
                    if (isResume)
                    {
                        lblStatus.Text = $"Resume mode: Last logged seed is {resumeInfo.LastSeed}. Will continue after it...";
                    }
                    else
                    {
                        lblStatus.Text = "Generating seeds on the fly. Testing...";
                    }
                }

                ushort p1 = 0x530A;
                ushort p2 = 0x00FC;
                ushort p3 = 0xCB51;
                ushort p4 = 0x8C4E;

                if (ushort.TryParse(txtP1.Text, NumberStyles.HexNumber, null, out ushort parsedP1)) p1 = parsedP1;
                if (ushort.TryParse(txtP2.Text, NumberStyles.HexNumber, null, out ushort parsedP2)) p2 = parsedP2;
                if (ushort.TryParse(txtP3.Text, NumberStyles.HexNumber, null, out ushort parsedP3)) p3 = parsedP3;
                if (ushort.TryParse(txtP4.Text, NumberStyles.HexNumber, null, out ushort parsedP4)) p4 = parsedP4;

                _sw.Restart();

                Interlocked.Exchange(ref _uiTested, 0);
                Interlocked.Exchange(ref _uiGenerated, 0);
                Interlocked.Exchange(ref _uiVerified, 0);
                UiTotal = 0;
                _uiTimer.Start();

                if (_testMode == TestMode.Dictionary)
                {
                    int startIndex = 0;
                    if (isResume)
                    {
                        int lastSeedIndex = seeds.FindLastIndex(s => string.Equals(s, resumeInfo.LastSeed, StringComparison.Ordinal));
                        if (lastSeedIndex >= 0)
                        {
                            startIndex = lastSeedIndex + 1;
                        }
                        else
                        {
                            lblStatus.Text = $"Resume seed {resumeInfo.LastSeed} was not found in the seed list. Testing from the beginning...";
                        }
                    }

                    List<string> untestedSeeds = startIndex < seeds.Count
                        ? seeds.Skip(startIndex).ToList()
                        : new List<string>();
                    _totalSeedsToTest = untestedSeeds.Count;
                    UiTotal = untestedSeeds.Count; // Set total for timer updates

                    if (isResume && startIndex > 0)
                    {
                        lblStatus.Text = $"Resume: Continuing after {resumeInfo.LastSeed}. Testing {untestedSeeds.Count:N0} remaining seeds...";
                    }
                    else
                    {
                        lblStatus.Text = $"Testing {untestedSeeds.Count:N0} seeds...";
                    }

                    await RunOnStaThreadAsync(() => RunWithOpenDongle(p1, p2, p3, p4, (r4s, handle, openP1, openP2, openP3, openP4, token) =>
                    {
                        TestSeeds(untestedSeeds, r4s, handle, openP1, openP2, openP3, openP4, targetPassword, token);
                    }, _cancellationTokenSource.Token));
                }
                else
                {
                    string charset = txtCharset.Text;
                    int length = (int)nudLength.Value;
                    long charsetSpace = (long)Math.Pow(charset.Length, length);

                    // Range start for this instance: the explicit "Start seed", else the beginning.
                    long rangeStart = 0;
                    string startSeedText = txtStartSeed.Text.Trim();
                    if (!string.IsNullOrEmpty(startSeedText))
                    {
                        if (TryGetCombinationIndex(startSeedText, charset, length, out long si))
                        {
                            rangeStart = si;
                        }
                        else
                        {
                            lblStatus.Text = $"Start seed '{startSeedText}' is invalid for this charset/length ({length} chars). Starting from the beginning...";
                        }
                    }

                    // Range end (exclusive): smallest of the full space, the "Stop before" seed, and
                    // (kept for back-compat) the optional count limit as an absolute index cap.
                    long rangeStopExclusive = charsetSpace;
                    string stopSeedText = txtStopSeed.Text.Trim();
                    if (!string.IsNullOrEmpty(stopSeedText) && TryGetCombinationIndex(stopSeedText, charset, length, out long sp))
                    {
                        rangeStopExclusive = Math.Min(rangeStopExclusive, sp);
                    }
                    if (chkLimit.Checked)
                    {
                        rangeStopExclusive = Math.Min(rangeStopExclusive, (long)nudLimit.Value);
                    }

                    // Resume advances the start point, but only within this instance's assigned range.
                    long startIndex = rangeStart;
                    bool resumedWithinRange = false;
                    if (isResume && TryGetCombinationIndex(resumeInfo.LastSeed, charset, length, out long lastCombinationIndex))
                    {
                        long next = lastCombinationIndex + 1;
                        if (next > startIndex && next < rangeStopExclusive)
                        {
                            startIndex = next;
                            resumedWithinRange = true;
                        }
                    }

                    long? limit = rangeStopExclusive; // GenerateCombinations treats limit as an absolute, exclusive upper bound
                    _totalSeedsToTest = Math.Max(0, rangeStopExclusive - startIndex);
                    UiTotal = _totalSeedsToTest; // Set total for timer updates

                    string rangeDesc = (startSeedText.Length > 0 || stopSeedText.Length > 0)
                        ? $" [{(startSeedText.Length > 0 ? startSeedText : "start")} → {(stopSeedText.Length > 0 ? stopSeedText : "end")}]"
                        : string.Empty;
                    if (resumedWithinRange)
                    {
                        lblStatus.Text = $"Resume: continuing after {resumeInfo.LastSeed}. Testing {_totalSeedsToTest:N0} combinations{rangeDesc}...";
                    }
                    else
                    {
                        lblStatus.Text = $"Brute-forcing {_totalSeedsToTest:N0} combinations{rangeDesc}...";
                    }

                    await RunOnStaThreadAsync(() => RunWithOpenDongle(p1, p2, p3, p4, (r4s, handle, openP1, openP2, openP3, openP4, token) =>
                    {
                        TestCombinations(charset, length, limit, startIndex, r4s, handle, openP1, openP2, openP3, openP4, targetPassword, token);
                    }, _cancellationTokenSource.Token));
                }

                _sw.Stop();

                _uiTimer.Stop();

                this.Invoke((MethodInvoker)delegate
                {
                    lblStatus.Text = $"Complete! Tested {_seedsTested:N0} seeds, {_verifiedPasswords} verified passwords.";
                    lblSeedsTested.Text = $"{_seedsTested:N0}";
                    lblMatchesFound.Text = $"{_matchesFound:N0}";
                    lblVerified.Text = $"{_verifiedPasswords:N0}";
                    lblDongleStatus.Text = "Closed";
                    lblDongleStatus.ForeColor = System.Drawing.Color.Gray;
                    lblHandle.Text = "Closed";
                    if (!string.IsNullOrEmpty(_lastTestedSeed))
                    {
                        txtLastSeed.Text = _lastTestedSeed;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                _sw.Stop();
                _uiTimer.Stop();
                lblStatus.Text = "Testing cancelled.";
            }
            catch (InvalidOperationException ex)
            {
                _sw.Stop();
                _uiTimer.Stop();

                string errorTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Recovery is handled inside RunWithOpenDongle; if it gives up, we stop cleanly and ask
                // for a physical replug. We do NOT auto-restart the whole test (that previously caused
                // an unstoppable restart loop when a dongle stayed dead).
                this.Invoke((MethodInvoker)delegate
                {
                    txtResults.AppendText($"\r\n✗ [{errorTime}] {ex.Message}\r\n");
                    txtResults.AppendText($"   Please disconnect and reconnect the USB dongle, then click Start to resume from: {_lastTestedSeed}\r\n");
                    txtResults.ScrollToCaret();
                    lblStatus.Text = "Stopped - reconnect the dongle and click Start to resume.";
                });
            }
            catch (Exception ex)
            {
                _sw.Stop();
                _uiTimer.Stop();
                MessageBox.Show($"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error occurred.";
            }
            finally
            {
                FlushLogBuffer();
                _logWriter?.Close();
                _logWriter?.Dispose();
                _logWriter = null;

                btnTest.Enabled = true;
                btnCancel.Enabled = false;
                btnPauseResume.Enabled = false;
                btnClear.Enabled = true;
                grpMultiInstance.Enabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private void btnPauseResume_Click(object sender, EventArgs e)
        {
            if (_isPaused)
            {
                _isPaused = false;
                btnPauseResume.Text = "Pause";
                lblStatus.Text = "Resuming testing...";
            }
            else
            {
                _isPaused = true;
                btnPauseResume.Text = "Resume";
                lblStatus.Text = "Paused.";
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtResults.Clear();
        }

        private string ResolveLogFilePath(string logFilePath)
        {
            string trimmedPath = logFilePath.Trim();
            if (Path.IsPathRooted(trimmedPath))
            {
                return trimmedPath;
            }

            return Path.GetFullPath(trimmedPath);
        }

        private Task RunOnStaThreadAsync(Action action)
        {
            var completion = new TaskCompletionSource<object?>();
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                    completion.SetResult(null);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private void RunWithOpenDongle(ushort p1, ushort p2, ushort p3, ushort p4, DongleOperation operation, CancellationToken cancellationToken)
        {
            const int MAX_DONGLE_RETRY_ATTEMPTS = 3; // attempt 0 -> reset level 1, attempt 1 -> level 2, then give up
            const int FIND_RETRY_ATTEMPTS = 5;
            int retryAttempt = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Rockey4SmartClass.Rockey4Smart r4s = new Rockey4SmartClass.Rockey4Smart();
                ushort handle = 0;
                uint lp1 = 0;
                uint lp2 = 0;
                byte[] buffer = new byte[1024];
                bool opened = false;

                try
                {
                    ushort openP1 = p1;
                    ushort openP2 = p2;
                    ushort openP3 = p3;
                    ushort openP4 = p4;

                    // Retry finding device multiple times
                    ushort retcode = 1;
                    for (int findAttempt = 0; findAttempt < FIND_RETRY_ATTEMPTS; findAttempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        retcode = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref openP1, ref openP2, ref openP3, ref openP4, buffer);
                        if (retcode == 0) break;

                        if (findAttempt < FIND_RETRY_ATTEMPTS - 1)
                        {
                            AppendResult($"   RY_FIND attempt {findAttempt + 1}/{FIND_RETRY_ATTEMPTS} failed (code {retcode}). Retrying...");
                            // Cancellable 1s wait (returns immediately if the user clicks Stop).
                            if (cancellationToken.WaitHandle.WaitOne(1000))
                                throw new OperationCanceledException(cancellationToken);
                        }
                    }

                    if (retcode != 0)
                    {
                        throw new InvalidOperationException($"ROCKEY not found after {FIND_RETRY_ATTEMPTS} attempts. Last error code: {retcode}");
                    }

                    // Advance to the dongle this instance is bound to. RY_FIND landed on dongle #0
                    // (its hardware ID is in lp1). We then step RY_FIND_NEXT so multiple app instances
                    // can each drive a different physical dongle.
                    int dongleIndex;
                    uint? wantHid;
                    lock (_dongleSelectionLock) { dongleIndex = _selectedDongleIndex; wantHid = _selectedDongleHid; }
                    uint selectedHid = lp1;

                    if (wantHid.HasValue)
                    {
                        // Preferred path (user picked a dongle in the list): walk to the exact hardware
                        // ID. Robust even if the SDK enumerates the dongles in a different order than it
                        // did at Detect time - which is what made index-based selection unreliable.
                        int guard = 0;
                        while (selectedHid != wantHid.Value)
                        {
                            if (guard++ >= 64)
                                throw new InvalidOperationException($"Dongle with hardware ID 0x{wantHid.Value:X8} not found among the connected dongles.");
                            openP1 = p1; openP2 = p2; openP3 = p3; openP4 = p4;
                            retcode = r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref openP1, ref openP2, ref openP3, ref openP4, buffer);
                            if (retcode != 0)
                                throw new InvalidOperationException($"Dongle with hardware ID 0x{wantHid.Value:X8} not found. RY_FIND_NEXT returned error code {retcode}.");
                            selectedHid = lp1;
                        }
                        AppendResult($"   Bound to dongle #{dongleIndex} (hardware ID 0x{selectedHid:X8}).");
                    }
                    else
                    {
                        // Fallback (Detect never run): step by index as before.
                        for (int step = 0; step < dongleIndex; step++)
                        {
                            openP1 = p1; openP2 = p2; openP3 = p3; openP4 = p4;
                            retcode = r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref openP1, ref openP2, ref openP3, ref openP4, buffer);
                            if (retcode != 0)
                            {
                                throw new InvalidOperationException($"Dongle #{dongleIndex} not found (only {step + 1} dongle(s) present). RY_FIND_NEXT returned error code {retcode}.");
                            }
                            selectedHid = lp1;
                        }
                        if (dongleIndex > 0)
                        {
                            AppendResult($"   Bound to dongle #{dongleIndex} (hardware ID 0x{selectedHid:X8}).");
                        }
                    }

                    openP1 = p1;
                    openP2 = p2;
                    openP3 = p3;
                    openP4 = p4;
                    retcode = r4s.Rockey(RY_OPEN, ref handle, ref lp1, ref lp2, ref openP1, ref openP2, ref openP3, ref openP4, buffer);
                    if (retcode != 0)
                    {
                        throw new InvalidOperationException($"Failed to open dongle. RY_OPEN returned error code {retcode}.");
                    }

                    opened = true;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        lblDongleStatus.Text = "Connected ✓";
                        lblDongleStatus.ForeColor = System.Drawing.Color.Green;
                        lblHandle.Text = $"0x{handle:X4}";
                    });

                    operation(r4s, handle, openP1, openP2, openP3, openP4, cancellationToken);

                    // The operation returns normally both on completion AND on cancel (it breaks out of
                    // its loop). Distinguish so a Stop shows "cancelled" rather than "complete".
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);
                    return; // genuine completion
                }
                catch (OperationCanceledException)
                {
                    throw; // Stop requested - let it propagate cleanly, don't treat as a dongle error
                }
                catch (InvalidOperationException ex)
                {
                    // Release the SDK handle BEFORE resetting. Disabling a USB device while our own
                    // process still holds it open can leave the device busy and make the disable a
                    // no-op. Closing first lets Windows fully remove and re-enumerate it.
                    if (opened)
                    {
                        try
                        {
                            ushort ch = handle; uint clp1 = 0, clp2 = 0;
                            ushort cp1 = p1, cp2 = p2, cp3 = p3, cp4 = p4;
                            r4s.Rockey(RY_CLOSE, ref ch, ref clp1, ref clp2, ref cp1, ref cp2, ref cp3, ref cp4, buffer);
                        }
                        catch { }
                        opened = false;
                    }

                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);

                    string errorTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    AppendResult($"✗ [{errorTime}] Dongle error: {ex.Message}");

                    // Give up after the last attempt instead of looping forever.
                    if (retryAttempt >= MAX_DONGLE_RETRY_ATTEMPTS - 1)
                    {
                        throw new InvalidOperationException($"Dongle did not recover after {MAX_DONGLE_RETRY_ATTEMPTS} automatic attempts.");
                    }

                    // Escalate: level 1 (immediate hub) on the first wedge, level 2 (next hub up) after.
                    int resetLevel = retryAttempt + 1;
                    AppendResult($"   Automatic recovery attempt {retryAttempt + 1}/{MAX_DONGLE_RETRY_ATTEMPTS - 1} (reset level {resetLevel})...");
                    try
                    {
                        ResetUsbDongleAsync(resetLevel, cancellationToken).Wait(cancellationToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (AggregateException) { /* reset itself failed; fall through to wait + retry */ }

                    // Give the dongle time to re-initialise - but bail out instantly if Stop is clicked.
                    AppendResult($"   Waiting {USB_RESET_RETRY_DELAY_MS / 1000}s for the dongle to re-initialise...");
                    if (cancellationToken.WaitHandle.WaitOne(USB_RESET_RETRY_DELAY_MS))
                        throw new OperationCanceledException(cancellationToken);

                    retryAttempt++;
                }
                finally
                {
                    if (opened)
                    {
                        try
                        {
                            ushort closeHandle = handle;
                            uint closeLp1 = 0;
                            uint closeLp2 = 0;
                            ushort closeP1 = p1;
                            ushort closeP2 = p2;
                            ushort closeP3 = p3;
                            ushort closeP4 = p4;
                            r4s.Rockey(RY_CLOSE, ref closeHandle, ref closeLp1, ref closeLp2, ref closeP1, ref closeP2, ref closeP3, ref closeP4, buffer);
                        }
                        catch { }

                        BeginInvoke((MethodInvoker)delegate
                        {
                            lblDongleStatus.Text = "Closed";
                            lblDongleStatus.ForeColor = System.Drawing.Color.Gray;
                            lblHandle.Text = "Closed";
                        });
                    }
                }
            }
        }

        // Recovery works by power-cycling the USB HUB the dongle sits on: when the dongle wedges it
        // drops OFF the bus, so its own device node is gone and only re-powering the port brings it
        // back. `level` escalates UP the captured hub chain: level 1 = immediate hub (least
        // disruptive), level 2 = the next hub up, etc. If we have no hub info at all we fall back to
        // toggling the device node (only useful if it's still present).
        private async Task<bool> ResetUsbDongleAsync(int level = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Refresh the hub chain if the dongle happens to be present right now; otherwise use
                // what we captured while it was healthy. Off the UI thread (spawns powershell.exe) so a
                // manual reset can't freeze the window.
                await Task.Run(() => CaptureDongleHubChain(), cancellationToken);

                string? targetInstanceId;
                string? hubToCycle;
                lock (_dongleSelectionLock)
                {
                    targetInstanceId = _selectedUsbInstanceId;
                    hubToCycle = _dongleHubChain.Count > 0
                        ? _dongleHubChain[Math.Min(Math.Max(level - 1, 0), _dongleHubChain.Count - 1)]
                        : null;
                }

                // No hub known -> device-node toggle fallback (no shared-hub concern here).
                if (string.IsNullOrEmpty(hubToCycle))
                {
                    AppendResult($"   Reset level {level}: no hub captured yet - toggling device node.");
                    AppendResult("   (Tip: click 'Detect Dongles' or start a run while the dongle works so the hub can be remembered.)");
                    return await RunElevatedScriptAsync(BuildResetScript(targetInstanceId, null), cancellationToken);
                }

                // Cross-instance coordination. Cycling a hub disconnects EVERY dongle on it, so if
                // several app instances share a hub we must ensure only one instance cycles it and the
                // others recognise the disconnect as "a sibling is resetting the hub" - wait and
                // reconnect - instead of each firing its own reset (which would cascade endlessly).
                if (IsHubResetActive(hubToCycle))
                {
                    AppendResult($"   Another instance is power-cycling hub {hubToCycle}.");
                    AppendResult("   Waiting for it to finish instead of resetting again...");
                    await WaitForHubResetToClearAsync(hubToCycle, TimeSpan.FromSeconds(60), cancellationToken);
                    AppendResult("   Sibling hub reset finished. Reconnecting to the dongle...");
                    return true; // let the caller re-FIND/OPEN
                }

                using var gate = new Semaphore(1, 1, HubResetSemaphoreName(hubToCycle));
                // Cancellable acquire: wake on the semaphore OR the cancellation handle, up to 30s.
                int waitResult = await Task.Run(() =>
                    WaitHandle.WaitAny(new[] { gate, cancellationToken.WaitHandle }, TimeSpan.FromSeconds(30)));
                if (waitResult == 1) throw new OperationCanceledException(cancellationToken);
                bool acquired = waitResult == 0;
                try
                {
                    if (!acquired)
                    {
                        AppendResult("   Timed out waiting for the hub lock; another instance is likely handling it. Reconnecting...");
                        return true;
                    }

                    // A sibling may have cycled the hub while we queued on the lock. If our dongle is
                    // back on the bus, skip a redundant cycle (a wedged dongle is OFF the bus, so
                    // "present" here means recovered).
                    if (IsMyDonglePresent())
                    {
                        AppendResult("   Hub was already power-cycled by another instance; dongle is back. Reconnecting...");
                        return true;
                    }

                    SetHubResetMarker(hubToCycle, TimeSpan.FromSeconds(45));
                    AppendResult($"   Reset level {level}: power-cycling hub {hubToCycle}...");
                    return await RunElevatedScriptAsync(BuildResetScript(targetInstanceId, hubToCycle), cancellationToken);
                }
                finally
                {
                    ClearHubResetMarker(hubToCycle);
                    if (acquired) { try { gate.Release(); } catch { } }
                }
            }
            catch (Exception ex)
            {
                AppendResult($"   ✗ Reset exception: {ex.Message}");
                return false;
            }
        }

        // Launches the elevated PowerShell reset/cycle script, streams its output to the results box,
        // and returns true on exit code 0. Killed promptly if the run is cancelled.
        private async Task<bool> RunElevatedScriptAsync(string script, CancellationToken cancellationToken = default)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) { AppendResult("   ✗ Could not start reset helper."); return false; }

            // Kill the helper if the user clicks Stop mid-reset.
            using var reg = cancellationToken.Register(() => { try { process.Kill(); } catch { } });

            Task<string> outTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errTask = process.StandardError.ReadToEndAsync();

            bool finished = await Task.Run(() => process.WaitForExit(25000));
            cancellationToken.ThrowIfCancellationRequested();
            if (!finished)
            {
                try { process.Kill(); } catch { }
                AppendResult("   ✗ Reset timeout");
                return false;
            }

            string output = string.Empty, error = string.Empty;
            try { output = await outTask; } catch { }
            try { error = await errTask; } catch { }
            int exitCode = process.ExitCode;

            string outTrim = output.Trim(), errTrim = error.Trim();
            if (!string.IsNullOrWhiteSpace(outTrim)) AppendResult("   " + outTrim.Replace("\n", "\r\n   "));

            if (exitCode == 0)
            {
                AppendResult("   ✓ Reset command completed (you should hear the USB disconnect/reconnect chime).");
                return true;
            }

            AppendResult($"   ✗ Reset failed (exit code {exitCode}).");
            if (!string.IsNullOrWhiteSpace(errTrim)) AppendResult("   Error: " + errTrim);
            if (exitCode == 1) AppendResult("   The device/hub was not found. Is it plugged in?");
            return false;
        }

        private void AppendResult(string text)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate
            {
                txtResults.AppendText(text + "\r\n");
                txtResults.ScrollToCaret();
            });
        }

        private bool IsMyDonglePresent()
        {
            var present = GetPhysicalDongleInstanceIds();
            string? myId;
            lock (_dongleSelectionLock) { myId = _selectedUsbInstanceId; }
            if (!string.IsNullOrEmpty(myId))
                return present.Any(x => string.Equals(x, myId, StringComparison.OrdinalIgnoreCase));
            return present.Count > 0;
        }

        // ---- Cross-instance hub-reset coordination -------------------------------------------
        // A named Semaphore serialises the cycle across processes; a small marker file (with an
        // expiry, so a crashed process can't wedge the lock forever) tells sibling instances a reset
        // is in progress for that hub. Both are keyed by a hash of the hub InstanceId, so only
        // instances sharing the SAME physical hub coordinate; dongles on other hubs are independent.

        private static string HubResetKey(string hubId)
        {
            using var sha = System.Security.Cryptography.SHA1.Create();
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(hubId.ToUpperInvariant()));
            return Convert.ToHexString(h, 0, 8);
        }

        private static string HubResetSemaphoreName(string hubId) => @"Global\RockeyHubReset_" + HubResetKey(hubId);

        private static string HubResetMarkerPath(string hubId) =>
            Path.Combine(Path.GetTempPath(), "rockey-hubreset-" + HubResetKey(hubId) + ".lock");

        private static bool IsHubResetActive(string hubId)
        {
            try
            {
                string path = HubResetMarkerPath(hubId);
                if (!File.Exists(path)) return false;
                if (long.TryParse(File.ReadAllText(path).Trim(), out long expiryTicks))
                    return DateTime.UtcNow.Ticks < expiryTicks;
                return false;
            }
            catch { return false; }
        }

        private static void SetHubResetMarker(string hubId, TimeSpan duration)
        {
            try { File.WriteAllText(HubResetMarkerPath(hubId), (DateTime.UtcNow + duration).Ticks.ToString(CultureInfo.InvariantCulture)); }
            catch { /* best effort */ }
        }

        private static void ClearHubResetMarker(string hubId)
        {
            try { string p = HubResetMarkerPath(hubId); if (File.Exists(p)) File.Delete(p); }
            catch { /* best effort */ }
        }

        private static async Task WaitForHubResetToClearAsync(string hubId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            while (IsHubResetActive(hubId) && sw.Elapsed < timeout)
                await Task.Delay(500, cancellationToken);
        }

        // Builds the PowerShell recovery script, passed via -EncodedCommand (Base64 UTF-16LE) so no
        // quote/newline escaping is needed. Requires elevation (see app.manifest).
        //   - hubInstanceId set  -> disable+enable that HUB (re-powers the port, re-enumerates a
        //     dongle that dropped off the bus). This is the real fix.
        //   - hubInstanceId null -> fall back to toggling the dongle's own device node (only helps
        //     if it's still present).
        private static string BuildResetScript(string? targetInstanceId, string? hubInstanceId)
        {
            if (!string.IsNullOrEmpty(hubInstanceId))
            {
                string hub = EscapePsSingleQuoted(hubInstanceId);
                // Verbatim string: braces are literal; only " would need doubling (none here).
                return @"
$ErrorActionPreference = 'Stop'
try {
    $hubId = '" + hub + @"'
    $ph = Get-PnpDevice -InstanceId $hubId -ErrorAction Stop
    Write-Host ('Power-cycling hub ' + $hubId + ' (' + $ph.FriendlyName + ')')
    Disable-PnpDevice -InstanceId $hubId -Confirm:$false -ErrorAction Stop
    Write-Host 'Hub disabled'
    Start-Sleep -Seconds 3
    Enable-PnpDevice -InstanceId $hubId -Confirm:$false -ErrorAction Stop
    Write-Host 'Hub enabled'
    Start-Sleep -Seconds 4
    Write-Host 'Hub power-cycle complete'
    exit 0
} catch {
    Write-Host ('Error: ' + $_)
    exit 3
}";
            }

            // Fallback: toggle the dongle's own device node. The top-level USB node enumerates under
            // Class HIDClass, so match by the "USB\VID_096E&PID_0006" InstanceId prefix (the "USB\"
            // prefix excludes the "HID\..." children).
            string selection = !string.IsNullOrEmpty(targetInstanceId)
                ? "$dev = Get-PnpDevice -InstanceId '" + EscapePsSingleQuoted(targetInstanceId) + "' -ErrorAction Stop"
                : "$dev = Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object { $_.InstanceId -like '" + EscapePsSingleQuoted(ROCKEY_HARDWARE_ID) + "*' }";

            return @"
$ErrorActionPreference = 'Stop'
try {
    " + selection + @"
    if (-not $dev) { Write-Host 'ROCKEY dongle not found'; exit 1 }
    foreach ($d in @($dev)) {
        Disable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction Stop
        Write-Host ('Disabled ' + $d.InstanceId)
    }
    Start-Sleep -Seconds 2
    foreach ($d in @($dev)) {
        Enable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction Stop
        Write-Host ('Enabled ' + $d.InstanceId)
    }
    Start-Sleep -Seconds 2
    Write-Host 'USB reset complete'
    exit 0
} catch {
    Write-Host ('Error: ' + $_)
    exit 3
}";
        }

        private static string EscapePsSingleQuoted(string value)
        {
            // In a PowerShell single-quoted string, only the single quote itself needs escaping
            // (by doubling). Backslashes and & are literal, which is what we want for InstanceIds.
            return value.Replace("'", "''");
        }

        // ---- Multi-instance: dongle detection & selection -------------------------------------

        private void GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4)
        {
            p1 = 0x530A; p2 = 0x00FC; p3 = 0xCB51; p4 = 0x8C4E;
            if (ushort.TryParse(txtP1.Text, NumberStyles.HexNumber, null, out ushort v1)) p1 = v1;
            if (ushort.TryParse(txtP2.Text, NumberStyles.HexNumber, null, out ushort v2)) p2 = v2;
            if (ushort.TryParse(txtP3.Text, NumberStyles.HexNumber, null, out ushort v3)) p3 = v3;
            if (ushort.TryParse(txtP4.Text, NumberStyles.HexNumber, null, out ushort v4)) p4 = v4;
        }

        // Enumerates the hardware IDs of every connected dongle via RY_FIND + RY_FIND_NEXT.
        // Must be called on an STA thread (the native SDK expects it). Best-effort: returns an
        // empty list on any error.
        private List<uint> EnumerateSdkDongles(ushort p1, ushort p2, ushort p3, ushort p4)
        {
            var ids = new List<uint>();
            try
            {
                var r4s = new Rockey4SmartClass.Rockey4Smart();
                ushort handle = 0;
                uint lp1 = 0, lp2 = 0;
                byte[] buffer = new byte[1024];

                ushort a = p1, b = p2, c = p3, d = p4;
                ushort rc = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);

                int guard = 0;
                while (rc == 0 && guard++ < 64)
                {
                    ids.Add(lp1); // RY_FIND / RY_FIND_NEXT return the dongle hardware ID in lp1
                    a = p1; b = p2; c = p3; d = p4; lp1 = 0; lp2 = 0;
                    rc = r4s.Rockey(RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref a, ref b, ref c, ref d, buffer);
                }
            }
            catch { /* best effort */ }
            return ids;
        }

        // Lists the Windows PnP InstanceIds of the physical Rockey dongles currently present,
        // sorted for a stable order so a given index maps to the same device across calls.
        private List<string> GetPhysicalDongleInstanceIds()
        {
            var list = new List<string>();
            try
            {
                // Match the USB device node by InstanceId prefix (no -Class filter: the node is
                // Class HIDClass). The "USB\" prefix excludes the "HID\..." function children.
                string script =
                    "Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | " +
                    "Where-Object { $_.InstanceId -like '" + EscapePsSingleQuoted(ROCKEY_HARDWARE_ID) + "*' } | " +
                    "Sort-Object InstanceId | Select-Object -ExpandProperty InstanceId";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                list = RunPowerShellCaptureLines(script, 10000);
            }
            catch { /* best effort */ }
            return list;
        }

        // Runs a short PowerShell script and returns its non-empty stdout lines. Read-only PnP
        // queries don't need elevation. Best-effort: returns whatever it captured.
        private static List<string> RunPowerShellCaptureLines(string script, int timeoutMs)
        {
            var list = new List<string>();
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? p = null;
            try
            {
                p = Process.Start(psi);
                if (p == null) return list;

                // Drain BOTH streams asynchronously and enforce the timeout by killing a wedged
                // process. The old code did a synchronous StandardOutput.ReadToEnd() (which has no
                // timeout) BEFORE WaitForExit(timeoutMs), so the timeout never applied: if powershell
                // stalled - e.g. walking a longer parent-hub chain when the dongle is behind a USB hub,
                // or filling the un-drained stderr pipe - ReadToEnd blocked forever, freezing the UI on
                // Detect and hanging the pre-run hub capture on Start.
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();

                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                    return list;
                }

                string outp = string.Empty;
                try { outp = outTask.GetAwaiter().GetResult(); } catch { /* best effort */ }
                try { _ = errTask.GetAwaiter().GetResult(); } catch { /* drained, ignored */ }

                foreach (var line in outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) list.Add(trimmed);
                }
            }
            catch { /* best effort */ }
            finally
            {
                try { p?.Dispose(); } catch { /* best effort */ }
            }
            return list;
        }

        // Queries the dongle's parent USB hub chain (immediate hub first, up to and including the
        // root hub) while the physical device is present. Stops before the PCI host controller.
        private List<string> QueryDongleHubChain()
        {
            string hw = EscapePsSingleQuoted(ROCKEY_HARDWARE_ID);
            string script =
                "$dev = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | " +
                "Where-Object { $_.InstanceId -like '" + hw + "*' }; " +
                "if ($dev) { $cur = (@($dev)[0]).InstanceId; for ($i=0; $i -lt 8; $i++) { " +
                "$p = (Get-PnpDeviceProperty -InstanceId $cur -KeyName 'DEVPKEY_Device_Parent' -ErrorAction SilentlyContinue).Data; " +
                "if (-not $p) { break }; " +
                "if ($p -like 'PCI\\*') { break }; " +
                "if (($p -like 'ROOT\\*') -and ($p -notlike 'USB\\ROOT_HUB*')) { break }; " +
                "$p; $cur = $p; " +
                "if ($p -like 'USB\\ROOT_HUB*') { break } } }";
            try { return RunPowerShellCaptureLines(script, 10000); }
            catch { return new List<string>(); }
        }

        // Captures & persists the hub chain if the dongle is currently present. No-op if it's absent
        // (we keep whatever we captured last time).
        private void CaptureDongleHubChain()
        {
            var chain = QueryDongleHubChain();
            if (chain.Count == 0) return;
            int index;
            lock (_dongleSelectionLock)
            {
                index = _selectedDongleIndex;
                _dongleHubChain.Clear();
                _dongleHubChain.AddRange(chain);
            }
            try { File.WriteAllLines(HubChainFilePath(index), chain); } catch { /* best effort */ }
        }

        private void LoadHubChain()
        {
            try
            {
                int index;
                lock (_dongleSelectionLock) { index = _selectedDongleIndex; }
                string path = HubChainFilePath(index);
                if (!File.Exists(path)) return;
                var lines = File.ReadAllLines(path);
                lock (_dongleSelectionLock)
                {
                    _dongleHubChain.Clear();
                    foreach (var l in lines)
                    {
                        string t = l.Trim();
                        if (t.Length > 0) _dongleHubChain.Add(t);
                    }
                }
            }
            catch { /* best effort */ }
        }

        // Records which dongle index this instance uses and the physical InstanceId to reset for it.
        // When the index is out of range of the detected physical devices, the reset target is left
        // null so ResetUsbDongleAsync falls back to the VID/PID match (fine for a single dongle).
        private void ResolveSelectedDongle(List<string> physicalInstanceIds, int index)
        {
            lock (_dongleSelectionLock)
            {
                _selectedDongleIndex = index;
                _selectedUsbInstanceId = (index >= 0 && index < physicalInstanceIds.Count)
                    ? physicalInstanceIds[index]
                    : null;
            }
        }

        // One entry in the dongle picker. Index is the SDK enumeration position (RY_FIND = 0, then one
        // per RY_FIND_NEXT); Hid is the SDK hardware ID used to bind to this exact dongle regardless of
        // enumeration order. UsbInstanceId pairs it (best-effort) with a Windows device for reset.
        private sealed class DongleOption
        {
            public int Index { get; init; }
            public uint? Hid { get; init; }
            public string? UsbInstanceId { get; init; }

            // The pre-Detect default: bind to whatever dongle RY_FIND lands on first.
            public static DongleOption FirstFound() => new DongleOption { Index = 0, Hid = null };

            public override string ToString() =>
                Hid.HasValue ? $"#{Index} — HID 0x{Hid.Value:X8}" : "#0 (first found)";
        }

        // The currently picked entry (UI thread only). Never null: falls back to the "first found"
        // default so a run works even before Detect has ever been clicked.
        private DongleOption CurrentDongleSelection() =>
            cmbDongle.SelectedItem as DongleOption ?? DongleOption.FirstFound();

        private void cmbDongle_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbDongle.SelectedItem is not DongleOption opt) return;
            lock (_dongleSelectionLock)
            {
                _selectedDongleIndex = opt.Index;
                _selectedDongleHid = opt.Hid;
                _selectedUsbInstanceId = opt.UsbInstanceId;
            }
        }

        private async void btnDetectDongles_Click(object? sender, EventArgs e)
        {
            btnDetectDongles.Enabled = false;
            lblDongleDetected.ForeColor = System.Drawing.Color.Gray;
            lblDongleDetected.Text = "Detecting...";
            try
            {
                GetDongleParams(out ushort p1, out ushort p2, out ushort p3, out ushort p4);

                List<uint> hids = new List<uint>();
                await RunOnStaThreadAsync(() => { hids = EnumerateSdkDongles(p1, p2, p3, p4); });
                List<string> usb = await Task.Run(() => GetPhysicalDongleInstanceIds());

                // Keep the user's current pick (by hardware ID) selected across a re-detect.
                uint? previousHid;
                lock (_dongleSelectionLock) { previousHid = _selectedDongleHid; }

                // Rebuild the picker from the SDK-enumerated dongles - the same order the run walks with
                // RY_FIND / RY_FIND_NEXT, so the chosen HID maps to the right device. Pairing each with a
                // Windows USB InstanceId (for reset targeting) is best-effort: that list is sorted
                // independently, so it only lines up when the two orders happen to agree.
                cmbDongle.SelectedIndexChanged -= cmbDongle_SelectedIndexChanged;
                cmbDongle.Items.Clear();
                if (hids.Count == 0)
                {
                    cmbDongle.Items.Add(DongleOption.FirstFound());
                }
                else
                {
                    for (int i = 0; i < hids.Count; i++)
                    {
                        cmbDongle.Items.Add(new DongleOption
                        {
                            Index = i,
                            Hid = hids[i],
                            UsbInstanceId = i < usb.Count ? usb[i] : null
                        });
                    }
                }

                // Restore the previous selection by HID if that dongle is still present; else pick #0.
                int restore = 0;
                if (previousHid.HasValue)
                {
                    for (int i = 0; i < cmbDongle.Items.Count; i++)
                    {
                        if (cmbDongle.Items[i] is DongleOption o && o.Hid == previousHid)
                        {
                            restore = i;
                            break;
                        }
                    }
                }
                cmbDongle.SelectedIndexChanged += cmbDongle_SelectedIndexChanged;
                cmbDongle.SelectedIndex = restore; // fires the handler -> updates the shared selection

                // Capture the hub for the now-selected dongle (off the UI thread - spawns powershell).
                await Task.Run(() => CaptureDongleHubChain());

                if (hids.Count > 0)
                {
                    lblDongleDetected.ForeColor = System.Drawing.Color.Black;
                    lblDongleDetected.Text =
                        $"Found {hids.Count} dongle(s); Windows sees {usb.Count} USB device(s). Pick one on the left.";
                }
                else
                {
                    lblDongleDetected.ForeColor = usb.Count > 0
                        ? System.Drawing.Color.DarkOrange
                        : System.Drawing.Color.Red;
                    lblDongleDetected.Text = usb.Count > 0
                        ? $"SDK found no dongles, but Windows sees {usb.Count} USB device(s). Check the dongle params / drivers."
                        : "No dongles found. Is one plugged in?";
                }
            }
            catch (Exception ex)
            {
                lblDongleDetected.ForeColor = System.Drawing.Color.Red;
                lblDongleDetected.Text = "Detect failed: " + ex.Message;
            }
            finally
            {
                btnDetectDongles.Enabled = true;
            }
        }

        // Runs a reset on demand so you can confirm recovery works without waiting for a wedge.
        private async Task ManualResetAsync(int level)
        {
            if (_cancellationTokenSource != null)
            {
                MessageBox.Show("A test is running. Stop it before resetting the dongle manually.",
                    "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnResetL1.Enabled = false;
            btnResetL2.Enabled = false;
            btnDetectDongles.Enabled = false;
            try
            {
                // Target this instance's dongle (falls back to VID/PID match if not resolvable).
                // GetPhysicalDongleInstanceIds spawns powershell.exe, so run it off the UI thread.
                int resetIndex = CurrentDongleSelection().Index;
                await Task.Run(() => ResolveSelectedDongle(GetPhysicalDongleInstanceIds(), resetIndex));
                txtResults.AppendText($"— Manual reset (level {level}) —\r\n");
                bool ok = await ResetUsbDongleAsync(level);
                txtResults.AppendText(ok
                    ? "   ✓ Reset command completed. Check whether the dongle now responds.\r\n"
                    : "   ✗ Reset command did not complete successfully (see messages above).\r\n");
                txtResults.ScrollToCaret();
            }
            finally
            {
                btnResetL1.Enabled = true;
                btnResetL2.Enabled = true;
                btnDetectDongles.Enabled = true;
            }
        }

        private void UpdateDongleInfo()
        {
            try
            {
                Rockey4SmartClass.Rockey4Smart r4s = new Rockey4SmartClass.Rockey4Smart();
                ushort handle = 0;
                uint lp1 = 0;
                uint lp2 = 0;
                ushort p1 = 0x530A;
                ushort p2 = 0x00FC;
                ushort p3 = 0xCB51;
                ushort p4 = 0x8C4E;
                if (ushort.TryParse(txtP1.Text, NumberStyles.HexNumber, null, out ushort parsedP1)) p1 = parsedP1;
                if (ushort.TryParse(txtP2.Text, NumberStyles.HexNumber, null, out ushort parsedP2)) p2 = parsedP2;
                if (ushort.TryParse(txtP3.Text, NumberStyles.HexNumber, null, out ushort parsedP3)) p3 = parsedP3;
                if (ushort.TryParse(txtP4.Text, NumberStyles.HexNumber, null, out ushort parsedP4)) p4 = parsedP4;
                byte[] buffer = new byte[1024];

                ushort findP1 = p1;
                ushort findP2 = p2;
                ushort findP3 = p3;
                ushort findP4 = p4;
                ushort retcode = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref findP1, ref findP2, ref findP3, ref findP4, buffer);

                if (retcode == 0)
                {
                    retcode = r4s.Rockey(RY_OPEN, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    if (retcode == 0)
                    {
                        lblDongleStatus.Text = "Connected ✓";
                        lblDongleStatus.ForeColor = System.Drawing.Color.Green;
                        lblHandle.Text = $"0x{handle:X4}";
                        r4s.Rockey(RY_CLOSE, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    }
                    else
                    {
                        lblDongleStatus.Text = "Not Connected ✗";
                        lblDongleStatus.ForeColor = System.Drawing.Color.Red;
                        lblHandle.Text = "N/A";
                    }
                }
                else
                {
                    lblDongleStatus.Text = "Not Found ✗";
                    lblDongleStatus.ForeColor = System.Drawing.Color.Red;
                    lblHandle.Text = "N/A";
                }
            }
            catch
            {
                lblDongleStatus.Text = "Error ✗";
                lblDongleStatus.ForeColor = System.Drawing.Color.Red;
                lblHandle.Text = "N/A";
            }
        }

        private ResumeInfo LoadResumeInfo(string logFilePath)
        {
            var resumeInfo = new ResumeInfo();

            if (File.Exists(logFilePath))
            {
                try
                {
                    List<string> fields = ParseCsvLine(ReadLastNonEmptyLine(logFilePath));
                    string seed = fields.Count > 0 ? fields[0] : string.Empty;
                    if (!string.IsNullOrEmpty(seed) && !seed.Equals("Seed", StringComparison.OrdinalIgnoreCase))
                    {
                        resumeInfo.LastSeed = seed;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Warning: Could not read log file for resume: {ex.Message}", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return resumeInfo;
        }

        private string ReadLastNonEmptyLine(string filePath)
        {
            const int BufferSize = 8192;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length == 0)
                    return string.Empty;

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
        }

        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
                return fields;

            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];

                if (current == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    fields.Add(field.ToString().Trim());
                    field.Clear();
                    continue;
                }

                field.Append(current);
            }

            fields.Add(field.ToString().Trim());
            return fields;
        }

        private async System.Threading.Tasks.Task<List<string>> LoadSeedsAsync(string filePath)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                List<string> seeds = new List<string>();

                try
                {
                    string[] lines = File.ReadAllLines(filePath);

                    foreach (string line in lines)
                    {
                        string seed = line.Trim();

                        if (!string.IsNullOrEmpty(seed) && !seed.StartsWith("#"))
                        {
                            seeds.Add(seed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load seed list: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return seeds;
            });
        }

        private IEnumerable<string> GenerateCombinations(string charset, int length, long? limit = null, long startIndex = 0)
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
                if (limit.HasValue && count >= limit.Value) break;

                var combination = new char[length];
                for (int i = 0; i < length; i++)
                {
                    combination[i] = charsetArray[indices[i]];
                }
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

        private bool TryGetCombinationIndex(string seed, string charset, int length, out long index)
        {
            index = 0;

            if (seed.Length != length || string.IsNullOrEmpty(charset))
                return false;

            var charIndexes = new Dictionary<char, int>();
            for (int i = 0; i < charset.Length; i++)
            {
                if (!charIndexes.ContainsKey(charset[i]))
                {
                    charIndexes.Add(charset[i], i);
                }
            }

            for (int i = 0; i < seed.Length; i++)
            {
                if (!charIndexes.TryGetValue(seed[i], out int charIndex))
                    return false;

                checked
                {
                    index = (index * charset.Length) + charIndex;
                }
            }

            return true;
        }

        private void TestSeeds(List<string> seeds, Rockey4SmartClass.Rockey4Smart r4s, ushort handle, ushort initialP1, ushort initialP2, ushort initialP3, ushort initialP4, string targetPassword, CancellationToken cancellationToken)
        {
            long tested = 0;
            long generated = 0;
            long verified = 0;

            uint lp1 = 0;
            uint lp2 = 0;
            ushort currentP1 = initialP1;
            ushort currentP2 = initialP2;
            ushort currentP3 = initialP3;
            ushort currentP4 = initialP4;
            byte[] buffer = new byte[1024];

            bool headerWritten = false;
            int yieldInterval = _speedMode == SpeedMode.Fast ? 50 : _speedMode == SpeedMode.Balanced ? 100 : 200;
            int consecutiveSeedErrors = 0;

            // If this call is a re-run after an auto-recovery, skip the seeds already tested so we
            // don't re-test (and re-log) them.
            int startFrom = 0;
            if (!string.IsNullOrEmpty(_lastTestedSeed))
            {
                int li = seeds.FindLastIndex(s => string.Equals(s, _lastTestedSeed, StringComparison.Ordinal));
                if (li >= 0) startFrom = li + 1;
            }

            for (int seedIdx = startFrom; seedIdx < seeds.Count; seedIdx++)
            {
                string seed = seeds[seedIdx];
                if (cancellationToken.IsCancellationRequested)
                    break;

                while (_isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    cancellationToken.WaitHandle.WaitOne(50);
                }

                try
                {
                    if (!TryConvertSeedToUint(seed, out uint seedValue))
                    {
                        throw new FormatException($"Seed '{seed}' is not an 8-character hexadecimal 32-bit value.");
                    }

                    lp2 = seedValue;
                    lp1 = 0;
                    Array.Clear(buffer, 0, buffer.Length);
                    ushort p1 = currentP1;
                    ushort p2 = currentP2;
                    ushort p3 = currentP3;
                    ushort p4 = currentP4;

                    ushort retcode = r4s.Rockey(RY_SEED, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    string passwordStr = "ERROR";
                    string status = "ERROR";
                    bool verifiedOK = false;

                    if (retcode == 0)
                    {
                        passwordStr = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
                        status = "SUCCESS";
                        generated++;
                        verifiedOK = true;
                        verified++;
                        _lastTestedSeed = seed;
                        currentP1 = p1;
                        currentP2 = p2;
                        currentP3 = p3;
                        currentP4 = p4;
                        consecutiveSeedErrors = 0;

                        if (!string.IsNullOrEmpty(targetPassword) && passwordStr.Equals(targetPassword, StringComparison.OrdinalIgnoreCase))
                        {
                            FlushLogBuffer();
                            _logWriter?.Close();

                            this.Invoke((MethodInvoker)delegate
                            {
                                MessageBox.Show($"✓✓ FOUND MATCH!\n\nSeed: {seed}\nPassword: {passwordStr}\n\nSeeds Tested: {tested:N0}\nTime Elapsed: {_sw.Elapsed:hh\\:mm\\:ss}", "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                txtResults.AppendText($"\n✓✓✓✓ MATCH FOUND! ✓✓✓✓\nSeed: {seed}\nPassword: {passwordStr}\nSeeds Tested: {tested:N0}\r\n");
                                txtLastSeed.Text = seed;
                                txtResults.ScrollToCaret();
                            });

                            _cancellationTokenSource?.Cancel();
                            return;
                        }
                    }
                    else
                    {
                        status = $"ERROR_{retcode}";
                        consecutiveSeedErrors++;
                    }

                    LogResult(seed, passwordStr, p1, p2, p3, p4, status, verifiedOK, ref headerWritten);

                    if (consecutiveSeedErrors >= MAX_CONSECUTIVE_SEED_ERRORS)
                    {
                        throw new InvalidOperationException($"RY_SEED failed {MAX_CONSECUTIVE_SEED_ERRORS} times in a row. Last error code: {retcode}. Testing stopped to avoid filling the log with invalid results.");
                    }

                    tested++;

                    // Update UI counter variables (timer will handle actual UI updates)
                    Interlocked.Exchange(ref _uiTested, tested);
                    Interlocked.Exchange(ref _uiGenerated, generated);
                    Interlocked.Exchange(ref _uiVerified, verified);

                    // Periodically yield to keep UI responsive
                    if (tested % yieldInterval == 0)
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogError(seed, ex.Message);
                }
            }

            FlushLogBuffer();
            _seedsTested = tested;
            _matchesFound = generated;
            _verifiedPasswords = verified;
        }

        private void TestCombinations(string charset, int length, long? limit, long startIndex, Rockey4SmartClass.Rockey4Smart r4s, ushort handle, ushort initialP1, ushort initialP2, ushort initialP3, ushort initialP4, string targetPassword, CancellationToken cancellationToken)
        {
            long tested = 0;
            long generated = 0;
            long verified = 0;
            long skipped = startIndex;

            uint lp1 = 0;
            uint lp2 = 0;
            ushort currentP1 = initialP1;
            ushort currentP2 = initialP2;
            ushort currentP3 = initialP3;
            ushort currentP4 = initialP4;
            byte[] buffer = new byte[1024];

            bool headerWritten = false;
            long total = limit.HasValue ? limit.Value : (long)Math.Pow(charset.Length, length);
            int yieldInterval = _speedMode == SpeedMode.Fast ? 50 : _speedMode == SpeedMode.Balanced ? 100 : 200;
            int consecutiveSeedErrors = 0;

            // If this is a re-run after an auto-recovery, resume just past the last tested seed so we
            // don't re-test (and re-log) combinations already done.
            long effectiveStart = startIndex;
            if (!string.IsNullOrEmpty(_lastTestedSeed)
                && TryGetCombinationIndex(_lastTestedSeed, charset, length, out long lastIdx)
                && lastIdx + 1 > effectiveStart)
            {
                effectiveStart = lastIdx + 1;
            }
            skipped = effectiveStart;

            foreach (string seed in GenerateCombinations(charset, length, limit, effectiveStart))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                while (_isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    cancellationToken.WaitHandle.WaitOne(50);
                }

                try
                {
                    if (!TryConvertSeedToUint(seed, out uint seedValue))
                    {
                        throw new FormatException($"Seed '{seed}' is not an 8-character hexadecimal 32-bit value.");
                    }

                    lp2 = seedValue;
                    lp1 = 0;
                    Array.Clear(buffer, 0, buffer.Length);
                    ushort p1 = currentP1;
                    ushort p2 = currentP2;
                    ushort p3 = currentP3;
                    ushort p4 = currentP4;

                    ushort retcode = r4s.Rockey(RY_SEED, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    string passwordStr = "ERROR";
                    string status = "ERROR";
                    bool verifiedOK = false;

                    if (retcode == 0)
                    {
                        passwordStr = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
                        status = "SUCCESS";
                        generated++;
                        verifiedOK = true;
                        verified++;
                        _lastTestedSeed = seed;
                        currentP1 = p1;
                        currentP2 = p2;
                        currentP3 = p3;
                        currentP4 = p4;
                        consecutiveSeedErrors = 0;

                        if (!string.IsNullOrEmpty(targetPassword) && passwordStr.Equals(targetPassword, StringComparison.OrdinalIgnoreCase))
                        {
                            FlushLogBuffer();
                            _logWriter?.Close();

                            this.Invoke((MethodInvoker)delegate
                            {
                                MessageBox.Show($"✓✓ FOUND MATCH!\n\nSeed: {seed}\nPassword: {passwordStr}\n\nSeeds Tested: {tested:N0}\nSkipped: {skipped:N0}\nTime Elapsed: {_sw.Elapsed:hh\\:mm\\:ss}", "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                txtResults.AppendText($"\n✓✓✓✓ MATCH FOUND! ✓✓✓✓\nSeed: {seed}\nPassword: {passwordStr}\nSeeds Tested: {tested:N0}\nSkipped: {skipped:N0}\r\n");
                                txtLastSeed.Text = seed;
                                txtResults.ScrollToCaret();
                            });

                            _cancellationTokenSource?.Cancel();
                            return;
                        }
                    }
                    else
                    {
                        status = $"ERROR_{retcode}";
                        consecutiveSeedErrors++;
                    }

                    LogResult(seed, passwordStr, p1, p2, p3, p4, status, verifiedOK, ref headerWritten);

                    if (consecutiveSeedErrors >= MAX_CONSECUTIVE_SEED_ERRORS)
                    {
                        throw new InvalidOperationException($"RY_SEED failed {MAX_CONSECUTIVE_SEED_ERRORS} times in a row. Last error code: {retcode}. Testing stopped to avoid filling the log with invalid results.");
                    }

                    tested++;

                    // Update UI counter variables (timer will handle actual UI updates)
                    Interlocked.Exchange(ref _uiTested, tested);
                    Interlocked.Exchange(ref _uiGenerated, generated);
                    Interlocked.Exchange(ref _uiVerified, verified);

                    // Periodically yield to keep UI responsive - more aggressive in fast mode
                    if (tested % yieldInterval == 0)
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogError(seed, ex.Message);
                }
            }

            FlushLogBuffer();
            _seedsTested = tested;
            _matchesFound = generated;
            _verifiedPasswords = verified;
        }

        private void LogResult(string seed, string passwordStr, ushort p1, ushort p2, ushort p3, ushort p4, string status, bool verifiedOK, ref bool headerWritten)
        {
            string word1 = verifiedOK ? $"{p1:X4}" : "N/A";
            string word2 = verifiedOK ? $"{p2:X4}" : "N/A";
            string word3 = verifiedOK ? $"{p3:X4}" : "N/A";
            string word4 = verifiedOK ? $"{p4:X4}" : "N/A";
            string verifiedStr = verifiedOK ? "YES" : "NO";

            string logLine = $"\"{seed}\",\"{passwordStr}\",{word1},{word2},{word3},{word4},{status},{verifiedStr},{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            lock (_logLock)
            {
                _logBuffer.AppendLine(logLine);
                _logBufferedLines++;

                if (!_hasFlushedFirstLogLine || _logBufferedLines >= LOG_FLUSH_LINE_COUNT || _logBuffer.Length > LOG_BUFFER_SIZE * 100)
                {
                    FlushLogBuffer();
                }
            }
        }

        private void LogError(string seed, string errorMessage)
        {
            string logLine = $"\"{seed}\",\"EXCEPTION\",N/A,N/A,N/A,N/A,EXCEPTION,NO,{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            lock (_logLock)
            {
                _logBuffer.AppendLine(logLine);
                _logBufferedLines++;

                if (!_hasFlushedFirstLogLine || _logBufferedLines >= LOG_FLUSH_LINE_COUNT)
                {
                    FlushLogBuffer();
                }
            }

            // In brute-force fast mode, skip UI updates entirely to maintain performance
            if (_testMode == TestMode.BruteForce && _speedMode == SpeedMode.Fast)
            {
                return;
            }

            // Only show errors in non-fast brute-force mode
            this.BeginInvoke((MethodInvoker)delegate
            {
                const int MaxDisplayLines = 10000;
                if (txtResults.Lines.Length > MaxDisplayLines)
                {
                    txtResults.Lines = txtResults.Lines.Skip(txtResults.Lines.Length - MaxDisplayLines / 2).ToArray();
                }
                txtResults.AppendText($"✗ Seed: {PadRight(seed, 20)} | Exception: {errorMessage}\r\n");
                txtResults.ScrollToCaret();
            });
        }

        private void FlushLogBuffer()
        {
            lock (_logLock)
            {
                if (_logBuffer.Length > 0 && _logWriter != null)
                {
                    try
                    {
                        _logWriter.Write(_logBuffer.ToString());
                        _logWriter.Flush();
                        _logBuffer.Clear();
                        _logBufferedLines = 0;
                        _hasFlushedFirstLogLine = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error writing to log file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void UpdateUI(int tested, int generated, int verified, long total)
        {
            double elapsed = _sw.Elapsed.TotalSeconds;
            double speed = elapsed > 0 ? tested / elapsed : 0;
            long remaining = total - tested;
            double etaSeconds = speed > 0 ? remaining / speed : 0;
            //string eta = etaSeconds > 0 ? TimeSpan.FromSeconds(etaSeconds).ToString(@"hh\:mm\:ss") : "N/A";
            string eta = etaSeconds > 0 ? TimeSpan.FromSeconds(etaSeconds).ToString("c") : "N/A";

            this.BeginInvoke((MethodInvoker)delegate
            //this.Invoke((MethodInvoker)delegate
            {
                lblSeedsTested.Text = $"{tested:N0}";
                lblMatchesFound.Text = $"{generated:N0}";
                lblVerified.Text = $"{verified:N0}";
                lblSpeed.Text = $"{speed:F1} seeds/sec";
                lblRemaining.Text = $"{remaining:N0}";
                lblETA.Text = eta;
                lblStatus.Text = $"Testing... {tested:N0}/{total:N0} seeds, {generated:N0} generated, {verified:N0} verified ({(tested * 100.0 / total):F1}%)";
                if (!string.IsNullOrEmpty(_lastTestedSeed))
                {
                    txtLastSeed.Text = _lastTestedSeed;
                }
            });
        }

        private bool TryConvertSeedToUint(string seed, out uint seedValue)
        {
            seedValue = 0;

            if (seed.Length != 8)
            {
                return false;
            }

            for (int i = 0; i < seed.Length; i++)
            {
                char c = seed[i];
                bool isHex = (c >= '0' && c <= '9')
                    || (c >= 'A' && c <= 'F')
                    || (c >= 'a' && c <= 'f');

                if (!isHex)
                {
                    return false;
                }
            }

            return uint.TryParse(seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seedValue);
        }

        private string PadRight(string str, int length)
        {
            if (str.Length > length)
                return str.Substring(0, length - 3) + "...";
            return str.PadRight(length);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            FlushLogBuffer();
            _logWriter?.Close();
            _logWriter?.Dispose();
            base.OnFormClosing(e);
        }

        private void btnHideResults_Click(object sender, EventArgs e)
        {
            // toggle grpResults visibility
            // then shrink or expand the form accordingly
            if (grpResults.Visible)
            {
                grpResults.Visible = false;
                this.Height -= grpResults.Height;
            }
            else
            {
                grpResults.Visible = true;
                this.Height += grpResults.Height;
            }
        }
    }
}

