using System.Windows;
using System.Windows.Controls;
using PolicyPlus.ViewModels;

namespace PolicyPlus.Views
{
    public class PolicyElementTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LabelTemplate { get; set; }
        public DataTemplate CheckBoxTemplate { get; set; }
        public DataTemplate DecimalTemplate { get; set; }
        public DataTemplate TextBoxTemplate { get; set; }
        public DataTemplate ComboBoxTemplate { get; set; }
        public DataTemplate DropDownTemplate { get; set; }
        public DataTemplate ListTemplate { get; set; }
        public DataTemplate MultiTextBoxTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) => item switch
        {
            LabelElementViewModel => LabelTemplate,
            CheckBoxElementViewModel => CheckBoxTemplate,
            DecimalElementViewModel => DecimalTemplate,
            TextBoxElementViewModel => TextBoxTemplate,
            ComboBoxElementViewModel => ComboBoxTemplate,
            DropDownElementViewModel => DropDownTemplate,
            ListElementViewModel => ListTemplate,
            MultiTextBoxElementViewModel => MultiTextBoxTemplate,
            _ => null,
        };
    }
}
