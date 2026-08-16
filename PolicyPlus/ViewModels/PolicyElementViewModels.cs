using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PolicyPlus.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    // Base for one row in EditSetting's dynamic "extra options" area. Mirrors elemDict/
    // ElementControls: an instance exists per <presentation> element, and only the ones with
    // a non-empty Id correspond to a <policy> element that GetPolicyOptionStates/SetPolicyState
    // read and write.
    public abstract class PolicyElementViewModel : ObservableObject
    {
        public string Id { get; }
        public string Label { get; }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        protected PolicyElementViewModel(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public virtual void LoadValue(object value) { }
        public virtual object GetValue() => null;
    }

    public sealed class LabelElementViewModel : PolicyElementViewModel
    {
        public string Text { get; }

        public LabelElementViewModel(string id, string text) : base(id, "")
        {
            Text = text;
        }
    }

    public sealed class CheckBoxElementViewModel : PolicyElementViewModel
    {
        public string Text { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }

        public CheckBoxElementViewModel(string id, string text, bool defaultState) : base(id, "")
        {
            Text = text;
            _isChecked = defaultState;
        }

        public override void LoadValue(object value) => IsChecked = Convert.ToBoolean(value);
        public override object GetValue() => IsChecked;
    }

    public sealed class DecimalElementViewModel : PolicyElementViewModel
    {
        public double Minimum { get; }
        public double Maximum { get; }
        public double Increment { get; }
        public bool HasSpinner { get; }

        private double? _value;
        public double? Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public DecimalElementViewModel(string id, string label, double minimum, double maximum, double increment, bool hasSpinner, double defaultValue)
            : base(id, label)
        {
            Minimum = minimum;
            Maximum = maximum;
            Increment = increment;
            HasSpinner = hasSpinner;
            _value = defaultValue;
        }

        public override void LoadValue(object value) => Value = Convert.ToUInt32(value);
        public override object GetValue() => (uint)Math.Round(Math.Clamp(Value ?? 0, Minimum, Maximum));
    }

    public sealed class TextBoxElementViewModel : PolicyElementViewModel
    {
        public int MaxLength { get; }

        private string _text = "";
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public TextBoxElementViewModel(string id, string label, int maxLength, string defaultValue) : base(id, label)
        {
            MaxLength = maxLength;
            _text = defaultValue ?? "";
        }

        public override void LoadValue(object value) => Text = Convert.ToString(value) ?? "";
        public override object GetValue() => Text;
    }

    public sealed class ComboBoxElementViewModel : PolicyElementViewModel
    {
        public int MaxLength { get; }
        public IReadOnlyList<string> Suggestions { get; }

        private string _text = "";
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public ComboBoxElementViewModel(string id, string label, int maxLength, string defaultText, IEnumerable<string> suggestions, bool sorted)
            : base(id, label)
        {
            MaxLength = maxLength;
            _text = defaultText ?? "";
            Suggestions = sorted ? suggestions.OrderBy(s => s, StringComparer.CurrentCulture).ToList() : suggestions.ToList();
        }

        public override void LoadValue(object value) => Text = Convert.ToString(value) ?? "";
        public override object GetValue() => Text;
    }

    public sealed class DropdownOption
    {
        public int Index { get; }
        public string DisplayName { get; }

        public DropdownOption(int index, string displayName)
        {
            Index = index;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    public sealed class DropDownElementViewModel : PolicyElementViewModel
    {
        public IReadOnlyList<DropdownOption> Items { get; }

        private DropdownOption _selectedItem;
        public DropdownOption SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public DropDownElementViewModel(string id, string label, IReadOnlyList<DropdownOption> items, int? defaultItemIndex)
            : base(id, label)
        {
            Items = items;
            _selectedItem = defaultItemIndex.HasValue ? items.FirstOrDefault(i => i.Index == defaultItemIndex.Value) : null;
        }

        public override void LoadValue(object value)
        {
            int index = Convert.ToInt32(value);
            SelectedItem = Items.FirstOrDefault(i => i.Index == index);
        }

        public override object GetValue() => SelectedItem?.Index ?? 0;
    }

    public sealed class ListElementViewModel : PolicyElementViewModel
    {
        public bool UserProvidesNames { get; }

        private object _data;
        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public ListElementViewModel(string id, string label, bool userProvidesNames) : base(id, label)
        {
            UserProvidesNames = userProvidesNames;
        }

        public override void LoadValue(object value) => Data = value;
        public override object GetValue() => Data;
    }

    public sealed class MultiTextBoxElementViewModel : PolicyElementViewModel
    {
        private string _text = "";
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public MultiTextBoxElementViewModel(string id, string label) : base(id, label)
        {
        }

        public override void LoadValue(object value)
        {
            var lines = (string[])value;
            Text = string.Join("\r\n", lines);
        }

        public override object GetValue() =>
            Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }
}
