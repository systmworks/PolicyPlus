using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace PolicyPlus
{
    internal static class ThemeService
    {
        private const string DefaultThemeId = "SolarizedLight";

        public static readonly string[] AvailableThemes = System.Array.ConvertAll(ThemeDefinitions.All, t => t.Id);

        private static readonly ConfigurationStorage Configuration =
            new ConfigurationStorage(RegistryHive.CurrentUser, @"Software\Policy Plus");

        public static string CurrentThemeName => ResolveThemeId(Configuration.GetValue("ColorMode", DefaultThemeId) as string);

        // Installs upgrading from the old 3-way Light/Dark/System toggle have a "ColorMode"
        // value that no longer matches any ThemeOption.Id - map those (and anything else
        // unrecognized) to a sensible default instead of failing the ThemeDefinitions.Find below.
        private static string ResolveThemeId(string storedId)
        {
            if (ThemeDefinitions.Find(storedId) is not null)
            {
                return storedId;
            }

            return storedId == "Dark" ? "Nord" : DefaultThemeId;
        }

        public static void Apply(string themeId)
        {
            WpfInterop.EnsureApplication();
            var option = ThemeDefinitions.Find(themeId) ?? ThemeDefinitions.Find(DefaultThemeId);

            ApplicationThemeManager.Apply(option.Base);
            ApplicationAccentColorManager.Apply(option.Accent, option.Base, false, false);

            var resources = Application.Current.Resources;
            resources["ApplicationBackgroundBrush"] = Brush(option.Background);
            resources["TextFillColorPrimaryBrush"] = Brush(option.TextPrimary);
            resources["TextFillColorSecondaryBrush"] = Brush(option.TextSecondary);
            resources["ControlFillColorDefaultBrush"] = Brush(option.Surface);
            resources["ControlFillColorSecondaryBrush"] = Brush(option.SurfaceHover);
            resources["ControlFillColorTertiaryBrush"] = Brush(option.SelectedFill);
            resources["ControlStrokeColorDefaultBrush"] = Brush(option.Border);
        }

        private static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
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
