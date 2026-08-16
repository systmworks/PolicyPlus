using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class LoadedSupportDefinitionsWindow : FluentWindow
    {
        private class Row
        {
            public string Name;
            public string DefinedIn;
            public PolicyPlusSupport Support;
        }

        private IEnumerable<PolicyPlusSupport> _definitions;

        public LoadedSupportDefinitionsWindow()
        {
            InitializeComponent();
        }

        private void UpdateListing()
        {
            var rows = _definitions
                .OrderBy(s => s.DisplayName.Trim())
                .Where(s => s.DisplayName.ToLowerInvariant().Contains((TextFilter.Text ?? "").ToLowerInvariant()))
                .Select(s => new Row { Name = s.DisplayName.Trim(), DefinedIn = System.IO.Path.GetFileName(s.RawSupport.DefinedIn.SourceFile), Support = s })
                .ToList();
            LsvSupport.ItemsSource = rows;
        }

        private void OpenSelected()
        {
            if (LsvSupport.SelectedItem is Row row)
            {
                DetailSupportWindow.PresentDialog(this, row.Support);
            }
        }

        private void LsvSupport_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenSelected();

        private void LsvSupport_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LsvSupport.SelectedItem is not null)
            {
                OpenSelected();
            }
        }

        private void TextFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateListing();
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, AdmxBundle workspace)
        {
            ThemeService.ApplyPersisted();
            var window = new LoadedSupportDefinitionsWindow { _definitions = workspace.SupportDefinitions.Values };
            window.UpdateListing();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
