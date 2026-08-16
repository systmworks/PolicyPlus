using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DetailAdmxWindow : FluentWindow
    {
        private class Row<T>
        {
            public string Id;
            public string Name;
            public T Item;
        }

        public DetailAdmxWindow()
        {
            InitializeComponent();
        }

        private static List<Row<T>> BuildRows<T>(IEnumerable<T> collection, Func<T, string> idSelector, Func<T, string> nameSelector) =>
            collection.Select(item => new Row<T> { Id = idSelector(item), Name = nameSelector(item), Item = item }).ToList();

        private void PrepareDialog(AdmxFile admx, AdmxBundle workspace)
        {
            TextPath.Text = admx.SourceFile;
            TextNamespace.Text = admx.AdmxNamespace;
            TextSupersededAdm.Text = admx.SupersededAdm;
            LsvPolicies.ItemsSource = BuildRows(
                workspace.Policies.Values.Where(p => ReferenceEquals(p.RawPolicy.DefinedIn, admx)),
                (PolicyPlusPolicy p) => p.RawPolicy.ID, (PolicyPlusPolicy p) => p.DisplayName);
            LsvCategories.ItemsSource = BuildRows(
                workspace.FlatCategories.Values.Where(c => ReferenceEquals(c.RawCategory.DefinedIn, admx)),
                (PolicyPlusCategory c) => c.RawCategory.ID, (PolicyPlusCategory c) => c.DisplayName);
            LsvProducts.ItemsSource = BuildRows(
                workspace.FlatProducts.Values.Where(p => ReferenceEquals(p.RawProduct.DefinedIn, admx)),
                (PolicyPlusProduct p) => p.RawProduct.ID, (PolicyPlusProduct p) => p.DisplayName);
            LsvSupportDefinitions.ItemsSource = BuildRows(
                workspace.SupportDefinitions.Values.Where(s => ReferenceEquals(s.RawSupport.DefinedIn, admx)),
                (PolicyPlusSupport s) => s.RawSupport.ID, (PolicyPlusSupport s) => s.DisplayName);
        }

        private void LsvPolicies_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LsvPolicies.SelectedItem is Row<PolicyPlusPolicy> row)
            {
                DetailPolicyWindow.PresentDialog(this, row.Item);
            }
        }

        private void LsvCategories_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LsvCategories.SelectedItem is Row<PolicyPlusCategory> row)
            {
                DetailCategoryWindow.PresentDialog(this, row.Item);
            }
        }

        private void LsvProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LsvProducts.SelectedItem is Row<PolicyPlusProduct> row)
            {
                DetailProductWindow.PresentDialog(this, row.Item);
            }
        }

        private void LsvSupportDefinitions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LsvSupportDefinitions.SelectedItem is Row<PolicyPlusSupport> row)
            {
                DetailSupportWindow.PresentDialog(this, row.Item);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, AdmxFile admx, AdmxBundle workspace)
        {
            ThemeService.ApplyPersisted();
            var window = new DetailAdmxWindow();
            window.PrepareDialog(admx, workspace);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
