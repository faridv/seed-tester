namespace RockeyPasswordTester
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            grpDongleParams = new System.Windows.Forms.GroupBox();
            chkDemo = new System.Windows.Forms.CheckBox();
            lblP4 = new System.Windows.Forms.Label();
            lblP3 = new System.Windows.Forms.Label();
            lblP2 = new System.Windows.Forms.Label();
            lblP1 = new System.Windows.Forms.Label();
            txtP4 = new System.Windows.Forms.TextBox();
            txtP3 = new System.Windows.Forms.TextBox();
            txtP2 = new System.Windows.Forms.TextBox();
            txtP1 = new System.Windows.Forms.TextBox();
            grpDongleInfo = new System.Windows.Forms.GroupBox();
            txtLastSeed = new System.Windows.Forms.TextBox();
            lblLastSeedLabel = new System.Windows.Forms.Label();
            lblHandle = new System.Windows.Forms.Label();
            lblHandleLabel = new System.Windows.Forms.Label();
            lblDongleStatus = new System.Windows.Forms.Label();
            lblStatus = new System.Windows.Forms.Label();
            tabsMode = new System.Windows.Forms.TabControl();
            tabDictionary = new System.Windows.Forms.TabPage();
            grpSeedList = new System.Windows.Forms.GroupBox();
            btnBrowseSeeds = new System.Windows.Forms.Button();
            txtSeedListFile = new System.Windows.Forms.TextBox();
            lblSeedList = new System.Windows.Forms.Label();
            tabBruteForce = new System.Windows.Forms.TabPage();
            grpBruteForceSettings = new System.Windows.Forms.GroupBox();
            lblCharsetLabel = new System.Windows.Forms.Label();
            txtCharset = new System.Windows.Forms.TextBox();
            lblLengthLabel = new System.Windows.Forms.Label();
            nudLength = new System.Windows.Forms.NumericUpDown();
            chkLimit = new System.Windows.Forms.CheckBox();
            lblLimitLabel = new System.Windows.Forms.Label();
            nudLimit = new System.Windows.Forms.NumericUpDown();
            tabRepair = new System.Windows.Forms.TabPage();
            lblRepairInfo = new System.Windows.Forms.Label();
            grpTargetPassword = new System.Windows.Forms.GroupBox();
            lblTargetPasswordInfo = new System.Windows.Forms.Label();
            txtTargetPassword = new System.Windows.Forms.TextBox();
            lblTargetPasswordLabel = new System.Windows.Forms.Label();
            grpLogFile = new System.Windows.Forms.GroupBox();
            btnBrowseLog = new System.Windows.Forms.Button();
            txtLogFile = new System.Windows.Forms.TextBox();
            lblLogFile = new System.Windows.Forms.Label();
            grpSpeed = new System.Windows.Forms.GroupBox();
            radSpeedSlow = new System.Windows.Forms.RadioButton();
            radSpeedBalanced = new System.Windows.Forms.RadioButton();
            radSpeedFast = new System.Windows.Forms.RadioButton();
            grpActions = new System.Windows.Forms.GroupBox();
            btnHideResults = new System.Windows.Forms.Button();
            btnPauseResume = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            btnTest = new System.Windows.Forms.Button();
            btnClear = new System.Windows.Forms.Button();
            grpStats = new System.Windows.Forms.GroupBox();
            lblETA = new System.Windows.Forms.Label();
            lblETALabel = new System.Windows.Forms.Label();
            lblRemaining = new System.Windows.Forms.Label();
            lblRemainingLabel = new System.Windows.Forms.Label();
            lblSpeed = new System.Windows.Forms.Label();
            lblSpeedLabel = new System.Windows.Forms.Label();
            lblVerified = new System.Windows.Forms.Label();
            lblVerifiedLabel = new System.Windows.Forms.Label();
            lblMatchesFound = new System.Windows.Forms.Label();
            lblMatchesFoundLabel = new System.Windows.Forms.Label();
            lblSeedsTested = new System.Windows.Forms.Label();
            lblSeedsTestedLabel = new System.Windows.Forms.Label();
            grpMultiDongle = new System.Windows.Forms.GroupBox();
            btnDetectDongles = new System.Windows.Forms.Button();
            lblMulti = new System.Windows.Forms.Label();
            radTandem = new System.Windows.Forms.RadioButton();
            radRoundRobin = new System.Windows.Forms.RadioButton();
            radParallel = new System.Windows.Forms.RadioButton();
            lvDongles = new System.Windows.Forms.ListView();
            colDongle = new System.Windows.Forms.ColumnHeader();
            colHid = new System.Windows.Forms.ColumnHeader();
            colPort = new System.Windows.Forms.ColumnHeader();
            colState = new System.Windows.Forms.ColumnHeader();
            lblDongleDetected = new System.Windows.Forms.Label();
            lblStartSeedLabel = new System.Windows.Forms.Label();
            txtStartSeed = new System.Windows.Forms.TextBox();
            lblStopSeedLabel = new System.Windows.Forms.Label();
            txtStopSeed = new System.Windows.Forms.TextBox();
            chkSerialize = new System.Windows.Forms.CheckBox();
            chkHubRecover = new System.Windows.Forms.CheckBox();
            lblRecovery = new System.Windows.Forms.Label();
            btnResetSafe = new System.Windows.Forms.Button();
            btnCycleHub = new System.Windows.Forms.Button();
            lblMultiHint = new System.Windows.Forms.Label();
            grpResults = new System.Windows.Forms.GroupBox();
            txtResults = new System.Windows.Forms.TextBox();
            grpDongleParams.SuspendLayout();
            grpDongleInfo.SuspendLayout();
            tabsMode.SuspendLayout();
            tabDictionary.SuspendLayout();
            grpSeedList.SuspendLayout();
            tabBruteForce.SuspendLayout();
            grpBruteForceSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudLimit).BeginInit();
            tabRepair.SuspendLayout();
            grpTargetPassword.SuspendLayout();
            grpLogFile.SuspendLayout();
            grpSpeed.SuspendLayout();
            grpActions.SuspendLayout();
            grpStats.SuspendLayout();
            grpMultiDongle.SuspendLayout();
            grpResults.SuspendLayout();
            SuspendLayout();
            //
            // grpDongleParams
            //
            grpDongleParams.Controls.Add(chkDemo);
            grpDongleParams.Controls.Add(lblP4);
            grpDongleParams.Controls.Add(lblP3);
            grpDongleParams.Controls.Add(lblP2);
            grpDongleParams.Controls.Add(lblP1);
            grpDongleParams.Controls.Add(txtP4);
            grpDongleParams.Controls.Add(txtP3);
            grpDongleParams.Controls.Add(txtP2);
            grpDongleParams.Controls.Add(txtP1);
            grpDongleParams.Location = new System.Drawing.Point(12, 12);
            grpDongleParams.Name = "grpDongleParams";
            grpDongleParams.Size = new System.Drawing.Size(300, 122);
            grpDongleParams.TabStop = false;
            grpDongleParams.Text = "Dongle Parameters (P1-P4)";
            //
            // chkDemo
            //
            chkDemo.AutoSize = true;
            chkDemo.Checked = true;
            chkDemo.CheckState = System.Windows.Forms.CheckState.Checked;
            chkDemo.Location = new System.Drawing.Point(10, 90);
            chkDemo.Name = "chkDemo";
            chkDemo.Size = new System.Drawing.Size(150, 19);
            chkDemo.TabIndex = 8;
            chkDemo.Text = "Demo (disables P1-P4)";
            chkDemo.CheckedChanged += chkDemo_CheckedChanged;
            //
            // lblP4
            //
            lblP4.AutoSize = true;
            lblP4.Location = new System.Drawing.Point(222, 58);
            lblP4.Name = "lblP4";
            lblP4.Size = new System.Drawing.Size(20, 15);
            lblP4.Text = "P4";
            //
            // lblP3
            //
            lblP3.AutoSize = true;
            lblP3.Location = new System.Drawing.Point(163, 58);
            lblP3.Name = "lblP3";
            lblP3.Size = new System.Drawing.Size(20, 15);
            lblP3.Text = "P3";
            //
            // lblP2
            //
            lblP2.AutoSize = true;
            lblP2.Location = new System.Drawing.Point(105, 58);
            lblP2.Name = "lblP2";
            lblP2.Size = new System.Drawing.Size(20, 15);
            lblP2.Text = "P2";
            //
            // lblP1
            //
            lblP1.AutoSize = true;
            lblP1.Location = new System.Drawing.Point(47, 58);
            lblP1.Name = "lblP1";
            lblP1.Size = new System.Drawing.Size(20, 15);
            lblP1.Text = "P1";
            //
            // txtP4
            //
            txtP4.Location = new System.Drawing.Point(217, 23);
            txtP4.MaxLength = 4;
            txtP4.Name = "txtP4";
            txtP4.Size = new System.Drawing.Size(46, 23);
            txtP4.TabIndex = 3;
            txtP4.Text = "8C4E";
            //
            // txtP3
            //
            txtP3.Location = new System.Drawing.Point(159, 23);
            txtP3.MaxLength = 4;
            txtP3.Name = "txtP3";
            txtP3.Size = new System.Drawing.Size(46, 23);
            txtP3.TabIndex = 2;
            txtP3.Text = "CB51";
            //
            // txtP2
            //
            txtP2.Location = new System.Drawing.Point(100, 23);
            txtP2.MaxLength = 4;
            txtP2.Name = "txtP2";
            txtP2.Size = new System.Drawing.Size(46, 23);
            txtP2.TabIndex = 1;
            txtP2.Text = "00FC";
            //
            // txtP1
            //
            txtP1.Location = new System.Drawing.Point(42, 23);
            txtP1.MaxLength = 4;
            txtP1.Name = "txtP1";
            txtP1.Size = new System.Drawing.Size(46, 23);
            txtP1.TabIndex = 0;
            txtP1.Text = "530A";
            //
            // grpDongleInfo
            //
            grpDongleInfo.Controls.Add(txtLastSeed);
            grpDongleInfo.Controls.Add(lblLastSeedLabel);
            grpDongleInfo.Controls.Add(lblHandle);
            grpDongleInfo.Controls.Add(lblHandleLabel);
            grpDongleInfo.Controls.Add(lblDongleStatus);
            grpDongleInfo.Controls.Add(lblStatus);
            grpDongleInfo.Location = new System.Drawing.Point(318, 12);
            grpDongleInfo.Name = "grpDongleInfo";
            grpDongleInfo.Size = new System.Drawing.Size(440, 122);
            grpDongleInfo.TabStop = false;
            grpDongleInfo.Text = "Dongle Status & Resume";
            //
            // txtLastSeed
            //
            txtLastSeed.Location = new System.Drawing.Point(84, 73);
            txtLastSeed.Name = "txtLastSeed";
            txtLastSeed.ReadOnly = true;
            txtLastSeed.Size = new System.Drawing.Size(348, 23);
            txtLastSeed.TabIndex = 5;
            //
            // lblLastSeedLabel
            //
            lblLastSeedLabel.AutoSize = true;
            lblLastSeedLabel.Location = new System.Drawing.Point(6, 78);
            lblLastSeedLabel.Name = "lblLastSeedLabel";
            lblLastSeedLabel.Size = new System.Drawing.Size(59, 15);
            lblLastSeedLabel.Text = "Last Seed:";
            //
            // lblHandle
            //
            lblHandle.AutoSize = true;
            lblHandle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblHandle.Location = new System.Drawing.Point(80, 53);
            lblHandle.Name = "lblHandle";
            lblHandle.Size = new System.Drawing.Size(34, 17);
            lblHandle.Text = "N/A";
            //
            // lblHandleLabel
            //
            lblHandleLabel.AutoSize = true;
            lblHandleLabel.Location = new System.Drawing.Point(7, 55);
            lblHandleLabel.Name = "lblHandleLabel";
            lblHandleLabel.Size = new System.Drawing.Size(48, 15);
            lblHandleLabel.Text = "Handle:";
            //
            // lblDongleStatus
            //
            lblDongleStatus.AutoSize = true;
            lblDongleStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblDongleStatus.ForeColor = System.Drawing.Color.Gray;
            lblDongleStatus.Location = new System.Drawing.Point(300, 53);
            lblDongleStatus.Name = "lblDongleStatus";
            lblDongleStatus.Size = new System.Drawing.Size(34, 17);
            lblDongleStatus.Text = "Idle";
            //
            // lblStatus
            //
            lblStatus.AutoSize = true;
            lblStatus.Location = new System.Drawing.Point(7, 27);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(42, 15);
            lblStatus.Text = "Ready.";
            //
            // tabsMode
            //
            tabsMode.Controls.Add(tabDictionary);
            tabsMode.Controls.Add(tabBruteForce);
            tabsMode.Controls.Add(tabRepair);
            tabsMode.Location = new System.Drawing.Point(12, 140);
            tabsMode.Name = "tabsMode";
            tabsMode.SelectedIndex = 0;
            tabsMode.Size = new System.Drawing.Size(746, 150);
            tabsMode.TabIndex = 2;
            tabsMode.SelectedIndexChanged += tabsMode_SelectedIndexChanged;
            //
            // tabDictionary
            //
            tabDictionary.Controls.Add(grpSeedList);
            tabDictionary.Location = new System.Drawing.Point(4, 24);
            tabDictionary.Name = "tabDictionary";
            tabDictionary.Padding = new System.Windows.Forms.Padding(3);
            tabDictionary.Size = new System.Drawing.Size(738, 122);
            tabDictionary.Text = "Dictionary";
            tabDictionary.UseVisualStyleBackColor = true;
            //
            // grpSeedList
            //
            grpSeedList.Controls.Add(btnBrowseSeeds);
            grpSeedList.Controls.Add(txtSeedListFile);
            grpSeedList.Controls.Add(lblSeedList);
            grpSeedList.Location = new System.Drawing.Point(4, 6);
            grpSeedList.Name = "grpSeedList";
            grpSeedList.Size = new System.Drawing.Size(728, 90);
            grpSeedList.TabStop = false;
            grpSeedList.Text = "Seed List File (one seed per line)";
            //
            // btnBrowseSeeds
            //
            btnBrowseSeeds.Location = new System.Drawing.Point(634, 40);
            btnBrowseSeeds.Name = "btnBrowseSeeds";
            btnBrowseSeeds.Size = new System.Drawing.Size(86, 24);
            btnBrowseSeeds.TabIndex = 2;
            btnBrowseSeeds.Text = "Browse...";
            btnBrowseSeeds.UseVisualStyleBackColor = true;
            btnBrowseSeeds.Click += btnBrowseSeeds_Click;
            //
            // txtSeedListFile
            //
            txtSeedListFile.Location = new System.Drawing.Point(8, 41);
            txtSeedListFile.Name = "txtSeedListFile";
            txtSeedListFile.Size = new System.Drawing.Size(618, 23);
            txtSeedListFile.TabIndex = 1;
            //
            // lblSeedList
            //
            lblSeedList.AutoSize = true;
            lblSeedList.Location = new System.Drawing.Point(7, 20);
            lblSeedList.Name = "lblSeedList";
            lblSeedList.Size = new System.Drawing.Size(202, 15);
            lblSeedList.Text = "Text file containing one seed per line:";
            //
            // tabBruteForce
            //
            tabBruteForce.Controls.Add(grpBruteForceSettings);
            tabBruteForce.Location = new System.Drawing.Point(4, 24);
            tabBruteForce.Name = "tabBruteForce";
            tabBruteForce.Padding = new System.Windows.Forms.Padding(3);
            tabBruteForce.Size = new System.Drawing.Size(738, 122);
            tabBruteForce.Text = "Brute-Force";
            tabBruteForce.UseVisualStyleBackColor = true;
            //
            // grpBruteForceSettings
            //
            grpBruteForceSettings.Controls.Add(lblCharsetLabel);
            grpBruteForceSettings.Controls.Add(txtCharset);
            grpBruteForceSettings.Controls.Add(lblLengthLabel);
            grpBruteForceSettings.Controls.Add(nudLength);
            grpBruteForceSettings.Controls.Add(chkLimit);
            grpBruteForceSettings.Controls.Add(lblLimitLabel);
            grpBruteForceSettings.Controls.Add(nudLimit);
            grpBruteForceSettings.Location = new System.Drawing.Point(4, 6);
            grpBruteForceSettings.Name = "grpBruteForceSettings";
            grpBruteForceSettings.Size = new System.Drawing.Size(728, 100);
            grpBruteForceSettings.TabStop = false;
            grpBruteForceSettings.Text = "Brute-Force Settings";
            //
            // lblCharsetLabel
            //
            lblCharsetLabel.AutoSize = true;
            lblCharsetLabel.Location = new System.Drawing.Point(7, 29);
            lblCharsetLabel.Name = "lblCharsetLabel";
            lblCharsetLabel.Size = new System.Drawing.Size(50, 15);
            lblCharsetLabel.Text = "Charset:";
            //
            // txtCharset
            //
            txtCharset.Location = new System.Drawing.Point(75, 25);
            txtCharset.Name = "txtCharset";
            txtCharset.Size = new System.Drawing.Size(349, 23);
            txtCharset.TabIndex = 5;
            txtCharset.Text = "0123456789ABCDEF";
            //
            // lblLengthLabel
            //
            lblLengthLabel.AutoSize = true;
            lblLengthLabel.Location = new System.Drawing.Point(443, 29);
            lblLengthLabel.Name = "lblLengthLabel";
            lblLengthLabel.Size = new System.Drawing.Size(47, 15);
            lblLengthLabel.Text = "Length:";
            //
            // nudLength
            //
            nudLength.Location = new System.Drawing.Point(500, 25);
            nudLength.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            nudLength.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudLength.Name = "nudLength";
            nudLength.Size = new System.Drawing.Size(58, 23);
            nudLength.TabIndex = 3;
            nudLength.Value = new decimal(new int[] { 8, 0, 0, 0 });
            //
            // chkLimit
            //
            chkLimit.AutoSize = true;
            chkLimit.Location = new System.Drawing.Point(11, 62);
            chkLimit.Name = "chkLimit";
            chkLimit.Size = new System.Drawing.Size(56, 19);
            chkLimit.TabIndex = 2;
            chkLimit.Text = "Limit:";
            chkLimit.UseVisualStyleBackColor = true;
            //
            // lblLimitLabel
            //
            lblLimitLabel.AutoSize = true;
            lblLimitLabel.Location = new System.Drawing.Point(83, 63);
            lblLimitLabel.Name = "lblLimitLabel";
            lblLimitLabel.Size = new System.Drawing.Size(32, 15);
            lblLimitLabel.Text = "Max:";
            //
            // nudLimit
            //
            nudLimit.Location = new System.Drawing.Point(128, 60);
            nudLimit.Maximum = new decimal(new int[] { 0, 1, 0, 0 });
            nudLimit.Name = "nudLimit";
            nudLimit.Size = new System.Drawing.Size(117, 23);
            nudLimit.TabIndex = 0;
            nudLimit.Value = new decimal(new int[] { 1000000, 0, 0, 0 });
            //
            // tabRepair
            //
            tabRepair.Controls.Add(lblRepairInfo);
            tabRepair.Location = new System.Drawing.Point(4, 24);
            tabRepair.Name = "tabRepair";
            tabRepair.Padding = new System.Windows.Forms.Padding(3);
            tabRepair.Size = new System.Drawing.Size(738, 122);
            tabRepair.Text = "Repair";
            tabRepair.UseVisualStyleBackColor = true;
            //
            // lblRepairInfo
            //
            lblRepairInfo.Location = new System.Drawing.Point(10, 12);
            lblRepairInfo.Name = "lblRepairInfo";
            lblRepairInfo.Size = new System.Drawing.Size(715, 95);
            lblRepairInfo.Text = "Repair scans the log file (set below) for seeds with no valid password - both error rows and gaps - and re-tests just those, writing the fixes to a separate \"<log>.repaired.csv\". Set the Start/Stop range below, or name the log like 11111111_22222222.csv so the range is read from it.";
            //
            // grpTargetPassword
            //
            grpTargetPassword.Controls.Add(lblTargetPasswordInfo);
            grpTargetPassword.Controls.Add(txtTargetPassword);
            grpTargetPassword.Controls.Add(lblTargetPasswordLabel);
            grpTargetPassword.Location = new System.Drawing.Point(12, 296);
            grpTargetPassword.Name = "grpTargetPassword";
            grpTargetPassword.Size = new System.Drawing.Size(746, 58);
            grpTargetPassword.TabStop = false;
            grpTargetPassword.Text = "Target Password (Optional - stops when found)";
            //
            // lblTargetPasswordInfo
            //
            lblTargetPasswordInfo.AutoSize = true;
            lblTargetPasswordInfo.ForeColor = System.Drawing.Color.Gray;
            lblTargetPasswordInfo.Location = new System.Drawing.Point(303, 25);
            lblTargetPasswordInfo.Name = "lblTargetPasswordInfo";
            lblTargetPasswordInfo.Size = new System.Drawing.Size(419, 15);
            lblTargetPasswordInfo.Text = "Format: C44C C8F8 CB51 8C4E (leave empty to test all seeds without stopping)";
            //
            // txtTargetPassword
            //
            txtTargetPassword.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            txtTargetPassword.Location = new System.Drawing.Point(75, 22);
            txtTargetPassword.MaxLength = 19;
            txtTargetPassword.Name = "txtTargetPassword";
            txtTargetPassword.Size = new System.Drawing.Size(220, 23);
            txtTargetPassword.TabIndex = 1;
            //
            // lblTargetPasswordLabel
            //
            lblTargetPasswordLabel.AutoSize = true;
            lblTargetPasswordLabel.Location = new System.Drawing.Point(7, 25);
            lblTargetPasswordLabel.Name = "lblTargetPasswordLabel";
            lblTargetPasswordLabel.Size = new System.Drawing.Size(43, 15);
            lblTargetPasswordLabel.Text = "Target:";
            //
            // grpLogFile
            //
            grpLogFile.Controls.Add(btnBrowseLog);
            grpLogFile.Controls.Add(txtLogFile);
            grpLogFile.Controls.Add(lblLogFile);
            grpLogFile.Location = new System.Drawing.Point(12, 358);
            grpLogFile.Name = "grpLogFile";
            grpLogFile.Size = new System.Drawing.Size(746, 78);
            grpLogFile.TabStop = false;
            grpLogFile.Text = "Password Log File (CSV)";
            //
            // btnBrowseLog
            //
            btnBrowseLog.Location = new System.Drawing.Point(652, 41);
            btnBrowseLog.Name = "btnBrowseLog";
            btnBrowseLog.Size = new System.Drawing.Size(86, 24);
            btnBrowseLog.TabIndex = 2;
            btnBrowseLog.Text = "Browse...";
            btnBrowseLog.UseVisualStyleBackColor = true;
            btnBrowseLog.Click += btnBrowseLog_Click;
            //
            // txtLogFile
            //
            txtLogFile.Location = new System.Drawing.Point(8, 42);
            txtLogFile.Name = "txtLogFile";
            txtLogFile.Size = new System.Drawing.Size(636, 23);
            txtLogFile.TabIndex = 1;
            txtLogFile.Text = "password_log.csv";
            //
            // lblLogFile
            //
            lblLogFile.AutoSize = true;
            lblLogFile.Location = new System.Drawing.Point(11, 19);
            lblLogFile.Name = "lblLogFile";
            lblLogFile.Size = new System.Drawing.Size(228, 15);
            lblLogFile.Text = "CSV file to save seed/password mappings:";
            //
            // grpSpeed
            //
            grpSpeed.Controls.Add(radSpeedSlow);
            grpSpeed.Controls.Add(radSpeedBalanced);
            grpSpeed.Controls.Add(radSpeedFast);
            grpSpeed.Location = new System.Drawing.Point(12, 442);
            grpSpeed.Name = "grpSpeed";
            grpSpeed.Size = new System.Drawing.Size(746, 54);
            grpSpeed.TabStop = false;
            grpSpeed.Text = "Speed Mode (Balanced / Slow also list each seed + password in Results)";
            //
            // radSpeedSlow
            //
            radSpeedSlow.AutoSize = true;
            radSpeedSlow.Location = new System.Drawing.Point(241, 22);
            radSpeedSlow.Name = "radSpeedSlow";
            radSpeedSlow.Size = new System.Drawing.Size(118, 19);
            radSpeedSlow.Text = "Slow (UI every 10)";
            radSpeedSlow.UseVisualStyleBackColor = true;
            //
            // radSpeedBalanced
            //
            radSpeedBalanced.AutoSize = true;
            radSpeedBalanced.Location = new System.Drawing.Point(125, 22);
            radSpeedBalanced.Name = "radSpeedBalanced";
            radSpeedBalanced.Size = new System.Drawing.Size(73, 19);
            radSpeedBalanced.Text = "Balanced";
            radSpeedBalanced.UseVisualStyleBackColor = true;
            //
            // radSpeedFast
            //
            radSpeedFast.AutoSize = true;
            radSpeedFast.Checked = true;
            radSpeedFast.Location = new System.Drawing.Point(15, 22);
            radSpeedFast.Name = "radSpeedFast";
            radSpeedFast.Size = new System.Drawing.Size(46, 19);
            radSpeedFast.TabIndex = 0;
            radSpeedFast.TabStop = true;
            radSpeedFast.Text = "Fast";
            radSpeedFast.UseVisualStyleBackColor = true;
            //
            // grpActions
            //
            grpActions.Controls.Add(btnHideResults);
            grpActions.Controls.Add(btnPauseResume);
            grpActions.Controls.Add(btnCancel);
            grpActions.Controls.Add(btnTest);
            grpActions.Controls.Add(btnClear);
            grpActions.Location = new System.Drawing.Point(12, 500);
            grpActions.Name = "grpActions";
            grpActions.Size = new System.Drawing.Size(746, 69);
            grpActions.TabStop = false;
            grpActions.Text = "Actions";
            //
            // btnHideResults
            //
            btnHideResults.Location = new System.Drawing.Point(621, 22);
            btnHideResults.Name = "btnHideResults";
            btnHideResults.Size = new System.Drawing.Size(117, 27);
            btnHideResults.TabIndex = 4;
            btnHideResults.Text = "Toggle Results";
            btnHideResults.UseVisualStyleBackColor = true;
            btnHideResults.Click += btnHideResults_Click;
            //
            // btnPauseResume
            //
            btnPauseResume.Enabled = false;
            btnPauseResume.Location = new System.Drawing.Point(136, 22);
            btnPauseResume.Name = "btnPauseResume";
            btnPauseResume.Size = new System.Drawing.Size(117, 27);
            btnPauseResume.TabIndex = 3;
            btnPauseResume.Text = "Pause";
            btnPauseResume.UseVisualStyleBackColor = true;
            btnPauseResume.Click += btnPauseResume_Click;
            //
            // btnCancel
            //
            btnCancel.Enabled = false;
            btnCancel.Location = new System.Drawing.Point(261, 22);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(117, 27);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Stop";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            //
            // btnTest
            //
            btnTest.Location = new System.Drawing.Point(11, 22);
            btnTest.Name = "btnTest";
            btnTest.Size = new System.Drawing.Size(117, 27);
            btnTest.TabIndex = 0;
            btnTest.Text = "Start Testing";
            btnTest.UseVisualStyleBackColor = true;
            btnTest.Click += btnTest_Click;
            //
            // btnClear
            //
            btnClear.Location = new System.Drawing.Point(490, 22);
            btnClear.Name = "btnClear";
            btnClear.Size = new System.Drawing.Size(117, 27);
            btnClear.TabIndex = 1;
            btnClear.Text = "Clear Results";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            //
            // grpStats
            //
            grpStats.Controls.Add(lblETA);
            grpStats.Controls.Add(lblETALabel);
            grpStats.Controls.Add(lblRemaining);
            grpStats.Controls.Add(lblRemainingLabel);
            grpStats.Controls.Add(lblSpeed);
            grpStats.Controls.Add(lblSpeedLabel);
            grpStats.Controls.Add(lblVerified);
            grpStats.Controls.Add(lblVerifiedLabel);
            grpStats.Controls.Add(lblMatchesFound);
            grpStats.Controls.Add(lblMatchesFoundLabel);
            grpStats.Controls.Add(lblSeedsTested);
            grpStats.Controls.Add(lblSeedsTestedLabel);
            grpStats.Location = new System.Drawing.Point(12, 574);
            grpStats.Name = "grpStats";
            grpStats.Size = new System.Drawing.Size(746, 96);
            grpStats.TabStop = false;
            grpStats.Text = "Statistics";
            //
            // lblETA
            //
            lblETA.AutoSize = true;
            lblETA.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblETA.Location = new System.Drawing.Point(518, 66);
            lblETA.Name = "lblETA";
            lblETA.Size = new System.Drawing.Size(34, 17);
            lblETA.Text = "N/A";
            //
            // lblETALabel
            //
            lblETALabel.AutoSize = true;
            lblETALabel.Location = new System.Drawing.Point(460, 66);
            lblETALabel.Name = "lblETALabel";
            lblETALabel.Size = new System.Drawing.Size(30, 15);
            lblETALabel.Text = "ETA:";
            //
            // lblRemaining
            //
            lblRemaining.AutoSize = true;
            lblRemaining.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblRemaining.Location = new System.Drawing.Point(518, 43);
            lblRemaining.Name = "lblRemaining";
            lblRemaining.Size = new System.Drawing.Size(34, 17);
            lblRemaining.Text = "N/A";
            //
            // lblRemainingLabel
            //
            lblRemainingLabel.AutoSize = true;
            lblRemainingLabel.Location = new System.Drawing.Point(423, 44);
            lblRemainingLabel.Name = "lblRemainingLabel";
            lblRemainingLabel.Size = new System.Drawing.Size(67, 15);
            lblRemainingLabel.Text = "Remaining:";
            //
            // lblSpeed
            //
            lblSpeed.AutoSize = true;
            lblSpeed.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblSpeed.Location = new System.Drawing.Point(150, 64);
            lblSpeed.Name = "lblSpeed";
            lblSpeed.Size = new System.Drawing.Size(95, 17);
            lblSpeed.Text = "0 seeds/sec";
            //
            // lblSpeedLabel
            //
            lblSpeedLabel.AutoSize = true;
            lblSpeedLabel.Location = new System.Drawing.Point(83, 66);
            lblSpeedLabel.Name = "lblSpeedLabel";
            lblSpeedLabel.Size = new System.Drawing.Size(42, 15);
            lblSpeedLabel.Text = "Speed:";
            //
            // lblVerified
            //
            lblVerified.AutoSize = true;
            lblVerified.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold);
            lblVerified.ForeColor = System.Drawing.Color.Blue;
            lblVerified.Location = new System.Drawing.Point(148, 40);
            lblVerified.Name = "lblVerified";
            lblVerified.Size = new System.Drawing.Size(21, 24);
            lblVerified.Text = "0";
            //
            // lblVerifiedLabel
            //
            lblVerifiedLabel.AutoSize = true;
            lblVerifiedLabel.Location = new System.Drawing.Point(18, 44);
            lblVerifiedLabel.Name = "lblVerifiedLabel";
            lblVerifiedLabel.Size = new System.Drawing.Size(107, 15);
            lblVerifiedLabel.Text = "Verified Passwords:";
            //
            // lblMatchesFound
            //
            lblMatchesFound.AutoSize = true;
            lblMatchesFound.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold);
            lblMatchesFound.ForeColor = System.Drawing.Color.Green;
            lblMatchesFound.Location = new System.Drawing.Point(148, 16);
            lblMatchesFound.Name = "lblMatchesFound";
            lblMatchesFound.Size = new System.Drawing.Size(21, 24);
            lblMatchesFound.Text = "0";
            //
            // lblMatchesFoundLabel
            //
            lblMatchesFoundLabel.AutoSize = true;
            lblMatchesFoundLabel.Location = new System.Drawing.Point(31, 22);
            lblMatchesFoundLabel.Name = "lblMatchesFoundLabel";
            lblMatchesFoundLabel.Size = new System.Drawing.Size(94, 15);
            lblMatchesFoundLabel.Text = "Generated:";
            //
            // lblSeedsTested
            //
            lblSeedsTested.AutoSize = true;
            lblSeedsTested.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold);
            lblSeedsTested.Location = new System.Drawing.Point(518, 16);
            lblSeedsTested.Name = "lblSeedsTested";
            lblSeedsTested.Size = new System.Drawing.Size(21, 24);
            lblSeedsTested.Text = "0";
            //
            // lblSeedsTestedLabel
            //
            lblSeedsTestedLabel.AutoSize = true;
            lblSeedsTestedLabel.Location = new System.Drawing.Point(413, 22);
            lblSeedsTestedLabel.Name = "lblSeedsTestedLabel";
            lblSeedsTestedLabel.Size = new System.Drawing.Size(77, 15);
            lblSeedsTestedLabel.Text = "Seeds Tested:";
            //
            // grpMultiDongle
            //
            grpMultiDongle.Controls.Add(btnDetectDongles);
            grpMultiDongle.Controls.Add(lblMulti);
            grpMultiDongle.Controls.Add(radTandem);
            grpMultiDongle.Controls.Add(radRoundRobin);
            grpMultiDongle.Controls.Add(radParallel);
            grpMultiDongle.Controls.Add(lvDongles);
            grpMultiDongle.Controls.Add(lblDongleDetected);
            grpMultiDongle.Controls.Add(lblStartSeedLabel);
            grpMultiDongle.Controls.Add(txtStartSeed);
            grpMultiDongle.Controls.Add(lblStopSeedLabel);
            grpMultiDongle.Controls.Add(txtStopSeed);
            grpMultiDongle.Controls.Add(chkSerialize);
            grpMultiDongle.Controls.Add(chkHubRecover);
            grpMultiDongle.Controls.Add(lblRecovery);
            grpMultiDongle.Controls.Add(btnResetSafe);
            grpMultiDongle.Controls.Add(btnCycleHub);
            grpMultiDongle.Controls.Add(lblMultiHint);
            grpMultiDongle.Location = new System.Drawing.Point(12, 674);
            grpMultiDongle.Name = "grpMultiDongle";
            grpMultiDongle.Size = new System.Drawing.Size(746, 234);
            grpMultiDongle.TabStop = false;
            grpMultiDongle.Text = "Dongles & Recovery";
            //
            // btnDetectDongles
            //
            btnDetectDongles.Location = new System.Drawing.Point(8, 20);
            btnDetectDongles.Name = "btnDetectDongles";
            btnDetectDongles.Size = new System.Drawing.Size(120, 26);
            btnDetectDongles.TabIndex = 0;
            btnDetectDongles.Text = "Detect Dongles";
            btnDetectDongles.UseVisualStyleBackColor = true;
            btnDetectDongles.Click += btnDetectDongles_Click;
            //
            // lblMulti
            //
            lblMulti.AutoSize = true;
            lblMulti.Location = new System.Drawing.Point(146, 25);
            lblMulti.Name = "lblMulti";
            lblMulti.Size = new System.Drawing.Size(39, 15);
            lblMulti.Text = "Mode:";
            //
            // radTandem
            //
            radTandem.AutoSize = true;
            radTandem.Checked = true;
            radTandem.Location = new System.Drawing.Point(190, 23);
            radTandem.Name = "radTandem";
            radTandem.Size = new System.Drawing.Size(68, 19);
            radTandem.TabStop = true;
            radTandem.Text = "Tandem";
            radTandem.UseVisualStyleBackColor = true;
            radTandem.CheckedChanged += radMode_CheckedChanged;
            //
            // radRoundRobin
            //
            radRoundRobin.AutoSize = true;
            radRoundRobin.Location = new System.Drawing.Point(270, 23);
            radRoundRobin.Name = "radRoundRobin";
            radRoundRobin.Size = new System.Drawing.Size(92, 19);
            radRoundRobin.Text = "Round-robin";
            radRoundRobin.UseVisualStyleBackColor = true;
            radRoundRobin.CheckedChanged += radMode_CheckedChanged;
            //
            // radParallel
            //
            radParallel.AutoSize = true;
            radParallel.Location = new System.Drawing.Point(370, 23);
            radParallel.Name = "radParallel";
            radParallel.Size = new System.Drawing.Size(146, 19);
            radParallel.Text = "Parallel (experimental)";
            radParallel.UseVisualStyleBackColor = true;
            radParallel.CheckedChanged += radMode_CheckedChanged;
            //
            // lvDongles
            //
            lvDongles.CheckBoxes = true;
            lvDongles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { colDongle, colHid, colPort, colState });
            lvDongles.FullRowSelect = true;
            lvDongles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            lvDongles.Location = new System.Drawing.Point(8, 52);
            lvDongles.Name = "lvDongles";
            lvDongles.Size = new System.Drawing.Size(500, 110);
            lvDongles.TabIndex = 5;
            lvDongles.UseCompatibleStateImageBehavior = false;
            lvDongles.View = System.Windows.Forms.View.Details;
            //
            // colDongle
            //
            colDongle.Text = "#";
            colDongle.Width = 30;
            //
            // colHid
            //
            colHid.Text = "HID";
            colHid.Width = 110;
            //
            // colPort
            //
            colPort.Text = "Port";
            colPort.Width = 170;
            //
            // colState
            //
            colState.Text = "State";
            colState.Width = 170;
            //
            // lblDongleDetected
            //
            lblDongleDetected.ForeColor = System.Drawing.Color.Gray;
            lblDongleDetected.Location = new System.Drawing.Point(516, 52);
            lblDongleDetected.Name = "lblDongleDetected";
            lblDongleDetected.Size = new System.Drawing.Size(222, 110);
            lblDongleDetected.Text = "Click 'Detect Dongles' to list connected dongles.";
            //
            // lblStartSeedLabel
            //
            lblStartSeedLabel.AutoSize = true;
            lblStartSeedLabel.Location = new System.Drawing.Point(8, 174);
            lblStartSeedLabel.Name = "lblStartSeedLabel";
            lblStartSeedLabel.Size = new System.Drawing.Size(62, 15);
            lblStartSeedLabel.Text = "Start seed:";
            //
            // txtStartSeed
            //
            txtStartSeed.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            txtStartSeed.Location = new System.Drawing.Point(78, 171);
            txtStartSeed.MaxLength = 8;
            txtStartSeed.Name = "txtStartSeed";
            txtStartSeed.Size = new System.Drawing.Size(84, 23);
            txtStartSeed.TabIndex = 6;
            //
            // lblStopSeedLabel
            //
            lblStopSeedLabel.AutoSize = true;
            lblStopSeedLabel.Location = new System.Drawing.Point(172, 174);
            lblStopSeedLabel.Name = "lblStopSeedLabel";
            lblStopSeedLabel.Size = new System.Drawing.Size(72, 15);
            lblStopSeedLabel.Text = "Stop before:";
            //
            // txtStopSeed
            //
            txtStopSeed.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            txtStopSeed.Location = new System.Drawing.Point(250, 171);
            txtStopSeed.MaxLength = 8;
            txtStopSeed.Name = "txtStopSeed";
            txtStopSeed.Size = new System.Drawing.Size(84, 23);
            txtStopSeed.TabIndex = 7;
            //
            // chkSerialize
            //
            chkSerialize.AutoSize = true;
            chkSerialize.Location = new System.Drawing.Point(352, 173);
            chkSerialize.Name = "chkSerialize";
            chkSerialize.Size = new System.Drawing.Size(88, 19);
            chkSerialize.Text = "Serialize I/O";
            chkSerialize.UseVisualStyleBackColor = true;
            //
            // chkHubRecover
            //
            chkHubRecover.AutoSize = true;
            chkHubRecover.Location = new System.Drawing.Point(452, 173);
            chkHubRecover.Name = "chkHubRecover";
            chkHubRecover.Size = new System.Drawing.Size(172, 19);
            chkHubRecover.Text = "Hub power-cycle on wedge";
            chkHubRecover.UseVisualStyleBackColor = true;
            //
            // lblRecovery
            //
            lblRecovery.AutoSize = true;
            lblRecovery.Location = new System.Drawing.Point(8, 204);
            lblRecovery.Name = "lblRecovery";
            lblRecovery.Size = new System.Drawing.Size(60, 15);
            lblRecovery.Text = "Recovery:";
            //
            // btnResetSafe
            //
            btnResetSafe.Location = new System.Drawing.Point(78, 200);
            btnResetSafe.Name = "btnResetSafe";
            btnResetSafe.Size = new System.Drawing.Size(120, 26);
            btnResetSafe.TabIndex = 8;
            btnResetSafe.Text = "Reset Dongle";
            btnResetSafe.UseVisualStyleBackColor = true;
            btnResetSafe.Click += btnResetSafe_Click;
            //
            // btnCycleHub
            //
            btnCycleHub.Location = new System.Drawing.Point(204, 200);
            btnCycleHub.Name = "btnCycleHub";
            btnCycleHub.Size = new System.Drawing.Size(140, 26);
            btnCycleHub.TabIndex = 9;
            btnCycleHub.Text = "Cycle Dongle Hub";
            btnCycleHub.UseVisualStyleBackColor = true;
            btnCycleHub.Click += btnCycleHub_Click;
            //
            // lblMultiHint
            //
            lblMultiHint.ForeColor = System.Drawing.Color.Gray;
            lblMultiHint.Location = new System.Drawing.Point(352, 200);
            lblMultiHint.Name = "lblMultiHint";
            lblMultiHint.Size = new System.Drawing.Size(386, 30);
            lblMultiHint.Text = "Tandem is the most reliable. On a wedge the app recovers automatically (reset then resume) and only asks you to replug if that fails.";
            //
            // grpResults
            //
            grpResults.Controls.Add(txtResults);
            grpResults.Location = new System.Drawing.Point(12, 912);
            grpResults.Name = "grpResults";
            grpResults.Size = new System.Drawing.Size(746, 92);
            grpResults.TabStop = false;
            grpResults.Text = "Results";
            //
            // txtResults
            //
            txtResults.BackColor = System.Drawing.Color.White;
            txtResults.Font = new System.Drawing.Font("Consolas", 9F);
            txtResults.Location = new System.Drawing.Point(8, 21);
            txtResults.Multiline = true;
            txtResults.Name = "txtResults";
            txtResults.ReadOnly = true;
            txtResults.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtResults.Size = new System.Drawing.Size(730, 63);
            txtResults.TabIndex = 0;
            //
            // MainForm
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            AutoScroll = true;
            ClientSize = new System.Drawing.Size(772, 1016);
            Controls.Add(grpDongleParams);
            Controls.Add(grpDongleInfo);
            Controls.Add(tabsMode);
            Controls.Add(grpTargetPassword);
            Controls.Add(grpLogFile);
            Controls.Add(grpSpeed);
            Controls.Add(grpActions);
            Controls.Add(grpStats);
            Controls.Add(grpMultiDongle);
            Controls.Add(grpResults);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Rockey4 Smart Password Tester";
            grpDongleParams.ResumeLayout(false);
            grpDongleParams.PerformLayout();
            grpDongleInfo.ResumeLayout(false);
            grpDongleInfo.PerformLayout();
            tabsMode.ResumeLayout(false);
            tabDictionary.ResumeLayout(false);
            grpSeedList.ResumeLayout(false);
            grpSeedList.PerformLayout();
            tabBruteForce.ResumeLayout(false);
            grpBruteForceSettings.ResumeLayout(false);
            grpBruteForceSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudLength).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudLimit).EndInit();
            tabRepair.ResumeLayout(false);
            grpTargetPassword.ResumeLayout(false);
            grpTargetPassword.PerformLayout();
            grpLogFile.ResumeLayout(false);
            grpLogFile.PerformLayout();
            grpSpeed.ResumeLayout(false);
            grpSpeed.PerformLayout();
            grpActions.ResumeLayout(false);
            grpStats.ResumeLayout(false);
            grpStats.PerformLayout();
            grpMultiDongle.ResumeLayout(false);
            grpMultiDongle.PerformLayout();
            grpResults.ResumeLayout(false);
            grpResults.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox grpDongleParams;
        private System.Windows.Forms.CheckBox chkDemo;
        private System.Windows.Forms.Label lblP4;
        private System.Windows.Forms.Label lblP3;
        private System.Windows.Forms.Label lblP2;
        private System.Windows.Forms.Label lblP1;
        private System.Windows.Forms.TextBox txtP4;
        private System.Windows.Forms.TextBox txtP3;
        private System.Windows.Forms.TextBox txtP2;
        private System.Windows.Forms.TextBox txtP1;
        private System.Windows.Forms.GroupBox grpDongleInfo;
        private System.Windows.Forms.Label lblHandle;
        private System.Windows.Forms.Label lblHandleLabel;
        private System.Windows.Forms.Label lblDongleStatus;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TabControl tabsMode;
        private System.Windows.Forms.TabPage tabDictionary;
        private System.Windows.Forms.TabPage tabBruteForce;
        private System.Windows.Forms.TabPage tabRepair;
        private System.Windows.Forms.Label lblRepairInfo;
        private System.Windows.Forms.GroupBox grpSeedList;
        private System.Windows.Forms.Button btnBrowseSeeds;
        private System.Windows.Forms.TextBox txtSeedListFile;
        private System.Windows.Forms.Label lblSeedList;
        private System.Windows.Forms.GroupBox grpBruteForceSettings;
        private System.Windows.Forms.Label lblCharsetLabel;
        private System.Windows.Forms.TextBox txtCharset;
        private System.Windows.Forms.Label lblLengthLabel;
        private System.Windows.Forms.NumericUpDown nudLength;
        private System.Windows.Forms.CheckBox chkLimit;
        private System.Windows.Forms.Label lblLimitLabel;
        private System.Windows.Forms.NumericUpDown nudLimit;
        private System.Windows.Forms.GroupBox grpTargetPassword;
        private System.Windows.Forms.Label lblTargetPasswordInfo;
        private System.Windows.Forms.TextBox txtTargetPassword;
        private System.Windows.Forms.Label lblTargetPasswordLabel;
        private System.Windows.Forms.GroupBox grpLogFile;
        private System.Windows.Forms.Button btnBrowseLog;
        private System.Windows.Forms.TextBox txtLogFile;
        private System.Windows.Forms.Label lblLogFile;
        private System.Windows.Forms.GroupBox grpSpeed;
        private System.Windows.Forms.RadioButton radSpeedSlow;
        private System.Windows.Forms.RadioButton radSpeedBalanced;
        private System.Windows.Forms.RadioButton radSpeedFast;
        private System.Windows.Forms.GroupBox grpActions;
        private System.Windows.Forms.Button btnPauseResume;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.GroupBox grpStats;
        private System.Windows.Forms.Label lblETA;
        private System.Windows.Forms.Label lblETALabel;
        private System.Windows.Forms.Label lblRemaining;
        private System.Windows.Forms.Label lblRemainingLabel;
        private System.Windows.Forms.Label lblSpeed;
        private System.Windows.Forms.Label lblSpeedLabel;
        private System.Windows.Forms.Label lblVerified;
        private System.Windows.Forms.Label lblVerifiedLabel;
        private System.Windows.Forms.Label lblMatchesFound;
        private System.Windows.Forms.Label lblMatchesFoundLabel;
        private System.Windows.Forms.Label lblSeedsTested;
        private System.Windows.Forms.Label lblSeedsTestedLabel;
        private System.Windows.Forms.GroupBox grpResults;
        private System.Windows.Forms.TextBox txtLastSeed;
        private System.Windows.Forms.Label lblLastSeedLabel;
        private System.Windows.Forms.TextBox txtResults;
        private System.Windows.Forms.Button btnHideResults;
        private System.Windows.Forms.GroupBox grpMultiDongle;
        private System.Windows.Forms.Button btnDetectDongles;
        private System.Windows.Forms.Label lblMulti;
        private System.Windows.Forms.RadioButton radTandem;
        private System.Windows.Forms.RadioButton radRoundRobin;
        private System.Windows.Forms.RadioButton radParallel;
        private System.Windows.Forms.ListView lvDongles;
        private System.Windows.Forms.ColumnHeader colDongle;
        private System.Windows.Forms.ColumnHeader colHid;
        private System.Windows.Forms.ColumnHeader colPort;
        private System.Windows.Forms.ColumnHeader colState;
        private System.Windows.Forms.Label lblDongleDetected;
        private System.Windows.Forms.Label lblStartSeedLabel;
        private System.Windows.Forms.TextBox txtStartSeed;
        private System.Windows.Forms.Label lblStopSeedLabel;
        private System.Windows.Forms.TextBox txtStopSeed;
        private System.Windows.Forms.CheckBox chkSerialize;
        private System.Windows.Forms.CheckBox chkHubRecover;
        private System.Windows.Forms.Label lblRecovery;
        private System.Windows.Forms.Button btnResetSafe;
        private System.Windows.Forms.Button btnCycleHub;
        private System.Windows.Forms.Label lblMultiHint;
    }
}
