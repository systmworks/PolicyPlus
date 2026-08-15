using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class Phase0ProofWindow : FluentWindow
    {
        public Phase0ProofWindow()
        {
            InitializeComponent();
            SystemThemeWatcher.Watch(this);
        }

        private void LightButton_Click(object sender, RoutedEventArgs e) => ThemeService.Persist("Light");

        private void DarkButton_Click(object sender, RoutedEventArgs e) => ThemeService.Persist("Dark");

        private void SystemButton_Click(object sender, RoutedEventArgs e) => ThemeService.Persist("System");
    }
}
