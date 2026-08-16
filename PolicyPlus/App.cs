using System;
using System.Windows;

namespace PolicyPlus
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
            WpfInterop.EnsureApplication(); // Application.Current is already `app`; this just merges theme resources
            var mainWindow = new Views.MainWindow();
            app.MainWindow = mainWindow;
            app.Run(mainWindow);
        }
    }
}
