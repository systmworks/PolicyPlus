using System.Windows.Media;

namespace PolicyPlus.ViewModels
{
    // One row in Main's policy list. Tag is a PolicyPlusCategory (subcategory row, or the
    // pinned "Up" row - its parent category) or a PolicyPlusPolicy (policy row).
    public sealed class PolicyRowViewModel
    {
        public string Name { get; }
        public string State { get; }
        public string Comment { get; }
        public string Id { get; }
        public ImageSource Icon { get; }
        public object Tag { get; }

        // Marks the pinned "Up: <parent>" row so it always sorts first, matching
        // Main.PolicyListSorter's grouping (Up row, then categories, then policies).
        public bool IsUpRow { get; }

        public PolicyRowViewModel(string name, string state, string comment, string id, ImageSource icon, object tag, bool isUpRow = false)
        {
            Name = name;
            State = state;
            Comment = comment;
            Id = id;
            Icon = icon;
            Tag = tag;
            IsUpRow = isUpRow;
        }
    }
}
