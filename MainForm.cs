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
        private CancellationTokenSource? _cancellationTokenSource;
        private StreamWriter? _logWriter;
        private int _seedsTested = 0;
        private int _matchesFound = 0;
        private int _verifiedPasswords = 0;
        private Stopwatch _sw = new Stopwatch();
        private SpeedMode _speedMode = SpeedMode.Balanced;
        private TestMode _testMode = TestMode.Dictionary;
        private StringBuilder _logBuffer = new StringBuilder();
        private const int LOG_BUFFER_SIZE = 1000;
        private long _totalSeedsToTest = 0;
        private string _lastTestedSeed = string.Empty;
        private bool _isPaused = false;
        private DateTime _lastUIUpdate = DateTime.MinValue;

        private System.Windows.Forms.Timer _uiTimer;
        private volatile int _uiTested;
        private volatile int _uiGenerated;
        private volatile int _uiVerified;
        private long _uiTotal;
        private readonly object _uiTotalLock = new object();

        public MainForm()
        {
            InitializeComponent();
            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 1000; // 1 second, adjust as needed
            _uiTimer.Tick += UiTimer_Tick;
            UiTotal = 0; // Initialize to avoid division by zero
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

            double elapsed = _sw.Elapsed.TotalSeconds;
            double speed = elapsed > 0 ? _uiTested / elapsed : 0;
            long remaining = total - _uiTested;
            double etaSeconds = speed > 0 ? remaining / speed : 0;
            string eta = etaSeconds > 0 ? TimeSpan.FromSeconds(etaSeconds).ToString("c") : "N/A";

            lblSeedsTested.Text = $"{_uiTested:N0}";
            lblMatchesFound.Text = $"{_uiGenerated:N0}";
            lblVerified.Text = $"{_uiVerified:N0}";
            lblSpeed.Text = $"{speed:F1} seeds/sec";
            lblRemaining.Text = $"{remaining:N0}";
            lblETA.Text = eta;
            lblStatus.Text = $"Testing... {_uiTested:N0}/{total:N0} seeds, {_uiGenerated:N0} generated, {_uiVerified:N0} verified ({(_uiTested * 100.0 / total):F1}%)";
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

            txtResults.Clear();
            _seedsTested = 0;
            _matchesFound = 0;
            _verifiedPasswords = 0;
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
                HashSet<string> testedSeeds = LoadTestedSeeds(txtLogFile.Text);

                bool isResume = testedSeeds.Count > 0;

                _logWriter = new StreamWriter(txtLogFile.Text, true, Encoding.UTF8);
                bool headerWritten = File.Exists(txtLogFile.Text) && new FileInfo(txtLogFile.Text).Length > 0;

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
                        lblStatus.Text = $"Resume mode: Found {testedSeeds.Count:N0} already tested seeds. Loading remaining...";
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
                        lblStatus.Text = $"Resume mode: Found {testedSeeds.Count:N0} already tested seeds. Will skip them...";
                    }
                    else
                    {
                        lblStatus.Text = "Generating seeds on the fly. Testing...";
                    }
                }

                UpdateDongleInfo();

                ushort p1 = 0x530A;
                ushort p2 = 0x00FC;
                ushort p3 = 0xCB51;
                ushort p4 = 0x8C4E;

                if (ushort.TryParse(txtP1.Text, System.Globalization.NumberStyles.HexNumber, null, out p1)) { }
                if (ushort.TryParse(txtP2.Text, System.Globalization.NumberStyles.HexNumber, null, out p2)) { }
                if (ushort.TryParse(txtP3.Text, System.Globalization.NumberStyles.HexNumber, null, out p3)) { }
                if (ushort.TryParse(txtP4.Text, System.Globalization.NumberStyles.HexNumber, null, out p4)) { }

                Rockey4SmartClass.Rockey4Smart r4s = new Rockey4SmartClass.Rockey4Smart();
                ushort handle = 0;
                uint lp1 = 0;
                uint lp2 = 0;
                byte[] buffer = new byte[1024];

                ushort retcode = r4s.Rockey(1, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                if (retcode != 0)
                {
                    MessageBox.Show($"ROCKEY not found! Error code: {retcode}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "ROCKEY not found!";
                    return;
                }

                retcode = r4s.Rockey(3, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                if (retcode != 0)
                {
                    MessageBox.Show($"Failed to open dongle. Error code: {retcode}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Failed to open dongle.";
                    return;
                }

                this.Invoke((MethodInvoker)delegate
                {
                    lblHandle.Text = $"0x{handle:X4}";
                });

                _sw.Restart();

                _uiTested = 0;
                _uiGenerated = 0;
                _uiVerified = 0;
                _uiTotal = 0;
                _uiTimer.Start();

                if (_testMode == TestMode.Dictionary)
                {
                    List<string> untestedSeeds = seeds.Where(s => !testedSeeds.Contains(s)).ToList();
                    _totalSeedsToTest = untestedSeeds.Count;
                    _uiTotal = untestedSeeds.Count; // Set total for timer updates

                    if (testedSeeds.Count > 0)
                    {
                        lblStatus.Text = $"Resume: Skipping {testedSeeds.Count:N0} tested seeds. Testing {untestedSeeds.Count:N0} remaining seeds...";
                    }
                    else
                    {
                        lblStatus.Text = $"Testing {untestedSeeds.Count:N0} seeds...";
                    }

                    await TestSeedsAsync(untestedSeeds, r4s, handle, p1, p2, p3, p4, targetPassword, _cancellationTokenSource.Token);
                }
                else
                {
                    string charset = txtCharset.Text;
                    int length = (int)nudLength.Value;
                    long? limit = chkLimit.Checked ? (long?)nudLimit.Value : null;

                    _totalSeedsToTest = limit.HasValue ? limit.Value : (long)Math.Pow(charset.Length, length);
                    _uiTotal = _totalSeedsToTest; // Set total for timer updates

                    if (testedSeeds.Count > 0)
                    {
                        lblStatus.Text = $"Resume: Will skip {testedSeeds.Count:N0} tested seeds. Testing up to {_totalSeedsToTest:N0} combinations...";
                    }
                    else
                    {
                        lblStatus.Text = $"Brute-forcing up to {_totalSeedsToTest:N0} combinations...";
                    }

                    await TestCombinationsAsync(charset, length, limit, testedSeeds, r4s, handle, p1, p2, p3, p4, targetPassword, _cancellationTokenSource.Token);
                }

                r4s.Rockey(4, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                _sw.Stop();

                _uiTimer.Stop();

                this.Invoke((MethodInvoker)delegate
                {
                    lblStatus.Text = $"Complete! Tested {_seedsTested:N0} seeds, {_verifiedPasswords} verified passwords.";
                    lblSeedsTested.Text = $"{_seedsTested:N0}";
                    lblMatchesFound.Text = $"{_matchesFound:N0}";
                    lblVerified.Text = $"{_verifiedPasswords:N0}";
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
                byte[] buffer = new byte[1024];

                ushort retcode = r4s.Rockey(1, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                if (retcode == 0)
                {
                    retcode = r4s.Rockey(3, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    if (retcode == 0)
                    {
                        lblDongleStatus.Text = "Connected ✓";
                        lblDongleStatus.ForeColor = System.Drawing.Color.Green;
                        lblHandle.Text = $"0x{handle:X4}";
                        r4s.Rockey(4, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
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

        private HashSet<string> LoadTestedSeeds(string logFilePath)
        {
            var testedSeeds = new HashSet<string>();

            if (File.Exists(logFilePath))
            {
                try
                {
                    using (var reader = new StreamReader(logFilePath))
                    {
                        string line;
                        bool headerSkipped = false;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!headerSkipped)
                            {
                                headerSkipped = true;
                                continue;
                            }

                            int commaIndex = line.IndexOf(',');
                            if (commaIndex > 0)
                            {
                                string seed = line.Substring(0, commaIndex).Trim('"');
                                if (!string.IsNullOrEmpty(seed))
                                {
                                    testedSeeds.Add(seed);
                                }
                            }
                        }
                    }

                    if (testedSeeds.Count > 0)
                    {
                        using (var reader = new StreamReader(logFilePath))
                        {
                            string line;
                            bool headerSkipped = false;

                            while ((line = reader.ReadLine()) != null)
                            {
                                if (!headerSkipped)
                                {
                                    headerSkipped = true;
                                    continue;
                                }

                                int commaIndex = line.IndexOf(',');
                                if (commaIndex > 0)
                                {
                                    string seed = line.Substring(0, commaIndex).Trim('"');
                                    if (!string.IsNullOrEmpty(seed))
                                    {
                                        _lastTestedSeed = seed;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Warning: Could not read log file for resume: {ex.Message}", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return testedSeeds;
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

        private IEnumerable<string> GenerateCombinations(string charset, int length, long? limit = null)
        {
            var indices = new int[length];
            var charsetArray = charset.ToCharArray();
            long count = 0;

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

        private async System.Threading.Tasks.Task TestSeedsAsync(List<string> seeds, Rockey4SmartClass.Rockey4Smart r4s, ushort handle, ushort initialP1, ushort initialP2, ushort initialP3, ushort initialP4, string targetPassword, CancellationToken cancellationToken)
        {
            int tested = 0;
            int generated = 0;
            int verified = 0;

            uint lp1 = 0;
            uint lp2 = 0;
            ushort p1 = initialP1;
            ushort p2 = initialP2;
            ushort p3 = initialP3;
            ushort p4 = initialP4;
            byte[] buffer = new byte[1024];

            bool headerWritten = false;

            foreach (string seed in seeds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                while (_isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    await Task.Delay(50, cancellationToken);
                }

                try
                {
                    uint seedValue = ConvertSeedToUint(seed);

                    lp2 = seedValue;
                    lp1 = 0;

                    ushort retcode = r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    string passwordStr = "ERROR";
                    string status = "ERROR";
                    bool verifiedOK = false;

                    if (retcode == 0)
                    {
                        passwordStr = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
                        status = "SUCCESS";
                        generated++;
                        verifiedOK = true;
                        _lastTestedSeed = seed;

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
                    }

                    LogResult(seed, passwordStr, p1, p2, p3, p4, status, verifiedOK, ref headerWritten);

                    tested++;

                    // Update UI counter variables (timer will handle actual UI updates)
                    _uiTested = tested;
                    _uiGenerated = generated;
                    _uiVerified = verified;

                    // Periodically yield to keep UI responsive in fast mode
                    if ((_speedMode == SpeedMode.Fast || _speedMode == SpeedMode.Balanced) && tested % 1000 == 0)
                    {
                        await Task.Yield();
                    }
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

        private async System.Threading.Tasks.Task TestCombinationsAsync(string charset, int length, long? limit, HashSet<string> testedSeeds, Rockey4SmartClass.Rockey4Smart r4s, ushort handle, ushort initialP1, ushort initialP2, ushort initialP3, ushort initialP4, string targetPassword, CancellationToken cancellationToken)
        {
            int tested = 0;
            int generated = 0;
            int verified = 0;
            int skipped = testedSeeds.Count;

            uint lp1 = 0;
            uint lp2 = 0;
            ushort p1 = initialP1;
            ushort p2 = initialP2;
            ushort p3 = initialP3;
            ushort p4 = initialP4;
            byte[] buffer = new byte[1024];

            bool headerWritten = false;
            long total = limit.HasValue ? limit.Value : (long)Math.Pow(charset.Length, length);
            int yieldInterval = (_speedMode == SpeedMode.Fast) ? 500 : 1000; // More frequent yielding in fast mode

            foreach (string seed in GenerateCombinations(charset, length, limit))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                while (_isPaused)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    await Task.Delay(50, cancellationToken);
                }

                if (testedSeeds.Contains(seed))
                {
                    continue;
                }

                try
                {
                    uint seedValue = ConvertSeedToUint(seed);

                    lp2 = seedValue;
                    lp1 = 0;

                    ushort retcode = r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

                    string passwordStr = "ERROR";
                    string status = "ERROR";
                    bool verifiedOK = false;

                    if (retcode == 0)
                    {
                        passwordStr = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
                        status = "SUCCESS";
                        generated++;
                        verifiedOK = true;
                        _lastTestedSeed = seed;

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
                    }

                    LogResult(seed, passwordStr, p1, p2, p3, p4, status, verifiedOK, ref headerWritten);

                    tested++;

                    // Update UI counter variables (timer will handle actual UI updates)
                    _uiTested = tested;
                    _uiGenerated = generated;
                    _uiVerified = verified;

                    // Periodically yield to keep UI responsive - more aggressive in fast mode
                    if (tested % yieldInterval == 0)
                    {
                        await Task.Yield();
                    }
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
            string[] words = passwordStr.Split(' ');
            string word1 = words.Length > 0 ? words[0] : "N/A";
            string word2 = words.Length > 1 ? words[1] : "N/A";
            string word3 = words.Length > 2 ? words[2] : "N/A";
            string word4 = words.Length > 3 ? words[3] : "N/A";
            string verifiedStr = verifiedOK ? "YES" : "NO";

            string logLine = $"\"{seed}\",\"{passwordStr}\",{word1},{word2},{word3},{word4},{status},{verifiedStr},{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            _logBuffer.AppendLine(logLine);

            if (_logBuffer.Length > LOG_BUFFER_SIZE * 100)
            {
                FlushLogBuffer();
            }
        }

        private void LogError(string seed, string errorMessage)
        {
            string logLine = $"\"{seed}\",\"EXCEPTION\",N/A,N/A,N/A,N/A,EXCEPTION,NO,{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            _logBuffer.AppendLine(logLine);

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
            if (_logBuffer.Length > 0 && _logWriter != null)
            {
                try
                {
                    _logWriter.Write(_logBuffer.ToString());
                    _logWriter.Flush();
                    _logBuffer.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error writing to log file: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private uint ConvertSeedToUint(string seed)
        {
            if (seed.Length == 8 && uint.TryParse(seed, System.Globalization.NumberStyles.HexNumber, null, out uint parsedValue))
            {
                return parsedValue;
            }

            byte[] seedBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(seed);
            if (seedBytes.Length < 4)
            {
                Array.Resize(ref seedBytes, 4);
            }
            return BitConverter.ToUInt32(seedBytes, 0);
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