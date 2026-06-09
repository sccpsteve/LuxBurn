// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
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
            Application.Run(new MainForm());
        }
    }
}

