using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class LoadedProductsWindow : FluentWindow
    {
        private class Row
        {
            public string Name;
            public string Version;
            public string Children;
            public PolicyPlusProduct Product;
        }

        public LoadedProductsWindow()
        {
            InitializeComponent();
        }

        private void Fill(AdmxBundle workspace)
        {
            var topRows = workspace.Products.Values
                .OrderBy(p => p.DisplayName)
                .Select(p => new Row { Name = p.DisplayName, Children = p.Children.Count.ToString(), Product = p })
                .ToList();
            LsvTopLevelProducts.ItemsSource = topRows;
            UpdateMajorList();
        }

        private void UpdateMajorList()
        {
            if (LsvTopLevelProducts.SelectedItem is Row selRow)
            {
                var rows = selRow.Product.Children.OrderBy(p => p.RawProduct.Version)
                    .Select(p => new Row { Name = p.DisplayName, Version = p.RawProduct.Version.ToString(), Children = p.Children.Count.ToString(), Product = p })
                    .ToList();
                LsvMajorVersions.ItemsSource = rows;
                LabelMajorVersion.Text = "Major versions of \"" + selRow.Product.DisplayName + "\"";
            }
            else
            {
                LsvMajorVersions.ItemsSource = null;
                LabelMajorVersion.Text = "Select a product to show its major versions";
            }

            UpdateMinorList();
        }

        private void UpdateMinorList()
        {
            if (LsvMajorVersions.SelectedItem is Row selRow)
            {
                var rows = selRow.Product.Children.OrderBy(p => p.RawProduct.Version)
                    .Select(p => new Row { Name = p.DisplayName, Version = p.RawProduct.Version.ToString(), Product = p })
                    .ToList();
                LsvMinorVersions.ItemsSource = rows;
                LabelMinorVersion.Text = "Minor versions of \"" + selRow.Product.DisplayName + "\"";
            }
            else
            {
                LsvMinorVersions.ItemsSource = null;
                LabelMinorVersion.Text = "Select a major version to show its minor versions";
            }
        }

        private void LsvTopLevelProducts_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateMajorList();

        private void LsvMajorVersions_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateMinorList();

        private void OpenDetails(System.Windows.Controls.ListView lsv)
        {
            if (lsv.SelectedItem is Row row)
            {
                DetailProductWindow.PresentDialog(this, row.Product);
            }
        }

        private void LsvTopLevelProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails(LsvTopLevelProducts);

        private void LsvMajorVersions_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails(LsvMajorVersions);

        private void LsvMinorVersions_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails(LsvMinorVersions);

        private void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenDetails((System.Windows.Controls.ListView)sender);
                e.Handled = true;
            }
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
        }

        public static void PresentDialog(System.Windows.Window owner, AdmxBundle workspace)
        {
            ThemeService.ApplyPersisted();
            var window = new LoadedProductsWindow();
            window.Fill(workspace);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
