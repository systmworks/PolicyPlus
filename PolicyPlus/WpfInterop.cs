using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PolicyPlus
{
    internal static class WpfInterop
    {
        public static void EnsureApplication()
        {
            if (Application.Current is null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // A normal WPF-UI app merges both of these via App.xaml. This app has no App.xaml
                // (WinForms Main is still the real entry point), so they're merged here instead,
                // matching the same baseline every WPF-UI app starts with:
                //  - ThemesDictionary: the color tokens (ApplicationBackgroundBrush,
                //    ControlFillColorDefaultBrush, ...) every control template binds to via
                //    DynamicResource. Without it, controls render with no fill/border at all - visible
                //    but colorless. ThemeService.Apply/ApplicationThemeManager swaps this at runtime.
                //  - ControlsDictionary: the actual control templates for every ui: control (Button,
                //    TextBox, TitleBar, ...). Without it, ui: controls have no template at all and
                //    render as nothing.
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                AddOpaqueListSurfaceStyles(app);
            }
        }

        // ListView/DataGrid rows and TreeView items render with a transparent background in their
        // idle state (only an explicit hover/selection state paints an opaque fill) - by design,
        // that idle background is meant to let a themed ancestor surface show through. With
        // WindowBackdropType="Mica" and no explicit Background between the row and the window, what
        // shows through instead is the Mica backdrop, which does not reliably match the app's
        // resolved text color - text becomes invisible until hovered, when the opaque highlight
        // finally gives it something to contrast against. Giving these three container types an
        // explicit opaque themed Background (plus a 1px border, consistent with other bordered
        // surfaces like EditSetting's options panel) removes the Mica pass-through for idle rows.
        private static void AddOpaqueListSurfaceStyles(Application app)
        {
            void addStyle(Type targetType)
            {
                var style = new Style(targetType);
                style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlFillColorDefaultBrush")));
                style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
                style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("ControlStrokeColorDefaultBrush")));
                style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                app.Resources[targetType] = style;
            }

            addStyle(typeof(Wpf.Ui.Controls.ListView));
            addStyle(typeof(Wpf.Ui.Controls.DataGrid));
            addStyle(typeof(System.Windows.Controls.TreeView));
        }

        public static void SetOwner(Window window, System.Windows.Forms.IWin32Window owner)
        {
            EnsureApplication();
            _ = new WindowInteropHelper(window) { Owner = owner.Handle };
        }

        // Lets a WPF window act as the "owner" for a PresentDialog(IWin32Window owner, ...) call,
        // since every PresentDialog signature is IWin32Window-based for uniformity with the many
        // still-WinForms callers (Main, EditPol, etc.).
        public static System.Windows.Forms.IWin32Window AsIWin32Window(Window window) =>
            new Win32WindowAdapter(new WindowInteropHelper(window).EnsureHandle());

        private sealed class Win32WindowAdapter : System.Windows.Forms.IWin32Window
        {
            public Win32WindowAdapter(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }

        public static ImageSource ToImageSource(System.Drawing.Image image)
        {
            if (image is null)
            {
                return null;
            }

            using var bitmap = new System.Drawing.Bitmap(image);
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                PInvoke.DeleteObject(hBitmap);
            }
        }
    }
}
