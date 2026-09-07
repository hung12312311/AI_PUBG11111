using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InputLogic
{
    public static class GlobalMouseHook
    {
        public static event Func<int, bool>? OnMouseWheel;

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static void Start()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
            }
        }

        public static void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                int mouseData = Marshal.ReadInt32(lParam, 8); // High order word of mouseData
                // mouseData is actually a struct, but offset 8 (in 32-bit layout? No, 64-bit.)
                // MSLLHOOKSTRUCT: pt (8 bytes), mouseData (4 bytes), flags (4 bytes)...
                // Actually mouseData is at offset 8 (point is 2 ints = 8 bytes).
                // Wait, Point is 8 bytes (long) or 2 ints?
                // struct MSLLHOOKSTRUCT { POINT pt; DWORD mouseData; ... }
                // POINT is { LONG x; LONG y; } -> 8 bytes.
                // mouseData is at offset 8.
                // The HIWORD of mouseData indicates the wheel rotation.
                // In C#, taking direct memory might be unsafe if alignment differs.
                // Let's use Marshal.PtrToStructure.
                
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                
                if (OnMouseWheel != null)
                {
                    bool handled = OnMouseWheel.Invoke(delta);
                    if (handled)
                    {
                        return (IntPtr)1; // Block event
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
