// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OpenBurningSuite.Xp.Services;

namespace OpenBurningSuite.Xp
{
    internal sealed class MainForm : Form
    {
        private const int OperationRailWidth = 252;

        private readonly LegacyBurningService _burningService = new LegacyBurningService();

        private ComboBox _driveCombo;
        private ComboBox _burnDriveCombo;
        private ListView _driveList;
        private TextBox _buildSourceText;
        private TextBox _buildOutputText;
        private TextBox _volumeNameText;
        private TextBox _burnImageText;
        private TextBox _copyOutputText;
        private TextBox _verifyFileText;
        private TextBox _expectedHashText;
        private TextBox _actualHashText;
        private TextBox _logText;
        private ComboBox _algorithmCombo;
        private ComboBox _copyDriveCombo;
        private ComboBox _eraseDriveCombo;
        private CheckBox _verifyAfterCopyCheck;
        private CheckBox _fullEraseCheck;
        private CheckBox _ejectAfterBurnCheck;
        private CheckBox _verifyAfterBurnCheck;
        private Label _componentStatusLabel;
        private Label _statusLabel;
        private Control _refreshButton;
        private Button _buildButton;
        private Button _burnButton;
        private Button _abortButton;
        private Button _copyButton;
        private Button _copyAbortButton;
        private Button _eraseButton;
        private Button _verifyButton;
        private NeutralProgressBar _writeProgress;
        private NeutralProgressBar _bufferProgress;
        private NeutralProgressBar _deviceBufferProgress;
        private NeutralProgressBar _copyProgress;
        private TabControl _tabs;
        private CancellationTokenSource _burnCancellation;
        private bool _burnInProgress;

        public MainForm()
        {
            Text = "LuxBurn";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 700);
            Size = new Size(1280, 760);
            Font = new Font("Tahoma", 8.25f);
            BackColor = Color.FromArgb(240, 240, 236);

            BuildInterface();
            RefreshComponentStatus();
            RefreshDrives();
            FormClosing += MainFormFormClosing;
        }

        private void BuildInterface()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 66;
            header.BackColor = Color.FromArgb(45, 52, 57);
            Controls.Add(header);

            PictureBox titleLogo = new PictureBox();
            titleLogo.Image = LoadBrandAsset("LB-Logo.png");
            titleLogo.Location = new Point(16, 10);
            titleLogo.Size = titleLogo.Image == null ? new Size(184, 46) : titleLogo.Image.Size;
            titleLogo.SizeMode = PictureBoxSizeMode.Normal;
            header.Controls.Add(titleLogo);

            _componentStatusLabel = new Label();
            _componentStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _componentStatusLabel.ForeColor = Color.FromArgb(226, 226, 218);
            _componentStatusLabel.TextAlign = ContentAlignment.MiddleRight;
            _componentStatusLabel.Size = new Size(430, 38);
            _componentStatusLabel.Location = new Point(ClientSize.Width - 446, 14);
            header.Controls.Add(_componentStatusLabel);
            header.Resize += delegate
            {
                _componentStatusLabel.Location = new Point(header.ClientSize.Width - 446, 14);
            };

            StatusStrip statusStrip = new StatusStrip();
            statusStrip.SizingGrip = false;
            ToolStripStatusLabel status = new ToolStripStatusLabel("Ready");
            status.Spring = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            statusStrip.Items.Add(status);
            Controls.Add(statusStrip);

            _statusLabel = new Label();
            _statusLabel.Visible = false;
            _statusLabel.Tag = status;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.FixedPanel = FixedPanel.Panel1;
            split.Panel1MinSize = OperationRailWidth;
            split.SplitterWidth = 4;
            split.SplitterDistance = OperationRailWidth;
            Controls.Add(split);
            split.BringToFront();
            Load += delegate { split.SplitterDistance = OperationRailWidth; };

            BuildLeftPanel(split.Panel1);
            BuildTabs(split.Panel2);
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.BackColor = Color.FromArgb(229, 229, 224);
            parent.Padding = new Padding(12);

            Label section = new Label();
            section.Text = "Operations";
            section.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            section.AutoSize = true;
            section.Location = new Point(12, 14);
            parent.Controls.Add(section);

            _refreshButton = CreateSideButton(44, "Refresh Drives", "icon_refresh.png");
            _refreshButton.Click += delegate { RefreshDrives(); };
            parent.Controls.Add(_refreshButton);

            Control buildTabButton = CreateSideButton(84, "Build", "145_add.png");
            buildTabButton.Click += delegate { _tabs.SelectedIndex = 1; };
            parent.Controls.Add(buildTabButton);

            Control burnTabButton = CreateSideButton(124, "Write", "139-Edit.png");
            burnTabButton.Click += delegate { _tabs.SelectedIndex = 2; };
            parent.Controls.Add(burnTabButton);

            Control eraseTabButton = CreateSideButton(164, "Erase Disc", "18_delete.png");
            eraseTabButton.Click += delegate { _tabs.SelectedIndex = 4; };
            parent.Controls.Add(eraseTabButton);

            Control verifyTabButton = CreateSideButton(204, "Verify Files", "6-ApplyButton.png");
            verifyTabButton.Click += delegate { _tabs.SelectedIndex = 5; };
            parent.Controls.Add(verifyTabButton);

            Control wizardTabButton = CreateSideButton(244, "Wizards", "wand.png");
            wizardTabButton.Click += delegate { _tabs.SelectedIndex = 6; };
            parent.Controls.Add(wizardTabButton);
        }

        private void BuildTabs(Control parent)
        {
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(12, 5);
            parent.Controls.Add(_tabs);

            _tabs.TabPages.Add(CreateDriveTab());
            _tabs.TabPages.Add(CreateBuildTab());
            _tabs.TabPages.Add(CreateBurnTab());
            _tabs.TabPages.Add(CreateCopyTab());
            _tabs.TabPages.Add(CreateEraseTab());
            _tabs.TabPages.Add(CreateVerifyTab());
            _tabs.TabPages.Add(CreateWizardTab());
            _tabs.TabPages.Add(CreateLogTab());
        }

        private TabPage CreateDriveTab()
        {
            TabPage page = CreatePage("Drives");

            GroupBox group = CreateGroup("Detected optical drives", 14, 14, 760, 420);
            page.Controls.Add(group);

            _driveCombo = new ComboBox();
            _driveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _driveCombo.Location = new Point(18, 28);
            _driveCombo.Size = new Size(520, 22);
            group.Controls.Add(_driveCombo);

            Button refresh = CreateButton("Refresh", 550, 26, 90, 25);
            refresh.Click += delegate { RefreshDrives(); };
            group.Controls.Add(refresh);

            _driveList = new ListView();
            _driveList.Location = new Point(18, 68);
            _driveList.Size = new Size(720, 320);
            _driveList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _driveList.View = View.Details;
            _driveList.FullRowSelect = true;
            _driveList.GridLines = true;
            _driveList.Columns.Add("Drive", 300);
            _driveList.Columns.Add("Vendor", 130);
            _driveList.Columns.Add("Product", 160);
            _driveList.Columns.Add("Recorder ID", 360);
            group.Controls.Add(_driveList);

            return page;
        }

        private TabPage CreateBuildTab()
        {
            TabPage page = CreatePage("Build Image");
            GroupBox group = CreateGroup("Create ISO image from folder", 14, 14, 760, 250);
            page.Controls.Add(group);

            AddLabel(group, "Source folder", 18, 32);
            _buildSourceText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_buildSourceText);
            Button browseSource = CreateButton("Browse", 642, 27, 86, 25);
            browseSource.Click += delegate { BrowseFolder(_buildSourceText); };
            group.Controls.Add(browseSource);

            AddLabel(group, "Output image", 18, 70);
            _buildOutputText = CreateTextBox(130, 66, 500);
            group.Controls.Add(_buildOutputText);
            Button browseOutput = CreateButton("Save as", 642, 65, 86, 25);
            browseOutput.Click += delegate { BrowseSaveIso(_buildOutputText); };
            group.Controls.Add(browseOutput);

            AddLabel(group, "Volume label", 18, 108);
            _volumeNameText = CreateTextBox(130, 104, 200);
            _volumeNameText.Text = "OBS_DISC";
            group.Controls.Add(_volumeNameText);

            Label hint = new Label();
            hint.Text = "Uses IMAPI2FS to create ISO/Joliet/UDF images. Keep labels short for older drives and operating systems.";
            hint.Location = new Point(130, 138);
            hint.Size = new Size(560, 36);
            hint.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(hint);

            _buildButton = CreateButton("Build image", 130, 188, 120, 29);
            _buildButton.Click += delegate { BuildImage(); };
            group.Controls.Add(_buildButton);

            return page;
        }

        private TabPage CreateBurnTab()
        {
            TabPage page = CreatePage("Burn Image");
            GroupBox group = CreateGroup("Write image to optical media", 14, 14, 760, 430);
            page.Controls.Add(group);

            AddLabel(group, "Image file", 18, 32);
            _burnImageText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_burnImageText);
            Button browseImage = CreateButton("Browse", 642, 27, 86, 25);
            browseImage.Click += delegate { BrowseOpenImage(_burnImageText); };
            group.Controls.Add(browseImage);

            AddLabel(group, "Target drive", 18, 70);
            _burnDriveCombo = new ComboBox();
            _burnDriveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _burnDriveCombo.Location = new Point(130, 66);
            _burnDriveCombo.Size = new Size(500, 22);
            group.Controls.Add(_burnDriveCombo);

            _ejectAfterBurnCheck = new CheckBox();
            _ejectAfterBurnCheck.Text = "Eject media after burn";
            _ejectAfterBurnCheck.Checked = true;
            _ejectAfterBurnCheck.Location = new Point(130, 104);
            _ejectAfterBurnCheck.Size = new Size(190, 22);
            group.Controls.Add(_ejectAfterBurnCheck);

            _verifyAfterBurnCheck = new CheckBox();
            _verifyAfterBurnCheck.Text = "Calculate SHA-256 after burn";
            _verifyAfterBurnCheck.Location = new Point(130, 132);
            _verifyAfterBurnCheck.Size = new Size(220, 22);
            group.Controls.Add(_verifyAfterBurnCheck);

            Label note = new Label();
            note.Text = "Burning uses the bundled cdrecord backend. If cdrecord is unavailable, LuxBurn opens Windows Disc Image Burner.";
            note.Location = new Point(130, 164);
            note.Size = new Size(560, 36);
            note.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(note);

            AddLabel(group, "Progress", 18, 220);
            _writeProgress = CreateProgressBar(130, 218, 500);
            group.Controls.Add(_writeProgress);

            AddLabel(group, "Buffer", 18, 256);
            _bufferProgress = CreateProgressBar(130, 254, 500);
            group.Controls.Add(_bufferProgress);

            AddLabel(group, "Device buffer", 18, 292);
            _deviceBufferProgress = CreateProgressBar(130, 290, 500);
            group.Controls.Add(_deviceBufferProgress);

            _burnButton = CreateButton("Burn image", 130, 344, 120, 29);
            _burnButton.Click += delegate { BurnImage(); };
            group.Controls.Add(_burnButton);

            _abortButton = CreateButton("Abort", 262, 344, 90, 29);
            _abortButton.Enabled = false;
            _abortButton.Click += delegate { AbortBurn(); };
            group.Controls.Add(_abortButton);

            return page;
        }

        private TabPage CreateEraseTab()
        {
            TabPage page = CreatePage("Erase");
            GroupBox group = CreateGroup("Erase rewritable optical media", 14, 14, 760, 220);
            page.Controls.Add(group);

            AddLabel(group, "Target drive", 18, 32);
            _eraseDriveCombo = new ComboBox();
            _eraseDriveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _eraseDriveCombo.Location = new Point(130, 28);
            _eraseDriveCombo.Size = new Size(500, 22);
            group.Controls.Add(_eraseDriveCombo);

            _fullEraseCheck = new CheckBox();
            _fullEraseCheck.Text = "Full erase";
            _fullEraseCheck.Location = new Point(130, 66);
            _fullEraseCheck.Size = new Size(150, 22);
            group.Controls.Add(_fullEraseCheck);

            Label note = new Label();
            note.Text = "Erase works on rewritable discs such as CD-RW and DVD-RW. CD-R cannot be erased.";
            note.Location = new Point(130, 100);
            note.Size = new Size(560, 36);
            note.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(note);

            _eraseButton = CreateButton("Erase disc", 130, 154, 120, 29);
            _eraseButton.Click += delegate { EraseDisc(); };
            group.Controls.Add(_eraseButton);

            return page;
        }

        private TabPage CreateCopyTab()
        {
            TabPage page = CreatePage("Copy Disc");
            GroupBox group = CreateGroup("Copy optical media to image", 14, 14, 760, 310);
            page.Controls.Add(group);

            AddLabel(group, "Source drive", 18, 32);
            _copyDriveCombo = new ComboBox();
            _copyDriveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _copyDriveCombo.Location = new Point(130, 28);
            _copyDriveCombo.Size = new Size(500, 22);
            group.Controls.Add(_copyDriveCombo);

            Button refresh = CreateButton("Refresh", 642, 27, 86, 25);
            refresh.Click += delegate { RefreshDrives(); };
            group.Controls.Add(refresh);

            AddLabel(group, "Output image", 18, 70);
            _copyOutputText = CreateTextBox(130, 66, 500);
            group.Controls.Add(_copyOutputText);
            Button browseOutput = CreateButton("Save as", 642, 65, 86, 25);
            browseOutput.Click += delegate { BrowseSaveIso(_copyOutputText); };
            group.Controls.Add(browseOutput);

            _verifyAfterCopyCheck = new CheckBox();
            _verifyAfterCopyCheck.Text = "Calculate SHA-256 after copy";
            _verifyAfterCopyCheck.Checked = true;
            _verifyAfterCopyCheck.Location = new Point(130, 104);
            _verifyAfterCopyCheck.Size = new Size(220, 22);
            group.Controls.Add(_verifyAfterCopyCheck);

            Label note = new Label();
            note.Text = "Copies standard data discs to ISO-style images using the bundled readcd backend. Audio CDs and special multi-session layouts are planned for a later release.";
            note.Location = new Point(130, 136);
            note.Size = new Size(560, 48);
            note.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(note);

            AddLabel(group, "Progress", 18, 202);
            _copyProgress = CreateProgressBar(130, 200, 500);
            group.Controls.Add(_copyProgress);

            _copyButton = CreateButton("Copy disc", 130, 250, 120, 29);
            _copyButton.Click += delegate { CopyDisc(); };
            group.Controls.Add(_copyButton);

            _copyAbortButton = CreateButton("Abort", 262, 250, 90, 29);
            _copyAbortButton.Enabled = false;
            _copyAbortButton.Click += delegate { AbortBurn(); };
            group.Controls.Add(_copyAbortButton);

            return page;
        }

        private TabPage CreateVerifyTab()
        {
            TabPage page = CreatePage("Verify");
            GroupBox group = CreateGroup("Checksum verification", 14, 14, 760, 280);
            page.Controls.Add(group);

            AddLabel(group, "File", 18, 32);
            _verifyFileText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_verifyFileText);
            Button browseFile = CreateButton("Browse", 642, 27, 86, 25);
            browseFile.Click += delegate { BrowseOpenImage(_verifyFileText); };
            group.Controls.Add(browseFile);

            AddLabel(group, "Algorithm", 18, 70);
            _algorithmCombo = new ComboBox();
            _algorithmCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _algorithmCombo.Items.AddRange(new object[] { "SHA256", "SHA1", "SHA512", "MD5" });
            _algorithmCombo.SelectedIndex = 0;
            _algorithmCombo.Location = new Point(130, 66);
            _algorithmCombo.Size = new Size(140, 22);
            group.Controls.Add(_algorithmCombo);

            AddLabel(group, "Expected", 18, 108);
            _expectedHashText = CreateTextBox(130, 104, 598);
            group.Controls.Add(_expectedHashText);

            AddLabel(group, "Actual", 18, 146);
            _actualHashText = CreateTextBox(130, 142, 598);
            _actualHashText.ReadOnly = true;
            group.Controls.Add(_actualHashText);

            _verifyButton = CreateButton("Calculate", 130, 190, 120, 29);
            _verifyButton.Click += delegate { VerifyFile(); };
            group.Controls.Add(_verifyButton);

            return page;
        }

        private TabPage CreateWizardTab()
        {
            TabPage page = CreatePage("Wizards");

            Panel surface = new Panel();
            surface.Dock = DockStyle.Fill;
            surface.AutoScroll = true;
            surface.BackColor = page.BackColor;
            page.Controls.Add(surface);

            GroupBox group = CreateGroup("Quick Start Wizards", 14, 14, 910, 500);
            surface.Controls.Add(group);

            Label intro = new Label();
            intro.Text = "Choose a task. LuxBurn will open the matching workspace and preselect sensible settings.";
            intro.Location = new Point(18, 24);
            intro.Size = new Size(850, 34);
            intro.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(intro);

            CreateWizardCard(
                group,
                18,
                66,
                "Data Disc Wizard",
                "Build a standard data image from a folder, then burn it or save it for later.",
                "Build image",
                delegate { StartBuildWizard("DATA_DISC"); },
                "Burn image",
                delegate { StartBurnWizard("Data Disc Wizard"); });

            CreateWizardCard(
                group,
                462,
                66,
                "Audio && Music Wizard",
                "Copy music files as a data disc. Red Book audio CD authoring is planned for a later release.",
                "Music data disc",
                delegate { StartBuildWizard("MUSIC_DISC"); },
                "Audio CD",
                delegate { ShowPlannedWizardNotice("Audio CD authoring and audio ripping are planned for a later release."); });

            CreateWizardCard(
                group,
                18,
                196,
                "Video Disc Wizard",
                "Build an image from a prepared VIDEO_TS, BDMV, or BDAV folder, or burn an existing image.",
                "Video folder",
                delegate { StartBuildWizard("VIDEO_DISC"); },
                "Burn image",
                delegate { StartBurnWizard("Video Disc Wizard"); });

            CreateWizardCard(
                group,
                462,
                196,
                "Game Disc Wizard",
                "Burn or verify an existing game image. Console-specific patching is planned for a later release.",
                "Burn game image",
                delegate { StartBurnWizard("Game Disc Wizard"); },
                "Verify image",
                delegate { StartVerifyWizard("Game Disc Wizard"); });

            CreateWizardCard(
                group,
                18,
                326,
                "Copy Disc Wizard",
                "Copy a standard data disc to an ISO-style image with optional checksum verification.",
                "Copy disc",
                delegate { StartCopyWizard(); },
                "Refresh drives",
                delegate { RefreshDrives(); _tabs.SelectedIndex = 3; });

            CreateWizardCard(
                group,
                462,
                326,
                "Blank / Erase Wizard",
                "Erase rewritable media before burning. CD-R and DVD-R media cannot be erased.",
                "Erase disc",
                delegate { _tabs.SelectedIndex = 4; SetStatus("Blank / Erase Wizard opened."); },
                "Refresh drives",
                delegate { RefreshDrives(); _tabs.SelectedIndex = 4; });

            return page;
        }

        private TabPage CreateLogTab()
        {
            TabPage page = CreatePage("Log");
            _logText = new TextBox();
            _logText.Multiline = true;
            _logText.ReadOnly = true;
            _logText.ScrollBars = ScrollBars.Vertical;
            _logText.Dock = DockStyle.Fill;
            _logText.BackColor = Color.White;
            _logText.Font = new Font("Consolas", 8.25f);
            page.Controls.Add(_logText);
            return page;
        }

        private TabPage CreatePage(string text)
        {
            TabPage page = new TabPage(text);
            page.BackColor = Color.FromArgb(240, 240, 236);
            page.Padding = new Padding(10);
            return page;
        }

        private GroupBox CreateGroup(string text, int x, int y, int width, int height)
        {
            GroupBox group = new GroupBox();
            group.Text = text;
            group.Location = new Point(x, y);
            group.Size = new Size(width, height);
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            group.BackColor = Color.FromArgb(248, 248, 245);
            return group;
        }

        private Button CreateButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.System;
            return button;
        }

        private NeutralProgressBar CreateProgressBar(int x, int y, int width)
        {
            NeutralProgressBar bar = new NeutralProgressBar();
            bar.Location = new Point(x, y);
            bar.Size = new Size(width, 22);
            return bar;
        }

        private Control CreateSideButton(int top, string caption, string iconName)
        {
            OperationButton button = new OperationButton(
                caption,
                LoadButtonAsset("ButtonDark.png"),
                LoadButtonAsset("ButtonLit.png"),
                LoadButtonAsset(iconName));
            button.Location = new Point(12, top);
            button.BackColor = Color.FromArgb(229, 229, 224);
            return button;
        }

        private TextBox CreateTextBox(int x, int y, int width)
        {
            TextBox textBox = new TextBox();
            textBox.Location = new Point(x, y);
            textBox.Size = new Size(width, 22);
            return textBox;
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(100, 18);
            parent.Controls.Add(label);
        }

        private void CreateWizardCard(Control parent, int x, int y, string title, string body, string primaryText, EventHandler primaryClick, string secondaryText, EventHandler secondaryClick)
        {
            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(420, 112);
            card.BackColor = Color.FromArgb(242, 242, 238);
            card.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(card);

            Label heading = new Label();
            heading.Text = title;
            heading.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            heading.Location = new Point(12, 10);
            heading.Size = new Size(392, 18);
            card.Controls.Add(heading);

            Label description = new Label();
            description.Text = body;
            description.Location = new Point(12, 32);
            description.Size = new Size(392, 34);
            description.ForeColor = Color.FromArgb(72, 72, 72);
            card.Controls.Add(description);

            Button primary = CreateButton(primaryText, 12, 74, 112, 25);
            primary.Click += primaryClick;
            card.Controls.Add(primary);

            Button secondary = CreateButton(secondaryText, 132, 74, 112, 25);
            secondary.Click += secondaryClick;
            card.Controls.Add(secondary);
        }

        private void RefreshComponentStatus()
        {
            string imapi = _burningService.IsImapi2Available ? "IMAPI2: ready" : "IMAPI2: not installed";
            string fs = _burningService.IsImapi2FileSystemAvailable ? "IMAPI2FS: ready" : "IMAPI2FS: not installed";
            string cdrecord = _burningService.IsCdrecordAvailable ? "cdrecord: ready" : "cdrecord: not found";
            string readcd = _burningService.IsReadcdAvailable ? "readcd: ready" : "readcd: not found";
            _componentStatusLabel.Text = cdrecord + "    " + readcd + Environment.NewLine + imapi + "    " + fs;
        }

        private void StartBuildWizard(string preset)
        {
            _tabs.SelectedIndex = 1;

            if (string.Equals(preset, "MUSIC_DISC", StringComparison.OrdinalIgnoreCase))
                _volumeNameText.Text = "MUSIC_DISC";
            else if (string.Equals(preset, "VIDEO_DISC", StringComparison.OrdinalIgnoreCase))
                _volumeNameText.Text = "VIDEO_DISC";
            else
                _volumeNameText.Text = "DATA_DISC";

            SetStatus("Build wizard opened.");
            Log("Wizard selected: " + preset + ".");

            if (_buildSourceText.Text.Trim().Length == 0)
                BrowseFolder(_buildSourceText);

            if (_buildOutputText.Text.Trim().Length == 0)
                BrowseSaveIso(_buildOutputText);
        }

        private void StartBurnWizard(string sourceWizard)
        {
            _tabs.SelectedIndex = 2;
            SetStatus(sourceWizard + " opened.");
            Log("Wizard selected: " + sourceWizard + " burn image.");

            if (_burnImageText.Text.Trim().Length == 0)
                BrowseOpenImage(_burnImageText);
        }

        private void StartVerifyWizard(string sourceWizard)
        {
            _tabs.SelectedIndex = 5;
            SetStatus(sourceWizard + " verification opened.");
            Log("Wizard selected: " + sourceWizard + " verify image.");

            if (_verifyFileText.Text.Trim().Length == 0)
                BrowseOpenImage(_verifyFileText);
        }

        private void StartCopyWizard()
        {
            _tabs.SelectedIndex = 3;
            SetStatus("Copy Disc Wizard opened.");
            Log("Wizard selected: Copy Disc.");

            if (_copyOutputText.Text.Trim().Length == 0)
                BrowseSaveIso(_copyOutputText);
        }

        private void ShowPlannedWizardNotice(string message)
        {
            SetStatus("Wizard feature is planned for a later release.");
            Log(message);
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static Image LoadButtonAsset(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Buttons", fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Buttons", fileName);

            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private static Image LoadBrandAsset(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Brand", fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Brand", fileName);

            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private sealed class OperationButton : Control
        {
            private readonly Image _normalImage;
            private readonly Image _hoverImage;
            private bool _isHovering;

            public OperationButton(string caption, Image normalBackground, Image hoverBackground, Image icon)
            {
                Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
                _normalImage = ComposeButton(normalBackground, icon, caption, Font);
                _hoverImage = ComposeButton(hoverBackground ?? normalBackground, icon, caption, Font);

                Size = _normalImage == null
                    ? new Size(209, 32)
                    : _normalImage.Size;

                TabStop = false;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _isHovering = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _isHovering = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                Image image = _isHovering && _hoverImage != null ? _hoverImage : _normalImage;
                if (image != null)
                    e.Graphics.DrawImageUnscaled(image, 0, 0);
            }

            private static Image ComposeButton(Image background, Image icon, string caption, Font font)
            {
                int width = background == null ? 209 : background.Width / 2;
                int height = background == null ? 32 : background.Height / 2;
                Bitmap composed = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                using (Graphics graphics = Graphics.FromImage(composed))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                    if (background != null)
                        graphics.DrawImage(background, new Rectangle(0, 0, width, height));

                    if (icon != null)
                    {
                        Rectangle iconRect = FitIcon(icon, new Rectangle(5, 4, 24, 24));
                        graphics.DrawImage(icon, iconRect);
                    }

                    TextRenderer.DrawText(
                        graphics,
                        caption,
                        font,
                        new Rectangle(46, 0, width - 50, height),
                        Color.White,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }

                return composed;
            }

            private static Rectangle FitIcon(Image image, Rectangle bounds)
            {
                double scale = Math.Min(bounds.Width / (double)image.Width, bounds.Height / (double)image.Height);
                int width = Math.Max(1, (int)Math.Round(image.Width * scale));
                int height = Math.Max(1, (int)Math.Round(image.Height * scale));
                int x = bounds.Left + (bounds.Width - width) / 2;
                int y = bounds.Top + (bounds.Height - height) / 2;
                return new Rectangle(x, y, width, height);
            }
        }

        private sealed class NeutralProgressBar : Control
        {
            private int _value;

            public NeutralProgressBar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Font = new Font("Tahoma", 8.25f);
                BackColor = Color.FromArgb(238, 238, 234);
                ForeColor = Color.FromArgb(42, 48, 52);
            }

            public int Value
            {
                get { return _value; }
                set
                {
                    int clamped = Math.Max(0, Math.Min(100, value));
                    if (_value == clamped)
                        return;

                    _value = clamped;
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                e.Graphics.Clear(BackColor);
                using (Brush fill = new SolidBrush(Color.FromArgb(92, 98, 98)))
                {
                    int fillWidth = (int)Math.Round((Width - 2) * (_value / 100.0));
                    if (fillWidth > 0)
                        e.Graphics.FillRectangle(fill, 1, 1, fillWidth, Height - 2);
                }

                ControlPaint.DrawBorder(e.Graphics, bounds, Color.FromArgb(150, 150, 144), ButtonBorderStyle.Solid);
                TextRenderer.DrawText(
                    e.Graphics,
                    _value.ToString() + "%",
                    Font,
                    bounds,
                    Color.FromArgb(24, 24, 24),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void RefreshDrives()
        {
            try
            {
                IList<DiscRecorderInfo> recorders = _burningService.GetRecorders();
                _driveCombo.Items.Clear();
                _burnDriveCombo.Items.Clear();
                _copyDriveCombo.Items.Clear();
                _eraseDriveCombo.Items.Clear();
                _driveList.Items.Clear();

                foreach (DiscRecorderInfo recorder in recorders)
                {
                    _driveCombo.Items.Add(recorder);
                    _burnDriveCombo.Items.Add(recorder);
                    _copyDriveCombo.Items.Add(recorder);
                    _eraseDriveCombo.Items.Add(recorder);

                    ListViewItem item = new ListViewItem(recorder.DisplayName);
                    item.SubItems.Add(recorder.VendorId);
                    item.SubItems.Add(recorder.ProductId);
                    item.SubItems.Add(recorder.Id);
                    _driveList.Items.Add(item);
                }

                if (_driveCombo.Items.Count > 0)
                    _driveCombo.SelectedIndex = 0;
                if (_burnDriveCombo.Items.Count > 0)
                    _burnDriveCombo.SelectedIndex = 0;
                if (_copyDriveCombo.Items.Count > 0)
                    _copyDriveCombo.SelectedIndex = 0;
                if (_eraseDriveCombo.Items.Count > 0)
                    _eraseDriveCombo.SelectedIndex = 0;

                SetStatus(recorders.Count == 0 ? "No optical recorders were found." : "Detected " + recorders.Count + " optical recorder(s).");
                Log(recorders.Count == 0 ? "No optical recorders found." : "Drive list refreshed.");
            }
            catch (Exception ex)
            {
                SetStatus("Drive refresh failed.");
                Log("Drive refresh failed: " + ex.Message);
            }
        }

        private void BuildImage()
        {
            string source = _buildSourceText.Text.Trim();
            string output = _buildOutputText.Text.Trim();
            string volume = _volumeNameText.Text.Trim();

            if (source.Length == 0 || output.Length == 0)
            {
                MessageBox.Show(this, "Choose a source folder and output image path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunWork("Building image...", delegate
            {
                Log("Building image from " + source);
                _burningService.BuildIso(source, output, volume);
            },
            delegate
            {
                Log("Image built: " + output);
                _burnImageText.Text = output;
                _verifyFileText.Text = output;
            });
        }

        private void BurnImage()
        {
            string image = _burnImageText.Text.Trim();
            DiscRecorderInfo recorder = _burnDriveCombo.SelectedItem as DiscRecorderInfo;
            string recorderId = recorder == null ? string.Empty : recorder.Id;
            string burnMethod = "Auto";
            bool eject = _ejectAfterBurnCheck.Checked;
            bool verifyAfter = _verifyAfterBurnCheck.Checked;

            if (image.Length == 0)
            {
                MessageBox.Show(this, "Choose an image file first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ResetBurnProgress();
            _burnCancellation = new CancellationTokenSource();
            _burnInProgress = true;
            SetBusy(true, "Starting burner...");

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                bool usingExternalBurner = _burningService.WillUseWindowsDiscImageBurner(burnMethod);
                Log("Burning image: " + image);
                Log("Burn backend: " + (usingExternalBurner ? "Windows Disc Image Burner" : "cdrecord"));
                _burningService.BurnImage(image, recorderId, eject, burnMethod, Log, UpdateBurnProgress, _burnCancellation.Token);

                if (!usingExternalBurner && verifyAfter)
                {
                    string hash = ChecksumService.ComputeFileHash(image, "SHA256");
                    Log("Post-burn SHA-256: " + hash);
                }

                e.Result = usingExternalBurner;
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                _burnInProgress = false;
                _burnCancellation = null;
                SetBusy(false, "Ready");

                if (e.Error != null)
                {
                    if (e.Error is OperationCanceledException)
                    {
                        Log("Burn cancelled.");
                        SetStatus("Burn cancelled.");
                        return;
                    }

                    Log("Operation failed: " + e.Error.GetType().Name + ": " + e.Error.Message);
                    if (e.Error.InnerException != null)
                        Log("Original error: " + e.Error.InnerException.GetType().Name + ": " + e.Error.InnerException.Message);

                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                bool usingExternalBurner = Convert.ToBoolean(e.Result);
                Log(usingExternalBurner ? "Burn window opened." : "Burn completed.");
                if (!usingExternalBurner)
                    PlaySuccessSound();
            };
            worker.RunWorkerAsync();
        }

        private void CopyDisc()
        {
            DiscRecorderInfo recorder = _copyDriveCombo.SelectedItem as DiscRecorderInfo;
            string recorderId = recorder == null ? string.Empty : recorder.Id;
            string output = _copyOutputText.Text.Trim();
            bool verifyAfter = _verifyAfterCopyCheck.Checked;

            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a source drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (output.Length == 0)
            {
                MessageBox.Show(this, "Choose an output image path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (File.Exists(output) &&
                MessageBox.Show(this, "Replace the existing output image?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            ResetBurnProgress();
            _burnCancellation = new CancellationTokenSource();
            _burnInProgress = true;
            SetBusy(true, "Starting copy...");

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                Log("Copying disc from " + recorder.DisplayName + " to " + output);
                _burningService.CopyDiscToImage(recorderId, output, Log, UpdateBurnProgress, _burnCancellation.Token);

                if (verifyAfter)
                {
                    string hash = ChecksumService.ComputeFileHash(output, "SHA256");
                    Log("Copy SHA-256: " + hash);
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                _burnInProgress = false;
                _burnCancellation = null;
                SetBusy(false, "Ready");

                if (e.Error != null)
                {
                    if (e.Error is OperationCanceledException)
                    {
                        Log("Copy cancelled.");
                        SetStatus("Copy cancelled.");
                        return;
                    }

                    Log("Operation failed: " + e.Error.GetType().Name + ": " + e.Error.Message);
                    if (e.Error.InnerException != null)
                        Log("Original error: " + e.Error.InnerException.GetType().Name + ": " + e.Error.InnerException.Message);

                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Log("Copy completed.");
                _burnImageText.Text = output;
                _verifyFileText.Text = output;
                PlaySuccessSound();
            };
            worker.RunWorkerAsync();
        }

        private void EraseDisc()
        {
            DiscRecorderInfo recorder = _eraseDriveCombo.SelectedItem as DiscRecorderInfo;
            string recorderId = recorder == null ? string.Empty : recorder.Id;
            bool fullErase = _fullEraseCheck.Checked;

            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a target drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, "Erase the rewritable disc in the selected drive?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ResetBurnProgress();
            _burnCancellation = new CancellationTokenSource();
            _burnInProgress = true;
            SetBusy(true, "Erasing disc...");

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                Log("Erasing disc in " + recorder.DisplayName);
                _burningService.EraseDisc(recorderId, fullErase, Log, UpdateBurnProgress, _burnCancellation.Token);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                _burnInProgress = false;
                _burnCancellation = null;
                SetBusy(false, "Ready");

                if (e.Error != null)
                {
                    if (e.Error is OperationCanceledException)
                    {
                        Log("Erase cancelled.");
                        SetStatus("Erase cancelled.");
                        return;
                    }

                    Log("Operation failed: " + e.Error.GetType().Name + ": " + e.Error.Message);
                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Log("Erase completed.");
            };
            worker.RunWorkerAsync();
        }

        private void VerifyFile()
        {
            string file = _verifyFileText.Text.Trim();
            string algorithm = Convert.ToString(_algorithmCombo.SelectedItem);
            string expected = NormalizeHash(_expectedHashText.Text);

            if (file.Length == 0)
            {
                MessageBox.Show(this, "Choose a file first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunWork("Calculating checksum...", delegate
            {
                Log("Calculating " + algorithm + " for " + file);
                return ChecksumService.ComputeFileHash(file, algorithm);
            },
            delegate(object result)
            {
                string actual = Convert.ToString(result);
                _actualHashText.Text = actual;

                if (expected.Length > 0)
                    Log(string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ? "Checksum matches." : "Checksum does not match.");
                else
                    Log("Checksum calculated: " + actual);
            });
        }

        private void RunWork(string busyText, Action work, Action completed)
        {
            RunWork(busyText, delegate { work(); return null; }, delegate(object result) { if (completed != null) completed(); });
        }

        private void RunWork(string busyText, Func<object> work, Action<object> completed)
        {
            SetBusy(true, busyText);

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = work();
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                SetBusy(false, "Ready");

                if (e.Error != null)
                {
                    Log("Operation failed: " + e.Error.GetType().Name + ": " + e.Error.Message);
                    if (e.Error.InnerException != null)
                        Log("Original error: " + e.Error.InnerException.GetType().Name + ": " + e.Error.InnerException.Message);

                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (completed != null)
                    completed(e.Result);

                SetStatus("Ready");
            };
            worker.RunWorkerAsync();
        }

        private void SetBusy(bool busy, string status)
        {
            _refreshButton.Enabled = !busy;
            _buildButton.Enabled = !busy;
            _burnButton.Enabled = !busy;
            _copyButton.Enabled = !busy;
            _eraseButton.Enabled = !busy;
            _verifyButton.Enabled = !busy;
            _abortButton.Enabled = busy && _burnInProgress;
            _copyAbortButton.Enabled = busy && _burnInProgress;
            SetStatus(status);
        }

        private void AbortBurn()
        {
            if (!_burnInProgress || _burnCancellation == null)
                return;

            if (MessageBox.Show(this, "Cancel the current disc operation?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Log("Cancellation requested.");
            SetStatus("Cancelling...");
            _burnCancellation.Cancel();
            _abortButton.Enabled = false;
            _copyAbortButton.Enabled = false;
        }

        private void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_burnInProgress)
                return;

            e.Cancel = true;
            if (MessageBox.Show(this, "A disc operation is in progress. Do you want to cancel it?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                AbortBurnWithoutPrompt();
        }

        private void AbortBurnWithoutPrompt()
        {
            if (_burnCancellation == null)
                return;

            Log("Cancellation requested.");
            SetStatus("Cancelling...");
            _burnCancellation.Cancel();
            _abortButton.Enabled = false;
            _copyAbortButton.Enabled = false;
        }

        private void ResetBurnProgress()
        {
            _writeProgress.Value = 0;
            _bufferProgress.Value = 0;
            _deviceBufferProgress.Value = 0;
            _copyProgress.Value = 0;
        }

        private void UpdateBurnProgress(BurnProgress progress)
        {
            if (progress == null)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<BurnProgress>(UpdateBurnProgress), progress);
                return;
            }

            if (progress.ProgressPercent >= 0)
            {
                _writeProgress.Value = progress.ProgressPercent;
                _copyProgress.Value = progress.ProgressPercent;
            }
            if (progress.BufferPercent >= 0)
                _bufferProgress.Value = progress.BufferPercent;
            if (progress.DeviceBufferPercent >= 0)
                _deviceBufferProgress.Value = progress.DeviceBufferPercent;
            if (!string.IsNullOrEmpty(progress.Status))
                SetStatus(progress.Status);
        }

        private void SetStatus(string text)
        {
            ToolStripStatusLabel status = _statusLabel.Tag as ToolStripStatusLabel;
            if (status != null)
                status.Text = text;
        }

        private void Log(string message)
        {
            if (_logText == null)
                return;

            if (_logText.InvokeRequired)
            {
                _logText.BeginInvoke(new Action<string>(Log), message);
                return;
            }

            _logText.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        }

        private void PlaySuccessSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "jingle-of-success.mp3");
                if (!File.Exists(soundPath))
                    return;

                string alias = "LuxBurnSuccess";
                MciSendString("close " + alias, null, 0, IntPtr.Zero);
                MciSendString("open \"" + soundPath + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
                MciSendString("play " + alias, null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log("Success sound failed: " + ex.Message);
            }
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int MciSendString(string command, string returnValue, int returnLength, IntPtr callback);

        private void BrowseFolder(TextBox target)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder to place in the image";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.SelectedPath;
            }
        }

        private void BrowseSaveIso(TextBox target)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "ISO image (*.iso)|*.iso|All files (*.*)|*.*";
                dialog.DefaultExt = "iso";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }

        private void BrowseOpenImage(TextBox target)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Disc images and files (*.iso;*.img;*.bin;*.cue;*.nrg;*.mdf)|*.iso;*.img;*.bin;*.cue;*.nrg;*.mdf|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }
    }
}
