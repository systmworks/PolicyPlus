using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PolicyPlus.ViewModels;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditSettingWindow : FluentWindow
    {
        private PolicyPlusPolicy _currentSetting;
        private AdmxPolicySection _currentSection;
        private AdmxBundle _admxWorkspace;
        private IPolicySource _compPolSource, _userPolSource;
        private PolicyLoader _compPolLoader, _userPolLoader;
        private Dictionary<string, string> _compComments, _userComments;

        private IPolicySource _currentSource;
        private PolicyLoader _currentLoader;
        private Dictionary<string, string> _currentComments;

        private ObservableCollection<PolicyElementViewModel> _elements;
        private Dictionary<string, PolicyElementViewModel> _elementsById;
        private bool _changesMade;
        private bool _accepted;

        public EditSettingWindow()
        {
            InitializeComponent();
            Loaded += EditSettingWindow_Loaded;
        }

        private void EditSettingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SettingNameLabel.Text = _currentSetting.DisplayName;
            SupportedTextbox.Text = _currentSetting.SupportedOn is null ? "" : _currentSetting.SupportedOn.DisplayName;
            HelpTextbox.Text = MainWindow.PrettifyDescription(_currentSetting.DisplayExplanation);

            if (_currentSetting.RawPolicy.Section == AdmxPolicySection.Both)
            {
                SectionDropdown.IsEnabled = true;
                _currentSection = _currentSection == AdmxPolicySection.Both ? AdmxPolicySection.Machine : _currentSection;
            }
            else
            {
                SectionDropdown.IsEnabled = false;
                _currentSection = _currentSetting.RawPolicy.Section;
            }

            PreparePolicyElements();

            // Triggers SectionDropdown_SelectionChanged, which sets up CurrentSource/Loader/Comments
            // and calls PreparePolicyState().
            SectionDropdown.SelectedIndex = _currentSection == AdmxPolicySection.Machine ? 1 : 0;
        }

        private void PreparePolicyElements()
        {
            _elements = new ObservableCollection<PolicyElementViewModel>();
            _elementsById = new Dictionary<string, PolicyElementViewModel>();

            if (_currentSetting.RawPolicy.Elements is not null && _currentSetting.Presentation is not null)
            {
                var elemDict = _currentSetting.RawPolicy.Elements.ToDictionary(el => el.ID);
                foreach (var pres in _currentSetting.Presentation.Elements)
                {
                    // Presentation element references an ID the ADMX doesn't define - skip
                    // rather than throw, so one mismatched element doesn't block the whole dialog.
                    elemDict.TryGetValue(pres.ID ?? "", out var rawElem);
                    if (rawElem is null && (pres.ElementType ?? "") is "decimalTextBox" or "textBox" or "comboBox" or "dropdownList" or "listBox")
                        continue;

                    PolicyElementViewModel vm = (pres.ElementType ?? "") switch
                    {
                        "text" => BuildLabel((LabelPresentationElement)pres),
                        "decimalTextBox" => BuildDecimal((NumericBoxPresentationElement)pres, (DecimalPolicyElement)rawElem),
                        "textBox" => BuildTextBox((TextBoxPresentationElement)pres, (TextPolicyElement)rawElem),
                        "checkBox" => BuildCheckBox((CheckBoxPresentationElement)pres),
                        "comboBox" => BuildComboBox((ComboBoxPresentationElement)pres, (TextPolicyElement)rawElem),
                        "dropdownList" => BuildDropDown((DropDownPresentationElement)pres, (EnumPolicyElement)rawElem),
                        "listBox" => BuildListBox((ListPresentationElement)pres, (ListPolicyElement)rawElem),
                        "multiTextBox" => BuildMultiTextBox((MultiTextPresentationElement)pres),
                        _ => null,
                    };

                    if (vm is null)
                        continue;

                    _elements.Add(vm);
                    if (!string.IsNullOrEmpty(vm.Id))
                        _elementsById.Add(vm.Id, vm);
                }
            }

            ExtraOptionsList.ItemsSource = _elements;
        }

        private static LabelElementViewModel BuildLabel(LabelPresentationElement pres) =>
            new(pres.ID, pres.Text);

        private static DecimalElementViewModel BuildDecimal(NumericBoxPresentationElement pres, DecimalPolicyElement elem) =>
            new(pres.ID, pres.Label, elem.Minimum, elem.Maximum, pres.SpinnerIncrement, pres.HasSpinner, pres.DefaultValue);

        private static TextBoxElementViewModel BuildTextBox(TextBoxPresentationElement pres, TextPolicyElement elem) =>
            new(pres.ID, pres.Label, elem.MaxLength, pres.DefaultValue);

        private static CheckBoxElementViewModel BuildCheckBox(CheckBoxPresentationElement pres) =>
            new(pres.ID, pres.Text, pres.DefaultState);

        private static ComboBoxElementViewModel BuildComboBox(ComboBoxPresentationElement pres, TextPolicyElement elem) =>
            new(pres.ID, pres.Label, elem.MaxLength, pres.DefaultText, pres.Suggestions, !pres.NoSort);

        private DropDownElementViewModel BuildDropDown(DropDownPresentationElement pres, EnumPolicyElement elem)
        {
            var items = new List<DropdownOption>();
            for (int i = 0; i < elem.Items.Count; i++)
            {
                string displayName = _admxWorkspace.ResolveString(elem.Items[i].DisplayCode, _currentSetting.RawPolicy.DefinedIn);
                items.Add(new DropdownOption(i, displayName));
            }

            IReadOnlyList<DropdownOption> displayItems = pres.NoSort
                ? items
                : items.OrderBy(i => i.DisplayName, StringComparer.CurrentCulture).ToList();

            return new DropDownElementViewModel(pres.ID, pres.Label, displayItems, pres.DefaultItemID);
        }

        private static ListElementViewModel BuildListBox(ListPresentationElement pres, ListPolicyElement elem) =>
            new(pres.ID, pres.Label, elem.UserProvidesNames);

        private static MultiTextBoxElementViewModel BuildMultiTextBox(MultiTextPresentationElement pres) =>
            new(pres.ID, pres.Label);

        private void PreparePolicyState()
        {
            switch (PolicyProcessing.GetPolicyState(_currentSource, _currentSetting))
            {
                case PolicyState.Disabled:
                    DisabledOption.IsChecked = true;
                    break;
                case PolicyState.Enabled:
                    EnabledOption.IsChecked = true;
                    foreach (var kv in PolicyProcessing.GetPolicyOptionStates(_currentSource, _currentSetting))
                    {
                        if (_elementsById.TryGetValue(kv.Key, out var vm))
                            vm.LoadValue(kv.Value);
                    }

                    break;
                default:
                    NotConfiguredOption.IsChecked = true;
                    break;
            }

            bool canWrite = _currentLoader.GetWritability() != PolicySourceWritability.NoWriting;
            ApplyButton.IsEnabled = canWrite;
            OkButton.IsEnabled = canWrite;

            if (_currentComments is null)
            {
                CommentTextbox.IsEnabled = false;
                CommentTextbox.Text = "Comments unavailable for this policy source";
            }
            else if (_currentComments.TryGetValue(_currentSetting.UniqueID, out var comment))
            {
                CommentTextbox.IsEnabled = true;
                CommentTextbox.Text = comment;
            }
            else
            {
                CommentTextbox.IsEnabled = true;
                CommentTextbox.Text = "";
            }
        }

        private void ApplyToPolicySource()
        {
            PolicyProcessing.ForgetPolicy(_currentSource, _currentSetting);
            if (EnabledOption.IsChecked == true)
            {
                var options = new Dictionary<string, object>();
                if (_currentSetting.RawPolicy.Elements is not null)
                {
                    foreach (var elem in _currentSetting.RawPolicy.Elements)
                    {
                        if (_elementsById.TryGetValue(elem.ID, out var vm))
                            options.Add(elem.ID, vm.GetValue());
                    }
                }

                PolicyProcessing.SetPolicyState(_currentSource, _currentSetting, PolicyState.Enabled, options);
            }
            else if (DisabledOption.IsChecked == true)
            {
                PolicyProcessing.SetPolicyState(_currentSource, _currentSetting, PolicyState.Disabled, null);
            }

            if (_currentComments is not null)
            {
                if (string.IsNullOrEmpty(CommentTextbox.Text))
                {
                    _currentComments.Remove(_currentSetting.UniqueID);
                }
                else
                {
                    _currentComments[_currentSetting.UniqueID] = CommentTextbox.Text;
                }
            }
        }

        private void StateRadiosChanged(object sender, RoutedEventArgs e)
        {
            if (_elements is null)
                return;

            bool allowOptions = EnabledOption.IsChecked == true;
            foreach (var vm in _elements)
                vm.IsEnabled = allowOptions;
        }

        private void SectionDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_elementsById is null)
                return;

            bool isUser = (SectionDropdown.SelectedItem as ComboBoxItem)?.Content as string == "User";
            _currentSource = isUser ? _userPolSource : _compPolSource;
            _currentLoader = isUser ? _userPolLoader : _compPolLoader;
            _currentComments = isUser ? _userComments : _compComments;
            PreparePolicyState();
        }

        private void ListEditButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ListElementViewModel vm)
                return;

            var result = ListEditorWindow.PresentDialog(this, vm.Label, vm.Data, vm.UserProvidesNames);
            if (result is not null)
                vm.Data = result;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyToPolicySource();
            _accepted = true;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyToPolicySource();
            _changesMade = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_changesMade)
                _accepted = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static bool PresentDialog(
            System.Windows.Window owner,
            PolicyPlusPolicy policy,
            AdmxPolicySection section,
            AdmxBundle workspace,
            IPolicySource compPolSource,
            IPolicySource userPolSource,
            PolicyLoader compPolLoader,
            PolicyLoader userPolLoader,
            Dictionary<string, string> compComments,
            Dictionary<string, string> userComments)
        {
            var window = WpfInterop.PreparePresented(new EditSettingWindow
            {
                _currentSetting = policy,
                _currentSection = section,
                _admxWorkspace = workspace,
                _compPolSource = compPolSource,
                _userPolSource = userPolSource,
                _compPolLoader = compPolLoader,
                _userPolLoader = userPolLoader,
                _compComments = compComments,
                _userComments = userComments,
            }, owner);
            window.ShowDialog();
            return window._accepted;
        }
    }
}
