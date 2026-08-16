using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    // Maps NumericBoxPresentationElement.HasSpinner (spin="") to NumberBox's spin-button display.
    public class SpinButtonPlacementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (bool)value ? NumberBoxSpinButtonPlacementMode.Inline : NumberBoxSpinButtonPlacementMode.Hidden;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
