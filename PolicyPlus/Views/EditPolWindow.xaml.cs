using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolWindow : FluentWindow
    {
        private class PolValueInfo
        {
            public string Key;
            public string Name;
            public RegistryValueKind Kind;
            public object Data;
            public bool IsDeleter;
        }

        private class Row
        {
            public string Name;
            public string Value;
            public int Depth;
            public Thickness Indent => new(Depth * 16, 0, 0, 0);
            public ImageSource Icon;
            public object Tag; // string keypath, or PolValueInfo
        }

        private PolFile _editingPol;
        private bool _editingUserSource;
        private ImageSource[] _icons;
        private List<Row> _rows = new();

        public EditPolWindow()
        {
            InitializeComponent();
        }

        private ImageSource Icon(int index) => index >= 0 && index < _icons.Length ? _icons[index] : null;

        public void UpdateTree()
        {
            var selectedTag = (LsvPol.SelectedItem as Row)?.Tag;
            _rows = new List<Row>();

            void AddKey(string prefix, int depth)
            {
                var subkeys = _editingPol.GetKeyNames(prefix);
                subkeys.Sort(StringComparer.InvariantCultureIgnoreCase);
                foreach (var subkey in subkeys)
                {
                    string keypath = string.IsNullOrEmpty(prefix) ? subkey : prefix + @"\" + subkey;
                    _rows.Add(new Row { Name = subkey, Depth = depth, Icon = Icon(0), Tag = keypath }); // Folder
                    AddKey(keypath, depth + 1);
                }

                var values = _editingPol.GetValueNames(prefix, false);
                values.Sort(StringComparer.InvariantCultureIgnoreCase);
                foreach (var value in values)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    var data = _editingPol.GetValue(prefix, value);
                    var kind = _editingPol.GetValueKind(prefix, value);

                    Row AddRow(string itemText, int iconIndex, bool deletion)
                    {
                        var tag = new PolValueInfo { Name = value, Key = prefix };
                        if (deletion)
                        {
                            tag.IsDeleter = true;
                        }
                        else
                        {
                            tag.Kind = kind;
                            tag.Data = data;
                        }

                        var row = new Row { Name = itemText, Depth = depth, Icon = Icon(iconIndex), Tag = tag };
                        _rows.Add(row);
                        return row;
                    }

                    if (value.Equals("**deletevalues", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddRow("Delete values", 8, true).Value = data.ToString();
                    }
                    else if (value.StartsWith("**del.", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddRow("Delete value", 8, true).Value = value.Substring(6);
                    }
                    else if (value.StartsWith("**delvals", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddRow("Delete all values", 8, true);
                    }
                    else
                    {
                        string text = "";
                        int iconIndex = 13;
                        if (data is string[] strings)
                        {
                            text = string.Join(" ", strings);
                            iconIndex = 39; // Two pages
                        }
                        else if (data is string s)
                        {
                            text = s;
                            iconIndex = kind == RegistryValueKind.ExpandString ? 42 : 40; // One page with arrow, or without
                        }
                        else if (data is uint u)
                        {
                            text = u.ToString();
                            iconIndex = 15; // Calculator
                        }
                        else if (data is ulong ul)
                        {
                            text = ul.ToString();
                            iconIndex = 41; // Calculator+
                        }
                        else if (data is byte[] bytes)
                        {
                            text = BitConverter.ToString(bytes).Replace("-", " ");
                            iconIndex = 13; // Gear
                        }

                        AddRow(value, iconIndex, false).Value = text;
                    }
                }
            }

            AddKey("", 0);
            LsvPol.ItemsSource = _rows;
            if (selectedTag is not null)
            {
                var toReselect = _rows.FirstOrDefault(r => TagEquals(r.Tag, selectedTag));
                if (toReselect is not null)
                {
                    LsvPol.SelectedItem = toReselect;
                }
            }
        }

        private static bool TagEquals(object a, object b)
        {
            if (a is string sa && b is string sb)
            {
                return sa.Equals(sb, StringComparison.InvariantCultureIgnoreCase);
            }

            if (a is PolValueInfo pa && b is PolValueInfo pb)
            {
                return pa.Key.Equals(pb.Key, StringComparison.InvariantCultureIgnoreCase) && pa.Name.Equals(pb.Name, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        public void SelectKey(string keyPath)
        {
            var row = _rows.FirstOrDefault(r => r.Tag is string s && keyPath.Equals(s, StringComparison.InvariantCultureIgnoreCase));
            if (row is null)
            {
                return;
            }

            LsvPol.SelectedItem = row;
            LsvPol.ScrollIntoView(row);
        }

        public void SelectValue(string keyPath, string valueName)
        {
            var row = _rows.FirstOrDefault(r => r.Tag is PolValueInfo pvi && pvi.Key.Equals(keyPath, StringComparison.InvariantCultureIgnoreCase) && pvi.Name.Equals(valueName, StringComparison.InvariantCultureIgnoreCase));
            if (row is null)
            {
                return;
            }

            LsvPol.SelectedItem = row;
            LsvPol.ScrollIntoView(row);
        }

        public bool IsKeyNameValid(string name) => !name.Contains(@"\");

        public bool IsKeyNameAvailable(string containerPath, string name) =>
            !_editingPol.GetKeyNames(containerPath).Any(k => k.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        private void ButtonAddKey_Click(object sender, RoutedEventArgs e)
        {
            string keyName = EditPolKeyWindow.PresentDialog(this, "");
            if (string.IsNullOrEmpty(keyName))
            {
                return;
            }

            if (!IsKeyNameValid(keyName))
            {
                MsgBoxCompat.Show("The key name is not valid.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            string containerKey = (LsvPol.SelectedItem as Row)?.Tag as string ?? "";
            if (!IsKeyNameAvailable(containerKey, keyName))
            {
                MsgBoxCompat.Show("The key name is already taken.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            string newPath = string.IsNullOrEmpty(containerKey) ? keyName : containerKey + @"\" + keyName;
            _editingPol.SetValue(newPath, "", Array.CreateInstance(typeof(byte), 0), RegistryValueKind.None);
            UpdateTree();
            SelectKey(newPath);
        }

        public object PromptForNewValueData(string valueName, object currentData, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.String || kind == RegistryValueKind.ExpandString)
            {
                return EditPolStringDataWindow.PresentDialog(this, valueName, currentData?.ToString());
            }
            else if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord)
            {
                var result = EditPolNumericDataWindow.PresentDialog(this, valueName, Convert.ToUInt64(currentData), kind == RegistryValueKind.QWord);
                return result.HasValue ? (object)result.Value : null;
            }
            else if (kind == RegistryValueKind.MultiString)
            {
                return EditPolMultiStringDataWindow.PresentDialog(this, valueName, (string[])currentData);
            }
            else
            {
                MsgBoxCompat.Show("This value kind is not supported.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return null;
            }
        }

        private void ButtonAddValue_Click(object sender, RoutedEventArgs e)
        {
            string keyPath = (LsvPol.SelectedItem as Row)?.Tag as string;
            var chosen = EditPolValueWindow.PresentDialog(this);
            if (chosen is null)
            {
                return;
            }

            string value = chosen.Value.Name;
            var kind = chosen.Value.Kind;
            object defaultData = kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString => "",
                RegistryValueKind.DWord or RegistryValueKind.QWord => 0,
                _ => Array.CreateInstance(typeof(string), 0),
            };
            var newData = PromptForNewValueData(value, defaultData, kind);
            if (newData is not null)
            {
                _editingPol.SetValue(keyPath, value, newData, kind);
                UpdateTree();
                SelectValue(keyPath, value);
            }
        }

        private void ButtonDeleteValue_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = LsvPol.SelectedItem as Row;
            var tag = selectedRow?.Tag;
            if (tag is string keyPath)
            {
                var deleteChoice = EditPolDeleteWindow.PresentDialog(this, keyPath.Split('\\').Last());
                if (deleteChoice is null)
                {
                    return;
                }

                if (deleteChoice.Value.Purge)
                {
                    _editingPol.ClearKey(keyPath);
                }
                else if (deleteChoice.Value.ClearFirst)
                {
                    _editingPol.ForgetKeyClearance(keyPath);
                    _editingPol.ClearKey(keyPath);
                    // Add the existing values back
                    int index = _rows.IndexOf(selectedRow) + 1;
                    while (index < _rows.Count)
                    {
                        var subItem = _rows[index];
                        if (subItem.Depth <= selectedRow.Depth)
                        {
                            break;
                        }

                        if (subItem.Depth == selectedRow.Depth + 1 && subItem.Tag is PolValueInfo valueInfo)
                        {
                            if (!valueInfo.IsDeleter)
                            {
                                _editingPol.SetValue(valueInfo.Key, valueInfo.Name, valueInfo.Data, valueInfo.Kind);
                            }
                        }

                        index += 1;
                    }
                }
                else
                {
                    _editingPol.DeleteValue(keyPath, deleteChoice.Value.ValueName);
                }

                UpdateTree();
                SelectKey(keyPath);
            }
            else
            {
                var info = (PolValueInfo)tag;
                _editingPol.DeleteValue(info.Key, info.Name);
                UpdateTree();
                SelectValue(info.Key, "**del." + info.Name);
            }
        }

        private void ButtonForget_Click(object sender, RoutedEventArgs e)
        {
            string containerKey = "";
            var tag = (LsvPol.SelectedItem as Row)?.Tag;
            if (tag is string keyPath)
            {
                if (MsgBoxCompat.Show("Are you sure you want to remove this key and all its contents?", MsgBoxButtons.YesNo, MsgBoxIcon.Warning) == MsgBoxResult.No)
                {
                    return;
                }

                if (keyPath.Contains(@"\"))
                {
                    containerKey = keyPath.Remove(keyPath.LastIndexOf('\\'));
                }

                void RemoveKey(string key)
                {
                    foreach (var subkey in _editingPol.GetKeyNames(key))
                    {
                        RemoveKey(key + @"\" + subkey);
                    }

                    _editingPol.ClearKey(key);
                    _editingPol.ForgetKeyClearance(key);
                }

                RemoveKey(keyPath);
            }
            else
            {
                var info = (PolValueInfo)tag;
                containerKey = info.Key;
                _editingPol.ForgetValue(info.Key, info.Name);
            }

            UpdateTree();
            if (!string.IsNullOrEmpty(containerKey))
            {
                string[] pathParts = containerKey.Split('\\');
                for (int n = 1; n <= pathParts.Length; n++)
                {
                    SelectKey(string.Join(@"\", pathParts.Take(n)));
                }
            }
            else
            {
                UpdateButtonStates();
            }
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            var info = (PolValueInfo)((Row)LsvPol.SelectedItem).Tag;
            var newData = PromptForNewValueData(info.Name, info.Data, info.Kind);
            if (newData is not null)
            {
                _editingPol.SetValue(info.Key, info.Name, newData, info.Kind);
                UpdateTree();
                SelectValue(info.Key, info.Name);
            }
        }

        private void LsvPol_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateButtonStates();

        private void UpdateButtonStates()
        {
            if (LsvPol.SelectedItem is not Row row)
            {
                ButtonAddKey.IsEnabled = true;
                ButtonAddValue.IsEnabled = false;
                ButtonDeleteValue.IsEnabled = false;
                ButtonEdit.IsEnabled = false;
                ButtonForget.IsEnabled = false;
                return;
            }

            ButtonForget.IsEnabled = true;
            if (row.Tag is string)
            {
                ButtonAddKey.IsEnabled = true;
                ButtonAddValue.IsEnabled = true;
                ButtonEdit.IsEnabled = false;
                ButtonDeleteValue.IsEnabled = true;
            }
            else
            {
                ButtonAddKey.IsEnabled = false;
                ButtonAddValue.IsEnabled = false;
                bool delete = ((PolValueInfo)row.Tag).IsDeleter;
                ButtonEdit.IsEnabled = !delete;
                ButtonDeleteValue.IsEnabled = !delete;
            }
        }

        private void ButtonImport_Click(object sender, RoutedEventArgs e)
        {
            if (ImportRegWindow.PresentDialog(this, _editingPol))
            {
                UpdateTree();
            }
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            string branch = (LsvPol.SelectedItem as Row)?.Tag as string ?? "";
            ExportRegWindow.PresentDialog(this, branch, _editingPol, _editingUserSource);
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, ImageSource[] icons, PolFile pol, bool isUser)
        {
            var window = WpfInterop.PreparePresented(new EditPolWindow
            {
                _icons = icons,
                _editingPol = pol,
                _editingUserSource = isUser,
            }, owner);

            window.UpdateTree();
            window.UpdateButtonStates();
            window.ShowDialog();
        }
    }
}
