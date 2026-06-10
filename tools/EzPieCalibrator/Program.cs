// Disposable LuxBurn EZ Mode pie calibrator.
// Builds with .NET Framework 4 and reads the real LuxBurn assets from this repo.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EzPieCalibrator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CalibratorForm());
        }
    }

    internal sealed class CalibratorForm : Form
    {
        private readonly string _root;
        private readonly PreviewPanel _preview;
        private readonly List<SliceState> _slices = new List<SliceState>();

        public CalibratorForm()
        {
            Text = "LuxBurn EZ Pie Calibrator";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 640);
            MinimumSize = new Size(880, 560);
            Font = new Font("MS Sans Serif", 8.25f);

            _root = FindRepoRoot();
            InitializeSlices();

            _preview = new PreviewPanel(_slices);
            _preview.Location = new Point(12, 12);
            _preview.Size = new Size(430, 420);
            _preview.BackColor = Color.FromArgb(20, 62, 85);
            Controls.Add(_preview);

            Button export = new Button();
            export.Text = "Export Patch Lines";
            export.Location = new Point(12, 448);
            export.Size = new Size(140, 28);
            export.Click += delegate { ExportPatchLines(); };
            Controls.Add(export);

            Button reset = new Button();
            reset.Text = "Reset Current";
            reset.Location = new Point(158, 448);
            reset.Size = new Size(110, 28);
            reset.Click += delegate { ResetSelectedSlice(); };
            Controls.Add(reset);

            Label hint = new Label();
            hint.Text = "Use the number fields, or focus a field and press Up/Down. Export writes ez-pie-offsets.txt and copies it.";
            hint.Location = new Point(12, 486);
            hint.Size = new Size(560, 36);
            Controls.Add(hint);

            Panel fields = new Panel();
            fields.Location = new Point(456, 12);
            fields.Size = new Size(500, 560);
            fields.AutoScroll = true;
            Controls.Add(fields);

            int y = 0;
            foreach (SliceState slice in _slices)
            {
                GroupBox group = CreateSliceGroup(slice, y);
                fields.Controls.Add(group);
                y += group.Height + 8;
            }
        }

        private void InitializeSlices()
        {
            _slices.Add(new SliceState("Build", "Create image", "pie_6_1.png", "pie_6_1_O.png", "Build-Disc.png", new Point(30, -9), new Point(102, 31), 100, new Rectangle(92, 85, 92, 42), 100));
            _slices.Add(new SliceState("Write", "Burn image", "pie_6_2.png", "pie_6_2_O.png", "Write-Disc.png", new Point(197, -9), new Point(232, 31), 100, new Rectangle(216, 85, 92, 42), 100));
            _slices.Add(new SliceState("Copy", "Read disc", "pie_6_3.png", "pie_6_3_O.png", "Copy-To-Folder.png", new Point(245, 94), new Point(294, 147), 100, new Rectangle(284, 194, 92, 42), 100));
            _slices.Add(new SliceState("Verify", "Check image", "pie_6_4.png", "pie_6_4_O.png", "Verify-Disc.png", new Point(197, 219), new Point(232, 262), 100, new Rectangle(217, 312, 92, 42), 100));
            _slices.Add(new SliceState("Erase", "Blank disc", "pie_6_5.png", "pie_6_5_O.png", "Erase-Disc.png", new Point(30, 219), new Point(102, 262), 100, new Rectangle(95, 312, 92, 42), 100));
            _slices.Add(new SliceState("Drives", "Inspect", "pie_6_6.png", "pie_6_6_O.png", "Drives.png", new Point(-1, 94), new Point(37, 150), 55, new Rectangle(24, 197, 92, 42), 100));

            foreach (SliceState slice in _slices)
            {
                slice.NormalImage = LoadImage("Ui", slice.NormalFile);
                slice.HoverImage = LoadImage("Ui", slice.HoverFile);
                slice.IconImage = LoadImage("Buttons", slice.IconFile);
                slice.SaveDefaults();
            }
        }

        private GroupBox CreateSliceGroup(SliceState slice, int y)
        {
            GroupBox group = new GroupBox();
            group.Text = slice.Name;
            group.Location = new Point(0, y);
            group.Size = new Size(470, 122);

            int row = 20;
            AddNumber(group, slice, "Pie X", 12, row, slice.ImageLocation.X, delegate(int value) { slice.ImageLocation = new Point(value, slice.ImageLocation.Y); });
            AddNumber(group, slice, "Pie Y", 142, row, slice.ImageLocation.Y, delegate(int value) { slice.ImageLocation = new Point(slice.ImageLocation.X, value); });
            AddNumber(group, slice, "Icon X", 272, row, slice.IconLocation.X, delegate(int value) { slice.IconLocation = new Point(value, slice.IconLocation.Y); });

            row += 32;
            AddNumber(group, slice, "Icon Y", 12, row, slice.IconLocation.Y, delegate(int value) { slice.IconLocation = new Point(slice.IconLocation.X, value); });
            AddNumber(group, slice, "Icon %", 142, row, slice.IconScale, delegate(int value) { slice.IconScale = value; });
            AddNumber(group, slice, "Text X", 272, row, slice.LabelBounds.X, delegate(int value) { slice.LabelBounds = new Rectangle(value, slice.LabelBounds.Y, slice.LabelBounds.Width, slice.LabelBounds.Height); });

            row += 32;
            AddNumber(group, slice, "Text Y", 12, row, slice.LabelBounds.Y, delegate(int value) { slice.LabelBounds = new Rectangle(slice.LabelBounds.X, value, slice.LabelBounds.Width, slice.LabelBounds.Height); });
            AddNumber(group, slice, "Text %", 142, row, slice.TextScale, delegate(int value) { slice.TextScale = value; });

            Button select = new Button();
            select.Text = "Focus";
            select.Location = new Point(352, row - 1);
            select.Size = new Size(82, 24);
            select.Click += delegate { _preview.SelectedSlice = slice; _preview.Invalidate(); };
            group.Controls.Add(select);

            return group;
        }

        private void AddNumber(Control parent, SliceState slice, string labelText, int x, int y, int value, Action<int> changed)
        {
            Label label = new Label();
            label.Text = labelText;
            label.Location = new Point(x, y + 4);
            label.Size = new Size(50, 18);
            parent.Controls.Add(label);

            NumericUpDown input = new NumericUpDown();
            input.Location = new Point(x + 52, y);
            input.Size = new Size(62, 22);
            input.Minimum = -500;
            input.Maximum = 500;
            input.Value = value;
            input.Tag = slice;
            input.Enter += delegate { _preview.SelectedSlice = slice; _preview.Invalidate(); };
            input.ValueChanged += delegate
            {
                changed((int)input.Value);
                _preview.Invalidate();
            };
            parent.Controls.Add(input);
        }

        private void ResetSelectedSlice()
        {
            SliceState selected = _preview.SelectedSlice;
            if (selected == null)
                return;

            selected.ResetDefaults();
            RebuildFieldPanel();
            _preview.Invalidate();
        }

        private void RebuildFieldPanel()
        {
            Control fields = Controls[Controls.Count - 1];
            fields.Controls.Clear();
            int y = 0;
            foreach (SliceState slice in _slices)
            {
                GroupBox group = CreateSliceGroup(slice, y);
                fields.Controls.Add(group);
                y += group.Height + 8;
            }
        }

        private void ExportPatchLines()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("// LuxBurn EZ Mode offsets");
            output.AppendLine("// Paste these values into MainForm.cs.");
            foreach (SliceState slice in _slices)
            {
                output.AppendLine(slice.Name + ": pie = new Point(" + slice.ImageLocation.X + ", " + slice.ImageLocation.Y + "), icon = new Point(" + slice.IconLocation.X + ", " + slice.IconLocation.Y + "), iconScale = " + slice.IconScale + ", text = new Point(" + slice.LabelBounds.X + ", " + slice.LabelBounds.Y + "), textScale = " + slice.TextScale);
            }

            output.AppendLine();
            output.AppendLine("Patch-ready AddSlice lines:");
            foreach (SliceState slice in _slices)
            {
                output.AppendLine(slice.Name + " imageLocation = new Point(" + slice.ImageLocation.X + ", " + slice.ImageLocation.Y + "), iconLocation = new Point(" + slice.IconLocation.X + ", " + slice.IconLocation.Y + "), iconScale = " + slice.IconScale + ", labelBounds = new Rectangle(" + slice.LabelBounds.X + ", " + slice.LabelBounds.Y + ", " + slice.LabelBounds.Width + ", " + slice.LabelBounds.Height + "), textScale = " + slice.TextScale + ";");
            }

            string text = output.ToString();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ez-pie-offsets.txt");
            File.WriteAllText(path, text);
            Clipboard.SetText(text);
            MessageBox.Show(this, "Exported and copied to clipboard:" + Environment.NewLine + path, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Image LoadImage(string folder, string file)
        {
            string path = Path.Combine(_root, "LuxBurn", "Assets", folder, file);
            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "LuxBurn", "Assets")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }

    internal sealed class PreviewPanel : Panel
    {
        private readonly List<SliceState> _slices;
        public SliceState SelectedSlice;

        public PreviewPanel(List<SliceState> slices)
        {
            _slices = slices;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SelectedSlice = slices.Count > 0 ? slices[0] : null;
            Font = new Font("MS Sans Serif", 8.25f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            foreach (SliceState slice in _slices)
            {
                Image image = slice == SelectedSlice && slice.HoverImage != null ? slice.HoverImage : slice.NormalImage;
                if (image != null)
                    e.Graphics.DrawImageUnscaled(image, slice.ImageLocation);
            }

            foreach (SliceState slice in _slices)
            {
                if (slice.IconImage != null)
                    DrawScaledImage(e.Graphics, slice.IconImage, slice.IconLocation, slice.IconScale);

                float scale = Math.Max(50, Math.Min(180, slice.TextScale)) / 100f;
                using (Font titleFont = new Font(Font.FontFamily, 10f * scale, FontStyle.Bold))
                using (Font subtitleFont = new Font(Font.FontFamily, 8.25f * scale))
                {
                    Color title = slice == SelectedSlice ? Color.White : Color.FromArgb(232, 244, 248);
                    Color subtitle = slice == SelectedSlice ? Color.White : Color.FromArgb(205, 226, 235);
                    DrawOutlinedText(e.Graphics, slice.Name, titleFont, slice.LabelBounds, title);
                    DrawOutlinedText(e.Graphics, slice.Subtitle, subtitleFont, new Rectangle(slice.LabelBounds.X, slice.LabelBounds.Y + 22, slice.LabelBounds.Width, 20), subtitle);
                }
            }

            if (SelectedSlice != null)
            {
                using (Pen pen = new Pen(Color.White))
                    e.Graphics.DrawRectangle(pen, SelectedSlice.LabelBounds);
            }
        }

        private static void DrawScaledImage(Graphics graphics, Image image, Point location, int scale)
        {
            int clampedScale = Math.Max(10, Math.Min(200, scale));
            int width = Math.Max(1, (int)Math.Round(image.Width * clampedScale / 100.0));
            int height = Math.Max(1, (int)Math.Round(image.Height * clampedScale / 100.0));
            graphics.DrawImage(image, new Rectangle(location.X, location.Y, width, height), new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
        }

        private static void DrawOutlinedText(Graphics graphics, string text, Font font, Rectangle bounds, Color color)
        {
            TextRenderer.DrawText(graphics, text, font, new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(graphics, text, font, bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class SliceState
    {
        public readonly string Name;
        public readonly string Subtitle;
        public readonly string NormalFile;
        public readonly string HoverFile;
        public readonly string IconFile;
        public Image NormalImage;
        public Image HoverImage;
        public Image IconImage;
        public Point ImageLocation;
        public Point IconLocation;
        public int IconScale;
        public Rectangle LabelBounds;
        public int TextScale;
        private Point _defaultImageLocation;
        private Point _defaultIconLocation;
        private int _defaultIconScale;
        private Rectangle _defaultLabelBounds;
        private int _defaultTextScale;

        public SliceState(string name, string subtitle, string normalFile, string hoverFile, string iconFile, Point imageLocation, Point iconLocation, int iconScale, Rectangle labelBounds, int textScale)
        {
            Name = name;
            Subtitle = subtitle;
            NormalFile = normalFile;
            HoverFile = hoverFile;
            IconFile = iconFile;
            ImageLocation = imageLocation;
            IconLocation = iconLocation;
            IconScale = iconScale;
            LabelBounds = labelBounds;
            TextScale = textScale;
        }

        public void SaveDefaults()
        {
            _defaultImageLocation = ImageLocation;
            _defaultIconLocation = IconLocation;
            _defaultIconScale = IconScale;
            _defaultLabelBounds = LabelBounds;
            _defaultTextScale = TextScale;
        }

        public void ResetDefaults()
        {
            ImageLocation = _defaultImageLocation;
            IconLocation = _defaultIconLocation;
            IconScale = _defaultIconScale;
            LabelBounds = _defaultLabelBounds;
            TextScale = _defaultTextScale;
        }
    }
}
