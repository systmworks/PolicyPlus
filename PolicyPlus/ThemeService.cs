using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace PolicyPlus
{
    internal static class ThemeService
    {
        public static readonly string[] AvailableThemes = { "Light", "Dark", "System" };

        private static readonly ConfigurationStorage Configuration =
            new ConfigurationStorage(RegistryHive.CurrentUser, @"Software\Policy Plus");

        public static string CurrentThemeName => (string)Configuration.GetValue("ColorMode", "System");

        public static void Apply(string themeName)
        {
            WpfInterop.EnsureApplication();
            switch (themeName)
            {
                case "Dark":
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
                case "Light":
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    break;
                default:
                    ApplicationThemeManager.ApplySystemTheme();
                    break;
            }
        }

        public static void ApplyPersisted()
        {
            Apply(CurrentThemeName);
        }

        public static void Persist(string themeName)
        {
            Configuration.SetValue("ColorMode", themeName);
            Apply(themeName);
        }
    }
}
