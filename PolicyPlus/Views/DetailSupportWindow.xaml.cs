using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DetailSupportWindow : FluentWindow
    {
        private class EntryRow
        {
            public string Name;
            public string Min;
            public string Max;
            public PolicyPlusSupportEntry Entry;
        }

        public DetailSupportWindow()
        {
            InitializeComponent();
        }

        private void PrepareDialog(PolicyPlusSupport supported)
        {
            NameTextbox.Text = supported.DisplayName;
            IdTextbox.Text = supported.UniqueID;
            DefinedTextbox.Text = supported.RawSupport.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = supported.RawSupport.DisplayCode;
            LogicTextbox.Text = supported.RawSupport.Logic switch
            {
                AdmxSupportLogicType.AllOf => "Match all the referenced products",
                AdmxSupportLogicType.AnyOf => "Match any of the referenced products",
                AdmxSupportLogicType.Blank => "Do not match products",
                _ => LogicTextbox.Text,
            };

            var rows = new System.Collections.Generic.List<EntryRow>();
            if (supported.Elements is not null)
            {
                foreach (var element in supported.Elements)
                {
                    var row = new EntryRow { Entry = element };
                    if (element.SupportDefinition is not null)
                    {
                        row.Name = element.SupportDefinition.DisplayName;
                    }
                    else if (element.Product is not null)
                    {
                        row.Name = element.Product.DisplayName;
                        if (element.RawSupportEntry.IsRange)
                        {
                            row.Min = element.RawSupportEntry.MinVersion.HasValue ? element.RawSupportEntry.MinVersion.Value.ToString() : "";
                            row.Max = element.RawSupportEntry.MaxVersion.HasValue ? element.RawSupportEntry.MaxVersion.Value.ToString() : "";
                        }
                    }
                    else
                    {
                        row.Name = "<missing: " + element.RawSupportEntry.ProductID + ">";
                    }

                    rows.Add(row);
                }
            }

            EntriesListview.ItemsSource = rows;
        }

        private void EntriesListview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntriesListview.SelectedItem is not EntryRow row)
            {
                return;
            }

            var supEntry = row.Entry;
            if (supEntry.Product is not null)
            {
                DetailProductWindow.PresentDialog(this, supEntry.Product);
            }
            else if (supEntry.SupportDefinition is not null)
            {
                PrepareDialog(supEntry.SupportDefinition);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, PolicyPlusSupport supported)
        {
            ThemeService.ApplyPersisted();
            var window = new DetailSupportWindow();
            window.PrepareDialog(supported);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}
