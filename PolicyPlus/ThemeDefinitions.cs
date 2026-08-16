using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace PolicyPlus
{
    // One entry per selectable color theme. ThemeService.Apply uses Base to pick WPF-UI's
    // underlying Light/Dark token set, Accent to drive WPF-UI's accent-color system (primary
    // buttons, toggle switches, checkboxes), and the remaining colors to override the WPF-UI
    // tokens the app's own styles bind to (WpfInterop.cs AddOpaqueListSurfaceStyles for the
    // tree/list/grid surfaces) plus the app-specific "chrome" brushes (AppChromeBackgroundBrush/
    // AppChromeTextBrush) used for the title bar, menu strip, and status bar in MainWindow.xaml -
    // those three regions have no WPF-UI-native theming of their own (StatusBar isn't a WPF-UI
    // control at all, and the Menu row/TitleBar are transparent by default so a Mica backdrop can
    // show through), so without an explicit theme-driven brush they stayed visually unchanged
    // across every theme.
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
        public Color Chrome { get; }
        public Color ChromeText { get; }

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
            string border,
            string chrome,
            string chromeText)
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
            Chrome = (Color)ColorConverter.ConvertFromString(chrome);
            ChromeText = (Color)ColorConverter.ConvertFromString(chromeText);
        }

        // Selected-row fill: the theme's accent blended at moderate opacity over its background,
        // so the highlight (CHANGELOG [1.17] fixed this being illegible in plain Dark mode) reads
        // as "this theme's accent" instead of a generic gray in every theme.
        public Color SelectedFill => BlendOverBackground(Accent, 0.35);

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
            // Bold cyan-blue, deep-navy text - Solarized's own darkest tone (base03) used for
            // text instead of its softer base01/base00, for stronger contrast.
            new ThemeOption(
                id: "SolarizedLight",
                displayName: "(Light) Blue",
                baseTheme: ApplicationTheme.Light,
                background: "#FDF6E3",
                textPrimary: "#073642",
                textSecondary: "#39586D",
                surface: "#E3D9B8",
                surfaceHover: "#D8CBA0",
                accent: "#268BD2",
                border: "#B8A878",
                chrome: "#1E6FA8",
                chromeText: "#FFFFFF"),
            // Bold burnt-orange, Gruvbox's own near-black text.
            new ThemeOption(
                id: "GruvboxLight",
                displayName: "(Light) Orange",
                baseTheme: ApplicationTheme.Light,
                background: "#F9E8B8",
                textPrimary: "#282828",
                textSecondary: "#4E4436",
                surface: "#E8CB86",
                surfaceHover: "#DBB765",
                accent: "#D65D0E",
                border: "#BFA25A",
                chrome: "#AF3A03",
                chromeText: "#FFF5DC"),
            // Bold violet, Catppuccin's own "crust" near-black text (much stronger than its
            // softer body-text tone) so this reads as vivid rather than pastel.
            new ThemeOption(
                id: "CatppuccinLatte",
                displayName: "(Light) Purple",
                baseTheme: ApplicationTheme.Light,
                background: "#E0E4EF",
                textPrimary: "#181825",
                textSecondary: "#3E4159",
                surface: "#C7CEE0",
                surfaceHover: "#B3BCD6",
                accent: "#8839EF",
                border: "#9AA6C7",
                chrome: "#7128C7",
                chromeText: "#FFFFFF"),
            // Arctic blue, pushed more saturated than Nord's usual muted frost tones for a bolder
            // "Tron" feel.
            new ThemeOption(
                id: "Nord",
                displayName: "(Dark) Blue",
                baseTheme: ApplicationTheme.Dark,
                background: "#242933",
                textPrimary: "#ECEFF4",
                textSecondary: "#B8C4D8",
                surface: "#3B4252",
                surfaceHover: "#4C566A",
                accent: "#5E9FCE",
                border: "#4C566A",
                chrome: "#3B6EA5",
                chromeText: "#ECEFF4"),
            // Dracula's vivid pink as the primary accent instead of its softer purple, for a
            // punchier identity; deep magenta chrome band.
            new ThemeOption(
                id: "Dracula",
                displayName: "(Dark) Pink",
                baseTheme: ApplicationTheme.Dark,
                background: "#21222C",
                textPrimary: "#F8F8F2",
                textSecondary: "#C6C6DD",
                surface: "#343746",
                surfaceHover: "#44475A",
                accent: "#FF79C6",
                border: "#6272A4",
                chrome: "#7A2E63",
                chromeText: "#F8F8F2"),
            // The red/rose dark theme - accent pushed toward a bolder, more saturated red than
            // Rose Pine's usual muted dusty rose; near-black background and a blood-red chrome
            // band for a deliberately bold identity.
            new ThemeOption(
                id: "RosePine",
                displayName: "(Dark) Red",
                baseTheme: ApplicationTheme.Dark,
                background: "#14121F",
                textPrimary: "#E0DEF4",
                textSecondary: "#B3AFD1",
                surface: "#1F1D2E",
                surfaceHover: "#26233A",
                accent: "#E13B5C",
                border: "#524A67",
                chrome: "#7A1E36",
                chromeText: "#F4E9EC"),
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
