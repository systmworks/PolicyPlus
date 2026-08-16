using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace PolicyPlus
{
    // One entry per selectable color theme. ThemeService.Apply uses Base to pick WPF-UI's
    // underlying Light/Dark token set, Accent to drive WPF-UI's accent-color system (title bar,
    // primary buttons, toggle switches, checkboxes), and the remaining five colors to override
    // the handful of WPF-UI tokens the app's own styles bind to (WpfInterop.cs,
    // AddOpaqueListSurfaceStyles) - surface fill, hover fill, selected-row fill, secondary text,
    // and border stroke.
    internal sealed class ThemeOption
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ApplicationTheme Base { get; }
        public Color Background { get; }
        public Color TextPrimary { get; }
        public Color TextSecondary { get; }
        public Color Surface { get; }
        public Color SurfaceHover { get; }
        public Color Accent { get; }
        public Color Border { get; }

        public ThemeOption(
            string id,
            string displayName,
            ApplicationTheme baseTheme,
            string background,
            string textPrimary,
            string textSecondary,
            string surface,
            string surfaceHover,
            string accent,
            string border)
        {
            Id = id;
            DisplayName = displayName;
            Base = baseTheme;
            Background = (Color)ColorConverter.ConvertFromString(background);
            TextPrimary = (Color)ColorConverter.ConvertFromString(textPrimary);
            TextSecondary = (Color)ColorConverter.ConvertFromString(textSecondary);
            Surface = (Color)ColorConverter.ConvertFromString(surface);
            SurfaceHover = (Color)ColorConverter.ConvertFromString(surfaceHover);
            Accent = (Color)ColorConverter.ConvertFromString(accent);
            Border = (Color)ColorConverter.ConvertFromString(border);
        }

        // Selected-row fill: the theme's accent blended at low opacity over its background, so
        // the highlight (CHANGELOG [1.17] fixed this being illegible in plain Dark mode) reads as
        // "this theme's accent" instead of a generic gray in every theme.
        public Color SelectedFill => BlendOverBackground(Accent, 0.28);

        private Color BlendOverBackground(Color foreground, double amount)
        {
            byte Blend(byte bg, byte fg) => (byte)(bg + (fg - bg) * amount);
            return Color.FromRgb(
                Blend(Background.R, foreground.R),
                Blend(Background.G, foreground.G),
                Blend(Background.B, foreground.B));
        }
    }

    internal static class ThemeDefinitions
    {
        public static readonly ThemeOption[] All =
        {
            new ThemeOption(
                id: "SolarizedLight",
                displayName: "(Light) Solarized Light",
                baseTheme: ApplicationTheme.Light,
                background: "#FDF6E3",
                textPrimary: "#586E75",
                textSecondary: "#93A1A1",
                surface: "#EEE8D5",
                surfaceHover: "#E3DCC6",
                accent: "#268BD2",
                border: "#D3CBB0"),
            new ThemeOption(
                id: "GruvboxLight",
                displayName: "(Light) Gruvbox Light",
                baseTheme: ApplicationTheme.Light,
                background: "#FBF1C7",
                textPrimary: "#3C3836",
                textSecondary: "#7C6F64",
                surface: "#EBDBB2",
                surfaceHover: "#E0CFA0",
                accent: "#D65D0E",
                border: "#D5C4A1"),
            new ThemeOption(
                id: "CatppuccinLatte",
                displayName: "(Light) Catppuccin Latte",
                baseTheme: ApplicationTheme.Light,
                background: "#EFF1F5",
                textPrimary: "#4C4F69",
                textSecondary: "#6C6F85",
                surface: "#E6E9EF",
                surfaceHover: "#DCE0E8",
                accent: "#8839EF",
                border: "#CCD0DA"),
            new ThemeOption(
                id: "Nord",
                displayName: "(Dark) Nord",
                baseTheme: ApplicationTheme.Dark,
                background: "#2E3440",
                textPrimary: "#ECEFF4",
                textSecondary: "#D8DEE9",
                surface: "#3B4252",
                surfaceHover: "#434C5E",
                accent: "#88C0D0",
                border: "#4C566A"),
            new ThemeOption(
                id: "Dracula",
                displayName: "(Dark) Dracula",
                baseTheme: ApplicationTheme.Dark,
                background: "#282A36",
                textPrimary: "#F8F8F2",
                textSecondary: "#BFBFD4",
                surface: "#343746",
                surfaceHover: "#44475A",
                accent: "#BD93F9",
                border: "#6272A4"),
            new ThemeOption(
                id: "RosePine",
                displayName: "(Dark) Rosé Pine",
                baseTheme: ApplicationTheme.Dark,
                background: "#191724",
                textPrimary: "#E0DEF4",
                textSecondary: "#908CAA",
                surface: "#1F1D2E",
                surfaceHover: "#26233A",
                accent: "#EB6F92",
                border: "#403D52"),
        };

        public static ThemeOption Find(string id)
        {
            foreach (var option in All)
            {
                if (option.Id == id)
                {
                    return option;
                }
            }

            return null;
        }
    }
}
