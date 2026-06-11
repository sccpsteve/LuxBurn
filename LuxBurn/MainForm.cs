// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using LuxBurn.Services;

namespace LuxBurn
{
    internal sealed class MainForm : Form
    {
        private const int OperationRailWidth = 252;
        private const int CompactOperationRailWidth = 188;
        private static readonly Size DefaultWindowSize = new Size(1280, 760);
        private static readonly Size CompactMinimumWindowSize = new Size(560, 360);
        private static readonly Size NativeBackgroundLimit = new Size(2560, 1440);
        private static readonly Color ThemeText = Color.FromArgb(244, 248, 250);
        private static readonly Color ThemeMutedText = Color.FromArgb(210, 224, 232);
        private static readonly Color ThemeInputBack = Color.FromArgb(26, 40, 50);
        private static readonly Color ThemeInputBorder = Color.FromArgb(91, 123, 137);
        private static readonly Color ThemePanelBack = Color.FromArgb(24, 36, 45);
        private static readonly Dictionary<string, Image> UiAssetCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private const string UpdateManifestUrl = "https://github.com/sccpsteve/LuxBurn/releases/download/latest/LuxBurn-update.json";
        private const string TrustedUpdatePrefix = "https://github.com/sccpsteve/LuxBurn/releases/download/latest/";

        private readonly LegacyBurningService _burningService = new LegacyBurningService();
        private readonly ToolTip _toolTip = new ToolTip();
        private ComboBox _driveCombo;
        private ComboBox _burnDriveCombo;
        private ComboBox _buildBurnDriveCombo;
        private ComboBox _burnSpeedCombo;
        private ComboBox _burnCopiesCombo;
        private ComboBox _buildBurnSpeedCombo;
        private ComboBox _buildBurnCopiesCombo;
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
        private ComboBox _folderPlacementCombo;
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
        private Button _buildAndBurnButton;
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
            MinimumSize = CompactMinimumWindowSize;
            Size = GetStartupWindowSize();
            Font = CreateUiFont(9f, FontStyle.Regular);
            BackColor = Color.FromArgb(240, 240, 236);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            Icon windowIcon = LoadWindowIcon();
            if (windowIcon != null)
                Icon = windowIcon;

            ConfigureToolTips();
            BuildInterface();
            ApplyDarkTheme(this);
            RefreshComponentStatus();
            RefreshDrives();
            FormClosing += MainFormFormClosing;
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { CheckForUpdates(false); })); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _toolTip.Dispose();

            base.Dispose(disposing);
        }

        private void ConfigureToolTips()
        {
            _toolTip.Active = true;
            _toolTip.ShowAlways = true;
            _toolTip.InitialDelay = 450;
            _toolTip.ReshowDelay = 100;
            _toolTip.AutoPopDelay = 10000;
            _toolTip.UseAnimation = false;
            _toolTip.UseFading = false;
        }

        private void SetToolTip(Control control, string text)
        {
            if (control == null || string.IsNullOrEmpty(text))
                return;

            _toolTip.SetToolTip(control, text);
        }

        protected override void OnResizeBegin(EventArgs e)
        {
            BeginVisualTransition();
            base.OnResizeBegin(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            EndVisualTransition();
        }

        private Font CreateUiFont(float size, FontStyle style)
        {
            try
            {
                return new Font("Arial", size, style, GraphicsUnit.Point);
            }
            catch
            {
            }

            return (Font)SystemFonts.MessageBoxFont.Clone();
        }

        private static Size GetStartupWindowSize()
        {
            Rectangle workingArea = Screen.PrimaryScreen == null
                ? Rectangle.Empty
                : Screen.PrimaryScreen.WorkingArea;

            if (workingArea.Width <= 0 || workingArea.Height <= 0)
                return DefaultWindowSize;

            int width = Math.Min(DefaultWindowSize.Width, Math.Max(CompactMinimumWindowSize.Width, workingArea.Width - 32));
            int height = Math.Min(DefaultWindowSize.Height, Math.Max(CompactMinimumWindowSize.Height, workingArea.Height - 32));
            return new Size(width, height);
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

            _componentStatusLabel = new ShadowLabel();
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
            _mainSplit.Panel1MinSize = CompactOperationRailWidth;
            _mainSplit.SplitterWidth = 4;
            _mainSplit.SplitterDistance = GetPreferredOperationRailWidth();
            Controls.Add(_mainSplit);
            _mainSplit.BringToFront();
            Load += delegate { AdjustCompactLayout(); };
            Resize += delegate { AdjustCompactLayout(); };

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
            file.DropDownItems.Add("Write", null, delegate
            {
                if (_tabs != null && _tabs.SelectedIndex == 1)
                    BuildAndBurnImage();
                else
                {
                    _tabs.SelectedIndex = 2;
                    BurnImage();
                }
            });
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
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add("Credits", null, delegate { ShowCreditsDialog(); });
            menu.Items.Add(help);

            return menu;
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.BackColor = Color.FromArgb(32, 45, 55);
            ScrollableControl scrollableParent = parent as ScrollableControl;
            if (scrollableParent != null)
            {
                scrollableParent.AutoScroll = true;
                scrollableParent.AutoScrollMinSize = new Size(CompactOperationRailWidth, 548);
            }
            ApplyViewportBackground(parent, LoadUiAsset("LuxburnSidebar2.png"), true);
            parent.Padding = new Padding(12);

            Label section = new ShadowLabel();
            section.Text = "Operations";
            section.Font = CreateUiFont(8.25f, FontStyle.Bold);
            section.ForeColor = Color.White;
            section.BackColor = Color.Transparent;
            section.AutoSize = true;
            section.Location = new Point(12, 14);
            parent.Controls.Add(section);

            Control ezModeButton = CreateSideButton(42, "EZ Mode", "wand.png");
            ezModeButton.Click += delegate { _tabs.SelectedIndex = 6; };
            parent.Controls.Add(ezModeButton);

            _refreshButton = CreateSideButton(113, "Refresh", "Refresh.png");
            _refreshButton.Click += delegate { RefreshDrives(); };
            parent.Controls.Add(_refreshButton);

            Control buildTabButton = CreateSideButton(184, "Build", "Build-Disc.png");
            buildTabButton.Click += delegate { _tabs.SelectedIndex = 1; };
            parent.Controls.Add(buildTabButton);

            Control burnTabButton = CreateSideButton(255, "Write", "Write-Disc.png");
            burnTabButton.Click += delegate { _tabs.SelectedIndex = 2; };
            parent.Controls.Add(burnTabButton);

            Control copyTabButton = CreateSideButton(326, "Copy", "Copy-To-Folder.png");
            copyTabButton.Click += delegate { _tabs.SelectedIndex = 3; };
            parent.Controls.Add(copyTabButton);

            Control eraseTabButton = CreateSideButton(397, "Erase", "Erase-Disc.png");
            eraseTabButton.Click += delegate { _tabs.SelectedIndex = 4; };
            parent.Controls.Add(eraseTabButton);

            Control verifyTabButton = CreateSideButton(468, "Verify", "Verify-Data.png");
            verifyTabButton.Click += delegate { _tabs.SelectedIndex = 5; };
            parent.Controls.Add(verifyTabButton);
        }

        private void BuildTabs(Control parent)
        {
            _tabs = new ThemedTabControl(LoadUiAsset("BG-1.png"));
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
                    _mainSplit.SplitterDistance = GetPreferredOperationRailWidth();

                _mainSplit.Panel1Collapsed = hideSidebar;
            }
            finally
            {
                _mainSplit.ResumeLayout(true);
                ResumeLayout(true);
                EndVisualTransitionSoon();
            }
        }

        private int GetPreferredOperationRailWidth()
        {
            return ClientSize.Width <= 760 ? CompactOperationRailWidth : OperationRailWidth;
        }

        private void AdjustCompactLayout()
        {
            if (_mainSplit == null || _mainSplit.Panel1Collapsed)
                return;

            int desired = GetPreferredOperationRailWidth();
            int maximum = Math.Max(_mainSplit.Panel1MinSize, _mainSplit.Width - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth);
            desired = Math.Min(desired, maximum);
            if (desired >= _mainSplit.Panel1MinSize && _mainSplit.SplitterDistance != desired)
                _mainSplit.SplitterDistance = desired;
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
            BindWorkspaceGroup(page, group);

            _driveCombo = new TexturedComboBox();
            _driveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _driveCombo.Location = new Point(18, 28);
            _driveCombo.Size = new Size(520, 22);
            group.Controls.Add(_driveCombo);

            Button refresh = CreateButton("Refresh", 550, 26, 90, 25);
            refresh.Click += delegate { RefreshDrives(); };
            group.Controls.Add(refresh);

            _driveList = new ThemedListView(LoadUiAsset("BG-1.png"));
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
            BindWorkspaceGroup(page, group);

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

            _buildItemList = new ThemedListView(LoadUiAsset("BG-1.png"));
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

            Label dropHint = new ShadowLabel();
            dropHint.Text = "Drop files or folders here";
            dropHint.Location = new Point(18, 480);
            dropHint.Size = new Size(400, 20);
            dropHint.TextAlign = ContentAlignment.MiddleCenter;
            dropHint.ForeColor = Color.FromArgb(72, 72, 72);
            group.Controls.Add(dropHint);

            TabControl imageTabs = new ThemedTabControl(LoadUiAsset("BG-1.png"));
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

            _buildAndBurnButton = CreateButton("Build and burn", 262, 510, 128, 29);
            _buildAndBurnButton.Click += delegate { BuildAndBurnImage(); };
            group.Controls.Add(_buildAndBurnButton);

            Button calculate = CreateButton("Calculate", 402, 510, 100, 29);
            calculate.Click += delegate { CalculateBuildImageInformation(); };
            group.Controls.Add(calculate);

            return page;
        }

        private TabPage CreateBurnTab()
        {
            TabPage page = CreatePage("Burn Image");
            GroupBox group = CreateGroup("Write image to optical media", 14, 14, 760, 430);
            page.Controls.Add(group);
            BindWorkspaceGroup(page, group);

            AddLabel(group, "Image file", 18, 32);
            _burnImageText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_burnImageText);
            Button browseImage = CreateButton("Browse", 642, 27, 86, 25);
            browseImage.Click += delegate { BrowseOpenImage(_burnImageText); };
            group.Controls.Add(browseImage);

            AddLabel(group, "Target drive", 18, 70);
            _burnDriveCombo = new TexturedComboBox();
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
            _verifyAfterBurnCheck.Text = "Calculate source image SHA-256 after burn";
            _verifyAfterBurnCheck.Location = new Point(130, 132);
            _verifyAfterBurnCheck.Size = new Size(320, 22);
            group.Controls.Add(_verifyAfterBurnCheck);

            AddLabel(group, "Write speed", 18, 164);
            _burnSpeedCombo = CreateBurnSpeedCombo(130, 160);
            SetToolTip(_burnSpeedCombo, "Auto lets cdrecord choose the drive/media default. Numeric values pass speed=N to cdrecord.");
            group.Controls.Add(_burnSpeedCombo);

            AddLabel(group, "Copies", 350, 164, 54);
            _burnCopiesCombo = CreateBurnCopiesCombo(412, 160);
            SetToolTip(_burnCopiesCombo, "For multiple copies, LuxBurn burns one disc at a time and prompts for the next blank disc.");
            group.Controls.Add(_burnCopiesCombo);

            SetToolTip(group, "Burning uses the bundled cdrecord backend. If cdrecord is unavailable, LuxBurn opens Windows Disc Image Burner.");

            AddLabel(group, "Progress", 18, 202);
            _writeProgress = CreateProgressBar(130, 200, 500);
            group.Controls.Add(_writeProgress);

            AddLabel(group, "Buffer", 18, 238);
            _bufferProgress = CreateProgressBar(130, 236, 500);
            group.Controls.Add(_bufferProgress);

            AddLabel(group, "Device buffer", 18, 274);
            _deviceBufferProgress = CreateProgressBar(130, 272, 500);
            group.Controls.Add(_deviceBufferProgress);

            _burnButton = CreateButton("Burn image", 130, 326, 120, 29);
            _burnButton.Click += delegate { BurnImage(); };
            group.Controls.Add(_burnButton);

            _abortButton = CreateButton("Abort", 262, 326, 90, 29);
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
            BindWorkspaceGroup(page, group);

            AddLabel(group, "Target drive", 18, 32);
            _eraseDriveCombo = new TexturedComboBox();
            _eraseDriveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _eraseDriveCombo.Location = new Point(130, 28);
            _eraseDriveCombo.Size = new Size(500, 22);
            group.Controls.Add(_eraseDriveCombo);

            _fullEraseCheck = new CheckBox();
            _fullEraseCheck.Text = "Full erase";
            _fullEraseCheck.Location = new Point(130, 66);
            _fullEraseCheck.Size = new Size(150, 22);
            group.Controls.Add(_fullEraseCheck);

            SetToolTip(group, "Erase works on rewritable discs such as CD-RW and DVD-RW. CD-R cannot be erased.");

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
            BindWorkspaceGroup(page, group);

            AddLabel(group, "Source drive", 18, 32);
            _copyDriveCombo = new TexturedComboBox();
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

            SetToolTip(group, "Copies standard data discs to ISO-style images using the bundled readcd backend.");

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
            BindWorkspaceGroup(page, group);

            AddLabel(group, "File", 18, 32);
            _verifyFileText = CreateTextBox(130, 28, 500);
            group.Controls.Add(_verifyFileText);
            Button browseFile = CreateButton("Browse", 642, 27, 86, 25);
            browseFile.Click += delegate { BrowseOpenImage(_verifyFileText); };
            group.Controls.Add(browseFile);

            AddLabel(group, "Algorithm", 18, 70);
            _algorithmCombo = new TexturedComboBox();
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

            Label heading = new ShadowLabel();
            heading.Text = "EZ Mode Picker";
            heading.Font = CreateUiFont(18f, FontStyle.Bold);
            heading.ForeColor = Color.White;
            heading.BackColor = Color.Transparent;
            heading.Location = new Point(34, 24);
            heading.Size = new Size(420, 36);
            surface.Controls.Add(heading);

            Label intro = new ShadowLabel();
            intro.Text = "Choose the job and LuxBurn will open the matching workspace.";
            intro.Location = new Point(38, 64);
            intro.Size = new Size(520, 24);
            intro.ForeColor = Color.FromArgb(220, 235, 242);
            intro.BackColor = Color.Transparent;
            surface.Controls.Add(intro);

            EzModeWheel wheel = new EzModeWheel(CreateUiFont(8.25f, FontStyle.Regular));
            wheel.Location = new Point(72, 108);
            wheel.AddSlice("Build", "Create image", LoadUiAsset("pie_6_1.png"), LoadUiAsset("pie_6_1_O.png"), new Point(30, -9), LoadButtonAsset("Build-Disc.png"), new Point(102, 31), 100, new Rectangle(92, 85, 92, 42), 100, delegate { StartBuildWizard("DATA_DISC"); });
            wheel.AddSlice("Write", "Burn image", LoadUiAsset("pie_6_2.png"), LoadUiAsset("pie_6_2_O.png"), new Point(197, -9), LoadButtonAsset("Write-Disc.png"), new Point(232, 31), 100, new Rectangle(216, 85, 92, 42), 100, delegate { StartBurnWizard("EZ Mode"); });
            wheel.AddSlice("Copy", "Read disc", LoadUiAsset("pie_6_3.png"), LoadUiAsset("pie_6_3_O.png"), new Point(245, 94), LoadButtonAsset("Copy-To-Folder.png"), new Point(294, 147), 100, new Rectangle(284, 194, 92, 42), 100, delegate { StartCopyWizard(); });
            wheel.AddSlice("Verify", "Check image", LoadUiAsset("pie_6_4.png"), LoadUiAsset("pie_6_4_O.png"), new Point(197, 219), LoadButtonAsset("Verify-Disc.png"), new Point(232, 262), 100, new Rectangle(217, 312, 92, 42), 100, delegate { StartVerifyWizard("EZ Mode"); });
            wheel.AddSlice("Erase", "Blank disc", LoadUiAsset("pie_6_5.png"), LoadUiAsset("pie_6_5_O.png"), new Point(30, 219), LoadButtonAsset("Erase-Disc.png"), new Point(102, 262), 100, new Rectangle(95, 312, 92, 42), 100, delegate { _tabs.SelectedIndex = 4; SetStatus("Erase workspace opened."); });
            wheel.AddSlice("Drives", "Inspect", LoadUiAsset("pie_6_6.png"), LoadUiAsset("pie_6_6_O.png"), new Point(-1, 94), LoadButtonAsset("Drives.png"), new Point(37, 150), 55, new Rectangle(24, 197, 92, 42), 100, delegate { RefreshDrives(); _tabs.SelectedIndex = 0; });
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
            ApplyViewportBackground(page, LoadUiAsset("BG-1.png"), false);
            page.Padding = new Padding(10);
            page.AutoScroll = true;
            page.AutoScrollMargin = new Size(14, 14);
            return page;
        }

        private void BindWorkspaceGroup(TabPage page, Control workspace)
        {
            if (page == null || workspace == null)
                return;

            int minWidth = workspace.Width;
            int minHeight = workspace.Height;
            workspace.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            SizeWorkspaceForViewport(page, workspace, minWidth, minHeight);
            page.Resize += delegate { SizeWorkspaceForViewport(page, workspace, minWidth, minHeight); };
        }

        private static void SizeWorkspaceForViewport(TabPage page, Control workspace, int minWidth, int minHeight)
        {
            if (page == null || workspace == null)
                return;

            int availableWidth = page.ClientSize.Width - workspace.Left - 24;
            int availableHeight = page.ClientSize.Height - workspace.Top - 24;
            workspace.Size = new Size(Math.Max(minWidth, availableWidth), Math.Max(minHeight, availableHeight));
            page.AutoScrollMinSize = new Size(workspace.Left + minWidth + 18, workspace.Top + minHeight + 18);
        }

        private TabPage CreatePanePage(string text)
        {
            TabPage page = new TabPage(text);
            page.UseVisualStyleBackColor = false;
            page.BackColor = Color.Transparent;
            ApplyViewportBackground(page, LoadUiAsset("BG-1.png"), false);
            return page;
        }

        private static void ApplyViewportBackground(Control control, Image image, bool stretchWhenBeyondImage)
        {
            if (control == null || image == null)
                return;

            control.BackgroundImage = null;
            control.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control target = sender as Control;
                if (target == null)
                    return;

                DrawViewportBackground(e.Graphics, target.ClientRectangle, image, stretchWhenBeyondImage);
            };
            control.Resize += delegate { control.Invalidate(); };
        }

        private static void DrawViewportBackground(Graphics graphics, Rectangle bounds, Image image, bool stretchWhenBeyondImage)
        {
            if (graphics == null || image == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            bool shouldStretch = stretchWhenBeyondImage
                ? bounds.Width > image.Width || bounds.Height > image.Height
                : bounds.Width > NativeBackgroundLimit.Width || bounds.Height > NativeBackgroundLimit.Height;

            if (shouldStretch)
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(image, bounds);
                return;
            }

            graphics.DrawImageUnscaled(image, bounds.Location);
        }

        private static void DrawTextureBar(Graphics graphics, Rectangle bounds, bool lit, bool dropdown, bool dropdownHover)
        {
            if (graphics == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            Image left = LoadUiAsset(lit ? "LeftPartOfBar1-Lit.png" : "LeftPartOfBar1.png");
            Image center = LoadUiAsset(lit ? "CenterPartOfBar1-Lit.png" : "CenterPartOfBar1.png");
            Image right = dropdown
                ? LoadUiAsset(dropdownHover ? "RightPartOfBar1-Dropdowns-Hover.png" : "RightPartOfBar1-Dropdowns.png")
                : LoadUiAsset(lit ? "RightPartofBar1-Lit.png" : "RightPartofBar1.png");

            if (left == null || center == null || right == null)
            {
                using (Brush brush = new SolidBrush(lit ? Color.FromArgb(44, 82, 102) : ThemeInputBack))
                    graphics.FillRectangle(brush, bounds);
                ControlPaint.DrawBorder(graphics, bounds, ThemeInputBorder, ButtonBorderStyle.Solid);
                return;
            }

            int height = Math.Min(bounds.Height, left.Height);
            Rectangle leftBounds = new Rectangle(bounds.X, bounds.Y, Math.Min(left.Width, bounds.Width), height);
            Rectangle rightBounds = new Rectangle(bounds.Right - Math.Min(right.Width, bounds.Width), bounds.Y, Math.Min(right.Width, bounds.Width), height);
            Rectangle centerBounds = new Rectangle(leftBounds.Right, bounds.Y, Math.Max(0, rightBounds.Left - leftBounds.Right), height);

            graphics.DrawImage(left, leftBounds);
            for (int x = centerBounds.Left; x < centerBounds.Right; x += center.Width)
            {
                int tileWidth = Math.Min(center.Width, centerBounds.Right - x);
                Rectangle source = new Rectangle(0, 0, tileWidth, height);
                Rectangle target = new Rectangle(x, centerBounds.Top, tileWidth, height);
                graphics.DrawImage(center, target, source, GraphicsUnit.Pixel);
            }
            graphics.DrawImage(right, rightBounds);
        }

        private static void PaintInheritedThemeBackground(Graphics graphics, Control control)
        {
            if (graphics == null || control == null)
                return;

            Control root = control.Parent;
            while (root != null && !(root is TabPage))
                root = root.Parent;

            if (root != null)
            {
                Point offset = root.PointToClient(control.PointToScreen(Point.Empty));
                DrawViewportBackground(
                    graphics,
                    new Rectangle(-offset.X, -offset.Y, root.ClientSize.Width, root.ClientSize.Height),
                    LoadUiAsset("BG-1.png"),
                    false);
                return;
            }

            using (Brush brush = new SolidBrush(Color.FromArgb(9, 45, 67)))
                graphics.FillRectangle(brush, control.ClientRectangle);
        }

        private void ApplyDarkTheme(Control root)
        {
            if (root == null)
                return;

            if (root is ShadowLabel)
            {
                root.ForeColor = ThemeText;
                root.BackColor = Color.Transparent;
            }
            else if (root is Label)
            {
                root.ForeColor = ThemeText;
                root.BackColor = Color.Transparent;
                ApplyLabelTextShadow((Label)root);
            }
            else if (root is GroupBox)
            {
                root.ForeColor = ThemeText;
                root.BackColor = Color.Transparent;
                ApplyGroupBoxTextShadow((GroupBox)root);
            }
            else if (root is TabPage)
            {
                TabPage page = (TabPage)root;
                page.ForeColor = ThemeText;
                page.UseVisualStyleBackColor = false;
                if (page.BackgroundImage == null)
                    page.BackColor = Color.Transparent;
            }
            else if (root is CheckBox || root is RadioButton)
            {
                root.ForeColor = ThemeText;
                root.BackColor = Color.Transparent;
            }
            else if (root is TexturedTextBox)
            {
                TextBox textBox = (TextBox)root;
                textBox.BackColor = ThemeInputBack;
                textBox.ForeColor = ThemeText;
                textBox.BorderStyle = BorderStyle.None;
            }
            else if (root is ThemedButton)
            {
                root.ForeColor = Color.Transparent;
                root.BackColor = Color.Transparent;
            }
            else if (root is TextBox)
            {
                TextBox textBox = (TextBox)root;
                textBox.BackColor = ThemeInputBack;
                textBox.ForeColor = ThemeText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (root is ComboBox)
            {
                ComboBox combo = (ComboBox)root;
                combo.BackColor = ThemeInputBack;
                combo.ForeColor = ThemeText;
                combo.FlatStyle = FlatStyle.Flat;
            }
            else if (root is ListView)
            {
                StyleListView((ListView)root);
            }
            else if (root is TabControl)
            {
                TabControl tabs = (TabControl)root;
                tabs.ForeColor = ThemeText;
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.DrawItem += DrawThemeTab;
            }
            else if (root is Button)
            {
                StyleAssetButton((Button)root);
            }
            else if (root is OperationButton)
            {
                root.ForeColor = ThemeText;
            }
            foreach (Control child in root.Controls)
                ApplyDarkTheme(child);
        }

        private void StyleAssetButton(Button button)
        {
            if (button == null || string.Equals(button.Tag as string, "asset-button", StringComparison.Ordinal))
                return;

            Image normal = LoadUiAsset("but_108x32.png");
            Image hover = LoadUiAsset("but_108x32_O.png");
            button.Tag = "asset-button";
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.BackColor = Color.Transparent;
            button.ForeColor = Color.Transparent;
            button.BackgroundImageLayout = ImageLayout.Stretch;
            button.BackgroundImage = normal;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += delegate
            {
                if (button.Enabled && hover != null)
                    button.BackgroundImage = hover;
            };
            button.MouseLeave += delegate
            {
                button.BackgroundImage = normal;
            };
            button.EnabledChanged += delegate
            {
                button.ForeColor = Color.Transparent;
                button.BackgroundImage = normal;
            };
            button.Paint += delegate(object sender, PaintEventArgs e)
            {
                Button painted = sender as Button;
                if (painted == null || string.IsNullOrEmpty(painted.Text))
                    return;

                DrawShadowedText(e.Graphics, painted.Text, painted.Font, painted.ClientRectangle, painted.Enabled ? ThemeText : ThemeMutedText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        private static void StyleListView(ListView list)
        {
            if (list == null)
                return;

            list.BackColor = Color.FromArgb(18, 30, 38);
            list.ForeColor = ThemeText;
            list.GridLines = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            if (list.OwnerDraw)
                return;

            list.OwnerDraw = true;
            list.DrawColumnHeader += DrawThemeListHeader;
            list.DrawItem += DrawThemeListItem;
            list.DrawSubItem += DrawThemeListSubItem;
        }

        private static void DrawThemeListHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(20, 36, 48)))
                e.Graphics.FillRectangle(brush, e.Bounds);
            using (Pen pen = new Pen(Color.FromArgb(72, 104, 120)))
                e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            Rectangle textBounds = new Rectangle(e.Bounds.X + 5, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 8), e.Bounds.Height);
            DrawShadowedText(e.Graphics, e.Header.Text, SystemFonts.MessageBoxFont, textBounds, ThemeText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static void DrawThemeListItem(object sender, DrawListViewItemEventArgs e)
        {
            ListView list = sender as ListView;
            if (list == null || list.View == View.Details)
                return;

            ThemedListView themedList = list as ThemedListView;
            if (themedList != null && !e.Item.Selected)
                themedList.PaintThemeBackground(e.Graphics, e.Bounds);
            else
            {
                Color fill = e.Item.Selected ? Color.FromArgb(48, 92, 116) : list.BackColor;
                using (Brush brush = new SolidBrush(fill))
                    e.Graphics.FillRectangle(brush, e.Bounds);
            }

            Rectangle textBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 8), e.Bounds.Height);
            DrawShadowedText(e.Graphics, e.Item.Text, list.Font, textBounds, ThemeText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static void DrawThemeListSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            ListView list = sender as ListView;
            if (list == null)
                return;

            ThemedListView themedList = list as ThemedListView;
            if (themedList != null && !e.Item.Selected)
                themedList.PaintThemeBackground(e.Graphics, e.Bounds);
            else
            {
                Color fill = e.Item.Selected ? Color.FromArgb(48, 92, 116) : list.BackColor;
                using (Brush brush = new SolidBrush(fill))
                    e.Graphics.FillRectangle(brush, e.Bounds);
            }

            string text = e.SubItem == null ? string.Empty : e.SubItem.Text;
            Rectangle textBounds = new Rectangle(e.Bounds.X + 5, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 8), e.Bounds.Height);
            DrawShadowedText(e.Graphics, text, list.Font, textBounds, ThemeText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static void ApplyLabelTextShadow(Label label)
        {
            if (label == null)
                return;

            label.ForeColor = Color.Transparent;
            label.Paint += delegate(object sender, PaintEventArgs e)
            {
                Label painted = sender as Label;
                if (painted == null || string.IsNullOrEmpty(painted.Text))
                    return;

                DrawShadowedText(e.Graphics, painted.Text, painted.Font, painted.ClientRectangle, ThemeText, GetLabelTextFlags(painted.TextAlign));
            };
        }

        private static void ApplyGroupBoxTextShadow(GroupBox group)
        {
            if (group == null)
                return;

            string originalText = group.Tag as string;
            if (string.IsNullOrEmpty(originalText))
                originalText = group.Text;
            Image barImage = LoadUiAsset("BAR1.png");
            group.Text = string.Empty;
            group.Paint += delegate(object sender, PaintEventArgs e)
            {
                GroupBox painted = sender as GroupBox;
                if (painted == null || string.IsNullOrEmpty(originalText))
                    return;

                if (barImage != null)
                    e.Graphics.DrawImageUnscaled(barImage, 8, -8);

                Rectangle textBounds = barImage == null
                    ? new Rectangle(8, -8, Math.Max(1, painted.Width - 16), 18)
                    : new Rectangle(15, -3, Math.Max(1, barImage.Width - 20), Math.Max(1, barImage.Height - 9));

                using (Font titleFont = new Font("Arial", 9f, FontStyle.Regular, GraphicsUnit.Point))
                    DrawAliasedShadowedText(e.Graphics, originalText, titleFont, textBounds, ThemeText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        private static TextFormatFlags GetLabelTextFlags(ContentAlignment alignment)
        {
            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            if (alignment == ContentAlignment.TopCenter || alignment == ContentAlignment.MiddleCenter || alignment == ContentAlignment.BottomCenter)
                flags |= TextFormatFlags.HorizontalCenter;
            else if (alignment == ContentAlignment.TopRight || alignment == ContentAlignment.MiddleRight || alignment == ContentAlignment.BottomRight)
                flags |= TextFormatFlags.Right;
            else
                flags |= TextFormatFlags.Left;

            if (alignment == ContentAlignment.MiddleLeft || alignment == ContentAlignment.MiddleCenter || alignment == ContentAlignment.MiddleRight)
                flags |= TextFormatFlags.VerticalCenter;
            else if (alignment == ContentAlignment.BottomLeft || alignment == ContentAlignment.BottomCenter || alignment == ContentAlignment.BottomRight)
                flags |= TextFormatFlags.Bottom;
            else
                flags |= TextFormatFlags.Top;

            return flags;
        }

        private static void DrawShadowedText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            DrawAliasedShadowedText(graphics, text, font, bounds, color, flags);
        }

        private static void DrawPlainText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (graphics == null || string.IsNullOrEmpty(text) || font == null)
                return;

            TextRenderer.DrawText(graphics, text, font, bounds, color, NormalizeTextFlags(flags));
        }

        private static void DrawAliasedShadowedText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (graphics == null || string.IsNullOrEmpty(text) || font == null)
                return;

            TextFormatFlags drawFlags = NormalizeTextFlags(flags);
            Rectangle shadow = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height);
            TextRenderer.DrawText(graphics, text, font, shadow, Color.Black, drawFlags);
            TextRenderer.DrawText(graphics, text, font, bounds, color, drawFlags);
        }

        private static TextFormatFlags NormalizeTextFlags(TextFormatFlags flags)
        {
            TextFormatFlags drawFlags = flags | TextFormatFlags.NoPadding;
            if ((flags & TextFormatFlags.EndEllipsis) == TextFormatFlags.EndEllipsis)
                drawFlags |= TextFormatFlags.SingleLine;
            return drawFlags;
        }

        private static StringFormat CreateStringFormat(TextFormatFlags flags)
        {
            StringFormat format = new StringFormat();
            format.Trimming = StringTrimming.EllipsisCharacter;
            format.FormatFlags = StringFormatFlags.NoWrap;

            if ((flags & TextFormatFlags.HorizontalCenter) == TextFormatFlags.HorizontalCenter)
                format.Alignment = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Right) == TextFormatFlags.Right)
                format.Alignment = StringAlignment.Far;
            else
                format.Alignment = StringAlignment.Near;

            if ((flags & TextFormatFlags.VerticalCenter) == TextFormatFlags.VerticalCenter)
                format.LineAlignment = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Bottom) == TextFormatFlags.Bottom)
                format.LineAlignment = StringAlignment.Far;
            else
                format.LineAlignment = StringAlignment.Near;

            return format;
        }

        private static void DrawThemeTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count)
                return;

            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            Rectangle fillBounds = bounds;
            fillBounds.Inflate(1, 1);
            Color fill = selected ? Color.FromArgb(33, 64, 80) : Color.FromArgb(20, 36, 48);
            Color border = selected ? Color.FromArgb(26, 56, 72) : Color.FromArgb(12, 28, 40);
            using (Brush brush = new SolidBrush(fill))
                e.Graphics.FillRectangle(brush, fillBounds);
            using (Pen pen = new Pen(border))
                e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            DrawShadowedText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, bounds, ThemeText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private GroupBox CreateGroup(string text, int x, int y, int width, int height)
        {
            GroupBox group = new GroupBox();
            group.Text = text;
            group.Tag = text;
            group.Location = new Point(x, y);
            group.Size = new Size(width, height);
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            group.BackColor = Color.Transparent;
            return group;
        }

        private Button CreateButton(string text, int x, int y, int width, int height)
        {
            Button button = new ThemedButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            return button;
        }

        private NeutralProgressBar CreateProgressBar(int x, int y, int width)
        {
            NeutralProgressBar bar = new NeutralProgressBar();
            bar.Location = new Point(x, y);
            bar.Size = new Size(width, 26);
            return bar;
        }

        private Label CreateNoteLabel(string text, int x, int y, int width, int height)
        {
            Label label = new ShadowLabel();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.ForeColor = Color.FromArgb(72, 72, 72);
            SetToolTip(label, text);
            return label;
        }

        private Control CreateSideButton(int top, string caption, string iconName)
        {
            OperationButton button = new OperationButton(
                caption,
                LoadUiAsset("but_145x65.png"),
                LoadUiAsset("but_145x65_O.png"),
                LoadButtonAsset(iconName),
                CreateUiFont(8.25f, FontStyle.Regular));
            button.Location = new Point(34, top);
            button.BackColor = Color.Transparent;
            return button;
        }

        private TextBox CreateTextBox(int x, int y, int width)
        {
            TextBox textBox = new TexturedTextBox();
            textBox.Location = new Point(x, y);
            textBox.Size = new Size(width, 26);
            return textBox;
        }

        private Label AddLabel(Control parent, string text, int x, int y)
        {
            return AddLabel(parent, text, x, y, 100);
        }

        private Label AddLabel(Control parent, string text, int x, int y, int width)
        {
            Label label = new ShadowLabel();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 18);
            parent.Controls.Add(label);
            return label;
        }

        private TabPage CreateBuildInfoPage()
        {
            TabPage page = CreatePanePage("Information");
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
            TabPage page = CreatePanePage("Device");
            TextBox info = new TextBox();
            info.Multiline = true;
            info.ReadOnly = true;
            info.ScrollBars = ScrollBars.Vertical;
            info.Location = new Point(10, 12);
            info.Size = new Size(382, 178);
            info.Text = GetSelectedRecorderDisplay() + Environment.NewLine + "Current Profile: " + GetCurrentMediaStatus();
            page.Controls.Add(info);

            AddLabel(page, "Target Drive:", 10, 204);
            _buildBurnDriveCombo = new TexturedComboBox();
            _buildBurnDriveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _buildBurnDriveCombo.Location = new Point(126, 200);
            _buildBurnDriveCombo.Size = new Size(256, 22);
            page.Controls.Add(_buildBurnDriveCombo);

            AddLabel(page, "Write Speed:", 10, 240);
            _buildBurnSpeedCombo = CreateBurnSpeedCombo(126, 236);
            SetToolTip(_buildBurnSpeedCombo, "Auto lets cdrecord choose the drive/media default. Numeric values pass speed=N to cdrecord.");
            page.Controls.Add(_buildBurnSpeedCombo);

            AddLabel(page, "Copies:", 10, 268);
            _buildBurnCopiesCombo = CreateBurnCopiesCombo(126, 264);
            SetToolTip(_buildBurnCopiesCombo, "For multiple copies, LuxBurn burns one disc at a time and prompts for the next blank disc.");
            page.Controls.Add(_buildBurnCopiesCombo);
            return page;
        }

        private TabPage CreateBuildOptionsPage()
        {
            TabPage page = CreatePanePage("Options");
            AddLabel(page, "Data Type:", 18, 28);
            _buildDataTypeCombo = CreateDropDown(146, 24, 150, new object[] { "MODE1/2048", "MODE2/2336" });
            MakeReadOnlyCombo(_buildDataTypeCombo);
            SetToolTip(_buildDataTypeCombo, "IMAPI2FS currently builds data images with its automatic data mode.");
            page.Controls.Add(_buildDataTypeCombo);

            AddLabel(page, "File System:", 18, 56);
            _buildFileSystemCombo = CreateDropDown(146, 52, 150, new object[] { "ISO9660 + UDF", "ISO9660 + Joliet + UDF", "ISO9660", "UDF" });
            _buildFileSystemCombo.SelectedIndex = 1;
            MakeReadOnlyCombo(_buildFileSystemCombo);
            SetToolTip(_buildFileSystemCombo, "IMAPI2FS currently builds the file systems automatically.");
            page.Controls.Add(_buildFileSystemCombo);

            AddLabel(page, "UDF Revision:", 18, 84);
            _buildUdfRevisionCombo = CreateDropDown(146, 80, 90, new object[] { "1.02", "1.50", "2.00", "2.01" });
            MakeReadOnlyCombo(_buildUdfRevisionCombo);
            SetToolTip(_buildUdfRevisionCombo, "IMAPI2FS currently chooses the applied UDF revision automatically.");
            page.Controls.Add(_buildUdfRevisionCombo);

            Label folderLabel = AddLabel(page, "Folder Placement:", 18, 114);
            SetToolTip(folderLabel, "Disc root puts the selected folder contents at the top of the disc. Keep folders preserves each selected folder as a folder.");
            _folderPlacementCombo = CreateDropDown(146, 110, 246, new object[] { "Disc root", "Keep folders" });
            SetToolTip(_folderPlacementCombo, "Disc root puts the selected folder contents at the top of the disc. Keep folders preserves each selected folder as a folder.");
            page.Controls.Add(_folderPlacementCombo);

            _preservePathsCheck = CreateCheckBox("Preserve Full Pathnames", 18, 148, false);
            page.Controls.Add(_preservePathsCheck);
            _recurseSubdirectoriesCheck = CreateCheckBox("Recurse Subdirectories", 18, 172, true);
            page.Controls.Add(_recurseSubdirectoriesCheck);
            _includeHiddenFilesCheck = CreateCheckBox("Include Hidden Files", 18, 210, false);
            page.Controls.Add(_includeHiddenFilesCheck);
            _includeSystemFilesCheck = CreateCheckBox("Include System Files", 18, 234, false);
            page.Controls.Add(_includeSystemFilesCheck);
            _includeArchiveOnlyCheck = CreateCheckBox("Include Archive Files Only", 18, 258, false);
            page.Controls.Add(_includeArchiveOnlyCheck);
            _clearArchiveAttributeCheck = CreateCheckBox("Clear Archive Attribute", 18, 288, false);
            page.Controls.Add(_clearArchiveAttributeCheck);

            LinkLabel reset = new LinkLabel();
            reset.Text = "Reset Settings";
            reset.Location = new Point(146, 314);
            reset.AutoSize = true;
            reset.LinkClicked += delegate { ResetBuildOptions(); };
            page.Controls.Add(reset);
            return page;
        }

        private TabPage CreateBuildLabelsPage()
        {
            TabPage page = CreatePanePage("Labels");
            AddLabel(page, "ISO9660:", 18, 28);
            _iso9660LabelText = CreateTextBox(126, 24, 185);
            _iso9660LabelText.ReadOnly = true;
            page.Controls.Add(_iso9660LabelText);
            AddLabel(page, "Joliet:", 18, 56);
            _jolietLabelText = CreateTextBox(126, 52, 185);
            _jolietLabelText.ReadOnly = true;
            page.Controls.Add(_jolietLabelText);
            AddLabel(page, "UDF:", 18, 84);
            _udfLabelText = CreateTextBox(126, 80, 185);
            _udfLabelText.ReadOnly = true;
            page.Controls.Add(_udfLabelText);

            _syncLabelsCheck = CreateCheckBox("Synchronised Editing", 18, 118, true);
            MakeReadOnlyCheck(_syncLabelsCheck);
            page.Controls.Add(_syncLabelsCheck);
            _iso9660LabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_iso9660LabelText.Text); };
            _jolietLabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_jolietLabelText.Text); };
            _udfLabelText.TextChanged += delegate { if (_syncLabelsCheck.Checked) SynchronizeVolumeLabels(_udfLabelText.Text); };
            SynchronizeVolumeLabels(_volumeNameText.Text);

            AddLabel(page, "System:", 18, 164);
            _systemIdentifierText = CreateTextBox(126, 160, 174);
            _systemIdentifierText.ReadOnly = true;
            page.Controls.Add(_systemIdentifierText);
            AddLabel(page, "Volume Set:", 18, 192);
            _volumeSetText = CreateTextBox(126, 188, 174);
            _volumeSetText.ReadOnly = true;
            page.Controls.Add(_volumeSetText);
            AddLabel(page, "Publisher:", 18, 220);
            _publisherText = CreateTextBox(126, 216, 174);
            _publisherText.ReadOnly = true;
            page.Controls.Add(_publisherText);
            AddLabel(page, "Data Preparer:", 18, 248);
            _dataPreparerText = CreateTextBox(126, 244, 174);
            _dataPreparerText.ReadOnly = true;
            page.Controls.Add(_dataPreparerText);
            AddLabel(page, "Application:", 18, 276);
            _applicationIdentifierText = CreateTextBox(126, 272, 174);
            _applicationIdentifierText.Text = "LuxBurn";
            _applicationIdentifierText.ReadOnly = true;
            page.Controls.Add(_applicationIdentifierText);
            page.Controls.Add(CreateNoteLabel("The current ISO builder uses the main Volume label only.", 18, 314, 280, 22));
            return page;
        }

        private TabPage CreateBuildAdvancedPage()
        {
            TabPage page = CreatePanePage("Advanced");
            TabControl advanced = new ThemedTabControl(LoadUiAsset("BG-1.png"));
            advanced.Location = new Point(8, 8);
            advanced.Size = new Size(390, 294);
            page.Controls.Add(advanced);
            page.Controls.Add(CreateNoteLabel("Advanced date, restriction, and boot options are disabled until LuxBurn routes builds through a backend that applies them.", 12, 306, 370, 32));

            TabPage dates = CreatePanePage("Dates");
            dates.Controls.Add(CreateReadOnlyCheckBox("Creation:", 18, 24, false));
            dates.Controls.Add(CreateReadOnlyCheckBox("Modified:", 18, 52, false));
            dates.Controls.Add(CreateReadOnlyCheckBox("Effective:", 18, 80, false));
            dates.Controls.Add(CreateReadOnlyCheckBox("Expiration:", 18, 108, false));
            dates.Controls.Add(CreateReadOnlyRadioButton("Use File Date && Time", 18, 156, true));
            dates.Controls.Add(CreateReadOnlyRadioButton("Use System Date && Time", 18, 180, false));
            dates.Controls.Add(CreateReadOnlyRadioButton("Use Custom Date && Time", 18, 204, false));
            advanced.TabPages.Add(dates);

            TabPage restrictions = CreatePanePage("Restrictions");
            restrictions.Controls.Add(CreateReadOnlyCheckBox("Allow more than 8 directory levels", 18, 24, true));
            restrictions.Controls.Add(CreateReadOnlyCheckBox("Allow files without extensions", 18, 52, true));
            restrictions.Controls.Add(CreateReadOnlyCheckBox("Allow long file names", 18, 80, true));
            restrictions.Controls.Add(CreateReadOnlyCheckBox("Use relaxed ISO9660 character set", 18, 108, false));
            advanced.TabPages.Add(restrictions);

            TabPage boot = CreatePanePage("Bootable Disc");
            boot.Controls.Add(CreateReadOnlyCheckBox("Make Image Bootable", 18, 24, false));
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
            ShadowLabel label = new ShadowLabel();
            label.UseShadow = false;
            label.Text = "Unknown";
            label.Size = new Size(180, 18);
            return label;
        }

        private void AddInfoRow(Control parent, string caption, Label value, int x, int y)
        {
            Label captionLabel = new ShadowLabel();
            captionLabel.Text = caption;
            captionLabel.Location = new Point(x, y);
            captionLabel.Size = new Size(136, 18);
            parent.Controls.Add(captionLabel);
            value.Location = new Point(x + 150, y);
            parent.Controls.Add(value);
        }

        private ComboBox CreateDropDown(int x, int y, int width, object[] items)
        {
            ComboBox combo = new TexturedComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
            combo.Location = new Point(x, y);
            combo.Size = new Size(width, 22);
            return combo;
        }

        private ComboBox CreateBurnSpeedCombo(int x, int y)
        {
            ComboBox combo = CreateDropDown(x, y, 92, new object[] { "Auto", "Max", "48x", "40x", "32x", "24x", "16x", "8x", "4x" });
            combo.SelectedIndexChanged += delegate
            {
                SynchronizeComboSelection(combo, combo == _burnSpeedCombo ? _buildBurnSpeedCombo : _burnSpeedCombo);
            };
            return combo;
        }

        private ComboBox CreateBurnCopiesCombo(int x, int y)
        {
            ComboBox combo = CreateDropDown(x, y, 64, new object[] { "1", "2", "3", "4", "5" });
            combo.SelectedIndexChanged += delegate
            {
                SynchronizeComboSelection(combo, combo == _burnCopiesCombo ? _buildBurnCopiesCombo : _burnCopiesCombo);
            };
            return combo;
        }

        private static void SynchronizeComboSelection(ComboBox source, ComboBox target)
        {
            if (source == null || target == null || source.SelectedIndex < 0 || target.SelectedIndex == source.SelectedIndex)
                return;

            if (source.SelectedIndex < target.Items.Count)
                target.SelectedIndex = source.SelectedIndex;
        }

        private static string GetSelectedBurnSpeed(ComboBox combo)
        {
            if (combo == null || combo.SelectedItem == null)
                return "Auto";

            return Convert.ToString(combo.SelectedItem);
        }

        private static int GetSelectedBurnCopies(ComboBox combo)
        {
            if (combo == null || combo.SelectedItem == null)
                return 1;

            int copies;
            return int.TryParse(Convert.ToString(combo.SelectedItem), out copies) && copies > 0 ? copies : 1;
        }

        private bool ConfirmNextBurnCopy(int copyNumber, int totalCopies)
        {
            if (InvokeRequired)
                return (bool)Invoke(new Func<int, int, bool>(ConfirmNextBurnCopy), copyNumber, totalCopies);

            string message = "Insert a blank disc for copy " + copyNumber + " of " + totalCopies + ", then click OK.";
            return MessageBox.Show(this, message, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK;
        }

        private static void MakeReadOnlyCombo(ComboBox combo)
        {
            if (combo == null)
                return;

            combo.TabStop = false;
            combo.DropDown += delegate { combo.DroppedDown = false; };
            combo.KeyDown += delegate(object sender, KeyEventArgs e) { e.SuppressKeyPress = true; };
            combo.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                HandledMouseEventArgs handled = e as HandledMouseEventArgs;
                if (handled != null)
                    handled.Handled = true;
            };
        }

        private static void SelectComboText(ComboBox combo, string value)
        {
            if (combo == null)
                return;

            if (string.Equals(value, "Put folder contents at disc root", StringComparison.OrdinalIgnoreCase))
                value = "Disc root";
            else if (string.Equals(value, "Keep selected folders as folders", StringComparison.OrdinalIgnoreCase))
                value = "Keep folders";

            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(combo.Items[i]), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private CheckBox CreateCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox check = new ThemedCheckBox();
            check.Text = text;
            check.Checked = isChecked;
            check.Location = new Point(x, y);
            check.Size = new Size(260, 22);
            return check;
        }

        private CheckBox CreateReadOnlyCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox check = CreateCheckBox(text, x, y, isChecked);
            MakeReadOnlyCheck(check);
            return check;
        }

        private static void MakeReadOnlyCheck(CheckBox check)
        {
            if (check == null)
                return;

            check.AutoCheck = false;
            check.TabStop = false;
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

        private RadioButton CreateReadOnlyRadioButton(string text, int x, int y, bool isChecked)
        {
            RadioButton radio = CreateRadioButton(text, x, y, isChecked);
            radio.AutoCheck = false;
            radio.TabStop = false;
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

            Label heading = new ShadowLabel();
            heading.Text = title;
            heading.Font = CreateUiFont(8.25f, FontStyle.Bold);
            heading.Location = new Point(12, 10);
            heading.Size = new Size(392, 18);
            card.Controls.Add(heading);

            Label description = new ShadowLabel();
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

                if (Directory.Exists(path) && IsReparsePoint(path))
                {
                    Log("Skipped linked folder: " + path);
                    continue;
                }

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
                if (IsReparsePoint(path))
                    return;

                foreach (string file in Directory.GetFiles(path))
                {
                    if (!ShouldIncludeFile(file))
                        continue;
                    files++;
                    size += new FileInfo(file).Length;
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    if (IsReparsePoint(directory))
                    {
                        Log("Skipped linked folder: " + directory);
                        continue;
                    }

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
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return false;
            if (!_includeHiddenFilesCheck.Checked && (attributes & FileAttributes.Hidden) != 0)
                return false;
            if (!_includeSystemFilesCheck.Checked && (attributes & FileAttributes.System) != 0)
                return false;
            if (_includeArchiveOnlyCheck.Checked && (attributes & FileAttributes.Archive) == 0)
                return false;
            return true;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true;
            }
        }

        private string CreateBuildStagingFolder(bool placeFolderContentsAtRoot)
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
                    if (!_preservePathsCheck.Checked && placeFolderContentsAtRoot)
                    {
                        CopyDirectoryContentsForImage(source, staging);
                    }
                    else
                    {
                        string target = _preservePathsCheck.Checked
                            ? Path.Combine(staging, MakeSafePathRoot(source))
                            : Path.Combine(staging, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                        CopyDirectoryForImage(source, target);
                    }
                }
            }

            return staging;
        }

        private void CopyDirectoryContentsForImage(string source, string target)
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
            {
                if (IsReparsePoint(directory))
                    continue;

                CopyDirectoryForImage(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
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
            {
                if (IsReparsePoint(directory))
                    continue;

                CopyDirectoryForImage(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }

        private void CopyFileForImage(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (File.Exists(target))
                throw new IOException("Two source items would create the same disc file: " + target);

            File.Copy(source, target, false);
            if (_clearArchiveAttributeCheck != null && _clearArchiveAttributeCheck.Checked)
                File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.Archive);
        }

        private string MakeSafePathRoot(string path)
        {
            string root = Path.GetPathRoot(path);
            string remainder = path.Substring(root.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return root.Replace(Path.DirectorySeparatorChar, '_').Replace(':', '_') + remainder.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
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
            if (_folderPlacementCombo != null)
                _folderPlacementCombo.SelectedIndex = 0;
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
            if (_folderPlacementCombo != null)
                _folderPlacementCombo.SelectedIndex = 0;
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
                    writer.WriteLine("FolderPlacement=" + Convert.ToString(_folderPlacementCombo.SelectedItem));
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
                else if (line.StartsWith("FolderPlacement=", StringComparison.OrdinalIgnoreCase))
                    SelectComboText(_folderPlacementCombo, line.Substring(16));
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
            dialog.Controls.Add(CreateCheckBox("Calculate source image SHA-256 after burn", 18, 46, _verifyAfterBurnCheck.Checked));
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

        private void ShowCreditsDialog()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Credits";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(520, 420);
                dialog.Font = Font;

                PictureBox icon = new PictureBox();
                icon.Image = LoadBrandAsset("LBWindowLogo.png");
                icon.Location = new Point(16, 16);
                icon.Size = new Size(48, 48);
                icon.SizeMode = PictureBoxSizeMode.CenterImage;
                dialog.Controls.Add(icon);

                Label heading = new Label();
                heading.Text = "LuxBurn";
                heading.Font = CreateUiFont(14f, FontStyle.Bold);
                heading.Location = new Point(78, 18);
                heading.Size = new Size(390, 24);
                dialog.Controls.Add(heading);

                Label version = new Label();
                version.Text = "Version " + FormatVersion(GetRunningVersion());
                version.Location = new Point(80, 46);
                version.Size = new Size(390, 18);
                dialog.Controls.Add(version);

                TextBox credits = new TextBox();
                credits.Multiline = true;
                credits.ReadOnly = true;
                credits.ScrollBars = ScrollBars.Vertical;
                credits.Location = new Point(16, 78);
                credits.Size = new Size(488, 292);
                credits.Text = BuildCreditsText();
                dialog.Controls.Add(credits);

                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(414, 382);
                ok.Size = new Size(90, 26);
                dialog.Controls.Add(ok);

                dialog.AcceptButton = ok;
                dialog.CancelButton = ok;
                dialog.ShowDialog(this);
            }
        }

        private static string BuildCreditsText()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("LuxBurn");
            text.AppendLine("Created and maintained by Tristan | sccpsteve.com.");
            text.AppendLine("Coordinated with OpenAI Codex.");
            text.AppendLine();
            text.AppendLine("Project roots");
            text.AppendLine("- SvenGDK and Open Burning Suite, the original BSD-licensed base.");
            text.AppendLine("- CDRTFE and its contributors, for practical cdrtools workflow inspiration.");
            text.AppendLine("- ImgBurn by LIGHTNING UK!, used as a workflow and feature reference.");
            text.AppendLine();
            text.AppendLine("Disc and packaging tools");
            text.AppendLine("- cdrtools by Joerg Schilling and contributors.");
            text.AppendLine("- Cygwin runtime files by the Cygwin project and contributors.");
            text.AppendLine("- Inno Setup by Jordan Russell and Martijn Laan.");
            text.AppendLine("- 7-Zip by Igor Pavlov, used for local package creation.");
            text.AppendLine("- Microsoft IMAPI2 and IMAPI2FS APIs.");
            text.AppendLine();
            text.AppendLine("Artwork");
            text.AppendLine("- LuxBurn branding and application assets supplied by sccpsteve.");
            text.AppendLine("- Mozilla Firefox Pinstripe theme artwork and Mozilla contributors.");
            text.AppendLine("- Firefox logo artwork by Jon Hicks / Hicksdesign, based on a Daniel Burka concept and Stephen Desroches sketch.");
            text.AppendLine();
            text.AppendLine("Thanks");
            text.AppendLine("- FirmwareHQ for drive firmware search pages.");
            text.AppendLine("- Everyone testing real optical media, especially on older Windows systems.");
            return text.ToString();
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
            if (_buildBurnDriveCombo != null && _buildBurnDriveCombo.SelectedItem is DiscRecorderInfo)
                return (DiscRecorderInfo)_buildBurnDriveCombo.SelectedItem;
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
            Image cached;
            if (UiAssetCache.TryGetValue(fileName, out cached))
                return cached;

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Ui", fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Ui", fileName);

            cached = File.Exists(path) ? Image.FromFile(path) : null;
            UiAssetCache[fileName] = cached;
            return cached;
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
            public string InstallerSha256;
            public string PortableSha256;
            public string GeneratedAtUtc;

            public static UpdateInfo FromJson(string json)
            {
                if (string.IsNullOrEmpty(json))
                    return new UpdateInfo();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> values = serializer.DeserializeObject(json) as Dictionary<string, object>;
                UpdateInfo info = new UpdateInfo();
                info.LatestVersion = ReadJsonString(values, "latestVersion");
                info.InstallerUrl = ReadJsonString(values, "installerUrl");
                info.PortableUrl = ReadJsonString(values, "portableUrl");
                info.ReleasePageUrl = ReadJsonString(values, "releasePageUrl");
                info.InstallerSha256 = ReadJsonString(values, "installerSha256");
                info.PortableSha256 = ReadJsonString(values, "portableSha256");
                info.GeneratedAtUtc = ReadJsonString(values, "generatedAtUtc");
                return info;
            }

            private static string ReadJsonString(Dictionary<string, object> values, string name)
            {
                if (values == null || !values.ContainsKey(name) || values[name] == null)
                    return string.Empty;

                return Convert.ToString(values[name]);
            }
        }

        private sealed class UpdateDialog : Form
        {
            public UpdateDialog(string runningVersion, string latestVersion, Font uiFont)
            {
                Text = "LuxBurn";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(390, 164);
                Font = uiFont ?? new Font("MS Sans Serif", 8.25f);

                Label heading = new Label();
                heading.Text = "LuxBurn is out of date!";
                heading.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
                heading.Location = new Point(14, 14);
                heading.Size = new Size(360, 24);
                Controls.Add(heading);

                Label body = new Label();
                body.Text = "You're running " + runningVersion + ". The latest version" + Environment.NewLine + "is " + latestVersion + ". Would you like to update now?";
                body.Location = new Point(14, 50);
                body.Size = new Size(360, 42);
                Controls.Add(body);

                Button update = new Button();
                update.Text = "Download";
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

        private sealed class ShadowLabel : Label
        {
            public bool UseShadow = true;

            public ShadowLabel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (e == null)
                    return;

                base.OnPaintBackground(e);
                Color color = ForeColor == Color.Transparent ? ThemeText : ForeColor;
                TextFormatFlags flags = GetLabelTextFlags(TextAlign);
                if (Height > Font.Height + 6)
                {
                    flags &= ~TextFormatFlags.EndEllipsis;
                    flags |= TextFormatFlags.WordBreak;
                }

                if (UseShadow)
                    DrawShadowedText(e.Graphics, Text, Font, ClientRectangle, color, flags);
                else
                    DrawPlainText(e.Graphics, Text, Font, ClientRectangle, color, flags);
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Invalidate();
            }
        }

        private sealed class TexturedTextBox : TextBox
        {
            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;

            public TexturedTextBox()
            {
                Multiline = true;
                BorderStyle = BorderStyle.None;
                BackColor = ThemeInputBack;
                ForeColor = ThemeText;
                ScrollBars = ScrollBars.None;
                WordWrap = false;
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Invalidate();
            }

            protected override void OnGotFocus(EventArgs e)
            {
                base.OnGotFocus(e);
                Invalidate();
            }

            protected override void OnLostFocus(EventArgs e)
            {
                base.OnLostFocus(e);
                Invalidate();
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                base.OnKeyUp(e);
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_ERASEBKGND)
                {
                    m.Result = new IntPtr(1);
                    return;
                }

                if (m.Msg == WM_PAINT)
                {
                    PAINTSTRUCT ps;
                    IntPtr hdc = BeginPaint(Handle, out ps);
                    try
                    {
                        using (Graphics graphics = Graphics.FromHdc(hdc))
                            PaintTextBox(graphics);
                    }
                    finally
                    {
                        EndPaint(Handle, ref ps);
                    }
                    return;
                }

                base.WndProc(ref m);
            }

            private void PaintTextBox(Graphics graphics)
            {
                PaintInheritedThemeBackground(graphics, this);
                DrawTextureBar(graphics, new Rectangle(0, 0, Width, Math.Min(26, Height)), Focused, false, false);
                Rectangle textBounds = new Rectangle(7, 0, Math.Max(1, Width - 14), Math.Min(26, Height));
                DrawShadowedText(graphics, Text, Font, textBounds, Enabled ? ThemeText : ThemeMutedText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (Focused && !ReadOnly)
                {
                    int caretX = textBounds.Left + TextRenderer.MeasureText(Text.Substring(0, Math.Min(SelectionStart, Text.Length)), Font).Width - 2;
                    caretX = Math.Max(textBounds.Left, Math.Min(textBounds.Right - 1, caretX));
                    using (Pen pen = new Pen(ThemeText))
                        graphics.DrawLine(pen, caretX, 6, caretX, Math.Min(20, Height - 4));
                }
            }
        }

        private sealed class TexturedComboBox : ComboBox
        {
            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;
            private bool _hovering;

            public TexturedComboBox()
            {
                FlatStyle = FlatStyle.Flat;
                BackColor = ThemeInputBack;
                ForeColor = ThemeText;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hovering = true;
                base.OnMouseEnter(e);
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hovering = false;
                base.OnMouseLeave(e);
                Invalidate();
            }

            protected override void OnSelectedIndexChanged(EventArgs e)
            {
                base.OnSelectedIndexChanged(e);
                Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_ERASEBKGND)
                {
                    m.Result = new IntPtr(1);
                    return;
                }

                if (m.Msg != WM_PAINT)
                {
                    base.WndProc(ref m);
                    return;
                }

                PAINTSTRUCT ps;
                IntPtr hdc = BeginPaint(Handle, out ps);
                try
                {
                    using (Graphics graphics = Graphics.FromHdc(hdc))
                    {
                        Rectangle bounds = new Rectangle(0, 0, Width, Math.Min(26, Height));
                        PaintInheritedThemeBackground(graphics, this);
                        DrawTextureBar(graphics, bounds, Focused || _hovering, true, _hovering);
                        Rectangle textBounds = new Rectangle(7, 0, Math.Max(1, Width - 38), bounds.Height);
                        DrawShadowedText(graphics, Text, Font, textBounds, Enabled ? ThemeText : ThemeMutedText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                }
                finally
                {
                    EndPaint(Handle, ref ps);
                }
            }
        }

        private sealed class ThemedButton : Button
        {
            private bool _hovering;

            public ThemedButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                ForeColor = Color.Transparent;
                FlatStyle = FlatStyle.Flat;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hovering = true;
                base.OnMouseEnter(e);
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hovering = false;
                base.OnMouseLeave(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (e == null)
                    return;

                PaintInheritedThemeBackground(e.Graphics, this);
                DrawTextureBar(e.Graphics, new Rectangle(0, 0, Width, Math.Min(26, Height)), Enabled && _hovering, false, false);
                DrawShadowedText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ThemeText : ThemeMutedText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private sealed class ThemedCheckBox : CheckBox
        {
            public ThemedCheckBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                ForeColor = ThemeText;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (e == null)
                    return;

                base.OnPaintBackground(e);
                ButtonState state = Checked ? ButtonState.Checked : ButtonState.Normal;
                if (!Enabled)
                    state |= ButtonState.Inactive;

                Rectangle checkBounds = new Rectangle(0, Math.Max(0, (Height - 13) / 2), 13, 13);
                ControlPaint.DrawCheckBox(e.Graphics, checkBounds, state);
                Rectangle textBounds = new Rectangle(20, 0, Math.Max(1, Width - 20), Height);
                DrawShadowedText(e.Graphics, Text, Font, textBounds, Enabled ? ThemeText : ThemeMutedText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private sealed class ThemedTabControl : TabControl
        {
            private const int WM_PAINT = 0x000F;
            private readonly Image _backgroundImage;

            public ThemedTabControl(Image backgroundImage)
            {
                _backgroundImage = backgroundImage;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                PaintThemeBackground(e.Graphics, ClientRectangle);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_PAINT || Width <= 0 || Height <= 0)
                    return;

                using (Graphics graphics = Graphics.FromHwnd(Handle))
                    PaintNativeBorderCover(graphics);
            }

            private void PaintThemeBackground(Graphics graphics, Rectangle bounds)
            {
                if (graphics == null || bounds.Width <= 0 || bounds.Height <= 0)
                    return;

                if (_backgroundImage == null)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(9, 45, 67)))
                        graphics.FillRectangle(brush, bounds);
                    return;
                }

                DrawViewportBackground(graphics, bounds, _backgroundImage, false);
            }

            private void PaintNativeBorderCover(Graphics graphics)
            {
                Rectangle display = DisplayRectangle;
                if (display.Width <= 0 || display.Height <= 0)
                    return;

                int stripBottom = Math.Max(0, display.Top);
                int tabRight = 0;
                for (int i = 0; i < TabCount; i++)
                    tabRight = Math.Max(tabRight, GetTabRect(i).Right);

                if (stripBottom > 0 && tabRight < Width)
                    PaintThemeBackgroundClipped(graphics, new Rectangle(tabRight, 0, Width - tabRight, stripBottom + 2));

                using (Pen borderPen = new Pen(Color.FromArgb(8, 27, 39)))
                {
                    for (int offset = 0; offset < 4; offset++)
                    {
                        graphics.DrawRectangle(
                            borderPen,
                            display.Left - 3 + offset,
                            display.Top - 3 + offset,
                            display.Width + 5 - (offset * 2),
                            display.Height + 5 - (offset * 2));
                    }
                }
            }

            private void PaintThemeBackgroundClipped(Graphics graphics, Rectangle clipBounds)
            {
                if (graphics == null || clipBounds.Width <= 0 || clipBounds.Height <= 0)
                    return;

                using (Region oldClip = graphics.Clip.Clone())
                {
                    graphics.SetClip(clipBounds);
                    PaintThemeBackground(graphics, ClientRectangle);
                    graphics.Clip = oldClip;
                }
            }
        }

        private sealed class ThemedListView : ListView
        {
            private const int WM_PAINT = 0x000F;
            private readonly Image _backgroundImage;

            public ThemedListView(Image backgroundImage)
            {
                _backgroundImage = backgroundImage;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            }

            public void PaintThemeBackground(Graphics graphics, Rectangle bounds)
            {
                if (graphics == null || bounds.Width <= 0 || bounds.Height <= 0)
                    return;

                using (Region oldClip = graphics.Clip.Clone())
                {
                    graphics.SetClip(bounds);
                    if (_backgroundImage == null)
                    {
                        using (Brush brush = new SolidBrush(Color.FromArgb(18, 30, 38)))
                            graphics.FillRectangle(brush, bounds);
                    }
                    else
                        DrawViewportBackground(graphics, ClientRectangle, _backgroundImage, false);

                    graphics.Clip = oldClip;
                }
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_PAINT || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                    return;

                int top = GetContentFillTop();
                if (top >= ClientSize.Height)
                    return;

                using (Graphics graphics = Graphics.FromHwnd(Handle))
                    PaintThemeBackground(graphics, new Rectangle(0, top, ClientSize.Width, ClientSize.Height - top));
            }

            private int GetContentFillTop()
            {
                int top = View == View.Details && Columns.Count > 0 ? 22 : 0;
                if (Items.Count == 0)
                    return top;

                try
                {
                    Rectangle lastBounds = Items[Items.Count - 1].Bounds;
                    top = Math.Max(top, lastBounds.Bottom);
                }
                catch
                {
                }

                return top;
            }
        }

        private sealed class OperationButton : Control
        {
            private readonly Image _normalImage;
            private readonly Image _hoverImage;
            private bool _isHovering;

            public OperationButton(string caption, Image normalBackground, Image hoverBackground, Image icon, Font font)
            {
                Font = font ?? new Font("MS Sans Serif", 8.25f, FontStyle.Regular);
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

                if (_isHovering)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
                        e.Graphics.FillRectangle(brush, new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2)));
                    using (Pen pen = new Pen(Color.FromArgb(210, 174, 232, 255)))
                        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
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

            public EzModeWheel(Font font)
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                Font = font ?? new Font("MS Sans Serif", 8.25f);
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
                    using (Font titleFont = new Font(Font.FontFamily, 10f * textScale, FontStyle.Bold))
                    using (Font subtitleFont = new Font(Font.FontFamily, 8.25f * textScale))
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
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                Font = new Font("Arial", 9f);
                BackColor = Color.Transparent;
                ForeColor = ThemeText;
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
                Rectangle bounds = new Rectangle(0, 0, Width, Math.Min(26, Height));
                DrawTextureBar(e.Graphics, bounds, false, false, false);

                int fillWidth = (int)Math.Round(Width * (_value / 100.0));
                if (fillWidth > 0)
                {
                    using (Region oldClip = e.Graphics.Clip.Clone())
                    {
                        e.Graphics.SetClip(new Rectangle(0, 0, Math.Min(Width, fillWidth), Height));
                        DrawTextureBar(e.Graphics, bounds, true, false, false);
                        e.Graphics.Clip = oldClip;
                    }
                }

                DrawShadowedText(
                    e.Graphics,
                    _value.ToString() + "%",
                    Font,
                    bounds,
                    ThemeText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void RefreshDrives()
        {
            try
            {
                IList<DiscRecorderInfo> recorders = _burningService.GetRecorders();
                _driveCombo.Items.Clear();
                _buildBurnDriveCombo.Items.Clear();
                _burnDriveCombo.Items.Clear();
                _copyDriveCombo.Items.Clear();
                _eraseDriveCombo.Items.Clear();
                _driveList.Items.Clear();

                foreach (DiscRecorderInfo recorder in recorders)
                {
                    _driveCombo.Items.Add(recorder);
                    _buildBurnDriveCombo.Items.Add(recorder);
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
                if (_buildBurnDriveCombo.Items.Count > 0)
                    _buildBurnDriveCombo.SelectedIndex = 0;
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
            bool placeFolderContentsAtRoot = _folderPlacementCombo == null || _folderPlacementCombo.SelectedIndex == 0;

            if (_buildItemList.Items.Count == 0 || output.Length == 0)
            {
                MessageBox.Show(this, "Add files or folders and choose an output image path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunWork("Building image...", delegate
            {
                string stagingPath = CreateBuildStagingFolder(placeFolderContentsAtRoot);
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

        private void BuildAndBurnImage()
        {
            string output = _buildOutputText.Text.Trim();
            string volume = _volumeNameText.Text.Trim();
            bool temporaryOutput = false;
            DiscRecorderInfo recorder = _buildBurnDriveCombo == null ? null : _buildBurnDriveCombo.SelectedItem as DiscRecorderInfo;
            string recorderId = recorder == null ? string.Empty : recorder.Id;
            string burnMethod = "Auto";
            string writeSpeed = GetSelectedBurnSpeed(_buildBurnSpeedCombo);
            int copies = GetSelectedBurnCopies(_buildBurnCopiesCombo);
            bool eject = _ejectAfterBurnCheck == null || _ejectAfterBurnCheck.Checked;
            bool verifyAfter = _verifyAfterBurnCheck != null && _verifyAfterBurnCheck.Checked;
            bool handedOffToWindowsBurner = false;
            bool placeFolderContentsAtRoot = _folderPlacementCombo == null || _folderPlacementCombo.SelectedIndex == 0;
            string folderPlacement = _folderPlacementCombo == null ? "Disc root" : Convert.ToString(_folderPlacementCombo.SelectedItem);

            if (_buildItemList.Items.Count == 0)
            {
                MessageBox.Show(this, "Add files or folders first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (recorder == null)
            {
                MessageBox.Show(this, "Choose a target drive first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (output.Length == 0)
            {
                string folder = Path.Combine(Path.GetTempPath(), "LuxBurn");
                Directory.CreateDirectory(folder);
                output = Path.Combine(folder, "LuxBurnBuild_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".iso");
                temporaryOutput = true;
            }

            _burnImageText.Text = output;
            _verifyFileText.Text = output;
            if (_burnDriveCombo != null && recorder != null)
                _burnDriveCombo.SelectedItem = recorder;

            ResetBurnProgress();
            _burnCancellation = new CancellationTokenSource();
            _burnInProgress = true;
            SetBusy(true, "Building image...");

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                string stagingPath = CreateBuildStagingFolder(placeFolderContentsAtRoot);
                try
                {
                    Log("Building image from " + _buildItemList.Items.Count + " source item(s).");
                    Log("Folder placement: " + folderPlacement + ".");
                    _burningService.BuildIso(stagingPath, output, volume);
                    _burnCancellation.Token.ThrowIfCancellationRequested();

                    bool usingExternalBurner = _burningService.WillUseWindowsDiscImageBurner(burnMethod);
                    handedOffToWindowsBurner = usingExternalBurner;
                    Log("Burning built image: " + output);
                    Log("Target drive: " + recorder.DisplayName);
                    Log("Burn backend: " + (usingExternalBurner ? "Windows Disc Image Burner" : "cdrecord"));
                    if (usingExternalBurner && copies > 1)
                        throw new InvalidOperationException("Multiple copies require the cdrecord backend.");

                    for (int copyNumber = 1; copyNumber <= copies; copyNumber++)
                    {
                        _burnCancellation.Token.ThrowIfCancellationRequested();
                        if (copyNumber > 1 && !ConfirmNextBurnCopy(copyNumber, copies))
                            throw new OperationCanceledException();

                        if (copies > 1)
                            Log("Starting burn copy " + copyNumber + " of " + copies + ".");

                        _burningService.BurnImage(output, recorderId, eject || copies > 1, burnMethod, writeSpeed, Log, UpdateBurnProgress, _burnCancellation.Token);
                    }

                    if (!usingExternalBurner && verifyAfter)
                    {
                        string hash = ChecksumService.ComputeFileHash(output, "SHA256");
                        Log("Source image SHA-256 after burn: " + hash);
                    }

                    e.Result = usingExternalBurner;
                }
                finally
                {
                    TryDeleteDirectory(stagingPath);
                    if (temporaryOutput && !handedOffToWindowsBurner && File.Exists(output))
                    {
                        try { File.Delete(output); }
                        catch { }
                    }
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                _burnInProgress = false;
                _burnCancellation = null;
                SetBusy(false, "Ready");
                CalculateBuildImageInformation();

                if (temporaryOutput && !handedOffToWindowsBurner)
                {
                    _burnImageText.Text = string.Empty;
                    _verifyFileText.Text = string.Empty;
                }

                if (e.Error != null)
                {
                    if (e.Error is OperationCanceledException)
                    {
                        Log("Build and burn cancelled.");
                        SetStatus("Build and burn cancelled.");
                        return;
                    }

                    Log("Operation failed: " + e.Error.GetType().Name + ": " + e.Error.Message);
                    if (e.Error.InnerException != null)
                        Log("Original error: " + e.Error.InnerException.GetType().Name + ": " + e.Error.InnerException.Message);

                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                bool usingExternalBurner = Convert.ToBoolean(e.Result);
                Log(usingExternalBurner ? "Burn window opened for built image." : "Build and burn completed.");
                if (usingExternalBurner && temporaryOutput)
                    Log("Temporary ISO kept for Windows Disc Image Burner: " + output);
                if (!usingExternalBurner)
                    PlaySuccessSound();
            };
            worker.RunWorkerAsync();
        }

        private void BurnImage()
        {
            string image = _burnImageText.Text.Trim();
            DiscRecorderInfo recorder = _burnDriveCombo.SelectedItem as DiscRecorderInfo;
            string recorderId = recorder == null ? string.Empty : recorder.Id;
            string burnMethod = "Auto";
            string writeSpeed = GetSelectedBurnSpeed(_burnSpeedCombo);
            int copies = GetSelectedBurnCopies(_burnCopiesCombo);
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
                if (usingExternalBurner && copies > 1)
                    throw new InvalidOperationException("Multiple copies require the cdrecord backend.");

                for (int copyNumber = 1; copyNumber <= copies; copyNumber++)
                {
                    _burnCancellation.Token.ThrowIfCancellationRequested();
                    if (copyNumber > 1 && !ConfirmNextBurnCopy(copyNumber, copies))
                        throw new OperationCanceledException();

                    if (copies > 1)
                        Log("Starting burn copy " + copyNumber + " of " + copies + ".");

                    _burningService.BurnImage(image, recorderId, eject || copies > 1, burnMethod, writeSpeed, Log, UpdateBurnProgress, _burnCancellation.Token);
                }

                if (!usingExternalBurner && verifyAfter)
                {
                    string hash = ChecksumService.ComputeFileHash(image, "SHA256");
                    Log("Source image SHA-256 after burn: " + hash);
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
            if (_buildAndBurnButton != null)
                _buildAndBurnButton.Enabled = !busy;
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

                try
                {
                    ValidateUpdateInfo(update);
                }
                catch (Exception ex)
                {
                    SetStatus("Ready");
                    Log("Update manifest rejected: " + ex.Message);
                    if (manual)
                        MessageBox.Show(this, "Could not check for updates." + Environment.NewLine + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                ConfigureNoCacheRequest(client);
                string json = client.DownloadString(MakeUncachedUrl(UpdateManifestUrl));
                return UpdateInfo.FromJson(json);
            }
        }

        private void ShowUpdatePrompt(UpdateInfo update, Version running, Version latest)
        {
            using (UpdateDialog dialog = new UpdateDialog(FormatVersion(running), FormatVersion(latest), Font))
            {
                DialogResult result = dialog.ShowDialog(this);
                if (result == DialogResult.Yes)
                    OpenUpdateReleasePage(update);
                else if (result == DialogResult.Retry)
                    SaveUpdateReminder(DateTime.UtcNow.AddDays(7));
                else
                    SetStatus("Ready");
            }
        }

        private void OpenUpdateReleasePage(UpdateInfo update)
        {
            if (_burnInProgress)
            {
                MessageBox.Show(this, "A disc operation is in progress. Finish or cancel it before opening the update page.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string page = IsTrustedReleasePageUrl(update.ReleasePageUrl) ? update.ReleasePageUrl : "https://github.com/sccpsteve/LuxBurn/releases/tag/latest";
                Process.Start(page);
                SetStatus("Update page opened.");
            }
            catch (Exception ex)
            {
                SetStatus("Ready");
                Log("Could not open update page: " + ex.Message);
                MessageBox.Show(this, "Could not open the update page." + Environment.NewLine + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ValidateUpdateInfo(UpdateInfo update)
        {
            if (update == null)
                throw new InvalidOperationException("The update manifest was empty.");

            if (!IsTrustedUpdateAssetUrl(update.InstallerUrl, "-setup.exe") ||
                !IsTrustedUpdateAssetUrl(update.PortableUrl, "-portable.zip") ||
                !IsTrustedReleasePageUrl(update.ReleasePageUrl))
            {
                throw new InvalidOperationException("The update manifest points outside the official LuxBurn GitHub release.");
            }

            if (!IsValidSha256(update.InstallerSha256) || !IsValidSha256(update.PortableSha256))
                throw new InvalidOperationException("The update manifest is missing release SHA-256 hashes.");
        }

        private static bool IsTrustedUpdateAssetUrl(string value, string suffix)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return false;

            string url = uri.AbsoluteUri;
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                url.StartsWith(TrustedUpdatePrefix, StringComparison.OrdinalIgnoreCase) &&
                url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrustedReleasePageUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return false;

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith("/sccpsteve/LuxBurn/releases/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSha256(string value)
        {
            return Regex.IsMatch(value ?? string.Empty, "^[0-9a-fA-F]{64}$");
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

        private static void ConfigureNoCacheRequest(WebClient client)
        {
            client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            client.Headers.Add("User-Agent", "LuxBurn/" + FormatVersion(GetRunningVersion()));
            client.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            client.Headers.Add("Pragma", "no-cache");
            client.Headers.Add("Expires", "0");
        }

        private static string MakeUncachedUrl(string url)
        {
            string separator = url.IndexOf('?') >= 0 ? "&" : "?";
            string stamp = DateTime.UtcNow.Ticks.ToString() + "-" + Guid.NewGuid().ToString("N");
            return url + separator + "luxburn_nocache=" + stamp;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public Rectangle rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

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
                dialog.Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }
    }
}

