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
            txtResults = new System.Windows.Forms.TextBox();
            grpMode = new System.Windows.Forms.GroupBox();
            btnModeDictionary = new System.Windows.Forms.Button();
            btnModeBruteForce = new System.Windows.Forms.Button();
            lblMode = new System.Windows.Forms.Label();
            grpSeedList = new System.Windows.Forms.GroupBox();
            btnBrowseSeeds = new System.Windows.Forms.Button();
            txtSeedListFile = new System.Windows.Forms.TextBox();
            lblSeedList = new System.Windows.Forms.Label();
            grpBruteForceSettings = new System.Windows.Forms.GroupBox();
            lblCharsetLabel = new System.Windows.Forms.Label();
            txtCharset = new System.Windows.Forms.TextBox();
            lblLengthLabel = new System.Windows.Forms.Label();
            nudLength = new System.Windows.Forms.NumericUpDown();
            chkLimit = new System.Windows.Forms.CheckBox();
            lblLimitLabel = new System.Windows.Forms.Label();
            nudLimit = new System.Windows.Forms.NumericUpDown();
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
            grpResults = new System.Windows.Forms.GroupBox();
            grpDongleParams.SuspendLayout();
            grpDongleInfo.SuspendLayout();
            grpMode.SuspendLayout();
            grpSeedList.SuspendLayout();
            grpBruteForceSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudLimit).BeginInit();
            grpTargetPassword.SuspendLayout();
            grpLogFile.SuspendLayout();
            grpSpeed.SuspendLayout();
            grpActions.SuspendLayout();
            grpStats.SuspendLayout();
            grpResults.SuspendLayout();
            SuspendLayout();
            // 
            // grpDongleParams
            // 
            grpDongleParams.Controls.Add(lblP4);
            grpDongleParams.Controls.Add(lblP3);
            grpDongleParams.Controls.Add(lblP2);
            grpDongleParams.Controls.Add(lblP1);
            grpDongleParams.Controls.Add(txtP4);
            grpDongleParams.Controls.Add(txtP3);
            grpDongleParams.Controls.Add(txtP2);
            grpDongleParams.Controls.Add(txtP1);
            grpDongleParams.Location = new System.Drawing.Point(13, 12);
            grpDongleParams.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpDongleParams.Name = "grpDongleParams";
            grpDongleParams.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpDongleParams.Size = new System.Drawing.Size(303, 92);
            grpDongleParams.TabIndex = 1;
            grpDongleParams.TabStop = false;
            grpDongleParams.Text = "Dongle Parameters (P1-P4)";
            // 
            // lblP4
            // 
            lblP4.AutoSize = true;
            lblP4.Location = new System.Drawing.Point(222, 58);
            lblP4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblP4.Name = "lblP4";
            lblP4.Size = new System.Drawing.Size(20, 15);
            lblP4.TabIndex = 7;
            lblP4.Text = "P4";
            // 
            // lblP3
            // 
            lblP3.AutoSize = true;
            lblP3.Location = new System.Drawing.Point(163, 58);
            lblP3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblP3.Name = "lblP3";
            lblP3.Size = new System.Drawing.Size(20, 15);
            lblP3.TabIndex = 6;
            lblP3.Text = "P3";
            // 
            // lblP2
            // 
            lblP2.AutoSize = true;
            lblP2.Location = new System.Drawing.Point(105, 58);
            lblP2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblP2.Name = "lblP2";
            lblP2.Size = new System.Drawing.Size(20, 15);
            lblP2.TabIndex = 5;
            lblP2.Text = "P2";
            // 
            // lblP1
            // 
            lblP1.AutoSize = true;
            lblP1.Location = new System.Drawing.Point(47, 58);
            lblP1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblP1.Name = "lblP1";
            lblP1.Size = new System.Drawing.Size(20, 15);
            lblP1.TabIndex = 4;
            lblP1.Text = "P1";
            // 
            // txtP4
            // 
            txtP4.Location = new System.Drawing.Point(217, 23);
            txtP4.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtP4.MaxLength = 4;
            txtP4.Name = "txtP4";
            txtP4.Size = new System.Drawing.Size(46, 23);
            txtP4.TabIndex = 3;
            txtP4.Text = "8C4E";
            // 
            // txtP3
            // 
            txtP3.Location = new System.Drawing.Point(159, 23);
            txtP3.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtP3.MaxLength = 4;
            txtP3.Name = "txtP3";
            txtP3.Size = new System.Drawing.Size(46, 23);
            txtP3.TabIndex = 2;
            txtP3.Text = "CB51";
            // 
            // txtP2
            // 
            txtP2.Location = new System.Drawing.Point(100, 23);
            txtP2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtP2.MaxLength = 4;
            txtP2.Name = "txtP2";
            txtP2.Size = new System.Drawing.Size(46, 23);
            txtP2.TabIndex = 1;
            txtP2.Text = "00FC";
            // 
            // txtP1
            // 
            txtP1.Location = new System.Drawing.Point(42, 23);
            txtP1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            grpDongleInfo.Location = new System.Drawing.Point(323, 12);
            grpDongleInfo.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpDongleInfo.Name = "grpDongleInfo";
            grpDongleInfo.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpDongleInfo.Size = new System.Drawing.Size(420, 115);
            grpDongleInfo.TabIndex = 2;
            grpDongleInfo.TabStop = false;
            grpDongleInfo.Text = "Dongle Status & Resume";
            // 
            // txtLastSeed
            // 
            txtLastSeed.Location = new System.Drawing.Point(84, 73);
            txtLastSeed.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtLastSeed.Name = "txtLastSeed";
            txtLastSeed.ReadOnly = true;
            txtLastSeed.Size = new System.Drawing.Size(328, 23);
            txtLastSeed.TabIndex = 5;
            // 
            // lblLastSeedLabel
            // 
            lblLastSeedLabel.AutoSize = true;
            lblLastSeedLabel.Location = new System.Drawing.Point(6, 78);
            lblLastSeedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblLastSeedLabel.Name = "lblLastSeedLabel";
            lblLastSeedLabel.Size = new System.Drawing.Size(59, 15);
            lblLastSeedLabel.TabIndex = 4;
            lblLastSeedLabel.Text = "Last Seed:";
            // 
            // lblHandle
            // 
            lblHandle.AutoSize = true;
            lblHandle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblHandle.Location = new System.Drawing.Point(80, 53);
            lblHandle.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblHandle.Name = "lblHandle";
            lblHandle.Size = new System.Drawing.Size(34, 17);
            lblHandle.TabIndex = 3;
            lblHandle.Text = "N/A";
            // 
            // lblHandleLabel
            // 
            lblHandleLabel.AutoSize = true;
            lblHandleLabel.Location = new System.Drawing.Point(7, 55);
            lblHandleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblHandleLabel.Name = "lblHandleLabel";
            lblHandleLabel.Size = new System.Drawing.Size(48, 15);
            lblHandleLabel.TabIndex = 2;
            lblHandleLabel.Text = "Handle:";
            // 
            // lblDongleStatus
            // 
            lblDongleStatus.AutoSize = true;
            lblDongleStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblDongleStatus.ForeColor = System.Drawing.Color.Red;
            lblDongleStatus.Location = new System.Drawing.Point(281, 53);
            lblDongleStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblDongleStatus.Name = "lblDongleStatus";
            lblDongleStatus.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            lblDongleStatus.Size = new System.Drawing.Size(131, 17);
            lblDongleStatus.TabIndex = 1;
            lblDongleStatus.Text = "Not Connected ✗";
            lblDongleStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new System.Drawing.Point(7, 27);
            lblStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(42, 15);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Status:";
            // 
            // txtResults
            // 
            txtResults.BackColor = System.Drawing.Color.White;
            txtResults.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            txtResults.Location = new System.Drawing.Point(8, 21);
            txtResults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtResults.Multiline = true;
            txtResults.Name = "txtResults";
            txtResults.ReadOnly = true;
            txtResults.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtResults.Size = new System.Drawing.Size(716, 47);
            txtResults.TabIndex = 0;
            // 
            // grpMode
            // 
            grpMode.Controls.Add(btnModeDictionary);
            grpMode.Controls.Add(btnModeBruteForce);
            grpMode.Controls.Add(lblMode);
            grpMode.Location = new System.Drawing.Point(13, 110);
            grpMode.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpMode.Name = "grpMode";
            grpMode.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpMode.Size = new System.Drawing.Size(730, 58);
            grpMode.TabIndex = 3;
            grpMode.TabStop = false;
            grpMode.Text = "Test Mode";
            // 
            // btnModeDictionary
            // 
            btnModeDictionary.Location = new System.Drawing.Point(8, 22);
            btnModeDictionary.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnModeDictionary.Name = "btnModeDictionary";
            btnModeDictionary.Size = new System.Drawing.Size(175, 27);
            btnModeDictionary.TabIndex = 0;
            btnModeDictionary.Text = "Dictionary (File)";
            btnModeDictionary.UseVisualStyleBackColor = true;
            btnModeDictionary.Click += btnModeDictionary_Click;
            // 
            // btnModeBruteForce
            // 
            btnModeBruteForce.Location = new System.Drawing.Point(190, 22);
            btnModeBruteForce.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnModeBruteForce.Name = "btnModeBruteForce";
            btnModeBruteForce.Size = new System.Drawing.Size(175, 27);
            btnModeBruteForce.TabIndex = 1;
            btnModeBruteForce.Text = "Brute-Force";
            btnModeBruteForce.UseVisualStyleBackColor = true;
            btnModeBruteForce.Click += btnModeBruteForce_Click;
            // 
            // lblMode
            // 
            lblMode.AutoSize = true;
            lblMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, 0);
            lblMode.Location = new System.Drawing.Point(386, 27);
            lblMode.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblMode.Name = "lblMode";
            lblMode.Size = new System.Drawing.Size(168, 15);
            lblMode.TabIndex = 2;
            lblMode.Text = "Mode: Dictionary (File-based)";
            // 
            // grpSeedList
            // 
            grpSeedList.Controls.Add(btnBrowseSeeds);
            grpSeedList.Controls.Add(txtSeedListFile);
            grpSeedList.Controls.Add(lblSeedList);
            grpSeedList.Location = new System.Drawing.Point(13, 174);
            grpSeedList.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpSeedList.Name = "grpSeedList";
            grpSeedList.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpSeedList.Size = new System.Drawing.Size(730, 57);
            grpSeedList.TabIndex = 4;
            grpSeedList.TabStop = false;
            grpSeedList.Text = "Seed List File (Dictionary Mode)";
            // 
            // btnBrowseSeeds
            // 
            btnBrowseSeeds.Location = new System.Drawing.Point(636, 23);
            btnBrowseSeeds.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnBrowseSeeds.Name = "btnBrowseSeeds";
            btnBrowseSeeds.Size = new System.Drawing.Size(86, 24);
            btnBrowseSeeds.TabIndex = 2;
            btnBrowseSeeds.Text = "Browse...";
            btnBrowseSeeds.UseVisualStyleBackColor = true;
            btnBrowseSeeds.Click += btnBrowseSeeds_Click;
            // 
            // txtSeedListFile
            // 
            txtSeedListFile.Location = new System.Drawing.Point(8, 23);
            txtSeedListFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtSeedListFile.Name = "txtSeedListFile";
            txtSeedListFile.Size = new System.Drawing.Size(620, 23);
            txtSeedListFile.TabIndex = 1;
            // 
            // lblSeedList
            // 
            lblSeedList.AutoSize = true;
            lblSeedList.Location = new System.Drawing.Point(7, 0);
            lblSeedList.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSeedList.Name = "lblSeedList";
            lblSeedList.Size = new System.Drawing.Size(202, 15);
            lblSeedList.TabIndex = 0;
            lblSeedList.Text = "Text file containing one seed per line:";
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
            grpBruteForceSettings.Enabled = false;
            grpBruteForceSettings.Location = new System.Drawing.Point(13, 237);
            grpBruteForceSettings.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpBruteForceSettings.Name = "grpBruteForceSettings";
            grpBruteForceSettings.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpBruteForceSettings.Size = new System.Drawing.Size(730, 98);
            grpBruteForceSettings.TabIndex = 5;
            grpBruteForceSettings.TabStop = false;
            grpBruteForceSettings.Text = "Brute-Force Settings (Brute-Force Mode)";
            // 
            // lblCharsetLabel
            // 
            lblCharsetLabel.AutoSize = true;
            lblCharsetLabel.Location = new System.Drawing.Point(7, 29);
            lblCharsetLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblCharsetLabel.Name = "lblCharsetLabel";
            lblCharsetLabel.Size = new System.Drawing.Size(50, 15);
            lblCharsetLabel.TabIndex = 6;
            lblCharsetLabel.Text = "Charset:";
            // 
            // txtCharset
            // 
            txtCharset.Location = new System.Drawing.Point(75, 25);
            txtCharset.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtCharset.Name = "txtCharset";
            txtCharset.Size = new System.Drawing.Size(349, 23);
            txtCharset.TabIndex = 5;
            txtCharset.Text = "0123456789ABCDEF";
            // 
            // lblLengthLabel
            // 
            lblLengthLabel.AutoSize = true;
            lblLengthLabel.Location = new System.Drawing.Point(443, 29);
            lblLengthLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblLengthLabel.Name = "lblLengthLabel";
            lblLengthLabel.Size = new System.Drawing.Size(47, 15);
            lblLengthLabel.TabIndex = 4;
            lblLengthLabel.Text = "Length:";
            // 
            // nudLength
            // 
            nudLength.Location = new System.Drawing.Point(500, 25);
            nudLength.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            chkLimit.Checked = true;
            chkLimit.CheckState = System.Windows.Forms.CheckState.Checked;
            chkLimit.Location = new System.Drawing.Point(11, 62);
            chkLimit.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            lblLimitLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblLimitLabel.Name = "lblLimitLabel";
            lblLimitLabel.Size = new System.Drawing.Size(32, 15);
            lblLimitLabel.TabIndex = 1;
            lblLimitLabel.Text = "Max:";
            // 
            // nudLimit
            // 
            nudLimit.Location = new System.Drawing.Point(128, 60);
            nudLimit.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            nudLimit.Maximum = new decimal(new int[] { 0, 1, 0, 0 });
            nudLimit.Name = "nudLimit";
            nudLimit.Size = new System.Drawing.Size(117, 23);
            nudLimit.TabIndex = 0;
            nudLimit.Value = new decimal(new int[] { 1000000, 0, 0, 0 });
            // 
            // grpTargetPassword
            // 
            grpTargetPassword.Controls.Add(lblTargetPasswordInfo);
            grpTargetPassword.Controls.Add(txtTargetPassword);
            grpTargetPassword.Controls.Add(lblTargetPasswordLabel);
            grpTargetPassword.Location = new System.Drawing.Point(13, 341);
            grpTargetPassword.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpTargetPassword.Name = "grpTargetPassword";
            grpTargetPassword.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpTargetPassword.Size = new System.Drawing.Size(730, 59);
            grpTargetPassword.TabIndex = 6;
            grpTargetPassword.TabStop = false;
            grpTargetPassword.Text = "Target Password (Optional - Stops when found)";
            // 
            // lblTargetPasswordInfo
            // 
            lblTargetPasswordInfo.AutoSize = true;
            lblTargetPasswordInfo.ForeColor = System.Drawing.Color.Gray;
            lblTargetPasswordInfo.Location = new System.Drawing.Point(303, 25);
            lblTargetPasswordInfo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblTargetPasswordInfo.Name = "lblTargetPasswordInfo";
            lblTargetPasswordInfo.Size = new System.Drawing.Size(419, 15);
            lblTargetPasswordInfo.TabIndex = 2;
            lblTargetPasswordInfo.Text = "Format: C44C C8F8 CB51 8C4E (leave empty to test all seeds without stopping)";
            // 
            // txtTargetPassword
            // 
            txtTargetPassword.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            txtTargetPassword.Location = new System.Drawing.Point(75, 22);
            txtTargetPassword.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtTargetPassword.MaxLength = 19;
            txtTargetPassword.Name = "txtTargetPassword";
            txtTargetPassword.Size = new System.Drawing.Size(220, 23);
            txtTargetPassword.TabIndex = 1;
            txtTargetPassword.Text = "6E07 646C B21F 77EA";
            // 
            // lblTargetPasswordLabel
            // 
            lblTargetPasswordLabel.AutoSize = true;
            lblTargetPasswordLabel.Location = new System.Drawing.Point(7, 25);
            lblTargetPasswordLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblTargetPasswordLabel.Name = "lblTargetPasswordLabel";
            lblTargetPasswordLabel.Size = new System.Drawing.Size(43, 15);
            lblTargetPasswordLabel.TabIndex = 0;
            lblTargetPasswordLabel.Text = "Target:";
            // 
            // grpLogFile
            // 
            grpLogFile.Controls.Add(btnBrowseLog);
            grpLogFile.Controls.Add(txtLogFile);
            grpLogFile.Controls.Add(lblLogFile);
            grpLogFile.Location = new System.Drawing.Point(13, 406);
            grpLogFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpLogFile.Name = "grpLogFile";
            grpLogFile.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpLogFile.Size = new System.Drawing.Size(730, 78);
            grpLogFile.TabIndex = 7;
            grpLogFile.TabStop = false;
            grpLogFile.Text = "Password Log File (CSV)";
            // 
            // btnBrowseLog
            // 
            btnBrowseLog.Location = new System.Drawing.Point(636, 41);
            btnBrowseLog.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            txtLogFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtLogFile.Name = "txtLogFile";
            txtLogFile.Size = new System.Drawing.Size(620, 23);
            txtLogFile.TabIndex = 1;
            txtLogFile.Text = "password_log.csv";
            // 
            // lblLogFile
            // 
            lblLogFile.AutoSize = true;
            lblLogFile.Location = new System.Drawing.Point(11, 19);
            lblLogFile.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblLogFile.Name = "lblLogFile";
            lblLogFile.Size = new System.Drawing.Size(228, 15);
            lblLogFile.TabIndex = 0;
            lblLogFile.Text = "CSV file to save seed/password mappings:";
            // 
            // grpSpeed
            // 
            grpSpeed.Controls.Add(radSpeedSlow);
            grpSpeed.Controls.Add(radSpeedBalanced);
            grpSpeed.Controls.Add(radSpeedFast);
            grpSpeed.Location = new System.Drawing.Point(13, 490);
            grpSpeed.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpSpeed.Name = "grpSpeed";
            grpSpeed.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpSpeed.Size = new System.Drawing.Size(730, 54);
            grpSpeed.TabIndex = 8;
            grpSpeed.TabStop = false;
            grpSpeed.Text = "Speed Mode";
            // 
            // radSpeedSlow
            // 
            radSpeedSlow.AutoSize = true;
            radSpeedSlow.Location = new System.Drawing.Point(241, 22);
            radSpeedSlow.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            radSpeedSlow.Name = "radSpeedSlow";
            radSpeedSlow.Size = new System.Drawing.Size(118, 19);
            radSpeedSlow.TabIndex = 2;
            radSpeedSlow.TabStop = true;
            radSpeedSlow.Text = "Slow (UI every 10)";
            radSpeedSlow.UseVisualStyleBackColor = true;
            // 
            // radSpeedBalanced
            // 
            radSpeedBalanced.AutoSize = true;
            radSpeedBalanced.Checked = true;
            radSpeedBalanced.Location = new System.Drawing.Point(125, 22);
            radSpeedBalanced.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            radSpeedBalanced.Name = "radSpeedBalanced";
            radSpeedBalanced.Size = new System.Drawing.Size(73, 19);
            radSpeedBalanced.TabIndex = 1;
            radSpeedBalanced.TabStop = true;
            radSpeedBalanced.Text = "Balanced";
            radSpeedBalanced.UseVisualStyleBackColor = true;
            // 
            // radSpeedFast
            // 
            radSpeedFast.AutoSize = true;
            radSpeedFast.Location = new System.Drawing.Point(15, 22);
            radSpeedFast.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            grpActions.Location = new System.Drawing.Point(13, 550);
            grpActions.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpActions.Name = "grpActions";
            grpActions.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpActions.Size = new System.Drawing.Size(730, 69);
            grpActions.TabIndex = 9;
            grpActions.TabStop = false;
            grpActions.Text = "Actions";
            // 
            // btnHideResults
            // 
            btnHideResults.Location = new System.Drawing.Point(605, 22);
            btnHideResults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            btnPauseResume.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            btnCancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            btnTest.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnTest.Name = "btnTest";
            btnTest.Size = new System.Drawing.Size(117, 27);
            btnTest.TabIndex = 0;
            btnTest.Text = "Start Testing";
            btnTest.UseVisualStyleBackColor = true;
            btnTest.Click += btnTest_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new System.Drawing.Point(480, 22);
            btnClear.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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
            grpStats.Location = new System.Drawing.Point(13, 625);
            grpStats.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpStats.Name = "grpStats";
            grpStats.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpStats.Size = new System.Drawing.Size(730, 93);
            grpStats.TabIndex = 10;
            grpStats.TabStop = false;
            grpStats.Text = "Statistics";
            // 
            // lblETA
            // 
            lblETA.AutoSize = true;
            lblETA.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblETA.Location = new System.Drawing.Point(518, 66);
            lblETA.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblETA.Name = "lblETA";
            lblETA.Size = new System.Drawing.Size(34, 17);
            lblETA.TabIndex = 11;
            lblETA.Text = "N/A";
            // 
            // lblETALabel
            // 
            lblETALabel.AutoSize = true;
            lblETALabel.Location = new System.Drawing.Point(460, 66);
            lblETALabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblETALabel.Name = "lblETALabel";
            lblETALabel.Size = new System.Drawing.Size(30, 15);
            lblETALabel.TabIndex = 10;
            lblETALabel.Text = "ETA:";
            // 
            // lblRemaining
            // 
            lblRemaining.AutoSize = true;
            lblRemaining.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblRemaining.Location = new System.Drawing.Point(518, 43);
            lblRemaining.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblRemaining.Name = "lblRemaining";
            lblRemaining.Size = new System.Drawing.Size(34, 17);
            lblRemaining.TabIndex = 9;
            lblRemaining.Text = "N/A";
            // 
            // lblRemainingLabel
            // 
            lblRemainingLabel.AutoSize = true;
            lblRemainingLabel.Location = new System.Drawing.Point(423, 44);
            lblRemainingLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblRemainingLabel.Name = "lblRemainingLabel";
            lblRemainingLabel.Size = new System.Drawing.Size(67, 15);
            lblRemainingLabel.TabIndex = 8;
            lblRemainingLabel.Text = "Remaining:";
            // 
            // lblSpeed
            // 
            lblSpeed.AutoSize = true;
            lblSpeed.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblSpeed.Location = new System.Drawing.Point(150, 64);
            lblSpeed.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSpeed.Name = "lblSpeed";
            lblSpeed.Size = new System.Drawing.Size(95, 17);
            lblSpeed.TabIndex = 7;
            lblSpeed.Text = "0 seeds/sec";
            // 
            // lblSpeedLabel
            // 
            lblSpeedLabel.AutoSize = true;
            lblSpeedLabel.Location = new System.Drawing.Point(83, 66);
            lblSpeedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSpeedLabel.Name = "lblSpeedLabel";
            lblSpeedLabel.Size = new System.Drawing.Size(42, 15);
            lblSpeedLabel.TabIndex = 6;
            lblSpeedLabel.Text = "Speed:";
            // 
            // lblVerified
            // 
            lblVerified.AutoSize = true;
            lblVerified.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblVerified.ForeColor = System.Drawing.Color.Blue;
            lblVerified.Location = new System.Drawing.Point(148, 40);
            lblVerified.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblVerified.Name = "lblVerified";
            lblVerified.Size = new System.Drawing.Size(21, 24);
            lblVerified.TabIndex = 5;
            lblVerified.Text = "0";
            // 
            // lblVerifiedLabel
            // 
            lblVerifiedLabel.AutoSize = true;
            lblVerifiedLabel.Location = new System.Drawing.Point(18, 44);
            lblVerifiedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblVerifiedLabel.Name = "lblVerifiedLabel";
            lblVerifiedLabel.Size = new System.Drawing.Size(107, 15);
            lblVerifiedLabel.TabIndex = 4;
            lblVerifiedLabel.Text = "Verified Passwords:";
            // 
            // lblMatchesFound
            // 
            lblMatchesFound.AutoSize = true;
            lblMatchesFound.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblMatchesFound.ForeColor = System.Drawing.Color.Green;
            lblMatchesFound.Location = new System.Drawing.Point(148, 16);
            lblMatchesFound.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblMatchesFound.Name = "lblMatchesFound";
            lblMatchesFound.Size = new System.Drawing.Size(21, 24);
            lblMatchesFound.TabIndex = 3;
            lblMatchesFound.Text = "0";
            // 
            // lblMatchesFoundLabel
            // 
            lblMatchesFoundLabel.AutoSize = true;
            lblMatchesFoundLabel.Location = new System.Drawing.Point(31, 22);
            lblMatchesFoundLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblMatchesFoundLabel.Name = "lblMatchesFoundLabel";
            lblMatchesFoundLabel.Size = new System.Drawing.Size(94, 15);
            lblMatchesFoundLabel.TabIndex = 2;
            lblMatchesFoundLabel.Text = "Successful Tests:";
            // 
            // lblSeedsTested
            // 
            lblSeedsTested.AutoSize = true;
            lblSeedsTested.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblSeedsTested.Location = new System.Drawing.Point(518, 16);
            lblSeedsTested.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSeedsTested.Name = "lblSeedsTested";
            lblSeedsTested.Size = new System.Drawing.Size(21, 24);
            lblSeedsTested.TabIndex = 1;
            lblSeedsTested.Text = "0";
            // 
            // lblSeedsTestedLabel
            // 
            lblSeedsTestedLabel.AutoSize = true;
            lblSeedsTestedLabel.Location = new System.Drawing.Point(413, 22);
            lblSeedsTestedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSeedsTestedLabel.Name = "lblSeedsTestedLabel";
            lblSeedsTestedLabel.Size = new System.Drawing.Size(77, 15);
            lblSeedsTestedLabel.TabIndex = 0;
            lblSeedsTestedLabel.Text = "Seeds Tested:";
            // 
            // grpResults
            // 
            grpResults.Controls.Add(txtResults);
            grpResults.Location = new System.Drawing.Point(13, 724);
            grpResults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpResults.Name = "grpResults";
            grpResults.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            grpResults.Size = new System.Drawing.Size(730, 76);
            grpResults.TabIndex = 11;
            grpResults.TabStop = false;
            grpResults.Text = "Results (✓✓ = Verified password works for connection)";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(758, 807);
            Controls.Add(grpResults);
            Controls.Add(grpStats);
            Controls.Add(grpActions);
            Controls.Add(grpSpeed);
            Controls.Add(grpLogFile);
            Controls.Add(grpTargetPassword);
            Controls.Add(grpBruteForceSettings);
            Controls.Add(grpSeedList);
            Controls.Add(grpMode);
            Controls.Add(grpDongleInfo);
            Controls.Add(grpDongleParams);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Rockey4 Smart Password Tester - Brute-Force Enabled";
            grpDongleParams.ResumeLayout(false);
            grpDongleParams.PerformLayout();
            grpDongleInfo.ResumeLayout(false);
            grpDongleInfo.PerformLayout();
            grpMode.ResumeLayout(false);
            grpMode.PerformLayout();
            grpSeedList.ResumeLayout(false);
            grpSeedList.PerformLayout();
            grpBruteForceSettings.ResumeLayout(false);
            grpBruteForceSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudLength).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudLimit).EndInit();
            grpTargetPassword.ResumeLayout(false);
            grpTargetPassword.PerformLayout();
            grpLogFile.ResumeLayout(false);
            grpLogFile.PerformLayout();
            grpSpeed.ResumeLayout(false);
            grpSpeed.PerformLayout();
            grpActions.ResumeLayout(false);
            grpStats.ResumeLayout(false);
            grpStats.PerformLayout();
            grpResults.ResumeLayout(false);
            grpResults.PerformLayout();
            ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.GroupBox grpDongleParams;
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
        private System.Windows.Forms.GroupBox grpMode;
        private System.Windows.Forms.Button btnModeDictionary;
        private System.Windows.Forms.Button btnModeBruteForce;
        private System.Windows.Forms.Label lblMode;
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
    }
}
