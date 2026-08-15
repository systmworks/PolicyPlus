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
