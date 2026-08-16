using System.Collections.ObjectModel;
using System.Windows.Media;

namespace PolicyPlus.ViewModels
{
    // One row in Main's category tree. Category is null only for the synthetic "Favorites" node
    // pinned at the top - it isn't a real PolicyPlusCategory and has no entry in CategoryNodes.
    public sealed class CategoryNodeViewModel : ObservableObject
    {
        public string DisplayName { get; }
        public PolicyPlusCategory Category { get; }
        public ImageSource Icon { get; set; }

        // Shown instead of Icon while the node is selected, matching the WinForms
        // TreeNode.SelectedImageIndex behavior (regular categories swap to a "Go" folder icon;
        // the Favorites node keeps the same icon either way unless overridden).
        private ImageSource _selectedIcon;
        public ImageSource SelectedIcon
        {
            get => _selectedIcon ?? Icon;
            set => _selectedIcon = value;
        }

        public ObservableCollection<CategoryNodeViewModel> Children { get; } = new();

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public CategoryNodeViewModel(string displayName, PolicyPlusCategory category, ImageSource icon)
        {
            DisplayName = displayName;
            Category = category;
            Icon = icon;
        }
    }
}
