using MailMerge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;
using System;
using System.Windows;

namespace MailMergeUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load last saved theme
            ThemeManager.InitializeTheme();
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }
}
