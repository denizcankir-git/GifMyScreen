using System;
using GifMyScreen.Core.Native;

namespace GifMyScreen.Core
{
    public class DesktopManager
    {
        public IntPtr WorkerWHandle { get; private set; } = IntPtr.Zero;

        public bool InitializeDesktopHost()
        {
            // 1. Program Manager penceresini yakala
            IntPtr progman = Win32.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;

            IntPtr result;
            // 2. Windows Gezgini'ne simgelerin arkasında WorkerW üretmesi için sinyal gönder
            Win32.SendMessageTimeout(progman, Win32.WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out result);

            IntPtr workerw = IntPtr.Zero;

            // 3. Masaüstü simgelerini (SHELLDLL_DefView) barındıran pencerenin kardeş WorkerW'sunu bul
            Win32.EnumWindows((tophandle, lParam) =>
            {
                IntPtr shellView = Win32.FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerw = Win32.FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
                }
                return true;
            }, IntPtr.Zero);

            WorkerWHandle = workerw;
            return WorkerWHandle != IntPtr.Zero;
        }

        public void AttachWindow(IntPtr windowHandle)
        {
            if (WorkerWHandle != IntPtr.Zero && windowHandle != IntPtr.Zero)
            {
                Win32.SetParent(windowHandle, WorkerWHandle);
            }
        }
    }
}