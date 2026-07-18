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
        // Lets you run one app instance per dongle. Defaults to the first dongle found.
        private int _selectedDongleIndex = 0;
        // Exact Windows PnP InstanceId of the physical dongle this instance opened, e.g.
        // "USB\VID_096E&PID_0006\7&30D00E&0&4". Used to reset ONLY this instance's dongle so a
        // recovery on one instance does not knock the other instances' dongles offline. When
        // null, the reset falls back to matching every ROCKEY_HARDWARE_ID device (single-dongle case).
        private string? _selectedUsbInstanceId = null;
        private readonly object _dongleSelectionLock = new object();

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
        private NumericUpDown nudDongleIndex = null!;
        private Button btnDetectDongles = null!;
        private Label lblDongleDetected = null!;
        private TextBox txtStartSeed = null!;
        private TextBox txtStopSeed = null!;

        public MainForm()
        {
            InitializeComponent();
            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 1000; // 1 second, adjust as needed
            _uiTimer.Tick += UiTimer_Tick;
            UiTotal = 0; // Initialize to avoid division by zero
            BuildMultiInstanceUi();
        }

        // Adds the "Multi-Instance" group (dongle selector + brute-force seed range) below the
        // existing controls, and moves the Results box down to make room. Done programmatically
        // to avoid disturbing the generated Designer file.
        private void BuildMultiInstanceUi()
        {
            const int groupTop = 724;   // where grpResults currently sits
            const int groupHeight = 96;
            const int shift = groupHeight + 8;

            grpMultiInstance = new GroupBox
            {
                Text = "Multi-Instance (select a dongle & seed range for this instance)",
                Location = new System.Drawing.Point(13, groupTop),
                Size = new System.Drawing.Size(730, groupHeight),
                TabStop = false
            };

            var lblDongleIndex = new Label { Text = "Dongle #:", AutoSize = true, Location = new System.Drawing.Point(8, 27) };
            nudDongleIndex = new NumericUpDown
            {
                Location = new System.Drawing.Point(70, 24),
                Size = new System.Drawing.Size(50, 23),
                Minimum = 0,
                Maximum = 31,
                Value = 0
            };
            var toolTip = new ToolTip();
            toolTip.SetToolTip(nudDongleIndex, "0-based index of the dongle this instance uses (0 = first found). Run one app instance per dongle.");

            btnDetectDongles = new Button
            {
                Text = "Detect Dongles",
                Location = new System.Drawing.Point(128, 23),
                Size = new System.Drawing.Size(120, 25)
            };
            btnDetectDongles.Click += btnDetectDongles_Click;

            lblDongleDetected = new Label
            {
                Text = "Click 'Detect Dongles' to list connected dongles.",
                AutoSize = false,
                Location = new System.Drawing.Point(256, 27),
                Size = new System.Drawing.Size(466, 20),
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

            grpMultiInstance.Controls.Add(lblDongleIndex);
            grpMultiInstance.Controls.Add(nudDongleIndex);
            grpMultiInstance.Controls.Add(btnDetectDongles);
            grpMultiInstance.Controls.Add(lblDongleDetected);
            grpMultiInstance.Controls.Add(lblStartSeed);
            grpMultiInstance.Controls.Add(txtStartSeed);
            grpMultiInstance.Controls.Add(lblStopSeed);
            grpMultiInstance.Controls.Add(txtStopSeed);
            grpMultiInstance.Controls.Add(lblRangeHint);

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

            // Bind this run to the selected dongle and work out which physical device to reset if it
            // wedges. Done here (not only on the Detect button) so the reset targets the right dongle
            // even if the user never clicked Detect.
            ResolveSelectedDongle(GetPhysicalDongleInstanceIds(), (int)nudDongleIndex.Value);

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

                if (ex.Message.Contains("Automatic restart will be attempted"))
                {
                    // Dongle recovery failed - physical reset required
                    this.Invoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"\r\n✗ [{errorTime}] Software reset failed. Physical dongle reset required.\r\n");
                        txtResults.AppendText($"   Please disconnect and reconnect the USB dongle, then click Start.\r\n");
                        txtResults.AppendText($"   The test will resume from: {_lastTestedSeed}\r\n");
                        txtResults.ScrollToCaret();
                        lblStatus.Text = "Waiting for manual dongle reconnection...";
                    });
                }
                else if (ex.Message.Contains("RY_SEED failed"))
                {
                    // RY_SEED errors detected - trigger recovery
                    this.Invoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"\r\n✗ [{errorTime}] Dongle command failures detected. Initiating recovery...\r\n");
                        txtResults.ScrollToCaret();
                        lblStatus.Text = "Attempting recovery...";
                    });

                    // Attempt restart
                    this.Invoke((MethodInvoker)delegate
                    {
                        btnTest_Click(null, null);
                    });
                }
                else
                {
                    // Other unrecoverable error
                    this.Invoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"\r\n✗ [{errorTime}] Testing stopped: {ex.Message}\r\n");
                        txtResults.ScrollToCaret();
                        lblStatus.Text = "Testing stopped - manual intervention required.";
                    });
                }
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
            const int MAX_DONGLE_RETRY_ATTEMPTS = 2;
            const int FIND_RETRY_ATTEMPTS = 5;
            int retryAttempt = 0;

            while (retryAttempt < MAX_DONGLE_RETRY_ATTEMPTS)
            {
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
                        retcode = r4s.Rockey(RY_FIND, ref handle, ref lp1, ref lp2, ref openP1, ref openP2, ref openP3, ref openP4, buffer);
                        if (retcode == 0) break;

                        if (findAttempt < FIND_RETRY_ATTEMPTS - 1)
                        {
                            string logMsg = $"   RY_FIND attempt {findAttempt + 1}/{FIND_RETRY_ATTEMPTS} failed (code {retcode}). Retrying...";
                            BeginInvoke((MethodInvoker)delegate
                            {
                                txtResults.AppendText($"{logMsg}\r\n");
                                txtResults.ScrollToCaret();
                            });
                            Thread.Sleep(1000); // Wait 1 second between find attempts
                        }
                    }

                    if (retcode != 0)
                    {
                        throw new InvalidOperationException($"ROCKEY not found after {FIND_RETRY_ATTEMPTS} attempts. Last error code: {retcode}");
                    }

                    // Advance to the dongle this instance is bound to. RY_FIND landed on dongle #0;
                    // step RY_FIND_NEXT to reach the selected index so multiple app instances can
                    // each drive a different physical dongle.
                    int dongleIndex;
                    lock (_dongleSelectionLock) { dongleIndex = _selectedDongleIndex; }
                    uint selectedHid = lp1;
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
                        uint hidForLog = selectedHid;
                        BeginInvoke((MethodInvoker)delegate
                        {
                            txtResults.AppendText($"   Bound to dongle #{dongleIndex} (hardware ID 0x{hidForLog:X8}).\r\n");
                            txtResults.ScrollToCaret();
                        });
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
                    return; // Success - exit retry loop
                }
                catch (InvalidOperationException ex) when (retryAttempt < MAX_DONGLE_RETRY_ATTEMPTS - 1)
                {
                    // Dongle error - attempt recovery
                    string errorTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string errorMessage = $"✗ [{errorTime}] Dongle error: {ex.Message}";

                    BeginInvoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"{errorMessage}\r\n");
                        txtResults.AppendText($"   Attempting automatic dongle reset (attempt {retryAttempt + 1}/{MAX_DONGLE_RETRY_ATTEMPTS})...\r\n");
                        txtResults.ScrollToCaret();
                    });

                    // Reset dongle synchronously with longer wait
                    Task resetTask = Task.Run(async () =>
                    {
                        bool resetSuccess = await ResetUsbDongleAsync();

                        if (resetSuccess)
                        {
                            string recoveryTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            BeginInvoke((MethodInvoker)delegate
                            {
                                txtResults.AppendText($"✓ [{recoveryTime}] Dongle reset successful. Waiting for re-initialization...\r\n");
                                txtResults.ScrollToCaret();
                            });
                        }
                    });

                    if (!resetTask.Wait(15000))
                    {
                        resetTask.Wait(); // Wait indefinitely if needed
                    }

                    // Additional wait to ensure dongle is fully re-initialized
                    BeginInvoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"   Waiting {USB_RESET_RETRY_DELAY_MS / 1000} seconds for dongle to re-initialize...\r\n");
                    });
                    Thread.Sleep(USB_RESET_RETRY_DELAY_MS);

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

            // If we exhausted all retries, throw exception to trigger automatic test restart
            throw new InvalidOperationException("Dongle failed to recover after multiple reset attempts. Automatic restart will be attempted.");
        }

        private async Task<bool> ResetUsbDongleAsync()
        {
            try
            {
                string? targetInstanceId;
                lock (_dongleSelectionLock)
                {
                    targetInstanceId = _selectedUsbInstanceId;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    string what = string.IsNullOrEmpty(targetInstanceId) ? $"all {ROCKEY_HARDWARE_ID} devices" : targetInstanceId;
                    txtResults.AppendText($"   Virtually unplugging/replugging dongle ({what})...\r\n");
                    lblStatus.Text = "Resetting USB dongle...";
                });

                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildResetScript(targetInstanceId)));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            txtResults.AppendText($"   ✗ Could not start reset helper.\r\n");
                        });
                        return false;
                    }

                    // Read output on background threads while waiting, to avoid deadlocking on full pipes.
                    Task<string> outTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errTask = process.StandardError.ReadToEndAsync();

                    bool finished = await Task.Run(() => process.WaitForExit(25000));
                    if (!finished)
                    {
                        try { process.Kill(); } catch { }
                        BeginInvoke((MethodInvoker)delegate
                        {
                            txtResults.AppendText($"   ✗ Reset timeout\r\n");
                        });
                        return false;
                    }

                    string output = string.Empty;
                    string error = string.Empty;
                    try { output = await outTask; } catch { }
                    try { error = await errTask; } catch { }
                    int exitCode = process.ExitCode;

                    string outTrim = output.Trim();
                    string errTrim = error.Trim();

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!string.IsNullOrWhiteSpace(outTrim))
                            txtResults.AppendText($"   {outTrim.Replace("\n", "\r\n   ")}\r\n");
                    });

                    if (exitCode == 0)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            txtResults.AppendText($"   ✓ Dongle reset successful (you should hear the USB disconnect/reconnect chime).\r\n");
                        });
                        return true;
                    }

                    BeginInvoke((MethodInvoker)delegate
                    {
                        txtResults.AppendText($"   ✗ Reset failed (exit code {exitCode}).\r\n");
                        if (!string.IsNullOrWhiteSpace(errTrim))
                            txtResults.AppendText($"   Error: {errTrim}\r\n");
                        if (exitCode == 1)
                            txtResults.AppendText($"   The dongle was not found in Device Manager. Is it plugged in?\r\n");
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    txtResults.AppendText($"   ✗ Reset exception: {ex.Message}\r\n");
                });
                return false;
            }
        }

        // Builds the PowerShell script that disables then re-enables the physical dongle device
        // node - the software equivalent of unplugging and replugging it. Requires the app to run
        // elevated (see app.manifest); Disable-PnpDevice/Enable-PnpDevice fail without admin.
        // The script is passed via -EncodedCommand (Base64 UTF-16LE), so no quote/newline escaping
        // is needed here.
        private static string BuildResetScript(string? targetInstanceId)
        {
            // Selection line: an exact InstanceId for this instance's dongle when known, otherwise
            // every top-level USB node matching the Rockey hardware id (single-dongle fallback).
            // Note: the dongle's top-level USB-bus node enumerates under Class HIDClass (it's a HID
            // device), so we must NOT filter by Class 'USB'. We match on the "USB\VID_096E&PID_0006"
            // InstanceId prefix, which selects the USB device node (disabling it cascades to the HID
            // child = a real replug) while excluding the "HID\..." function children.
            string selection;
            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                selection = "$dev = Get-PnpDevice -InstanceId '" + EscapePsSingleQuoted(targetInstanceId) +
                            "' -ErrorAction Stop";
            }
            else
            {
                selection = "$dev = Get-PnpDevice -PresentOnly -ErrorAction Stop | " +
                            "Where-Object { $_.InstanceId -like '" + EscapePsSingleQuoted(ROCKEY_HARDWARE_ID) + "*' }";
            }

            // Verbatim (non-interpolated) string: only " needs doubling; braces are literal.
            string script = @"
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

            return script;
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

                using var p = Process.Start(psi);
                if (p == null) return list;
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                foreach (var line in outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) list.Add(trimmed);
                }
            }
            catch { /* best effort */ }
            return list;
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

                int idx = (int)nudDongleIndex.Value;
                ResolveSelectedDongle(usb, idx);

                string hidStr = hids.Count > 0
                    ? string.Join(", ", hids.ConvertAll(h => "0x" + h.ToString("X8")))
                    : "none";
                string resetTarget = _selectedUsbInstanceId ?? "(all Rockey devices - VID/PID fallback)";

                lblDongleDetected.ForeColor = (hids.Count > 0 || usb.Count > 0)
                    ? System.Drawing.Color.Black
                    : System.Drawing.Color.Red;
                lblDongleDetected.Text =
                    $"SDK found {hids.Count} dongle(s) [{hidStr}]; Windows sees {usb.Count} USB device(s). " +
                    $"This instance uses #{idx}; reset target: {resetTarget}";

                if (idx >= Math.Max(hids.Count, usb.Count))
                {
                    lblDongleDetected.ForeColor = System.Drawing.Color.DarkOrange;
                    lblDongleDetected.Text += "  ⚠ index exceeds the number of dongles found.";
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

            foreach (string seed in seeds)
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

            foreach (string seed in GenerateCombinations(charset, length, limit, startIndex))
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

