using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PolicyPlus
{
    internal static class WpfInterop
    {
        private static bool _resourcesReady;

        public static void EnsureApplication()
        {
            if (Application.Current is null)
            {
                // Every dialog still calls this before showing itself as a defensive fallback -
                // in normal operation App.Main (the WPF-native entry point) already constructs the
                // real Application before any window can open, so this branch shouldn't run.
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }

            if (_resourcesReady)
                return;
            _resourcesReady = true;
            var app = Application.Current;
            // A normal WPF-UI app merges both of these via App.xaml. This app has no App.xaml,
            // so they're merged here instead, matching the same baseline every WPF-UI app starts with:
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
            AddCompactTitleBarStyle(app);
        }

        // Wpf.Ui.Controls.TitleBar's stock style hardcodes Height="48" as a plain Setter (not a
        // MinHeight or a natural desired size), and its own template top/center-anchors the icon,
        // title text, and caption buttons rather than filling that height - the caption buttons are
        // a fixed 30px, Top-aligned, so roughly the bottom third of the stock 48px is genuine dead
        // space by design (most likely sized for apps that put a taller custom app-bar in the title
        // row - this app doesn't). That dead space is what repeatedly looked like "extra space above
        // the menu" and "extra space below Close" on short dialogs across several earlier fix
        // attempts that targeted the Menu/dialog-content side instead of the TitleBar itself.
        //
        // Re-based on WPF-UI's own style (not an independent one - unlike the list/tree item fix,
        // there's no Trigger/VisualState fight to route around here, just one Setter to override) so
        // every other Setter and the whole Template carry over unchanged; only Height moves from 48
        // to 32, verified by measurement to still fully contain the 30px caption buttons.
        private static void AddCompactTitleBarStyle(Application app)
        {
            if (app.TryFindResource(typeof(Wpf.Ui.Controls.TitleBar)) is not Style stockStyle)
            {
                return;
            }

            var compactStyle = new Style(typeof(Wpf.Ui.Controls.TitleBar), stockStyle);
            compactStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 32.0));
            app.Resources[typeof(Wpf.Ui.Controls.TitleBar)] = compactStyle;
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
            void addSurfaceStyle(Type targetType)
            {
                var style = new Style(targetType);
                style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlFillColorDefaultBrush")));
                style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
                style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("ControlStrokeColorDefaultBrush")));
                style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                app.Resources[targetType] = style;
            }

            addSurfaceStyle(typeof(Wpf.Ui.Controls.ListView));
            addSurfaceStyle(typeof(Wpf.Ui.Controls.DataGrid));
            addSurfaceStyle(typeof(System.Windows.Controls.TreeView));

            // The surface fix above wasn't enough for ListView/TreeView rows on their own, even after
            // a first attempt at re-basing WPF-UI's own item styles with a corrected Foreground Setter:
            // rows stayed invisible against the new opaque background too. WPF-UI's real item template
            // most likely sets Foreground from a VisualState/Trigger (not a plain Setter), which always
            // wins over a Setter added to a derived style regardless of base/derived order - re-basing
            // can't reliably out-prioritize that without knowing the exact trigger being fought.
            // Trading Fluent-specific hover chrome for guaranteed-readable text: a fresh, independent
            // style (not based on WPF-UI's) with an explicit Foreground and simple hover/selected
            // Background triggers of its own, so nothing else can silently out-prioritize it.
            Style buildPlainItemStyle(Type itemType, DependencyProperty isSelectedProperty)
            {
                var style = new Style(itemType);
                style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
                style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
                style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));

                var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlFillColorSecondaryBrush")));
                style.Triggers.Add(hover);

                var selected = new Trigger { Property = isSelectedProperty, Value = true };
                selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlFillColorTertiaryBrush")));
                style.Triggers.Add(selected);

                return style;
            }

            app.Resources[typeof(Wpf.Ui.Controls.ListViewItem)] = buildPlainItemStyle(typeof(Wpf.Ui.Controls.ListViewItem), ListBoxItem.IsSelectedProperty);

            // TreeView has no ui: subclass, so a plain <TreeView> generates native TreeViewItem
            // containers - style those directly (implicit lookup on the native type works here,
            // unlike ListView/DataGrid which needed their WPF-UI subclass targeted instead). A window
            // that sets its own local ItemContainerStyle (CategoriesTree, FilterOptionsWindow) needs
            // BasedOn="{StaticResource {x:Type TreeViewItem}}" to inherit this instead of overriding it.
            app.Resources[typeof(System.Windows.Controls.TreeViewItem)] = buildPlainItemStyle(typeof(System.Windows.Controls.TreeViewItem), System.Windows.Controls.TreeViewItem.IsSelectedProperty);
        }

        public static void SetOwner(Window window, Window owner)
        {
            EnsureApplication();
            window.Owner = owner;
        }

        // Every PresentDialog method across ~35 windows repeats
        // "ThemeService.ApplyPersisted(); var window = new XWindow(...); SetOwner(window, owner);"
        // before doing its own per-window setup and ShowDialog() - this collapses just that
        // identical prefix into one call so a future cross-cutting addition (error handling,
        // telemetry, ...) has one place to land instead of ~35. Each window's own construction,
        // ShowDialog() call, and result extraction stay exactly as they were - those differ too
        // much (void/bool/string/custom-type returns, differing constructor args) to unify.
        public static TWindow PreparePresented<TWindow>(TWindow window, Window owner) where TWindow : Window
        {
            ThemeService.ApplyPersisted();
            SetOwner(window, owner);
            return window;
        }

        // Shared Escape-to-close handler for the ~30 simple dialogs that just want Escape to
        // close them with no further logic. Wire it from XAML as KeyDown="Window_KeyDown" and
        // delegate: private void Window_KeyDown(object sender, KeyEventArgs e) =>
        // WpfInterop.HandleEscapeToClose(this, e);
        // A few windows need different Escape behavior (e.g. not closing while a text selection
        // is active) - those keep their own full Window_KeyDown instead of calling this.
        public static void HandleEscapeToClose(Window window, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
            }
        }

        // SizeToContent="Height" left these windows taller than their content, with dead space
        // below it - two earlier fixes here (a Loaded-based SizeToContent toggle, then a Mica-
        // timing theory using ContentRendered) both did nothing, and a third (measuring the gap
        // between window and content height directly) computed the right answer but still didn't
        // visibly help. Confirmed by headlessly showing a real AboutWindow and pumping its message
        // loop: the third fix WAS correctly setting window.Height to content's true 228px, but
        // WPF-UI's stock FluentWindow style sets MinHeight="320"/MinWidth="460" - sensible floors
        // for a resizable primary window, but these dialogs are all ResizeMode="NoResize" and
        // explicitly opting into SizeToContent, so any content shorter/narrower than the floor gets
        // silently clamped back up regardless of what Height/Width get set to. There's no
        // interactive resizing on these windows for the floor to protect against, so it's cleared
        // outright rather than lowered to some other guessed number.
        //
        // The direct-measurement shrink from the third attempt is kept as a second layer - once the
        // floor is gone SizeToContent should already produce the right height on its own, but this
        // still catches cases where it doesn't for some other reason. Call from a window's
        // constructor, after InitializeComponent, for any window seen sizing too tall.
        public static void FixSizeToContent(Window window)
        {
            if (window.SizeToContent == SizeToContent.Manual)
            {
                return;
            }

            // A local value is required, not ClearValue - there's no local MinHeight/MinWidth to
            // clear here, only the stock Style's Setter, and ClearValue only removes local values,
            // leaving a Style Setter fully in effect. A local value is the one thing guaranteed to
            // outrank it.
            window.MinHeight = 0;
            window.MinWidth = 0;

            window.ContentRendered += (s, e) =>
            {
                if (window.Content is not FrameworkElement content)
                {
                    return;
                }

                content.UpdateLayout();
                double needed = content.DesiredSize.Height;
                double chrome = window.ActualHeight - content.ActualHeight;
                if (needed <= 0 || chrome < 0)
                {
                    return;
                }

                double target = needed + chrome;
                if (target < window.ActualHeight - 1)
                {
                    window.SizeToContent = SizeToContent.Manual;
                    window.Height = target;
                }
            };
        }
    }
}
