using System.Windows;
using System.Windows.Interop;

namespace PolicyPlus
{
    internal static class WpfInterop
    {
        public static void EnsureApplication()
        {
            if (Application.Current is null)
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }
        }

        public static void SetOwner(Window window, System.Windows.Forms.IWin32Window owner)
        {
            EnsureApplication();
            _ = new WindowInteropHelper(window) { Owner = owner.Handle };
        }
    }
}
