using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class FindResultsWindow : FluentWindow
    {
        private class Row
        {
            public string Title;
            public string Category;
        }

        // Search results and selection must survive both across separate PresentDialog calls and
        // while the window isn't open at all (FindNext reads NextPolicy() without reopening it) -
        // unlike the WinForms singleton this replaces, a WPF Window can't be reshown after Close(),
        // so this state lives at the class level instead of on a reused window instance.
        private static readonly System.Collections.Generic.List<PolicyPlusPolicy> _results = new();
        private static int _lastSelectedIndex = -1;
        private static bool _hasSearched;

        private readonly ObservableCollection<Row> _rows = new();
        private volatile bool _cancelingSearch;
        private volatile bool _cancelDueToFormClose;
        private bool _accepted;

        public FindResultsWindow()
        {
            InitializeComponent();
            ResultsListview.ItemsSource = _rows;
        }

        private void PopulateFromResults()
        {
            _rows.Clear();
            foreach (var policy in _results)
            {
                _rows.Add(new Row { Title = policy.DisplayName, Category = policy.Category.DisplayName });
            }

            if (_lastSelectedIndex >= 0 && _lastSelectedIndex < _rows.Count)
            {
                ResultsListview.SelectedIndex = _lastSelectedIndex;
                ResultsListview.ScrollIntoView(ResultsListview.SelectedItem);
            }
        }

        private void StartSearch(AdmxBundle workspace, Func<PolicyPlusPolicy, bool> searcher)
        {
            _results.Clear();
            _rows.Clear();
            _hasSearched = true;
            _lastSelectedIndex = -1;
            SearchProgress.Maximum = workspace.Policies.Count;
            SearchProgress.Value = 0;
            StopButton.IsEnabled = true;
            ProgressLabel.Text = "Starting search";
            Task.Factory.StartNew(() => SearchJob(workspace, searcher));
        }

        private void SearchJob(AdmxBundle workspace, Func<PolicyPlusPolicy, bool> searcher)
        {
            int searchedSoFar = 0;
            int results = 0;
            bool stoppedByCancel = false;
            var pendingInsertions = new System.Collections.Generic.List<PolicyPlusPolicy>();

            void AddPendingInsertions()
            {
                foreach (var insert in pendingInsertions)
                {
                    _results.Add(insert);
                    _rows.Add(new Row { Title = insert.DisplayName, Category = insert.Category.DisplayName });
                }

                pendingInsertions.Clear();
            }

            foreach (var policy in workspace.Policies)
            {
                if (_cancelingSearch)
                {
                    stoppedByCancel = true;
                    break;
                }

                searchedSoFar += 1;
                bool isHit = searcher(policy.Value);
                if (isHit)
                {
                    results += 1;
                    pendingInsertions.Add(policy.Value);
                }

                if (searchedSoFar % 20 == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_cancelDueToFormClose)
                        {
                            return;
                        }

                        AddPendingInsertions();
                        SearchProgress.Value = searchedSoFar;
                        ProgressLabel.Text = "Searching: checked " + searchedSoFar + " policies so far, found " + results + " hits";
                    });
                }
            }

            if (stoppedByCancel && _cancelDueToFormClose)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                AddPendingInsertions();
                string status = stoppedByCancel ? "Search canceled" : "Finished searching";
                ProgressLabel.Text = status + ": checked " + searchedSoFar + " policies, found " + results + " hits";
                SearchProgress.Value = SearchProgress.Maximum;
                StopButton.IsEnabled = false;
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cancelingSearch = true;
        }

        private void GoClicked(object sender, RoutedEventArgs e)
        {
            if (ResultsListview.SelectedItem is not Row row)
            {
                return;
            }

            _lastSelectedIndex = ResultsListview.SelectedIndex;
            _accepted = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _cancelingSearch = true;
            _cancelDueToFormClose = true;
            if (SearchProgress.Value != SearchProgress.Maximum)
            {
                ProgressLabel.Text = "Search canceled";
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void ClearSearch()
        {
            _hasSearched = false;
            _results.Clear();
            _lastSelectedIndex = -1;
        }

        public static PolicyPlusPolicy NextPolicy()
        {
            if (_lastSelectedIndex >= _results.Count - 1 || !_hasSearched)
            {
                return null;
            }

            _lastSelectedIndex += 1;
            return _results[_lastSelectedIndex];
        }

        public static PolicyPlusPolicy PresentDialog(System.Windows.Window owner)
        {
            if (!_hasSearched)
            {
                MsgBoxCompat.Show("No search has been run yet, so there are no results to display.", MsgBoxButtons.OK, MsgBoxIcon.Information);
                return null;
            }

            var window = WpfInterop.PreparePresented(new FindResultsWindow(), owner);
            window.PopulateFromResults();
            window.StopButton.IsEnabled = false;
            window.ProgressLabel.Text = "Finished searching: " + _results.Count + " hit(s)";
            window.SearchProgress.Maximum = 1;
            window.SearchProgress.Value = 1;
            window.ShowDialog();
            return window._accepted ? _results[_lastSelectedIndex] : null;
        }

        public static PolicyPlusPolicy PresentDialogStartSearch(System.Windows.Window owner, AdmxBundle workspace, Func<PolicyPlusPolicy, bool> searcher)
        {
            var window = WpfInterop.PreparePresented(new FindResultsWindow(), owner);
            window.StartSearch(workspace, searcher);
            window.ShowDialog();
            return window._accepted ? _results[_lastSelectedIndex] : null;
        }
    }
}
