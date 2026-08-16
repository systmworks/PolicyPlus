using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class InspectPolicyElementsWindow : FluentWindow
    {
        private class InfoNode
        {
            public string Text;
            public ImageSource Icon;
            public ObservableCollection<InfoNode> Children = new();
        }

        private PolicyPlusPolicy _selectedPolicy;
        private ImageSource[] _icons;

        public InspectPolicyElementsWindow()
        {
            InitializeComponent();
        }

        private InfoNode AddNode(ObservableCollection<InfoNode> nodes, string text, int imageIndex)
        {
            var node = new InfoNode { Text = text, Icon = imageIndex >= 0 && imageIndex < _icons.Length ? _icons[imageIndex] : null };
            nodes.Add(node);
            return node;
        }

        private void PrepareDialog(PolicyPlusPolicy policy, ImageSource[] icons, AdmxBundle admxWorkspace)
        {
            _selectedPolicy = policy;
            _icons = icons;

            PolicyNameTextbox.Text = policy.DisplayName;
            var root = new ObservableCollection<InfoNode>();

            AddNode(root, "Registry key: " + policy.RawPolicy.RegistryKey, 0); // Folder
            if (!string.IsNullOrEmpty(policy.RawPolicy.RegistryValue))
                AddNode(root, "Registry value: " + policy.RawPolicy.RegistryValue, 13); // Gear
            if (!string.IsNullOrEmpty(policy.RawPolicy.ClientExtension))
                AddNode(root, "Client extension: " + policy.RawPolicy.ClientExtension, 19); // DOS window

            void AddValueData(PolicyRegistryValue regVal, InfoNode node)
            {
                switch (regVal.RegistryType)
                {
                    case PolicyRegistryValueType.Delete:
                        AddNode(node.Children, "Delete value", 18);
                        break;
                    case PolicyRegistryValueType.Numeric:
                        AddNode(node.Children, "Numeric value: " + regVal.NumberValue, 15);
                        break;
                    case PolicyRegistryValueType.Text:
                        AddNode(node.Children, "Text value: \"" + regVal.StringValue + "\"", 14);
                        break;
                }
            }

            void AddListEntry(PolicyRegistryListEntry regVal, InfoNode node)
            {
                var entryNode = AddNode(node.Children, "Set a value", 16); // Gear with pencil
                if (!string.IsNullOrEmpty(regVal.RegistryKey))
                    AddNode(entryNode.Children, "Registry key: " + regVal.RegistryKey, 0);
                AddNode(entryNode.Children, "Registry value: " + regVal.RegistryValue, 13);
                AddValueData(regVal.Value, entryNode);
            }

            void AddSingleListContents(PolicyRegistrySingleList singleList, InfoNode node)
            {
                if (!string.IsNullOrEmpty(singleList.DefaultRegistryKey))
                    AddNode(node.Children, "Registry key: " + singleList.DefaultRegistryKey, 0);
                foreach (var entry in singleList.AffectedValues)
                    AddListEntry(entry, node);
            }

            void AddList(PolicyRegistryList regList, ObservableCollection<InfoNode> nodes, bool hasValue)
            {
                var listNode = AddNode(nodes, "Affected Registry settings", 12); // Database
                if (regList.OnValue is not null)
                {
                    var onNode = AddNode(listNode.Children, "Set when enabled", 17); // Checkmark
                    AddValueData(regList.OnValue, onNode);
                }

                if (regList.OnValueList is not null)
                {
                    var onListNode = AddNode(listNode.Children, "Set list when enabled", 17);
                    AddSingleListContents(regList.OnValueList, onListNode);
                }

                if (regList.OffValue is not null)
                {
                    var offNode = AddNode(listNode.Children, "Set when disabled", 8); // Minus
                    AddValueData(regList.OffValue, offNode);
                }

                if (regList.OffValueList is not null)
                {
                    var offListNode = AddNode(listNode.Children, "Set list when disabled", 8);
                    AddSingleListContents(regList.OffValueList, offListNode);
                }

                if (listNode.Children.Count == 0)
                    AddNode(listNode.Children, hasValue ? "Left implicit" : "Left to elements", 37);
            }

            // Add the policy's basic Registry info
            AddList(policy.RawPolicy.AffectedValues, root, !string.IsNullOrEmpty(policy.RawPolicy.RegistryValue));

            // Add all the info on the policy's elements
            if (policy.Presentation is not null && policy.RawPolicy.Elements is not null)
            {
                var presNode = AddNode(root, "Presentation: " + policy.Presentation.Name, 20); // Form
                foreach (var presElem in policy.Presentation.Elements)
                {
                    var presPartNode = AddNode(presNode.Children, "Presentation element (type: " + presElem.ElementType + ")" + (!string.IsNullOrEmpty(presElem.ID) ? ", ID: " + presElem.ID : ""), -1);
                    switch (presElem.ElementType ?? "")
                    {
                        case "text":
                            {
                                var labelPres = (LabelPresentationElement)presElem;
                                presPartNode.Icon = _icons[21]; // Text rows
                                AddNode(presPartNode.Children, "Text: \"" + labelPres.Text + "\"", 14);
                                break;
                            }
                        case "decimalTextBox":
                            {
                                var decTextPres = (NumericBoxPresentationElement)presElem;
                                presPartNode.Icon = _icons[22]; // Calculator with pencil
                                if (!string.IsNullOrEmpty(decTextPres.Label))
                                    AddNode(presPartNode.Children, "Label: \"" + decTextPres.Label + "\"", 14);
                                AddNode(presPartNode.Children, "Default: " + decTextPres.DefaultValue, 23); // Wrench
                                AddNode(presPartNode.Children, decTextPres.HasSpinner ? "Spinner increment: " + decTextPres.SpinnerIncrement : "No spinner", 6);
                                break;
                            }
                        case "textBox":
                            {
                                var textPres = (TextBoxPresentationElement)presElem;
                                presPartNode.Icon = _icons[24]; // Text field
                                if (!string.IsNullOrEmpty(textPres.Label))
                                    AddNode(presPartNode.Children, "Label: \"" + textPres.Label + "\"", 14);
                                AddNode(presPartNode.Children, "Default: \"" + textPres.DefaultValue + "\"", 23);
                                break;
                            }
                        case "checkBox":
                            {
                                var checkPres = (CheckBoxPresentationElement)presElem;
                                presPartNode.Icon = _icons[25]; // Tickmark
                                AddNode(presPartNode.Children, "Text: \"" + checkPres.Text + "\"", 14);
                                AddNode(presPartNode.Children, "Default: " + (checkPres.DefaultState ? "checked" : "unchecked"), 23);
                                break;
                            }
                        case "comboBox":
                            {
                                var comboPres = (ComboBoxPresentationElement)presElem;
                                presPartNode.Icon = _icons[26]; // Bar with text
                                if (!string.IsNullOrEmpty(comboPres.Label))
                                    AddNode(presPartNode.Children, "Label: \"" + comboPres.Label + "\"", 14);
                                AddNode(presPartNode.Children, "Default: \"" + comboPres.DefaultText + "\"", 23);
                                AddNode(presPartNode.Children, "Sorting: " + (comboPres.NoSort ? "from ADMX" : "alphabetical"), 28); // Sorted table
                                if (comboPres.Suggestions is not null)
                                {
                                    var sugNode = AddNode(presPartNode.Children, comboPres.Suggestions.Count + " suggestions", 29); // Letter
                                    foreach (var sug in comboPres.Suggestions)
                                        AddNode(sugNode.Children, "\"" + sug + "\"", 14);
                                }

                                break;
                            }
                        case "dropdownList":
                            {
                                var dropdownPres = (DropDownPresentationElement)presElem;
                                presPartNode.Icon = _icons[30]; // List view
                                if (!string.IsNullOrEmpty(dropdownPres.Label))
                                    AddNode(presPartNode.Children, "Label: \"" + dropdownPres.Label + "\"", 14);
                                if (dropdownPres.DefaultItemID.HasValue)
                                    AddNode(presPartNode.Children, "Default: #" + dropdownPres.DefaultItemID.Value, 23);
                                AddNode(presPartNode.Children, "Sorting: " + (dropdownPres.NoSort ? "from ADMX" : "alphabetical"), 28);
                                break;
                            }
                        case "listBox":
                            {
                                var listPres = (ListPresentationElement)presElem;
                                presPartNode.Icon = _icons[27]; // Table window
                                AddNode(presPartNode.Children, "Label: \"" + listPres.Label + "\"", 14);
                                break;
                            }
                        case "multiTextBox":
                            {
                                var multiTextPres = (MultiTextPresentationElement)presElem;
                                presPartNode.Icon = _icons[38]; // Cascading boxes
                                AddNode(presPartNode.Children, "Label: \"" + multiTextPres.Label + "\"", 14);
                                break;
                            }
                    }

                    if (string.IsNullOrEmpty(presElem.ID))
                        continue;
                    var elem = policy.RawPolicy.Elements.FirstOrDefault(e => (e.ID ?? "") == (presElem.ID ?? ""));
                    if (elem is null)
                    {
                        AddNode(presPartNode.Children, "Policy element (unknown - no matching ID \"" + presElem.ID + "\" in ADMX)", 31);
                        continue;
                    }
                    var elemNode = AddNode(presPartNode.Children, "Policy element (type: " + elem.ElementType + ")", 31); // Brick
                    if (!string.IsNullOrEmpty(elem.ClientExtension))
                        AddNode(elemNode.Children, "Client extension: " + elem.ClientExtension, 19);
                    if (!string.IsNullOrEmpty(elem.RegistryKey))
                        AddNode(elemNode.Children, "Registry key: " + elem.RegistryKey, 0);
                    if (elem.ElementType != "list")
                        AddNode(elemNode.Children, "Registry value: " + elem.RegistryValue, 13);
                    switch (elem.ElementType ?? "")
                    {
                        case "decimal":
                            {
                                var decimalElem = (DecimalPolicyElement)elem;
                                AddNode(elemNode.Children, "Minimum: " + decimalElem.Minimum, 35); // Down arrow
                                AddNode(elemNode.Children, "Maximum: " + decimalElem.Maximum, 6);
                                if (decimalElem.StoreAsText)
                                    AddNode(elemNode.Children, "Stored as text", 33); // Letters
                                AddNode(elemNode.Children, "Required: " + (decimalElem.Required ? "yes" : "no"), 32); // Exclamation
                                if (decimalElem.NoOverwrite)
                                    AddNode(elemNode.Children, "Soft", 34); // Soft speaker
                                break;
                            }
                        case "boolean":
                            {
                                var booleanElem = (BooleanPolicyElement)elem;
                                AddList(booleanElem.AffectedRegistry, elemNode.Children, true);
                                break;
                            }
                        case "text":
                            {
                                var textElem = (TextPolicyElement)elem;
                                AddNode(elemNode.Children, "Maximum length: " + textElem.MaxLength, 6);
                                if (textElem.RegExpandSz)
                                    AddNode(elemNode.Children, "Stored as expandable string", 36); // Letters with arrow
                                AddNode(elemNode.Children, "Required: " + (textElem.Required ? "yes" : "no"), 32);
                                if (textElem.NoOverwrite)
                                    AddNode(elemNode.Children, "Soft", 34);
                                break;
                            }
                        case "list":
                            {
                                var listElem = (ListPolicyElement)elem;
                                if (listElem.UserProvidesNames)
                                    AddNode(elemNode.Children, "User provides value names", 13);
                                else if (listElem.HasPrefix)
                                    AddNode(elemNode.Children, "Value prefix: \"" + listElem.RegistryValue + "\"", 13);
                                else
                                    AddNode(elemNode.Children, "No prefix (values named for their data)", 13);
                                if (listElem.RegExpandSz)
                                    AddNode(elemNode.Children, "Stored as expandable strings", 36);
                                AddNode(elemNode.Children, "Preserve existing values: " + (listElem.NoPurgeOthers ? "yes" : "no"), 34);
                                break;
                            }
                        case "enum":
                            {
                                var enumElem = (EnumPolicyElement)elem;
                                AddNode(elemNode.Children, "Required: " + (enumElem.Required ? "yes" : "no"), 32);
                                var itemsNode = AddNode(elemNode.Children, enumElem.Items.Count + " choices", 26);
                                int id = 0;
                                foreach (var item in enumElem.Items)
                                {
                                    var itemNode = AddNode(itemsNode.Children, "Choice #" + id, 29);
                                    AddNode(itemNode.Children, "Display code: " + item.DisplayCode, 14);
                                    AddNode(itemNode.Children, "Display name: \"" + admxWorkspace.ResolveString(item.DisplayCode, policy.RawPolicy.DefinedIn) + "\"", 21);
                                    AddValueData(item.Value, itemNode);
                                    if (item.ValueList is not null)
                                    {
                                        var regNode = AddNode(itemNode.Children, "Additional Registry settings modified", 12);
                                        AddSingleListContents(item.ValueList, regNode);
                                    }

                                    id += 1;
                                }

                                break;
                            }
                    }
                }
            }

            InfoTreeview.ItemsSource = root;
        }

        private void InfoTreeview_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && InfoTreeview.SelectedItem is InfoNode node)
            {
                Clipboard.SetText(node.Text);
            }
        }

        private void PolicyDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            DetailPolicyWindow.PresentDialog(this, _selectedPolicy);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, PolicyPlusPolicy policy, ImageSource[] icons, AdmxBundle admxWorkspace)
        {
            var window = WpfInterop.PreparePresented(new InspectPolicyElementsWindow(), owner);
            window.PrepareDialog(policy, icons, admxWorkspace);
            window.ShowDialog();
        }
    }
}
