using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class FindByTextWindow : FluentWindow
    {
        private Func<PolicyPlusPolicy, bool> _searcher;

        public FindByTextWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => StringTextbox.Focus();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string text = StringTextbox.Text;
            if (string.IsNullOrEmpty(text))
            {
                MsgBoxCompat.Show("Please enter search terms.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

            bool checkTitle = TitleCheckbox.IsChecked == true;
            bool checkDesc = DescriptionCheckbox.IsChecked == true;
            bool checkComment = CommentCheckbox.IsChecked == true;
            if (!(checkTitle | checkDesc | checkComment))
            {
                MsgBoxCompat.Show("At least one attribute must be searched. Check one of the boxes and try again.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

            _searcher = PolicySearch.BuildMatcher(text, checkTitle, checkDesc, checkComment, false, false, CommentSources);
            Close();
        }

        private Dictionary<string, string>[] CommentSources { get; set; }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static Func<PolicyPlusPolicy, bool> PresentDialog(System.Windows.Forms.IWin32Window owner, params Dictionary<string, string>[] commentDicts)
        {
            ThemeService.ApplyPersisted();
            var window = new FindByTextWindow { CommentSources = commentDicts.Where(d => d is not null).ToArray() };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._searcher;
        }
    }
}
