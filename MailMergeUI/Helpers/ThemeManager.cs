using System.Windows;

namespace MailMergeUI
{
    public static class ThemeManager
    {
        private static bool _isDarkMode = Properties.Settings.Default.IsDarkMode;
        public static bool IsDarkMode => _isDarkMode;

        public static void InitializeTheme()
        {
            if (_isDarkMode)
                ApplyDarkTheme();
            else
                ApplyLightTheme();
        }

        public static void ToggleTheme()
        {
            if (_isDarkMode)
                ApplyLightTheme();
            else
                ApplyDarkTheme();

            _isDarkMode = !_isDarkMode;

            Properties.Settings.Default.IsDarkMode = _isDarkMode;
            Properties.Settings.Default.Save();
        }

        public static void ApplyLightTheme()
        {
            var resources = Application.Current.Resources;

            resources["PrimaryBlue"] = resources["PrimaryBlue_Light"];
            resources["PrimaryBlueDark"] = resources["PrimaryBlueDark_Light"];
            resources["PrimaryBlueLight"] = resources["PrimaryBlueLight_Light"];
            resources["AccentBlue"] = resources["AccentBlue_Light"];
            resources["TextDark"] = resources["TextDark_Light"];
            resources["TextLight"] = resources["TextLight_Light"];
            resources["DividerColor"] = resources["DividerColor_Light"];
            resources["BackgroundLight"] = resources["BackgroundLight_Light"];
            resources["CardBackgroundLight"] = resources["CardBackgroundLight_Light"];
            resources["HeaderBackgroundLight"] = resources["HeaderBackgroundLight_Light"];
        }

        public static void ApplyDarkTheme()
        {
            var resources = Application.Current.Resources;

            resources["PrimaryBlue"] = resources["PrimaryBlue_Dark"];
            resources["PrimaryBlueDark"] = resources["PrimaryBlueDark_Dark"];
            resources["PrimaryBlueLight"] = resources["PrimaryBlueLight_Dark"];
            resources["AccentBlue"] = resources["AccentBlue_Dark"];
            resources["TextDark"] = resources["TextDark_Dark"];
            resources["TextLight"] = resources["TextLight_Dark"];
            resources["DividerColor"] = resources["DividerColor_Dark"];
            resources["BackgroundLight"] = resources["Background_Dark"];
            resources["CardBackgroundLight"] = resources["CardBackground_Dark"];
            resources["HeaderBackgroundLight"] = resources["HeaderBackground_Dark"];
        }
    }
}
