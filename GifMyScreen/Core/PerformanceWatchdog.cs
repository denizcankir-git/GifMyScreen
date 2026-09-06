using System;
using System.Windows;
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

            // Eğer ön planda masaüstünün kendisi varsa duraklatma yapma
            if (foregroundHandle == IntPtr.Zero ||
                foregroundHandle == _desktopWindow ||
                foregroundHandle == _shellWindow)
            {
                UpdateState(false);
                return;
            }

            if (Win32.GetWindowRect(foregroundHandle, out Win32.RECT rect))
            {
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                // Ön plandaki pencere ana ekranı tamamen veya daha fazla kaplıyor mu?
                bool isFullscreen = (rect.Left <= 0 && rect.Top <= 0 &&
                                     windowWidth >= screenWidth &&
                                     windowHeight >= screenHeight);

                UpdateState(isFullscreen);
            }
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