using System.Runtime.InteropServices;

namespace Aimmy2.Class
{
    internal class ClickThroughOverlay
    {
        // Thanks to cobble (@castme) for giving me the hint :)
        // Based on: https://social.msdn.microsoft.com/Forums/en-US/a3cb7db6-5014-430f-a5c2-c9746b077d4f/click-through-windows-and-child-image-issue?forum=wpf
        // Nori

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", EntryPoint = "SetPropW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProp(IntPtr hwnd, string name, IntPtr value);

        public static void MakeClickThrough(IntPtr hwnd)
        {
            // Tell the shell before first show that this screen-sized overlay is
            // not a fullscreen app. Otherwise it can push the taskbar underneath.
            SetProp(hwnd, "NonRudeHWND", new IntPtr(1));
            // Click-through, hidden from Alt+Tab, and never steal activation.
            const int transparent = 0x20, toolWindow = 0x80, noActivate = 0x08000000, appWindow = 0x40000;
            SetWindowLong(hwnd, -20, (GetWindowLong(hwnd, -20) | transparent | toolWindow | noActivate) & ~appWindow);
        }
    }
}
