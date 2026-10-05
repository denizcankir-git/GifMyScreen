using System;
using System.Windows.Threading;
using GifMyScreen.Core.Native;

namespace GifMyScreen.Core
{
    public class PerformanceWatchdog
    {
        private readonly DispatcherTimer _timer;
        private readonly IntPtr _desktopWindow;
        private readonly IntPtr _shellWindow;
        private bool _isCurrentlyPaused = false;

        public event Action<bool>? FullscreenStateChanged;

        public PerformanceWatchdog(int checkIntervalMs = 500)
        {
            _desktopWindow = Win32.GetDesktopWindow();
            _shellWindow = Win32.GetShellWindow();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(checkIntervalMs)
            };
            _timer.Tick += (s, e) => CheckForegroundWindow();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void CheckForegroundWindow()
        {
            IntPtr foregroundHandle = Win32.GetForegroundWindow();

            // Ön planda masaüstünün kendisi varsa duraklatma yapma
            if (foregroundHandle == IntPtr.Zero || IsDesktopWindow(foregroundHandle))
            {
                UpdateState(false);
                return;
            }

            UpdateState(CoversPrimaryMonitor(foregroundHandle));
        }

        private bool IsDesktopWindow(IntPtr hwnd)
        {
            if (hwnd == _desktopWindow || hwnd == _shellWindow) return true;

            // Klasik düzende masaüstüne tıklanınca ön plana simgeleri barındıran WorkerW gelir
            string className = Win32.GetClassName(hwnd);
            return className is "Progman" or "WorkerW";
        }

        /// <summary>
        /// Pencere, duvar kağıdının bulunduğu ana monitörü tamamen kaplıyor mu?
        /// Pencere ve monitör sınırları aynı API ailesinden alındığı için DPI ölçeklemesinden bağımsızdır.
        /// </summary>
        private static bool CoversPrimaryMonitor(IntPtr hwnd)
        {
            if (!Win32.GetWindowRect(hwnd, out Win32.RECT rect)) return false;

            IntPtr monitor = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONULL);
            if (!Win32.TryGetMonitorInfo(monitor, out Win32.MONITORINFO info)) return false;
            if ((info.dwFlags & Win32.MONITORINFOF_PRIMARY) == 0) return false;

            Win32.RECT bounds = info.rcMonitor;
            return rect.Left <= bounds.Left &&
                   rect.Top <= bounds.Top &&
                   rect.Right >= bounds.Right &&
                   rect.Bottom >= bounds.Bottom;
        }

        private void UpdateState(bool shouldPause)
        {
            if (_isCurrentlyPaused != shouldPause)
            {
                _isCurrentlyPaused = shouldPause;
                FullscreenStateChanged?.Invoke(_isCurrentlyPaused);
            }
        }
    }
}
