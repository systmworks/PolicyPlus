using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DetailCategoryWindow : FluentWindow
    {
        private PolicyPlusCategory _selectedCategory;

        public DetailCategoryWindow()
        {
            InitializeComponent();
        }

        private void PrepareDialog(PolicyPlusCategory category)
        {
            _selectedCategory = category;
            NameTextbox.Text = category.DisplayName;
            IdTextbox.Text = category.UniqueID;
            DefinedTextbox.Text = category.RawCategory.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = category.RawCategory.DisplayCode;
            InfoCodeTextbox.Text = category.RawCategory.ExplainCode;
            ParentButton.IsEnabled = category.Parent is not null;
            if (category.Parent is not null)
            {
                ParentTextbox.Text = category.Parent.DisplayName;
            }
            else if (!string.IsNullOrEmpty(category.RawCategory.ParentID))
            {
                ParentTextbox.Text = "<orphaned from " + category.RawCategory.ParentID + ">";
            }
            else
            {
                ParentTextbox.Text = "";
            }
        }

        private void ParentButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareDialog(_selectedCategory.Parent);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static void PresentDialog(System.Windows.Forms.IWin32Window owner, PolicyPlusCategory category)
        {
            ThemeService.ApplyPersisted();
            var window = new DetailCategoryWindow();
            window.PrepareDialog(category);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
