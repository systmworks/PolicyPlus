using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class FilterOptionsWindow : FluentWindow
    {
        private class ProductNode : INotifyPropertyChanged
        {
            public string Name;
            public PolicyPlusProduct Product;
            public ObservableCollection<ProductNode> Children = new();
            private bool _isChecked;
            private bool _isExpanded;

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value)
                    {
                        return;
                    }

                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    foreach (var child in Children)
                    {
                        child.IsChecked = value;
                    }
                }
            }

            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public FilterConfiguration CurrentFilter;
        private readonly Dictionary<PolicyPlusProduct, ProductNode> _productNodes = new();
        private readonly ObservableCollection<ProductNode> _rootNodes = new();
        private bool _accepted;

        public FilterOptionsWindow()
        {
            InitializeComponent();
            AllowedProductsTreeview.ItemsSource = _rootNodes;
        }

        private void BuildProductTree(AdmxBundle workspace)
        {
            _productNodes.Clear();
            _rootNodes.Clear();

            void AddProducts(IEnumerable<PolicyPlusProduct> products, ICollection<ProductNode> nodes)
            {
                foreach (var product in products)
                {
                    var node = new ProductNode { Name = product.DisplayName, Product = product };
                    nodes.Add(node);
                    _productNodes.Add(product, node);
                    if (product.Children is not null)
                    {
                        AddProducts(product.Children, node.Children);
                    }
                }
            }

            AddProducts(workspace.Products.Values, _rootNodes);
        }

        public void PrepareDialog(FilterConfiguration configuration)
        {
            PolicyTypeCombobox.SelectedIndex = configuration.ManagedPolicy.HasValue ? (configuration.ManagedPolicy.Value ? 1 : 2) : 0;
            PolicyStateCombobox.SelectedIndex = configuration.PolicyState switch
            {
                FilterPolicyState.NotConfigured => 1,
                FilterPolicyState.Configured => 2,
                FilterPolicyState.Enabled => 3,
                FilterPolicyState.Disabled => 4,
                _ => 0,
            };
            CommentedCombobox.SelectedIndex = configuration.Commented.HasValue ? (configuration.Commented.Value ? 1 : 2) : 0;

            foreach (var node in _productNodes.Values)
            {
                node.IsChecked = false;
                node.IsExpanded = false;
            }

            if (configuration.AllowedProducts is null)
            {
                SupportedCheckbox.IsChecked = false;
                AlwaysMatchAnyCheckbox.IsChecked = true;
                MatchBlankSupportCheckbox.IsChecked = true;
            }
            else
            {
                SupportedCheckbox.IsChecked = true;
                foreach (var product in configuration.AllowedProducts)
                {
                    // A saved filter can reference a product from an ADMX that's since been
                    // unloaded - skip it rather than throw.
                    if (_productNodes.TryGetValue(product, out var productNode))
                        productNode.IsChecked = true;
                }

                AlwaysMatchAnyCheckbox.IsChecked = configuration.AlwaysMatchAny;
                MatchBlankSupportCheckbox.IsChecked = configuration.MatchBlankSupport;

                // Expand to show all products with a different check state than their parent
                bool ExpandIfNecessary(ProductNode node)
                {
                    bool anyDifferent = false;
                    foreach (var child in node.Children)
                    {
                        bool childDifferent = ExpandIfNecessary(child);
                        if (childDifferent || child.IsChecked != node.IsChecked)
                        {
                            node.IsExpanded = true;
                            anyDifferent = true;
                        }
                    }

                    return anyDifferent;
                }

                foreach (var node in _rootNodes)
                {
                    ExpandIfNecessary(node);
                }
            }
        }

        private void SupportedCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            RequirementsBox.IsEnabled = SupportedCheckbox.IsChecked == true;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareDialog(new FilterConfiguration());
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var newConf = new FilterConfiguration();
            newConf.ManagedPolicy = PolicyTypeCombobox.SelectedIndex switch { 1 => true, 2 => false, _ => (bool?)null };
            newConf.PolicyState = PolicyStateCombobox.SelectedIndex switch
            {
                1 => FilterPolicyState.NotConfigured,
                2 => FilterPolicyState.Configured,
                3 => FilterPolicyState.Enabled,
                4 => FilterPolicyState.Disabled,
                _ => (FilterPolicyState?)null,
            };
            newConf.Commented = CommentedCombobox.SelectedIndex switch { 1 => true, 2 => false, _ => (bool?)null };

            if (SupportedCheckbox.IsChecked == true)
            {
                newConf.AlwaysMatchAny = AlwaysMatchAnyCheckbox.IsChecked == true;
                newConf.MatchBlankSupport = MatchBlankSupportCheckbox.IsChecked == true;
                newConf.AllowedProducts = new List<PolicyPlusProduct>();
                foreach (var kv in _productNodes)
                {
                    if (kv.Value.IsChecked)
                    {
                        newConf.AllowedProducts.Add(kv.Key);
                    }
                }
            }

            CurrentFilter = newConf;
            _accepted = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static FilterConfiguration PresentDialog(System.Windows.Window owner, FilterConfiguration configuration, AdmxBundle workspace)
        {
            ThemeService.ApplyPersisted();
            var window = new FilterOptionsWindow();
            window.BuildProductTree(workspace);
            window.PrepareDialog(configuration);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window.CurrentFilter : null;
        }
    }
}
