// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LuxBurn
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (SplashForm splash = new SplashForm())
            {
                splash.Show();
                splash.Refresh();
                Application.DoEvents();

                MainForm main = new MainForm();
                main.Shown += delegate { splash.Close(); };
                Application.Run(main);
            }
        }

        private sealed class SplashForm : Form
        {
            private Image _image;

            public SplashForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.CenterScreen;
                ShowInTaskbar = false;
                BackColor = Color.Black;

                _image = LoadSplashImage();
                if (_image == null)
                {
                    ClientSize = new Size(360, 180);
                    return;
                }

                ClientSize = _image.Size;
                PictureBox picture = new PictureBox();
                picture.Dock = DockStyle.Fill;
                picture.Image = _image;
                picture.SizeMode = PictureBoxSizeMode.Normal;
                Controls.Add(picture);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _image != null)
                {
                    _image.Dispose();
                    _image = null;
                }

                base.Dispose(disposing);
            }

            private static Image LoadSplashImage()
            {
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Brand", "LBSplash.png");
                    return File.Exists(path) ? Image.FromFile(path) : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}

