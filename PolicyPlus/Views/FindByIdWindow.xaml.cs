using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class FindByIdWindow : FluentWindow
    {
        public class Result
        {
            public PolicyPlusCategory SelectedCategory;
            public PolicyPlusPolicy SelectedPolicy;
            public PolicyPlusProduct SelectedProduct;
            public PolicyPlusSupport SelectedSupport;
            public AdmxPolicySection SelectedSection;
        }

        private AdmxBundle _admxWorkspace;
        private ImageSource _categoryImage, _policyImage, _productImage, _supportImage, _notFoundImage, _blankImage;
        private bool _accepted;
        private readonly Result _result = new Result();

        public FindByIdWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
            Loaded += (s, e) =>
            {
                IdTextbox.Focus();
                IdTextbox.SelectAll();
            };
        }

        private void IdTextbox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_admxWorkspace is null)
            {
                return;
            }

            _result.SelectedPolicy = null;
            _result.SelectedCategory = null;
            _result.SelectedProduct = null;
            _result.SelectedSupport = null;
            string id = IdTextbox.Text.Trim();
            if (_admxWorkspace.FlatCategories.ContainsKey(id))
            {
                StatusImage.Source = _categoryImage;
                _result.SelectedCategory = _admxWorkspace.FlatCategories[id];
            }
            else if (_admxWorkspace.FlatProducts.ContainsKey(id))
            {
                StatusImage.Source = _productImage;
                _result.SelectedProduct = _admxWorkspace.FlatProducts[id];
            }
            else if (_admxWorkspace.SupportDefinitions.ContainsKey(id))
            {
                StatusImage.Source = _supportImage;
                _result.SelectedSupport = _admxWorkspace.SupportDefinitions[id];
            }
            else
            {
                string[] policyAndSection = id.Split(new[] { '@' }, 2);
                string policyId = policyAndSection[0];
                if (_admxWorkspace.Policies.ContainsKey(policyId))
                {
                    StatusImage.Source = _policyImage;
                    _result.SelectedPolicy = _admxWorkspace.Policies[policyId];
                    if (policyAndSection.Length == 2 && policyAndSection[1].Length == 1 && "UC".Contains(policyAndSection[1]))
                    {
                        _result.SelectedSection = policyAndSection[1] == "U" ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                    }
                    else
                    {
                        _result.SelectedSection = AdmxPolicySection.Both;
                    }
                }
                else
                {
                    StatusImage.Source = string.IsNullOrEmpty(id) ? _blankImage : _notFoundImage;
                }
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static Result PresentDialog(System.Windows.Window owner, AdmxBundle admxWorkspace, ImageSource[] policyIcons)
        {
            ThemeService.ApplyPersisted();
            var window = new FindByIdWindow
            {
                _categoryImage = policyIcons[0],
                _policyImage = policyIcons[4],
                _productImage = policyIcons[10],
                _supportImage = policyIcons[11],
                _notFoundImage = policyIcons[8],
                _blankImage = policyIcons[9],
                _admxWorkspace = admxWorkspace,
            };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window._result : null;
        }
    }
}
