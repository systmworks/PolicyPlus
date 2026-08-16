using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DetailProductWindow : FluentWindow
    {
        private class ChildRow
        {
            public string Version;
            public string Name;
            public PolicyPlusProduct Product;
        }

        private PolicyPlusProduct _selectedProduct;

        public DetailProductWindow()
        {
            InitializeComponent();
        }

        private void PrepareDialog(PolicyPlusProduct product)
        {
            _selectedProduct = product;
            NameTextbox.Text = product.DisplayName;
            IdTextbox.Text = product.UniqueID;
            DefinedTextbox.Text = product.RawProduct.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = product.RawProduct.DisplayCode;
            KindTextbox.Text = product.RawProduct.Type switch
            {
                AdmxProductType.MajorRevision => "Major revision",
                AdmxProductType.MinorRevision => "Minor revision",
                AdmxProductType.Product => "Top-level product",
                _ => KindTextbox.Text,
            };
            VersionTextbox.Text = product.RawProduct.Type == AdmxProductType.Product ? "" : product.RawProduct.Version.ToString();
            if (product.Parent is null)
            {
                ParentTextbox.Text = "";
                ParentButton.IsEnabled = false;
            }
            else
            {
                ParentTextbox.Text = product.Parent.DisplayName;
                ParentButton.IsEnabled = true;
            }

            var rows = new List<ChildRow>();
            if (product.Children is not null)
            {
                foreach (var child in product.Children)
                {
                    rows.Add(new ChildRow { Version = child.RawProduct.Version.ToString(), Name = child.DisplayName, Product = child });
                }
            }

            ChildrenListview.ItemsSource = rows;
        }

        private void ChildrenListview_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ChildrenListview.SelectedItem is ChildRow row)
            {
                PrepareDialog(row.Product);
            }
        }

        private void ParentButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareDialog(_selectedProduct.Parent);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static void PresentDialog(System.Windows.Window owner, PolicyPlusProduct product)
        {
            ThemeService.ApplyPersisted();
            var window = new DetailProductWindow();
            window.PrepareDialog(product);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
