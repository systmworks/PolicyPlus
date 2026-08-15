using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace PolicyPlus
{
    public partial class EditPol
    {
        public PolFile EditingPol;
        private bool EditingUserSource;

        public EditPol()
        {
            InitializeComponent();
        }
        public void UpdateTree()
        {
            // Repopulate the main list view, keeping the scroll position in the same place
            var topItemIndex = default(int?);
            if (LsvPol.TopItem is not null)
                topItemIndex = LsvPol.TopItem.Index;
            LsvPol.BeginUpdate();
            LsvPol.Items.Clear();
            void addKey(string Prefix, int Depth)
            {
                    var subkeys = EditingPol.GetKeyNames(Prefix);
                    subkeys.Sort(StringComparer.InvariantCultureIgnoreCase);
                    foreach (var subkey in subkeys)
                    {
                        string keypath = string.IsNullOrEmpty(Prefix) ? subkey : Prefix + @"\" + subkey;
                        var lsvi = LsvPol.Items.Add(subkey);
                        lsvi.IndentCount = Depth;
                        lsvi.ImageIndex = 0; // Folder
                        lsvi.Tag = keypath;
                        addKey(keypath, Depth + 1);
                    }
                    var values = EditingPol.GetValueNames(Prefix, false);
                    values.Sort(StringComparer.InvariantCultureIgnoreCase);
                    var iconIndex = default(int);
                    foreach (var value in values)
                    {
                        if (string.IsNullOrEmpty(value))
                            continue;
                        var data = EditingPol.GetValue(Prefix, value);
                        var kind = EditingPol.GetValueKind(Prefix, value);
                        ListViewItem addToLsv(string ItemText, int Icon, bool Deletion)
                        {
                            var lsvItem = LsvPol.Items.Add(ItemText, Icon);
                            lsvItem.IndentCount = Depth;
                            var tag = new PolValueInfo() { Name = value, Key = Prefix };
                            if (Deletion)
                            {
                                tag.IsDeleter = true;
                            }
                            else
                            {
                                tag.Kind = kind;
                                tag.Data = data;
                            }
                            lsvItem.Tag = tag;
                            return lsvItem;
                        };
                        if (value.Equals("**deletevalues", StringComparison.InvariantCultureIgnoreCase))
                        {
                            addToLsv("Delete values", 8, true).SubItems.Add(data.ToString());
                        }
                        else if (value.StartsWith("**del.", StringComparison.InvariantCultureIgnoreCase))
                        {
                            addToLsv("Delete value", 8, true).SubItems.Add(value.Substring(6));
                        }
                        else if (value.StartsWith("**delvals", StringComparison.InvariantCultureIgnoreCase))
                        {
                            addToLsv("Delete all values", 8, true);
                        }
                        else
                        {
                            string text = "";
                            if (data is string[])
                            {
                                text = string.Join(" ", (string[])data);
                                iconIndex = 39; // Two pages
                            }
                            else if (data is string)
                            {
                                text = Conversions.ToString(data);
                                iconIndex = kind == RegistryValueKind.ExpandString ? 42 : 40; // One page with arrow, or without
                            }
                            else if (data is uint)
                            {
                                text = data.ToString();
                                iconIndex = 15; // Calculator
                            }
                            else if (data is ulong)
                            {
                                text = data.ToString();
                                iconIndex = 41; // Calculator+
                            }
                            else if (data is byte[])
                            {
                                text = BitConverter.ToString((byte[])data).Replace("-", " ");
                                iconIndex = 13; // Gear
                            }
                            addToLsv(value, iconIndex, false).SubItems.Add(text);
                        }
                    }
            }
            addKey("", 0);
            LsvPol.EndUpdate();
            if (topItemIndex.HasValue && LsvPol.Items.Count > topItemIndex.Value)
                LsvPol.TopItem = LsvPol.Items[topItemIndex.Value];
        }
        public void PresentDialog(ImageList Images, PolFile Pol, bool IsUser)
        {
            LsvPol.SmallImageList = Images;
            EditingPol = Pol;
            EditingUserSource = IsUser;
            UpdateTree();
            ChItem.Width = LsvPol.ClientSize.Width - ChValue.Width - SystemInformation.VerticalScrollBarWidth;
            LsvPol_SelectedIndexChanged(null, null);
            ShowDialog();
        }
        private void ButtonSave_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
        private void EditPol_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }
        public void SelectKey(string KeyPath)
        {
            var lsvi = LsvPol.Items.OfType<ListViewItem>().FirstOrDefault(i => i.Tag is string && KeyPath.Equals(Conversions.ToString(i.Tag), StringComparison.InvariantCultureIgnoreCase));
            if (lsvi is null)
                return;
            lsvi.Selected = true;
            lsvi.EnsureVisible();
        }
        public void SelectValue(string KeyPath, string ValueName)
        {
            var lsvi = LsvPol.Items.OfType<ListViewItem>().FirstOrDefault((Item) =>
    {
        if (!(Item.Tag is PolValueInfo))
            return false;
        PolValueInfo pvi = (PolValueInfo)Item.Tag;
        return pvi.Key.Equals(KeyPath, StringComparison.InvariantCultureIgnoreCase) & pvi.Name.Equals(ValueName, StringComparison.InvariantCultureIgnoreCase);
    });
            if (lsvi is null)
                return;
            lsvi.Selected = true;
            lsvi.EnsureVisible();
        }
        public bool IsKeyNameValid(string Name)
        {
            return !Name.Contains(@"\");
        }
        public bool IsKeyNameAvailable(string ContainerPath, string Name)
        {
            return !EditingPol.GetKeyNames(ContainerPath).Any(k => k.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
        }
        private void ButtonAddKey_Click(object sender, EventArgs e)
        {
            string keyName = Views.EditPolKeyWindow.PresentDialog(this, "");
            if (string.IsNullOrEmpty(keyName))
                return;
            if (!IsKeyNameValid(keyName))
            {
                MsgBoxCompat.Show("The key name is not valid.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string containerKey = Conversions.ToString(LsvPol.SelectedItems.Count > 0 ? LsvPol.SelectedItems[0].Tag : "");
            if (!IsKeyNameAvailable(containerKey, keyName))
            {
                MsgBoxCompat.Show("The key name is already taken.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string newPath = string.IsNullOrEmpty(containerKey) ? keyName : containerKey + @"\" + keyName;
            EditingPol.SetValue(newPath, "", Array.CreateInstance(typeof(byte), 0), RegistryValueKind.None);
            UpdateTree();
            SelectKey(newPath);
        }
        public object PromptForNewValueData(string ValueName, object CurrentData, RegistryValueKind Kind)
        {
            if (Kind == RegistryValueKind.String | Kind == RegistryValueKind.ExpandString)
            {
                return Views.EditPolStringDataWindow.PresentDialog(this, ValueName, Conversions.ToString(CurrentData));
            }
            else if (Kind == RegistryValueKind.DWord | Kind == RegistryValueKind.QWord)
            {
                if (My.MyProject.Forms.EditPolNumericData.PresentDialog(ValueName, Conversions.ToULong(CurrentData), Kind == RegistryValueKind.QWord) == DialogResult.OK)
                {
                    return My.MyProject.Forms.EditPolNumericData.NumData.Value;
                }
                else
                {
                    return null;
                }
            }
            else if (Kind == RegistryValueKind.MultiString)
            {
                return Views.EditPolMultiStringDataWindow.PresentDialog(this, ValueName, (string[])CurrentData);
            }
            else
            {
                MsgBoxCompat.Show("This value kind is not supported.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return null;
            }
        }
        private void ButtonAddValue_Click(object sender, EventArgs e)
        {
            string keyPath = Conversions.ToString(LsvPol.SelectedItems[0].Tag);
            var chosen = Views.EditPolValueWindow.PresentDialog(this);
            if (chosen is null)
                return;
            string value = chosen.Value.Name;
            var kind = chosen.Value.Kind;
            object defaultData;
            if (kind == RegistryValueKind.String | kind == RegistryValueKind.ExpandString)
            {
                defaultData = "";
            }
            else if (kind == RegistryValueKind.DWord | kind == RegistryValueKind.QWord)
            {
                defaultData = 0;
            }
            else // Multi-string
            {
                defaultData = Array.CreateInstance(typeof(string), 0);
            }
            var newData = PromptForNewValueData(value, defaultData, kind);
            if (newData is not null)
            {
                EditingPol.SetValue(keyPath, value, newData, kind);
                UpdateTree();
                SelectValue(keyPath, value);
            }
        }
        private void ButtonDeleteValue_Click(object sender, EventArgs e)
        {
            var tag = LsvPol.SelectedItems[0].Tag;
            if (tag is string)
            {
                var deleteChoice = Views.EditPolDeleteWindow.PresentDialog(this, Strings.Split(Conversions.ToString(tag), @"\").Last());
                if (deleteChoice is null)
                    return;
                if (deleteChoice.Value.Purge)
                {
                    EditingPol.ClearKey(Conversions.ToString(tag)); // Delete everything
                }
                else if (deleteChoice.Value.ClearFirst)
                {
                    EditingPol.ForgetKeyClearance(Conversions.ToString(tag)); // So the clearance is before the values in the POL
                    EditingPol.ClearKey(Conversions.ToString(tag));
                    // Add the existing values back
                    int index = LsvPol.SelectedIndices[0] + 1;
                    do
                    {
                        if (index >= LsvPol.Items.Count)
                            break;
                        var subItem = LsvPol.Items[index];
                        if (subItem.IndentCount <= LsvPol.SelectedItems[0].IndentCount)
                            break;
                        if (subItem.IndentCount == LsvPol.SelectedItems[0].IndentCount + 1 & subItem.Tag is PolValueInfo)
                        {
                            PolValueInfo valueInfo = (PolValueInfo)subItem.Tag;
                            if (!valueInfo.IsDeleter)
                                EditingPol.SetValue(valueInfo.Key, valueInfo.Name, valueInfo.Data, valueInfo.Kind);
                        }
                        index += 1;
                    }
                    while (true);
                }
                else
                {
                    // Delete only the specified value
                    EditingPol.DeleteValue(Conversions.ToString(tag), deleteChoice.Value.ValueName);
                }
                UpdateTree();
                SelectKey(Conversions.ToString(tag));
            }
            else
            {
                PolValueInfo info = (PolValueInfo)tag;
                EditingPol.DeleteValue(info.Key, info.Name);
                UpdateTree();
                SelectValue(info.Key, "**del." + info.Name);
            }
        }
        private void ButtonForget_Click(object sender, EventArgs e)
        {
            string containerKey = "";
            var tag = LsvPol.SelectedItems[0].Tag;
            if (tag is string)
            {
                if (MsgBoxCompat.Show("Are you sure you want to remove this key and all its contents?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                    return;
                string keyPath = Conversions.ToString(tag);
                if (keyPath.Contains(@"\"))
                    containerKey = keyPath.Remove(keyPath.LastIndexOf('\\'));
                void removeKey(string Key)
                {
                        foreach (var subkey in EditingPol.GetKeyNames(Key))
                            removeKey(Key + @"\" + subkey);
                        EditingPol.ClearKey(Key);
                        EditingPol.ForgetKeyClearance(Key);
                }
                removeKey(keyPath);
            }
            else
            {
                PolValueInfo info = (PolValueInfo)tag;
                containerKey = info.Key;
                EditingPol.ForgetValue(info.Key, info.Name);
            }
            UpdateTree();
            if (!string.IsNullOrEmpty(containerKey))
            {
                string[] pathParts = Strings.Split(containerKey, @"\");
                for (int n = 1, loopTo = pathParts.Length; n <= loopTo; n++)
                    SelectKey(string.Join(@"\", pathParts.Take(n)));
            }
            else
            {
                LsvPol_SelectedIndexChanged(null, null);
            } // Make sure the buttons don't stay enabled
        }
        private void ButtonEdit_Click(object sender, EventArgs e)
        {
            PolValueInfo info = (PolValueInfo)LsvPol.SelectedItems[0].Tag;
            var newData = PromptForNewValueData(info.Name, info.Data, info.Kind);
            if (newData is not null)
            {
                EditingPol.SetValue(info.Key, info.Name, newData, info.Kind);
                UpdateTree();
                SelectValue(info.Key, info.Name);
            }
        }
        private void LsvPol_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update the enabled status of all the buttons
            if (LsvPol.SelectedItems.Count == 0)
            {
                ButtonAddKey.Enabled = true;
                ButtonAddValue.Enabled = false;
                ButtonDeleteValue.Enabled = false;
                ButtonEdit.Enabled = false;
                ButtonForget.Enabled = false;
                ButtonExport.Enabled = true;
            }
            else
            {
                var tag = LsvPol.SelectedItems[0].Tag;
                ButtonForget.Enabled = true;
                if (tag is string) // It's a key
                {
                    ButtonAddKey.Enabled = true;
                    ButtonAddValue.Enabled = true;
                    ButtonEdit.Enabled = false;
                    ButtonDeleteValue.Enabled = true;
                    ButtonExport.Enabled = true;
                }
                else // It's a value
                {
                    ButtonAddKey.Enabled = false;
                    ButtonAddValue.Enabled = false;
                    bool delete = ((PolValueInfo)tag).IsDeleter;
                    ButtonEdit.Enabled = !delete;
                    ButtonDeleteValue.Enabled = !delete;
                    ButtonExport.Enabled = false;
                }
            }
        }
        private void ButtonImport_Click(object sender, EventArgs e)
        {
            if (Views.ImportRegWindow.PresentDialog(this, EditingPol))
                UpdateTree();
        }
        private void ButtonExport_Click(object sender, EventArgs e)
        {
            string branch = "";
            if (LsvPol.SelectedItems.Count > 0)
                branch = Conversions.ToString(LsvPol.SelectedItems[0].Tag);
            Views.ExportRegWindow.PresentDialog(this, branch, EditingPol, EditingUserSource);
        }
        private class PolValueInfo
        {
            public string Key;
            public string Name;
            public RegistryValueKind Kind;
            public object Data;
            public bool IsDeleter;
        }
    }
}