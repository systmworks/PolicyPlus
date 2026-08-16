using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    // Replaces the WinForms WideRangeNumericUpDown (a NumericUpDown subclass that patched a
    // hex-parsing bug for values above 0x7FFFFFFF). WPF-UI's NumberBox stores its value as a
    // double, which cannot exactly represent the full ulong range QWord editing needs, so this
    // keeps the value as ulong in code-behind and drives a plain TextBox instead.
    public partial class EditPolNumericDataWindow : FluentWindow
    {
        private ulong _value;
        private ulong _maximum;
        private bool _isHex;
        private bool _accepted;

        public EditPolNumericDataWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
            Loaded += (s, e) =>
            {
                ValueTextBox.Focus();
                ValueTextBox.SelectAll();
            };
        }

        private void CommitText()
        {
            string text = ValueTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    ulong parsed = _isHex
                        ? ulong.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                        : ulong.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
                    _value = Math.Min(parsed, _maximum);
                }
                catch (OverflowException)
                {
                    _value = _maximum;
                }
                catch (FormatException)
                {
                    // Not a valid number - keep the last committed value.
                }
            }

            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            ValueTextBox.Text = _isHex
                ? _value.ToString("X", CultureInfo.InvariantCulture)
                : _value.ToString(CultureInfo.InvariantCulture);
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e) => CommitText();

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitText();
        }

        private void HexCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            CommitText();
            _isHex = HexCheckBox.IsChecked == true;
            UpdateDisplayText();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CommitText();
            _accepted = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static ulong? PresentDialog(System.Windows.Window owner, string valueName, ulong initialData, bool isQword)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolNumericDataWindow { _maximum = isQword ? ulong.MaxValue : uint.MaxValue };
            window.NameTextBox.Text = valueName;
            window._value = Math.Min(initialData, window._maximum);
            window.UpdateDisplayText();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window._value : (ulong?)null;
        }
    }
}
