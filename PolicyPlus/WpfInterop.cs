using System;
using System.Windows;
using System.Windows.Controls;
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

        // SizeToContent="Height" leaves these windows taller than their content, with the dead space
        // below it. Re-running the SizeToContent pass (at Loaded, then at ContentRendered on an idle
        // dispatch) was tried first on the theory that WPF-UI's async Mica backdrop was racing the
        // measure - neither helped, because it isn't a timing problem: with ExtendsContentIntoTitleBar
        // the caption is drawn by the window's own content, but WPF still reserves standard
        // non-client caption space when converting the measured content height into a window height,
        // so every re-measure lands on the same too-tall answer.
        //
        // Measuring the gap directly sidesteps the cause entirely. Once laid out, the content's
        // DesiredSize is its true height while its ActualHeight is the stretched height it was
        // given, so the difference between the window and the stretched content is the real
        // non-client overhead. Guarded to only ever shrink, so a window that's already correct (or
        // measures oddly) is left alone rather than made worse. Call from a window's constructor,
        // after InitializeComponent, for any window seen sizing too tall.
        public static void FixSizeToContent(Window window)
        {
            if (window.SizeToContent == SizeToContent.Manual)
            {
                return;
            }

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
