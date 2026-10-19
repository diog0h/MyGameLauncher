using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameLauncher.Helpers;

public static class NativeMethods
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Barra de título escura (Windows 10 20H1+ / Windows 11).</summary>
    public static void UseDarkTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int)); // versões antigas
        }
        catch { }
    }

    /// <summary>Pinta a barra de título com a cor do tema (só Windows 11; noutros não faz nada).</summary>
    public static void SetCaptionColor(Window window, System.Windows.Media.Color c)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int colorRef = c.R | (c.G << 8) | (c.B << 16); // COLORREF = 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, 35, ref colorRef, sizeof(int));
        }
        catch { }
    }
}
