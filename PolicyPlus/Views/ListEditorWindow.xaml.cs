using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public class ListEditorRow
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public partial class ListEditorWindow : FluentWindow
    {
        private readonly bool _userProvidesNames;
        private readonly ObservableCollection<ListEditorRow> _rows = new();
        private object _finalData;
        private bool _accepted;

        public ListEditorWindow(string title, object data, bool twoColumn)
        {
            InitializeComponent();
            _userProvidesNames = twoColumn;
            NameColumn.Visibility = twoColumn ? Visibility.Visible : Visibility.Collapsed;
            ElementNameLabel.Text = title;
            Title = title;

            if (data is not null)
            {
                if (twoColumn)
                {
                    foreach (var kv in (Dictionary<string, string>)data)
                        _rows.Add(new ListEditorRow { Name = kv.Key, Value = kv.Value });
                }
                else
                {
                    foreach (var entry in (List<string>)data)
                        _rows.Add(new ListEditorRow { Value = entry });
                }
            }

            EntriesGrid.ItemsSource = _rows;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            EntriesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (_userProvidesNames)
            {
                var dict = new Dictionary<string, string>();
                foreach (var row in _rows)
                {
                    if (dict.ContainsKey(row.Name))
                    {
                        MsgBoxCompat.Show(
                            "Multiple entries are named \"" + row.Name + "\"! Remove or rename all but one.",
                            MsgBoxButtons.OK,
                            MsgBoxIcon.Warning);
                        return;
                    }

                    dict.Add(row.Name, row.Value);
                }

                _finalData = dict;
            }
            else
            {
                var list = new List<string>();
                foreach (var row in _rows)
                    list.Add(row.Value);
                _finalData = list;
            }

            _accepted = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static object PresentDialog(System.Windows.Window owner, string title, object data, bool twoColumn)
        {
            var window = WpfInterop.PreparePresented(new ListEditorWindow(title, data, twoColumn), owner);
            window.ShowDialog();
            return window._accepted ? window._finalData : null;
        }
    }
}
