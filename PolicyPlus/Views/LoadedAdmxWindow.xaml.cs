using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class LoadedAdmxWindow : FluentWindow
    {
        private class Row
        {
            public string FileTitle;
            public string Folder;
            public string Namespace;
            public AdmxFile Admx;
        }

        private AdmxBundle _bundle;

        public LoadedAdmxWindow()
        {
            InitializeComponent();
        }

        private void OpenSelected()
        {
            if (LsvAdmx.SelectedItem is Row row)
            {
                DetailAdmxWindow.PresentDialog(this, row.Admx, _bundle);
            }
        }

        private void LsvAdmx_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelected();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter && LsvAdmx.SelectedItem is not null)
            {
                OpenSelected();
            }
        }

        public static void PresentDialog(System.Windows.Window owner, AdmxBundle workspace)
        {
            var window = WpfInterop.PreparePresented(new LoadedAdmxWindow { _bundle = workspace }, owner);
            var rows = new List<Row>();
            foreach (var admx in workspace.Sources.Keys)
            {
                rows.Add(new Row
                {
                    FileTitle = System.IO.Path.GetFileName(admx.SourceFile),
                    Folder = System.IO.Path.GetDirectoryName(admx.SourceFile),
                    Namespace = admx.AdmxNamespace,
                    Admx = admx,
                });
            }

            window.LsvAdmx.ItemsSource = rows;
            window.ShowDialog();
        }
    }
}
