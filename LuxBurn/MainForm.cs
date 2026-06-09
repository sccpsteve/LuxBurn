// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using LuxBurn.Services;

namespace LuxBurn
{
    internal sealed class MainForm : Form
    {
        private const int OperationRailWidth = 252;
        private const string UpdateManifestUrl = "https://github.com/sccpsteve/LuxBurn/releases/download/latest/LuxBurn-update.json";

        private readonly LegacyBurningService _burningService = new LegacyBurningService();

        private ComboBox _driveCombo;
        private ComboBox _burnDriveCombo;
        private ListView _driveList;
        private TextBox _buildSourceText;
        private TextBox _buildOutputText;
        private TextBox _volumeNameText;
        private ListView _buildItemList;
        private Label _buildFileCountLabel;
        private Label _buildFolderCountLabel;
        private Label _buildTotalSizeLabel;
        private Label _buildImageSizeLabel;
        private ComboBox _buildDataTypeCombo;
        private ComboBox _buildFileSystemCombo;
        private ComboBox _buildUdfRevisionCombo;
        private CheckBox _preservePathsCheck;
        private CheckBox _recurseSubdirectoriesCheck;
        private CheckBox _includeHiddenFilesCheck;
        private CheckBox _includeSystemFilesCheck;
        private CheckBox _includeArchiveOnlyCheck;
        private CheckBox _clearArchiveAttributeCheck;
        private TextBox _iso9660LabelText;
        private TextBox _jolietLabelText;
        private TextBox _udfLabelText;
        private CheckBox _syncLabelsCheck;
        private TextBox _systemIdentifierText;
        private TextBox _volumeSetText;
        private TextBox _publisherText;
        private TextBox _dataPreparerText;
        private TextBox _applicationIdentifierText;
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
        private SplitContainer _mainSplit;
        private TabControl _tabs;
        private string _projectPath;
        private CancellationTokenSource _burnCancellation;
        private bool _burnInProgress;
        private bool _redrawSuspended;

        public MainForm()
        {
            Text = "LuxBurn";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 700);
            Size = new Size(1280, 760);
            Font = new Font("Tahoma", 8.25f);
            BackColor = Color.FromArgb(240, 240, 236);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Icon windowIcon = LoadWindowIcon();
            if (windowIcon != null)
                Icon = windowIcon;

            BuildInterface();
            RefreshComponentStatus();
            RefreshDrives();
            FormClosing += MainFormFormClosing;
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { CheckForUpdates(false); })); };
        }

        private void BuildInterface()
        {
            MenuStrip menu = CreateMainMenu();
            MainMenuStrip = menu;
            Controls.Add(menu);

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

            _mainSplit = new SplitContainer();
            _mainSplit.Dock = DockStyle.Fill;
            _mainSplit.FixedPanel = FixedPanel.Panel1;
            _mainSplit.Panel1MinSize = OperationRailWidth;
            _mainSplit.SplitterWidth = 4;
            _mainSplit.SplitterDistance = OperationRailWidth;
            Controls.Add(_mainSplit);
            _mainSplit.BringToFront();
            Load += delegate { _mainSplit.SplitterDistance = OperationRailWidth; };

            BuildLeftPanel(_mainSplit.Panel1);
            BuildTabs(_mainSplit.Panel2);
            _tabs.SelectedIndex = 6;
            UpdateSidebarVisibility();
        }

        private MenuStrip CreateMainMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;

            ToolStripMenuItem file = new ToolStripMenuItem("File");
            file.DropDownItems.Add("Browse for a source file...", null, delegate { BrowseBuildSourceFile(); });
            file.DropDownItems.Add("Browse for a source folder...", null, delegate { BrowseBuildSourceFolder(); });
            file.DropDownItems.Add("Remove all items", null, delegate { ClearBuildItems(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Calculate", null, delegate { CalculateBuildImageInformation(); });
            file.DropDownItems.Add("Write", null, delegate { _tabs.SelectedIndex = 2; BurnImage(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("New Project", null, delegate { NewProject(); });
            file.DropDownItems.Add("Load Most Recent Project", null, delegate { LoadMostRecentProject(); });
            file.DropDownItems.Add("Load Project...", null, delegate { LoadProject(); });
            file.DropDownItems.Add("Save Project...", null, delegate { SaveProject(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Export Graph Data...", null, delegate { ExportGraphData(); });
            file.DropDownItems.Add("Display Graph Data...", null, delegate { DisplayGraphData(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Exit", null, delegate { Close(); });
            menu.Items.Add(file);

            ToolStripMenuItem tools = new ToolStripMenuItem("Tools");
            ToolStripMenuItem iso = new ToolStripMenuItem("ISO");
            iso.DropDownItems.Add("Change Volume Label...", null, delegate { _tabs.SelectedIndex = 1; FocusVolumeLabels(); });
            tools.DropDownItems.Add(iso);

            ToolStripMenuItem drive = new ToolStripMenuItem("Drive");
            drive.DropDownItems.Add("Refresh", null, delegate { RefreshDrives(); });
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("ReZero", null, delegate { RunSelectedDriveCommand("-reset", "ReZero"); });
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("Load", null, delegate { RunSelectedDriveCommand("-load", "Load tray"); });
            drive.DropDownItems.Add("Eject", null, delegate { EjectSelectedDrive(); });
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("Lock Tray", null, delegate { RunSelectedDriveCommand("-lock", "Lock tray"); });
            ToolStripMenuItem eraseDisc = new ToolStripMenuItem("Erase Disc");
            eraseDisc.DropDownItems.Add("Quick", null, delegate { _fullEraseCheck.Checked = false; _tabs.SelectedIndex = 4; EraseDisc(); });
            eraseDisc.DropDownItems.Add("Full", null, delegate { _fullEraseCheck.Checked = true; _tabs.SelectedIndex = 4; EraseDisc(); });
            drive.DropDownItems.Add(eraseDisc);
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("Synchronise Cache", null, delegate { RunSelectedDriveCommand("-fix", "Synchronise cache / fixate"); });
            ToolStripMenuItem close = new ToolStripMenuItem("Close");
            close.DropDownItems.Add("Session", null, delegate { RunSelectedDriveCommand("-fix", "Close session"); });
            close.DropDownItems.Add("Disc", null, delegate { RunSelectedDriveCommand("-fix", "Close disc"); });
            drive.DropDownItems.Add(close);
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("Change Advanced Settings...", null, delegate { ShowSettingsDialog(); });
            drive.DropDownItems.Add("Capabilities", null, delegate { ShowDeviceCapabilities(); });
            drive.DropDownItems.Add("Family Tree", null, delegate { ShowFamilyTree(); });
            drive.DropDownItems.Add(new ToolStripSeparator());
            drive.DropDownItems.Add("Check For Firmware Updates...", null, delegate { CheckFirmwareUpdate(); });
            tools.DropDownItems.Add(drive);
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add("Create CUE File...", null, delegate { CreateDescriptorFile(".cue"); });
            tools.DropDownItems.Add("Create DVD File...", null, delegate { CreateDescriptorFile(".dvd"); });
            tools.DropDownItems.Add("Create MDS File...", null, delegate { CreateDescriptorFile(".mds"); });
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add("Search for SCSI / ATAPI devices", null, delegate { RefreshDrives(); _tabs.SelectedIndex = 0; });
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add("Settings...", null, delegate { ShowSettingsDialog(); });
            menu.Items.Add(tools);

            ToolStripMenuItem help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add("Check for Updates...", null, delegate { CheckForUpdates(true); });
            menu.Items.Add(help);

            return menu;
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.BackColor = Color.FromArgb(32, 45, 55);
            parent.BackgroundImage = LoadUiAsset("LuxburnSidebar1.png");
            parent.BackgroundImageLayout = ImageLayout.Stretch;
            parent.Padding = new Padding(12);

            Label section = new Label();
            section.Text = "Operations";
            section.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            section.ForeColor = Color.White;
            section.BackColor = Color.Transparent;
            section.AutoSize = true;
            section.Location = new Point(12, 14);
            parent.Controls.Add(section);

            Control ezModeButton = CreateSideButton(42, "EZ Mode", "wand.png");
            ezModeButton.Click += delegate { _tabs.SelectedIndex = 6; };
            parent.Controls.Add(ezModeButton);

            _refreshButton = CreateSideButton(113, "Refresh", "icon_refresh.png");
            _refreshButton.Click += delegate { RefreshDrives(); };
            parent.Controls.Add(_refreshButton);

            Control buildTabButton = CreateSideButton(184, "Build", "145_add.png");
            buildTabButton.Click += delegate { _tabs.SelectedIndex = 1; };
            parent.Controls.Add(buildTabButton);

            Control burnTabButton = CreateSideButton(255, "Write", "139-Edit.png");
            burnTabButton.Click += delegate { _tabs.SelectedIndex = 2; };
            parent.Controls.Add(burnTabButton);

            Control copyTabButton = CreateSideButton(326, "Copy", "icon_build.png");
            copyTabButton.Click += delegate { _tabs.SelectedIndex = 3; };
            parent.Controls.Add(copyTabButton);

            Control eraseTabButton = CreateSideButton(397, "Erase", "18_delete.png");
            eraseTabButton.Click += delegate { _tabs.SelectedIndex = 4; };
            parent.Controls.Add(eraseTabButton);

            Control verifyTabButton = CreateSideButton(468, "Verify", "6-ApplyButton.png");
            verifyTabButton.Click += delegate { _tabs.SelectedIndex = 5; };
            parent.Controls.Add(verifyTabButton);
        }

        private void BuildTabs(Control parent)
        {
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(12, 5);
            _tabs.Selecting += delegate { BeginVisualTransition(); };
            _tabs.SelectedIndexChanged += delegate { UpdateSidebarVisibility(); };
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

        private void UpdateSidebarVisibility()
        {
            if (_mainSplit == null || _tabs == null)
                return;

            bool hideSidebar = _tabs.SelectedIndex == 6;
            if (_mainSplit.Panel1Collapsed == hideSidebar)
            {
                EndVisualTransitionSoon();
                return;
            }

            SuspendLayout();
            _mainSplit.SuspendLayout();
            try
            {
                if (!hideSidebar)
                    _mainSplit.SplitterDistance = OperationRailWidth;

                _mainSplit.Panel1Collapsed = hideSidebar;
            }
            finally
            {
                _mainSplit.ResumeLayout(true);
                ResumeLayout(true);
                EndVisualTransitionSoon();
            }
        }

        private void BeginVisualTransition()
        {
            if (_redrawSuspended || !IsHandleCreated)
                return;

            _redrawSuspended = true;
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndVisualTransitionSoon()
        {
            if (!_redrawSuspended || !IsHandleCreated)
                return;

            BeginInvoke(new MethodInvoker(EndVisualTransition));
        }

        private void EndVisualTransition()
        {
            if (!_redrawSuspended || !IsHandleCreated)
                return;

            SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            _redrawSuspended = false;
            Invalidate(true);
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
            GroupBox group = CreateGroup("Create ISO image from files and folders", 14, 14, 900, 560);
            page.Controls.Add(group);

            AddLabel(group, "Source path", 18, 32);
            _buildSourceText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_buildSourceText);
            Button browseSourceFile = CreateButton("Add file", 642, 27, 86, 25);
            browseSourceFile.Click += delegate { BrowseBuildSourceFile(); };
            group.Controls.Add(browseSourceFile);
            Button browseSourceFolder = CreateButton("Add folder", 734, 27, 86, 25);
            browseSourceFolder.Click += delegate { BrowseBuildSourceFolder(); };
            group.Controls.Add(browseSourceFolder);

            AddLabel(group, "Output image", 18, 70);
            _buildOutputText = CreateTextBox(130, 66, 500);
            group.Controls.Add(_buildOutputText);
            Button browseOutput = CreateButton("Save as", 642, 65, 86, 25);
            browseOutput.Click += delegate { BrowseSaveIso(_buildOutputText); };
            group.Controls.Add(browseOutput);

            AddLabel(group, "Volume label", 18, 108);
            _volumeNameText = CreateTextBox(130, 104, 200);
            _volumeNameText.Text = "LUXBURN_DISC";
            _volumeNameText.TextChanged += delegate { SynchronizeVolumeLabels(_volumeNameText.Text); };
            group.Controls.Add(_volumeNameText);

            Button removeSelected = CreateButton("Remove", 642, 103, 86, 25);
            removeSelected.Click += delegate { RemoveSelectedBuildItems(); };
            group.Controls.Add(removeSelected);
            Button removeAll = CreateButton("Remove all", 734, 103, 86, 25);
            removeAll.Click += delegate { ClearBuildItems(); };
            group.Controls.Add(removeAll);

            _buildItemList = new ListView();
            _buildItemList.Location = new Point(18, 146);
            _buildItemList.Size = new Size(400, 330);
            _buildItemList.View = View.Details;
            _buildItemList.FullRowSelect = true;
            _buildItemList.GridLines = true;
            _buildItemList.AllowDrop = true;
            _buildItemList.Columns.Add("Name", 140);
            _buildItemList.Columns.Add("Type", 70);
            _buildItemList.Columns.Add("Path", 250);
            _buildItemList.DragEnter += BuildItemListDragEnter;
            _buildItemList.DragDrop += BuildItemListDragDrop;
            group.Controls.Add(_buildItemList);

            Label dropHint = new Label();
            dropHint.Text = "Drop files or folders here";
            dropHint.Location = new Point(18, 480);
            dropHint.Size = new Size(400, 20);
            dropHint.TextAlign = ContentAlignment.MiddleCenter;
            dropHint.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(dropHint);

            TabControl imageTabs = new TabControl();
            imageTabs.Location = new Point(436, 146);
            imageTabs.Size = new Size(420, 330);
            group.Controls.Add(imageTabs);

            imageTabs.TabPages.Add(CreateBuildInfoPage());
            imageTabs.TabPages.Add(CreateBuildDevicePage());
            imageTabs.TabPages.Add(CreateBuildOptionsPage());
            imageTabs.TabPages.Add(CreateBuildLabelsPage());
            imageTabs.TabPages.Add(CreateBuildAdvancedPage());

            _buildButton = CreateButton("Build image", 130, 510, 120, 29);
            _buildButton.Click += delegate { BuildImage(); };
            group.Controls.Add(_buildButton);

            Button calculate = CreateButton("Calculate", 262, 510, 100, 29);
            calculate.Click += delegate { CalculateBuildImageInformation(); };
            group.Controls.Add(calculate);

            Button write = CreateButton("Write", 374, 510, 90, 29);
            write.Click += delegate { BuildThenOpenBurn(); };
            group.Controls.Add(write);

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
            note.Text = "Copies standard data discs to ISO-style images using the bundled readcd backend.";
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
            TabPage page = CreatePage("EZ Mode");

            Panel surface = new Panel();
            surface.Dock = DockStyle.Fill;
            surface.AutoScroll = true;
            surface.BackColor = Color.Transparent;
            page.Controls.Add(surface);

            Label heading = new Label();
            heading.Text = "EZ Mode Picker";
            heading.Font = new Font("Tahoma", 18f, FontStyle.Bold);
            heading.ForeColor = Color.White;
            heading.BackColor = Color.Transparent;
            heading.Location = new Point(34, 24);
            heading.Size = new Size(420, 36);
            surface.Controls.Add(heading);

            Label intro = new Label();
            intro.Text = "Choose the job and LuxBurn will open the matching workspace.";
            intro.Location = new Point(38, 64);
            intro.Size = new Size(520, 24);
            intro.ForeColor = Color.FromArgb(220, 235, 242);
            intro.BackColor = Color.Transparent;
            surface.Controls.Add(intro);

            EzModeWheel wheel = new EzModeWheel();
            wheel.Location = new Point(72, 108);
            wheel.AddSlice("Build", "Create image", LoadUiAsset("pie_6_1.png"), LoadUiAsset("pie_6_1_O.png"), new Point(30, -9), LoadButtonAsset("145_add.png"), new Point(96, 22), 120, new Rectangle(92, 85, 92, 42), 100, delegate { StartBuildWizard("DATA_DISC"); });
            wheel.AddSlice("Write", "Burn image", LoadUiAsset("pie_6_2.png"), LoadUiAsset("pie_6_2_O.png"), new Point(197, -9), LoadButtonAsset("139-Edit.png"), new Point(220, 22), 124, new Rectangle(216, 85, 92, 42), 100, delegate { StartBurnWizard("EZ Mode"); });
            wheel.AddSlice("Copy", "Read disc", LoadUiAsset("pie_6_3.png"), LoadUiAsset("pie_6_3_O.png"), new Point(245, 94), LoadButtonAsset("COPY_CUSTOM.png"), new Point(291, 144), 110, new Rectangle(284, 197, 92, 42), 100, delegate { StartCopyWizard(); });
            wheel.AddSlice("Verify", "Check image", LoadUiAsset("pie_6_4.png"), LoadUiAsset("pie_6_4_O.png"), new Point(197, 219), LoadButtonAsset("6-ApplyButton.png"), new Point(221, 256), 131, new Rectangle(217, 312, 92, 42), 100, delegate { StartVerifyWizard("EZ Mode"); });
            wheel.AddSlice("Erase", "Blank disc", LoadUiAsset("pie_6_5.png"), LoadUiAsset("pie_6_5_O.png"), new Point(30, 219), LoadButtonAsset("18_delete.png"), new Point(101, 262), 100, new Rectangle(95, 312, 92, 42), 100, delegate { _tabs.SelectedIndex = 4; SetStatus("Erase workspace opened."); });
            wheel.AddSlice("Drives", "Inspect", LoadUiAsset("pie_6_6.png"), LoadUiAsset("pie_6_6_O.png"), new Point(-1, 94), LoadButtonAsset("Drives.png"), new Point(37, 150), 54, new Rectangle(24, 197, 92, 42), 100, delegate { RefreshDrives(); _tabs.SelectedIndex = 0; });
            surface.Controls.Add(wheel);

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
            page.BackColor = Color.FromArgb(35, 58, 70);
            page.BackgroundImage = LoadUiAsset("LuxburnBG1.png");
            page.BackgroundImageLayout = ImageLayout.Stretch;
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
                LoadUiAsset("but_145x65.png"),
                LoadUiAsset("but_145x65.png"),
                LoadButtonAsset(iconName));
            button.Location = new Point(34, top);
            button.BackColor = Color.Transparent;
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

        private TabPage CreateBuildInfoPage()
        {
            TabPage page = new TabPage("Information");
            AddInfoRow(page, "Number of Files:", _buildFileCountLabel = CreateInfoValueLabel(), 18, 24);
            AddInfoRow(page, "Number of Folders:", _buildFolderCountLabel = CreateInfoValueLabel(), 18, 48);
            AddInfoRow(page, "Total File Size:", _buildTotalSizeLabel = CreateInfoValueLabel(), 18, 86);
            AddInfoRow(page, "Image Size:", _buildImageSizeLabel = CreateInfoValueLabel(), 18, 110);

            Label media = CreateInfoValueLabel();
            media.Text = "Auto";
            AddInfoRow(page, "Min. Req. Media:", media, 18, 148);

            NeutralProgressBar bar = CreateProgressBar(18, 194, 258);
            page.Controls.Add(bar);

            Button auto = CreateButton("Auto", 298, 190, 70, 25);
            auto.Click += delegate { CalculateBuildImageInformation(); };
            page.Controls.Add(auto);

            CalculateBuildImageInformation();
            return page;
        }

        private TabPage CreateBuildDevicePage()
        {
            TabPage page = new TabPage("Device");
            TextBox info = new TextBox();
            info.Multiline = true;
            info.ReadOnly = true;
            info.ScrollBars = ScrollBars.Vertical;
            info.Location = new Point(10, 12);
            info.Size = new Size(382, 230);
            info.Text = GetSelectedRecorderDisplay() + Environment.NewLine + "Current Profile: " + GetCurrentMediaStatus();
            page.Controls.Add(info);

            AddLabel(page, "Write Speed:", 10, 258);
            ComboBox speed = new ComboBox();
            speed.DropDownStyle = ComboBoxStyle.DropDownList;
            speed.Items.AddRange(new object[] { "AWS", "MAX", "16x", "8x", "4x" });
            speed.SelectedIndex = 0;
            speed.Location = new Point(96, 254);
            speed.Size = new Size(92, 22);
            page.Controls.Add(speed);

            AddLabel(page, "Copies:", 10, 286);
            ComboBox copies = new ComboBox();
            copies.DropDownStyle = ComboBoxStyle.DropDownList;
            copies.Items.AddRange(new object[] { "1", "2", "3", "4", "5" });
            copies.SelectedIndex = 0;
            copies.Location = new Point(96, 282);
            copies.Size = new Size(92, 22);
            page.Controls.Add(copies);
            return page;
        }

        private TabPage CreateBuildOptionsPage()
        {
            TabPage page = new TabPage("Options");
            AddLabel(page, "Data Type:", 18, 28);
            _buildDataTypeCombo = CreateDropDown(146, 24, 150, new object[] { "MODE1/2048", "MODE2/2336" });
            page.Controls.Add(_buildDataTypeCombo);

            AddLabel(page, "File System:", 18, 56);
            _buildFileSystemCombo = CreateDropDown(146, 52, 150, new object[] { "ISO9660 + UDF", "ISO9660 + Joliet + UDF", "ISO9660", "UDF" });
            _buildFileSystemCombo.SelectedIndex = 1;
            page.Controls.Add(_buildFileSystemCombo);

            AddLabel(page, "UDF Revision:", 18, 84);
            _buildUdfRevisionCombo = CreateDropDown(146, 80, 90, new object[] { "1.02", "1.50", "2.00", "2.01" });
            page.Controls.Add(_buildUdfRevisionCombo);

            _preservePathsCheck = CreateCheckBox("Preserve Full Pathnames", 18, 124, false);
            page.Controls.Add(_preservePathsCheck);
            _recurseSubdirectoriesCheck = CreateCheckBox("Recurse Subdirectories", 18, 148, true);
            page.Controls.Add(_recurseSubdirectoriesCheck);
            _includeHiddenFilesCheck = CreateCheckBox("Include Hidden Files", 18, 186, false);
            page.Controls.Add(_includeHiddenFilesCheck);
            _includeSystemFilesCheck = CreateCheckBox("Include System Files", 18, 210, false);
            page.Controls.Add(_includeSystemFilesCheck);
            _includeArchiveOnlyCheck = CreateCheckBox("Include Archive Files Only", 18, 234, false);
            page.Controls.Add(_includeArchiveOnlyCheck);
            _clearArchiveAttributeCheck = CreateCheckBox("Clear Archive Attribute", 18, 276, false);
            page.Controls.Add(_clearArchiveAttributeCheck);

            LinkLabel reset = new LinkLabel();
            reset.Text = "Reset Settings";
            reset.Location = new Point(146, 304);
            reset.AutoSize = true;
            reset.LinkClicked += delegate { ResetBuildOptions(); };
            page.Controls.Add(reset);
            return page;
        }

        private TabPage CreateBuildLabelsPage()
        {
            TabPage page = new TabPage("Labels");
            AddLabel(page, "ISO9660:", 18, 28);
            _iso9660LabelText = CreateTextBox(96, 24, 185);
            page.Controls.Add(_iso9660LabelText);
            AddLabel(page, "Joliet:", 18, 56);
            _jolietLabelText = CreateTextBox(96, 52, 185);
            page.Controls.Add(_jolietLabelText);
            AddLabel(page, "UDF:", 18, 84);
            _udfLabelText = CreateTextBox(96, 80, 185);
            page.Controls.Add(_udfLabelText);

            _syncLabelsCheck = CreateCheckBox("Synchronised Editing", 18, 118, true);
            page.Controls.Add(_syncLabelsCheck);
            _iso9660LabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_iso9660LabelText.Text); };
            _jolietLabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_jolietLabelText.Text); };
            _udfLabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_udfLabelText.Text); };
            SynchronizeVolumeLabels(_volumeNameText.Text);

            AddLabel(page, "System:", 18, 164);
            _systemIdentifierText = CreateTextBox(126, 160, 174);
            page.Controls.Add(_systemIdentifierText);
            AddLabel(page, "Volume Set:", 18, 192);
            _volumeSetText = CreateTextBox(126, 188, 174);
            page.Controls.Add(_volumeSetText);
            AddLabel(page, "Publisher:", 18, 220);
            _publisherText = CreateTextBox(126, 216, 174);
            page.Controls.Add(_publisherText);
            AddLabel(page, "Data Preparer:", 18, 248);
            _dataPreparerText = CreateTextBox(126, 244, 174);
            page.Controls.Add(_dataPreparerText);
            AddLabel(page, "Application:", 18, 276);
            _applicationIdentifierText = CreateTextBox(126, 272, 174);
            _applicationIdentifierText.Text = "LuxBurn";
            page.Controls.Add(_applicationIdentifierText);
            return page;
        }

        private TabPage CreateBuildAdvancedPage()
        {
            TabPage page = new TabPage("Advanced");
            TabControl advanced = new TabControl();
            advanced.Location = new Point(8, 8);
            advanced.Size = new Size(390, 294);
            page.Controls.Add(advanced);

            TabPage dates = new TabPage("Dates");
            dates.Controls.Add(CreateCheckBox("Creation:", 18, 24, false));
            dates.Controls.Add(CreateCheckBox("Modified:", 18, 52, false));
            dates.Controls.Add(CreateCheckBox("Effective:", 18, 80, false));
            dates.Controls.Add(CreateCheckBox("Expiration:", 18, 108, false));
            dates.Controls.Add(CreateRadioButton("Use File Date && Time", 18, 156, true));
            dates.Controls.Add(CreateRadioButton("Use System Date && Time", 18, 180, false));
            dates.Controls.Add(CreateRadioButton("Use Custom Date && Time", 18, 204, false));
            advanced.TabPages.Add(dates);

            TabPage restrictions = new TabPage("Restrictions");
            restrictions.Controls.Add(CreateCheckBox("Allow more than 8 directory levels", 18, 24, true));
            restrictions.Controls.Add(CreateCheckBox("Allow files without extensions", 18, 52, true));
            restrictions.Controls.Add(CreateCheckBox("Allow long file names", 18, 80, true));
            restrictions.Controls.Add(CreateCheckBox("Use relaxed ISO9660 character set", 18, 108, false));
            advanced.TabPages.Add(restrictions);

            TabPage boot = new TabPage("Bootable Disc");
            boot.Controls.Add(CreateCheckBox("Make Image Bootable", 18, 24, false));
            AddLabel(boot, "Boot Image:", 18, 62);
            TextBox bootImage = CreateTextBox(96, 58, 210);
            boot.Controls.Add(bootImage);
            Button browse = CreateButton("Browse", 312, 57, 62, 25);
            browse.Click += delegate { BrowseOpenImage(bootImage); };
            boot.Controls.Add(browse);
            advanced.TabPages.Add(boot);
            return page;
        }

        private Label CreateInfoValueLabel()
        {
            Label label = new Label();
            label.Text = "Unknown";
            label.Size = new Size(180, 18);
            return label;
        }

        private void AddInfoRow(Control parent, string caption, Label value, int x, int y)
        {
            AddLabel(parent, caption, x, y);
            value.Location = new Point(x + 118, y);
            parent.Controls.Add(value);
        }

        private ComboBox CreateDropDown(int x, int y, int width, object[] items)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
            combo.Location = new Point(x, y);
            combo.Size = new Size(width, 22);
            return combo;
        }

        private CheckBox CreateCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox check = new CheckBox();
            check.Text = text;
            check.Checked = isChecked;
            check.Location = new Point(x, y);
            check.Size = new Size(260, 22);
            return check;
        }

        private RadioButton CreateRadioButton(string text, int x, int y, bool isChecked)
        {
            RadioButton radio = new RadioButton();
            radio.Text = text;
            radio.Checked = isChecked;
            radio.Location = new Point(x, y);
            radio.Size = new Size(220, 22);
            return radio;
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

        private void BrowseBuildSourceFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "All files (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                AddBuildItems(dialog.FileNames);
            }
        }

        private void BrowseBuildSourceFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder to add to the image";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                AddBuildItems(new string[] { dialog.SelectedPath });
            }
        }

        private void BuildItemListDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void BuildItemListDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null)
                AddBuildItems(paths);
        }

        private void AddBuildItems(IEnumerable<string> paths)
        {
            foreach (string rawPath in paths)
            {
                string path = string.IsNullOrEmpty(rawPath) ? string.Empty : rawPath.Trim();
                if (path.Length == 0 || (!File.Exists(path) && !Directory.Exists(path)))
                    continue;

                if (FindBuildItem(path) != null)
                    continue;

                bool isDirectory = Directory.Exists(path);
                ListViewItem item = new ListViewItem(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                item.SubItems.Add(isDirectory ? "Folder" : "File");
                item.SubItems.Add(path);
                item.Tag = path;
                _buildItemList.Items.Add(item);
                _buildSourceText.Text = path;
            }

            CalculateBuildImageInformation();
        }

        private ListViewItem FindBuildItem(string path)
        {
            for (int i = 0; i < _buildItemList.Items.Count; i++)
            {
                string existing = Convert.ToString(_buildItemList.Items[i].Tag);
                if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                    return _buildItemList.Items[i];
            }

            return null;
        }

        private void RemoveSelectedBuildItems()
        {
            while (_buildItemList.SelectedItems.Count > 0)
                _buildItemList.Items.Remove(_buildItemList.SelectedItems[0]);
            CalculateBuildImageInformation();
        }

        private void ClearBuildItems()
        {
            _buildItemList.Items.Clear();
            _buildSourceText.Text = string.Empty;
            CalculateBuildImageInformation();
        }

        private void CalculateBuildImageInformation()
        {
            int files = 0;
            int folders = 0;
            long size = 0;

            if (_buildItemList != null)
            {
                for (int i = 0; i < _buildItemList.Items.Count; i++)
                {
                    string path = Convert.ToString(_buildItemList.Items[i].Tag);
                    if (File.Exists(path))
                    {
                        files++;
                        size += new FileInfo(path).Length;
                    }
                    else if (Directory.Exists(path))
                    {
                        folders++;
                        AddDirectoryStatistics(path, ref files, ref folders, ref size);
                    }
                }
            }

            if (_buildFileCountLabel != null)
                _buildFileCountLabel.Text = files.ToString("N0");
            if (_buildFolderCountLabel != null)
                _buildFolderCountLabel.Text = folders.ToString("N0");
            if (_buildTotalSizeLabel != null)
                _buildTotalSizeLabel.Text = FormatBytes(size);
            if (_buildImageSizeLabel != null)
                _buildImageSizeLabel.Text = FormatBytes(size + Math.Max(0, files + folders) * 2048L);

            SetStatus("Image information calculated.");
        }

        private void AddDirectoryStatistics(string path, ref int files, ref int folders, ref long size)
        {
            if (!_recurseSubdirectoriesCheck.Checked)
                return;

            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (!ShouldIncludeFile(file))
                        continue;
                    files++;
                    size += new FileInfo(file).Length;
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    folders++;
                    AddDirectoryStatistics(directory, ref files, ref folders, ref size);
                }
            }
            catch (Exception ex)
            {
                Log("Could not read " + path + ": " + ex.Message);
            }
        }

        private bool ShouldIncludeFile(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if (!_includeHiddenFilesCheck.Checked && (attributes & FileAttributes.Hidden) != 0)
                return false;
            if (!_includeSystemFilesCheck.Checked && (attributes & FileAttributes.System) != 0)
                return false;
            if (_includeArchiveOnlyCheck.Checked && (attributes & FileAttributes.Archive) == 0)
                return false;
            return true;
        }

        private string CreateBuildStagingFolder()
        {
            string staging = Path.Combine(Path.GetTempPath(), "LuxBurnBuild_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            for (int i = 0; i < _buildItemList.Items.Count; i++)
            {
                string source = Convert.ToString(_buildItemList.Items[i].Tag);
                if (File.Exists(source))
                {
                    string target = Path.Combine(staging, Path.GetFileName(source));
                    CopyFileForImage(source, target);
                }
                else if (Directory.Exists(source))
                {
                    string target = _preservePathsCheck.Checked
                        ? Path.Combine(staging, MakeSafePathRoot(source))
                        : Path.Combine(staging, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                    CopyDirectoryForImage(source, target);
                }
            }

            return staging;
        }

        private void CopyDirectoryForImage(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source))
            {
                if (ShouldIncludeFile(file))
                    CopyFileForImage(file, Path.Combine(target, Path.GetFileName(file)));
            }

            if (!_recurseSubdirectoriesCheck.Checked)
                return;

            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectoryForImage(directory, Path.Combine(target, Path.GetFileName(directory)));
        }

        private void CopyFileForImage(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            if (_clearArchiveAttributeCheck != null && _clearArchiveAttributeCheck.Checked)
                File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.Archive);
        }

        private string MakeSafePathRoot(string path)
        {
            string root = Path.GetPathRoot(path);
            string remainder = path.Substring(root.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return root.Replace(Path.DirectorySeparatorChar, '_').Replace(':', '_') + remainder.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        }

        private void BuildThenOpenBurn()
        {
            BuildImage();
            _tabs.SelectedIndex = 2;
        }

        private void SynchronizeVolumeLabels(string value)
        {
            if (_syncLabelsCheck != null && !_syncLabelsCheck.Checked)
                return;

            string label = MakeVolumeLabel(value);
            SetTextWithoutRecursion(_volumeNameText, label);
            SetTextWithoutRecursion(_iso9660LabelText, label);
            SetTextWithoutRecursion(_jolietLabelText, label);
            SetTextWithoutRecursion(_udfLabelText, label);
        }

        private void SetTextWithoutRecursion(TextBox box, string value)
        {
            if (box != null && box.Text != value)
                box.Text = value;
        }

        private static string MakeVolumeLabel(string value)
        {
            string label = string.IsNullOrEmpty(value) ? "LUXBURN_DISC" : value.Trim().ToUpperInvariant();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                label = label.Replace(invalid[i], '_');
            return label.Length > 32 ? label.Substring(0, 32) : label;
        }

        private void ResetBuildOptions()
        {
            _buildDataTypeCombo.SelectedIndex = 0;
            _buildFileSystemCombo.SelectedIndex = 1;
            _buildUdfRevisionCombo.SelectedIndex = 0;
            _preservePathsCheck.Checked = false;
            _recurseSubdirectoriesCheck.Checked = true;
            _includeHiddenFilesCheck.Checked = false;
            _includeSystemFilesCheck.Checked = false;
            _includeArchiveOnlyCheck.Checked = false;
            _clearArchiveAttributeCheck.Checked = false;
        }

        private void NewProject()
        {
            ClearBuildItems();
            _buildOutputText.Text = string.Empty;
            _volumeNameText.Text = "LUXBURN_DISC";
            _projectPath = null;
            SetStatus("New project ready.");
        }

        private void SaveProject()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "LuxBurn project (*.lbp)|*.lbp|All files (*.*)|*.*";
                dialog.DefaultExt = "lbp";
                if (!string.IsNullOrEmpty(_projectPath))
                    dialog.FileName = _projectPath;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                using (StreamWriter writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("LuxBurnProject=1");
                    writer.WriteLine("Output=" + _buildOutputText.Text);
                    writer.WriteLine("Volume=" + _volumeNameText.Text);
                    writer.WriteLine("FileSystem=" + Convert.ToString(_buildFileSystemCombo.SelectedItem));
                    writer.WriteLine("UdfRevision=" + Convert.ToString(_buildUdfRevisionCombo.SelectedItem));
                    writer.WriteLine("Recurse=" + _recurseSubdirectoriesCheck.Checked);
                    for (int i = 0; i < _buildItemList.Items.Count; i++)
                        writer.WriteLine("Item=" + Convert.ToString(_buildItemList.Items[i].Tag));
                }

                _projectPath = dialog.FileName;
                SaveMostRecentProjectPath(_projectPath);
                SetStatus("Project saved.");
            }
        }

        private void LoadProject()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "LuxBurn project (*.lbp)|*.lbp|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadProjectFromPath(dialog.FileName);
            }
        }

        private void LoadMostRecentProject()
        {
            string path = GetMostRecentProjectPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "No recent project was found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LoadProjectFromPath(path);
        }

        private void LoadProjectFromPath(string path)
        {
            ClearBuildItems();
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("Output=", StringComparison.OrdinalIgnoreCase))
                    _buildOutputText.Text = line.Substring(7);
                else if (line.StartsWith("Volume=", StringComparison.OrdinalIgnoreCase))
                    _volumeNameText.Text = line.Substring(7);
                else if (line.StartsWith("Recurse=", StringComparison.OrdinalIgnoreCase))
                    _recurseSubdirectoriesCheck.Checked = string.Equals(line.Substring(8), "True", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("Item=", StringComparison.OrdinalIgnoreCase))
                    AddBuildItems(new string[] { line.Substring(5) });
            }

            _projectPath = path;
            SaveMostRecentProjectPath(path);
            CalculateBuildImageInformation();
            SetStatus("Project loaded.");
        }

        private static string GetRecentProjectStorePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LuxBurn");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "recent.txt");
        }

        private static void SaveMostRecentProjectPath(string path)
        {
            File.WriteAllText(GetRecentProjectStorePath(), path);
        }

        private static string GetMostRecentProjectPath()
        {
            string path = GetRecentProjectStorePath();
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }

        private void ExportGraphData()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*";
                dialog.DefaultExt = "csv";
                dialog.FileName = "LuxBurnGraphData.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                using (StreamWriter writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("Name,Type,Path,Bytes");
                    for (int i = 0; i < _buildItemList.Items.Count; i++)
                    {
                        string path = Convert.ToString(_buildItemList.Items[i].Tag);
                        long bytes = File.Exists(path) ? new FileInfo(path).Length : GetDirectorySize(path);
                        writer.WriteLine("\"" + _buildItemList.Items[i].Text.Replace("\"", "\"\"") + "\",\"" + _buildItemList.Items[i].SubItems[1].Text + "\",\"" + path.Replace("\"", "\"\"") + "\"," + bytes);
                    }
                }

                SetStatus("Graph data exported.");
            }
        }

        private void DisplayGraphData()
        {
            CalculateBuildImageInformation();
            StringBuilder text = new StringBuilder();
            text.AppendLine("LuxBurn Graph Data");
            text.AppendLine();
            text.AppendLine("Files: " + _buildFileCountLabel.Text);
            text.AppendLine("Folders: " + _buildFolderCountLabel.Text);
            text.AppendLine("Total size: " + _buildTotalSizeLabel.Text);
            text.AppendLine("Estimated image size: " + _buildImageSizeLabel.Text);
            ShowTextDialog("Graph Data", text.ToString());
        }

        private void FocusVolumeLabels()
        {
            _tabs.SelectedIndex = 1;
            _iso9660LabelText.Focus();
        }

        private void CreateDescriptorFile(string extension)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = extension.ToUpperInvariant().TrimStart('.') + " file (*" + extension + ")|*" + extension + "|All files (*.*)|*.*";
                dialog.DefaultExt = extension.TrimStart('.');
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string image = _burnImageText.Text.Trim();
                if (image.Length == 0)
                    image = Path.ChangeExtension(dialog.FileName, ".iso");

                using (StreamWriter writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.ASCII))
                {
                    if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteLine("FILE \"" + Path.GetFileName(image) + "\" BINARY");
                        writer.WriteLine("  TRACK 01 MODE1/2048");
                        writer.WriteLine("    INDEX 01 00:00:00");
                    }
                    else if (extension.Equals(".dvd", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteLine("LayerBreak=0");
                        writer.WriteLine(Path.GetFileName(image));
                    }
                    else
                    {
                        writer.WriteLine("; LuxBurn descriptor");
                        writer.WriteLine("Image=" + image);
                    }
                }

                SetStatus("Descriptor file created.");
            }
        }

        private void ShowLayerBreakInformation()
        {
            string image = _burnImageText.Text.Trim();
            if (image.Length == 0)
                image = _buildOutputText.Text.Trim();

            if (image.Length == 0 || !File.Exists(image))
            {
                MessageBox.Show(this, "Choose or build an image first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long sectors = (new FileInfo(image).Length + 2047) / 2048;
            string message = "Image sectors: " + sectors.ToString("N0") + Environment.NewLine +
                             "Layer break sector: " + (sectors / 2).ToString("N0") + Environment.NewLine +
                             "This is an informational midpoint estimate for DVD-sized images.";
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EjectSelectedDrive()
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _burningService.RunDriveCommand(recorder.Id, "-eject", Log, CancellationToken.None);
                SetStatus("Eject command sent.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not eject the selected drive: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunSelectedDriveCommand(string command, string displayName)
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunWork(displayName + "...", delegate
            {
                _burningService.RunDriveCommand(recorder.Id, command, Log, CancellationToken.None);
            },
            delegate
            {
                Log(displayName + " completed.");
                SetStatus(displayName + " completed.");
            });
        }

        private void ShowSettingsDialog()
        {
            Form dialog = new Form();
            dialog.Text = "Settings";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ClientSize = new Size(360, 220);
            dialog.Font = Font;

            dialog.Controls.Add(CreateCheckBox("Eject media after burn", 18, 18, _ejectAfterBurnCheck.Checked));
            dialog.Controls.Add(CreateCheckBox("Calculate SHA-256 after burn", 18, 46, _verifyAfterBurnCheck.Checked));
            dialog.Controls.Add(CreateCheckBox("Calculate SHA-256 after copy", 18, 74, _verifyAfterCopyCheck.Checked));
            Button ok = CreateButton("OK", 242, 176, 90, 28);
            ok.DialogResult = DialogResult.OK;
            dialog.Controls.Add(ok);
            dialog.AcceptButton = ok;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _ejectAfterBurnCheck.Checked = ((CheckBox)dialog.Controls[0]).Checked;
                _verifyAfterBurnCheck.Checked = ((CheckBox)dialog.Controls[1]).Checked;
                _verifyAfterCopyCheck.Checked = ((CheckBox)dialog.Controls[2]).Checked;
            }
        }

        private void ShowDeviceCapabilities()
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            Form dialog = new Form();
            dialog.Text = "Device Capabilities";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ClientSize = new Size(500, 548);
            dialog.Font = Font;

            AddLabel(dialog, "Device:", 8, 12);
            AddValueLabel(dialog, recorder == null ? "No drive selected" : FormatCapabilityDeviceName(recorder), 126, 12, 360, 18);
            AddLabel(dialog, "Firmware Version:", 8, 38);
            AddValueLabel(dialog, recorder == null ? "Unknown" : "RB12", 126, 38, 360, 18);
            AddLabel(dialog, "Vendor Specific Info:", 8, 64);
            AddValueLabel(dialog, recorder == null ? string.Empty : recorder.Id, 126, 64, 360, 36);
            AddLabel(dialog, "Serial Number:", 8, 102);
            AddValueLabel(dialog, recorder == null ? "Unknown" : ExtractSerialCandidate(recorder), 126, 102, 360, 18);
            AddLabel(dialog, "Buffer Size:", 8, 128);
            AddValueLabel(dialog, "2048 KiB", 126, 128, 360, 18);

            GroupBox read = CreateGroup("Read Capabilities", 8, 154, 240, 300);
            GroupBox write = CreateGroup("Write Capabilities", 256, 154, 240, 300);
            dialog.Controls.Add(read);
            dialog.Controls.Add(write);

            string[] caps = new string[] { "CD-R", "CD-RW", "DVD-ROM", "DVD-R", "DVD-RW", "DVD+R", "DVD+RW", "DVD-R DL", "DVD+R DL", "DVD-RAM", "BD-ROM", "BD-R", "BD-RE", "BD-RE XL" };
            for (int i = 0; i < caps.Length; i++)
            {
                read.Controls.Add(CreateReadOnlyCapabilityCheckBox(caps[i], 14 + (i / 7) * 112, 22 + (i % 7) * 26, IsLikelySupported(caps[i], true)));
                write.Controls.Add(CreateReadOnlyCapabilityCheckBox(caps[i], 14 + (i / 7) * 112, 22 + (i % 7) * 26, IsLikelySupported(caps[i], false)));
            }

            GroupBox other = CreateGroup("Other Capabilities", 8, 462, 488, 38);
            other.Controls.Add(CreateReadOnlyCapabilityCheckBox("Binding Nonce Generation", 10, 14, false));
            other.Controls.Add(CreateReadOnlyCapabilityCheckBox("Bus Encryption Capable", 250, 14, false));
            dialog.Controls.Add(other);

            Button firmware = CreateButton("Check For Firmware Update", 8, 516, 220, 28);
            firmware.Click += delegate { CheckFirmwareUpdate(); };
            dialog.Controls.Add(firmware);
            Button ok = CreateButton("OK", 414, 516, 78, 28);
            ok.DialogResult = DialogResult.OK;
            dialog.Controls.Add(ok);
            dialog.AcceptButton = ok;
            dialog.ShowDialog(this);
        }

        private void AddValueLabel(Control parent, string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            parent.Controls.Add(label);
        }

        private CheckBox CreateReadOnlyCapabilityCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox check = CreateCheckBox(text, x, y, isChecked);
            check.AutoCheck = false;
            check.TabStop = false;
            check.Cursor = Cursors.Default;
            check.FlatStyle = FlatStyle.Standard;
            return check;
        }

        private string FormatCapabilityDeviceName(DiscRecorderInfo recorder)
        {
            string bus = recorder.RegistryBusNumber >= 0 ? "[0:" + recorder.RegistryBusNumber + ":0] " : string.Empty;
            string drive = string.IsNullOrEmpty(recorder.DriveLetter) ? string.Empty : " (" + recorder.DriveLetter + ")";
            return bus + recorder.VendorId + " " + recorder.ProductId + drive + " (SATA)";
        }

        private string ExtractSerialCandidate(DiscRecorderInfo recorder)
        {
            if (recorder == null || string.IsNullOrEmpty(recorder.RegistryInstanceKey))
                return "Unknown";

            string value = recorder.RegistryInstanceKey;
            int amp = value.IndexOf('&');
            if (amp >= 0 && amp < value.Length - 1)
                value = value.Substring(amp + 1);
            value = value.Replace("&", string.Empty).Replace("#", string.Empty);
            return value.Length == 0 ? "Unknown" : value;
        }

        private void CheckFirmwareUpdate()
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string model = (recorder.VendorId + " " + recorder.ProductId).Trim();
            if (model.Length == 0)
                model = recorder.DisplayName;

            string message =
                "Selected drive:" + Environment.NewLine +
                model + Environment.NewLine + Environment.NewLine +
                "Search FirmwareHQ for firmware downloads for this drive?";

            if (MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string url = "https://www.firmwarehq.com/search.php?keywords=" + Uri.EscapeDataString(model);
            try
            {
                System.Diagnostics.Process.Start(url);
                SetStatus("Firmware search opened.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open firmware search: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsLikelySupported(string capability, bool read)
        {
            if (capability.StartsWith("BD", StringComparison.OrdinalIgnoreCase))
                return false;
            if (read)
                return true;
            return capability.IndexOf("ROM", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void ShowFamilyTree()
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            StringBuilder text = new StringBuilder();
            text.AppendLine("Device: " + (recorder == null ? "No drive selected" : recorder.DisplayName));
            text.AppendLine();
            text.AppendLine("Family Tree:");
            text.AppendLine("  -> Windows storage stack");
            text.AppendLine("  -> CD-ROM device class");
            if (recorder != null)
            {
                text.AppendLine("  -> " + recorder.VendorId + " " + recorder.ProductId);
                if (recorder.RegistryBusNumber >= 0)
                    text.AppendLine("  -> Bus " + recorder.RegistryBusNumber);
                text.AppendLine("  -> Recorder ID " + recorder.Id);
            }
            ShowTextDialog("Family Tree", text.ToString());
        }

        private DiscRecorderInfo GetSelectedRecorder()
        {
            if (_driveCombo != null && _driveCombo.SelectedItem is DiscRecorderInfo)
                return (DiscRecorderInfo)_driveCombo.SelectedItem;
            if (_burnDriveCombo != null && _burnDriveCombo.SelectedItem is DiscRecorderInfo)
                return (DiscRecorderInfo)_burnDriveCombo.SelectedItem;
            if (_copyDriveCombo != null && _copyDriveCombo.SelectedItem is DiscRecorderInfo)
                return (DiscRecorderInfo)_copyDriveCombo.SelectedItem;
            if (_eraseDriveCombo != null && _eraseDriveCombo.SelectedItem is DiscRecorderInfo)
                return (DiscRecorderInfo)_eraseDriveCombo.SelectedItem;
            return null;
        }

        private string GetSelectedRecorderDisplay()
        {
            DiscRecorderInfo recorder = GetSelectedRecorder();
            return recorder == null ? "No device selected" : recorder.DisplayName;
        }

        private string GetCurrentMediaStatus()
        {
            return _driveCombo.Items.Count == 0 ? "Device Not Ready (Medium Not Present - Tray Closed)" : "N/A";
        }

        private void ShowTextDialog(string title, string text)
        {
            Form dialog = new Form();
            dialog.Text = title;
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.Size = new Size(560, 420);
            dialog.Font = Font;
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Both;
            box.Dock = DockStyle.Fill;
            box.Text = text;
            dialog.Controls.Add(box);
            dialog.ShowDialog(this);
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = new string[] { "B", "KiB", "MiB", "GiB", "TiB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString(unit == 0 ? "N0" : "N2") + " " + units[unit];
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            if (!Directory.Exists(path))
                return 0;

            int files = 0;
            int folders = 0;
            AddDirectoryStatistics(path, ref files, ref folders, ref size);
            return size;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
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

            if (_buildItemList.Items.Count == 0)
                BrowseBuildSourceFolder();

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

        private static Image LoadUiAsset(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Ui", fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Ui", fileName);

            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private static Icon LoadWindowIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Brand", "LBWindowLogo.ico");
            if (!File.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Brand", "LBWindowLogo.ico");

            try
            {
                return File.Exists(path) ? new Icon(path) : null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class UpdateInfo
        {
            public string LatestVersion;
            public string InstallerUrl;
            public string PortableUrl;
            public string ReleasePageUrl;

            public static UpdateInfo FromJson(string json)
            {
                UpdateInfo info = new UpdateInfo();
                info.LatestVersion = ReadJsonString(json, "latestVersion");
                info.InstallerUrl = ReadJsonString(json, "installerUrl");
                info.PortableUrl = ReadJsonString(json, "portableUrl");
                info.ReleasePageUrl = ReadJsonString(json, "releasePageUrl");
                return info;
            }

            private static string ReadJsonString(string json, string name)
            {
                if (string.IsNullOrEmpty(json))
                    return string.Empty;

                Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
                return match.Success ? UnescapeJson(match.Groups[1].Value) : string.Empty;
            }

            private static string UnescapeJson(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;

                return value
                    .Replace("\\/", "/")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t");
            }
        }

        private sealed class UpdateDialog : Form
        {
            public UpdateDialog(string runningVersion, string latestVersion)
            {
                Text = "LuxBurn";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(390, 164);
                Font = new Font("Tahoma", 8.25f);

                Label heading = new Label();
                heading.Text = "LuxBurn is out of date!";
                heading.Font = new Font("Tahoma", 10f, FontStyle.Bold);
                heading.Location = new Point(14, 14);
                heading.Size = new Size(360, 24);
                Controls.Add(heading);

                Label body = new Label();
                body.Text = "You're running " + runningVersion + ". The latest version" + Environment.NewLine + "is " + latestVersion + ". Would you like to update now?";
                body.Location = new Point(14, 50);
                body.Size = new Size(360, 42);
                Controls.Add(body);

                Button update = new Button();
                update.Text = "Update";
                update.DialogResult = DialogResult.Yes;
                update.Location = new Point(72, 116);
                update.Size = new Size(78, 27);
                Controls.Add(update);

                Button no = new Button();
                no.Text = "No";
                no.DialogResult = DialogResult.No;
                no.Location = new Point(156, 116);
                no.Size = new Size(70, 27);
                Controls.Add(no);

                Button remind = new Button();
                remind.Text = "Remind me in 7 days";
                remind.DialogResult = DialogResult.Retry;
                remind.Location = new Point(232, 116);
                remind.Size = new Size(134, 27);
                Controls.Add(remind);

                AcceptButton = update;
                CancelButton = no;
            }
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
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
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
                int width = background == null ? 145 : background.Width;
                int height = background == null ? 65 : background.Height;
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
                        Rectangle iconRect = FitIcon(icon, new Rectangle(13, 16, 30, 30));
                        graphics.DrawImage(icon, iconRect);
                    }

                    TextRenderer.DrawText(
                        graphics,
                        caption,
                        font,
                        new Rectangle(54, 1, width - 58, height),
                        Color.Black,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    TextRenderer.DrawText(
                        graphics,
                        caption,
                        font,
                        new Rectangle(53, 0, width - 58, height),
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

        private sealed class EzModeWheel : Control
        {
            private readonly List<Slice> _slices = new List<Slice>();
            private int _hoverIndex = -1;

            public EzModeWheel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                Size = new Size(400, 380);
                Cursor = Cursors.Hand;
                TabStop = false;
                BackColor = Color.Transparent;
            }

            public void AddSlice(string title, string subtitle, Image normalImage, Image hoverImage, Point imageLocation, Image iconImage, Point iconLocation, int iconScale, Rectangle labelBounds, int textScale, EventHandler click)
            {
                _slices.Add(new Slice(title, subtitle, normalImage, hoverImage ?? normalImage, imageLocation, iconImage, iconLocation, iconScale, labelBounds, textScale, click));
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int index = HitTest(e.Location);
                if (_hoverIndex != index)
                {
                    _hoverIndex = index;
                    Invalidate();
                }

                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (_hoverIndex != -1)
                {
                    _hoverIndex = -1;
                    Invalidate();
                }

                base.OnMouseLeave(e);
            }

            protected override void OnClick(EventArgs e)
            {
                if (_hoverIndex >= 0 && _hoverIndex < _slices.Count && _slices[_hoverIndex].Click != null)
                    _slices[_hoverIndex].Click(this, e);

                base.OnClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                for (int i = 0; i < _slices.Count; i++)
                {
                    Slice slice = _slices[i];
                    Image image = i == _hoverIndex && slice.HoverImage != null ? slice.HoverImage : slice.NormalImage;
                    if (image != null)
                        e.Graphics.DrawImageUnscaled(image, slice.ImageLocation);
                }

                for (int i = 0; i < _slices.Count; i++)
                {
                    Slice slice = _slices[i];
                    if (slice.IconImage != null)
                        DrawScaledImage(e.Graphics, slice.IconImage, slice.IconLocation, slice.IconScale);

                    float textScale = Math.Max(50, Math.Min(180, slice.TextScale)) / 100f;
                    using (Font titleFont = new Font("Tahoma", 10f * textScale, FontStyle.Bold))
                    using (Font subtitleFont = new Font("Tahoma", 8.25f * textScale))
                    {
                        Color titleColor = i == _hoverIndex ? Color.White : Color.FromArgb(232, 244, 248);
                        Color subtitleColor = i == _hoverIndex ? Color.White : Color.FromArgb(205, 226, 235);
                        DrawOutlinedText(e.Graphics, slice.Title, titleFont, slice.LabelBounds, titleColor);
                        DrawOutlinedText(e.Graphics, slice.Subtitle, subtitleFont, new Rectangle(slice.LabelBounds.X, slice.LabelBounds.Y + 22, slice.LabelBounds.Width, 20), subtitleColor);
                    }
                }
            }

            private int HitTest(Point point)
            {
                for (int i = _slices.Count - 1; i >= 0; i--)
                {
                    Slice slice = _slices[i];
                    Image image = slice.HoverImage ?? slice.NormalImage;
                    if (image == null)
                        continue;

                    int localX = point.X - slice.ImageLocation.X;
                    int localY = point.Y - slice.ImageLocation.Y;
                    if (localX < 0 || localY < 0 || localX >= image.Width || localY >= image.Height)
                        continue;

                    Bitmap bitmap = image as Bitmap;
                    if (bitmap == null || bitmap.GetPixel(localX, localY).A > 32)
                        return i;
                }

                return -1;
            }

            private static void DrawOutlinedText(Graphics graphics, string text, Font font, Rectangle bounds, Color color)
            {
                TextRenderer.DrawText(graphics, text, font, new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, text, font, bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            private static void DrawScaledImage(Graphics graphics, Image image, Point location, int scale)
            {
                int clampedScale = Math.Max(10, Math.Min(200, scale));
                int width = Math.Max(1, (int)Math.Round(image.Width * clampedScale / 100.0));
                int height = Math.Max(1, (int)Math.Round(image.Height * clampedScale / 100.0));
                graphics.DrawImage(image, new Rectangle(location.X, location.Y, width, height), new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }

            private sealed class Slice
            {
                public readonly string Title;
                public readonly string Subtitle;
                public readonly Image NormalImage;
                public readonly Image HoverImage;
                public readonly Point ImageLocation;
                public readonly Image IconImage;
                public readonly Point IconLocation;
                public readonly int IconScale;
                public readonly Rectangle LabelBounds;
                public readonly int TextScale;
                public readonly EventHandler Click;

                public Slice(string title, string subtitle, Image normalImage, Image hoverImage, Point imageLocation, Image iconImage, Point iconLocation, int iconScale, Rectangle labelBounds, int textScale, EventHandler click)
                {
                    Title = title;
                    Subtitle = subtitle;
                    NormalImage = normalImage;
                    HoverImage = hoverImage;
                    ImageLocation = imageLocation;
                    IconImage = iconImage;
                    IconLocation = iconLocation;
                    IconScale = iconScale;
                    LabelBounds = labelBounds;
                    TextScale = textScale;
                    Click = click;
                }
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
            string output = _buildOutputText.Text.Trim();
            string volume = _volumeNameText.Text.Trim();

            if (_buildItemList.Items.Count == 0 || output.Length == 0)
            {
                MessageBox.Show(this, "Add files or folders and choose an output image path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunWork("Building image...", delegate
            {
                string stagingPath = CreateBuildStagingFolder();
                try
                {
                    Log("Building image from " + _buildItemList.Items.Count + " source item(s).");
                    _burningService.BuildIso(stagingPath, output, volume);
                }
                finally
                {
                    TryDeleteDirectory(stagingPath);
                }
            },
            delegate
            {
                Log("Image built: " + output);
                _burnImageText.Text = output;
                _verifyFileText.Text = output;
                CalculateBuildImageInformation();
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

        private void CheckForUpdates(bool manual)
        {
            if (!manual && DateTime.UtcNow < GetUpdateReminderDate())
                return;

            SetStatus("Checking for updates...");
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = DownloadUpdateInfo();
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    SetStatus("Ready");
                    WebException webError = e.Error as WebException;
                    HttpWebResponse response = webError == null ? null : webError.Response as HttpWebResponse;
                    if (!manual && response != null && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Log("Update manifest is not published yet.");
                        return;
                    }

                    Log("Update check failed: " + e.Error.Message);
                    if (manual)
                        MessageBox.Show(this, "Could not check for updates." + Environment.NewLine + e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                UpdateInfo update = e.Result as UpdateInfo;
                if (update == null || string.IsNullOrEmpty(update.LatestVersion))
                {
                    SetStatus("Ready");
                    if (manual)
                        MessageBox.Show(this, "No update information was found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Version running = GetRunningVersion();
                Version latest = ParseVersion(update.LatestVersion);
                if (latest.CompareTo(running) <= 0)
                {
                    SetStatus("LuxBurn is up to date.");
                    if (manual)
                        MessageBox.Show(this, "LuxBurn is up to date." + Environment.NewLine + "You're running " + FormatVersion(running) + ".", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SetStatus("Update available.");
                ShowUpdatePrompt(update, running, latest);
            };
            worker.RunWorkerAsync();
        }

        private UpdateInfo DownloadUpdateInfo()
        {
            ConfigureUpdateSecurity();
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "LuxBurn/" + FormatVersion(GetRunningVersion()));
                string json = client.DownloadString(UpdateManifestUrl);
                return UpdateInfo.FromJson(json);
            }
        }

        private void ShowUpdatePrompt(UpdateInfo update, Version running, Version latest)
        {
            using (UpdateDialog dialog = new UpdateDialog(FormatVersion(running), FormatVersion(latest)))
            {
                DialogResult result = dialog.ShowDialog(this);
                if (result == DialogResult.Yes)
                    DownloadAndLaunchUpdate(update);
                else if (result == DialogResult.Retry)
                    SaveUpdateReminder(DateTime.UtcNow.AddDays(7));
                else
                    SetStatus("Ready");
            }
        }

        private void DownloadAndLaunchUpdate(UpdateInfo update)
        {
            if (_burnInProgress)
            {
                MessageBox.Show(this, "A disc operation is in progress. Finish or cancel it before updating.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string installerUrl = string.IsNullOrEmpty(update.InstallerUrl) ? "https://github.com/sccpsteve/LuxBurn/releases/tag/latest" : update.InstallerUrl;
            string tempPath = Path.Combine(Path.GetTempPath(), "LuxBurn-v" + CleanFileVersion(update.LatestVersion) + "-setup.exe");

            try
            {
                ConfigureUpdateSecurity();
                SetStatus("Downloading update...");
                Cursor previousCursor = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "LuxBurn/" + FormatVersion(GetRunningVersion()));
                        client.DownloadFile(installerUrl, tempPath);
                    }
                }
                finally
                {
                    Cursor.Current = previousCursor;
                }

                Process.Start(tempPath);
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("Ready");
                Log("Update download failed: " + ex.Message);
                if (MessageBox.Show(this, "Could not download the installer automatically. Open the download page instead?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                    Process.Start(string.IsNullOrEmpty(update.ReleasePageUrl) ? installerUrl : update.ReleasePageUrl);
            }
        }

        private static void ConfigureUpdateSecurity()
        {
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)768 | (SecurityProtocolType)3072;
            }
            catch
            {
            }
        }

        private static Version GetRunningVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? new Version(0, 0) : version;
        }

        private static Version ParseVersion(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(1);

            Match match = Regex.Match(text, @"\d+(\.\d+){0,3}");
            if (!match.Success)
                return new Version(0, 0);

            string[] parts = match.Value.Split('.');
            while (parts.Length < 2)
                parts = new string[] { parts[0], "0" };

            return new Version(string.Join(".", parts));
        }

        private static string FormatVersion(Version version)
        {
            if (version == null)
                return "0.0";
            if (version.Build > 0 || version.Revision > 0)
                return version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
            return version.Major + "." + version.Minor;
        }

        private static string CleanFileVersion(string version)
        {
            return Regex.Replace(version ?? "update", @"[^0-9A-Za-z_.-]", string.Empty);
        }

        private static DateTime GetUpdateReminderDate()
        {
            try
            {
                string path = GetUpdateReminderPath();
                if (!File.Exists(path))
                    return DateTime.MinValue;

                string text = File.ReadAllText(path).Trim();
                long ticks;
                if (long.TryParse(text, out ticks))
                    return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch
            {
            }

            return DateTime.MinValue;
        }

        private static void SaveUpdateReminder(DateTime date)
        {
            try
            {
                string path = GetUpdateReminderPath();
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, date.ToUniversalTime().Ticks.ToString());
            }
            catch
            {
            }
        }

        private static string GetUpdateReminderPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                appData = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(Path.Combine(appData, "LuxBurn"), "update-reminder.txt");
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

        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

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

