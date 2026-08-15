using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        // Lets a WPF window act as the "owner" for a PresentDialog(IWin32Window owner, ...) call,
        // since every PresentDialog signature is IWin32Window-based for uniformity with the many
        // still-WinForms callers (Main, EditPol, etc.).
        public static System.Windows.Forms.IWin32Window AsIWin32Window(Window window) =>
            new Win32WindowAdapter(new WindowInteropHelper(window).EnsureHandle());

        private sealed class Win32WindowAdapter : System.Windows.Forms.IWin32Window
        {
            public Win32WindowAdapter(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }

        public static ImageSource ToImageSource(System.Drawing.Image image)
        {
            if (image is null)
            {
                return null;
            }

            using var bitmap = new System.Drawing.Bitmap(image);
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                PInvoke.DeleteObject(hBitmap);
            }
        }
    }
}
