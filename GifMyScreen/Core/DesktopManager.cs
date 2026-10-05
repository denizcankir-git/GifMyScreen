using System;
using System.Text;
using GifMyScreen.Core.Native;

namespace GifMyScreen.Core
{
    /// <summary>
    /// Duvar kağıdı penceresini masaüstü simgelerinin arkasına yerleştirir.
    ///
    /// İki farklı Explorer düzeni desteklenir:
    /// - Klasik (Windows 10 / Windows 11 23H2 ve öncesi): 0x052C mesajından sonra simgeleri barındıran
    ///   üst düzey pencerenin hemen arkasında ayrı bir üst düzey WorkerW oluşur; pencere onun çocuğu yapılır.
    /// - Yükseltilmiş masaüstü (Windows 11 24H2 ve sonrası): SHELLDLL_DefView ve WorkerW doğrudan Progman'ın
    ///   çocuklarıdır. Pencere Progman'ın çocuğu yapılır ve Z-sırasında SHELLDLL_DefView ile WorkerW arasına konur.
    /// </summary>
    public class DesktopManager
    {
        private IntPtr _progman = IntPtr.Zero;
        private IntPtr _shellView = IntPtr.Zero;
        private IntPtr _workerW = IntPtr.Zero;
        private IntPtr _embeddedWindow = IntPtr.Zero;

        /// <summary>Pencerenin bağlandığı ebeveyn (klasik düzende WorkerW, 24H2+ düzeninde Progman).</summary>
        public IntPtr HostHandle { get; private set; } = IntPtr.Zero;

        /// <summary>Windows 11 24H2+ yükseltilmiş masaüstü düzeni algılandı mı?</summary>
        public bool IsRaisedDesktop { get; private set; }

        public bool InitializeDesktopHost()
        {
            HostHandle = IntPtr.Zero;

            // 1. Program Manager penceresini yakala
            _progman = Win32.FindWindow("Progman", null);
            if (_progman == IntPtr.Zero) return false;

            // 2. Windows Gezgini'ne simgelerin arkasında WorkerW üretmesi için sinyal gönder
            Win32.SendMessageTimeout(_progman, Win32.WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            // 3a. 24H2+ düzeni: SHELLDLL_DefView ve WorkerW doğrudan Progman'ın altında
            _shellView = Win32.FindWindowEx(_progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (_shellView != IntPtr.Zero)
            {
                _workerW = Win32.FindWindowEx(_progman, IntPtr.Zero, "WorkerW", null);
                IsRaisedDesktop = true;
                HostHandle = _progman;
                return true;
            }

            // 3b. Klasik düzen: simgeleri barındıran üst düzey pencerenin arkasındaki kardeş WorkerW
            IntPtr workerw = IntPtr.Zero;
            Win32.EnumWindows((tophandle, lParam) =>
            {
                IntPtr shellView = Win32.FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    _shellView = shellView;
                    workerw = Win32.FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            _workerW = workerw;
            IsRaisedDesktop = false;
            HostHandle = workerw;
            return HostHandle != IntPtr.Zero;
        }

        /// <summary>
        /// Pencereyi WS_CHILD stiline çevirip masaüstü katmanına bağlar ve ana monitörü kaplayacak şekilde konumlandırır.
        /// SetParent belgelerine uygun olarak stil değişikliği SetParent'tan önce yapılır.
        /// </summary>
        public bool AttachWindow(IntPtr windowHandle)
        {
            if (HostHandle == IntPtr.Zero || windowHandle == IntPtr.Zero) return false;

            long originalStyle = Win32.GetWindowLong(windowHandle, Win32.GWL_STYLE);
            long originalExStyle = Win32.GetWindowLong(windowHandle, Win32.GWL_EXSTYLE);
            _embeddedWindow = windowHandle;

            long style = originalStyle & ~(Win32.WS_POPUP | Win32.WS_CAPTION | Win32.WS_THICKFRAME |
                                           Win32.WS_SYSMENU | Win32.WS_MINIMIZEBOX | Win32.WS_MAXIMIZEBOX);
            Win32.SetWindowLong(windowHandle, Win32.GWL_STYLE, style | Win32.WS_CHILD);

            long exStyle = originalExStyle & ~Win32.WS_EX_APPWINDOW;
            if (IsRaisedDesktop)
            {
                // 24H2+: Progman WS_EX_NOREDIRECTIONBITMAP ile çizildiği için çocuk pencerenin
                // DWM tarafından birleştirilebilmesi için katmanlı (layered) olması gerekir.
                exStyle |= Win32.WS_EX_LAYERED;
            }
            Win32.SetWindowLong(windowHandle, Win32.GWL_EXSTYLE, exStyle);

            if (IsRaisedDesktop)
            {
                Win32.SetLayeredWindowAttributes(windowHandle, 0, 255, Win32.LWA_ALPHA);
            }

            if (Win32.SetParent(windowHandle, HostHandle) == IntPtr.Zero)
            {
                // Bağlanamadı: pencereyi özgün üst düzey haline geri döndür
                _embeddedWindow = IntPtr.Zero;
                Win32.SetWindowLong(windowHandle, Win32.GWL_STYLE, originalStyle);
                Win32.SetWindowLong(windowHandle, Win32.GWL_EXSTYLE, originalExStyle);
                return false;
            }

            FitToPrimaryMonitor(windowHandle);
            return true;
        }

        /// <summary>
        /// Gömülü pencerenin WM_STYLECHANGING mesajında çağrılır ve gereken bitleri (WS_CHILD, 24H2+ için WS_EX_LAYERED) korur.
        /// WPF'in HwndTarget katmanı AllowsTransparency kapalıyken WS_EX_LAYERED'ı her stil değişiminde temizler;
        /// bu yüzden ek stil mesajı burada sonuçlandırılmalı (true döner) ve WPF'e iletilmemelidir.
        /// </summary>
        public bool EnforceEmbeddedStyle(IntPtr windowHandle, int styleIndex, ref Win32.STYLESTRUCT style)
        {
            if (windowHandle == IntPtr.Zero || windowHandle != _embeddedWindow) return false;

            if (styleIndex == Win32.GWL_STYLE)
            {
                style.styleNew = (uint)((style.styleNew | Win32.WS_CHILD) & ~Win32.WS_POPUP);
                return false;
            }

            if (styleIndex == Win32.GWL_EXSTYLE && IsRaisedDesktop)
            {
                style.styleNew |= (uint)Win32.WS_EX_LAYERED;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Pencereyi ana monitörün fiziksel piksel sınırlarına oturtur.
        /// Çocuk pencere koordinatları ebeveynin istemci alanına göre olduğundan ekran koordinatları dönüştürülür.
        /// </summary>
        public void FitToPrimaryMonitor(IntPtr windowHandle)
        {
            if (HostHandle == IntPtr.Zero || windowHandle == IntPtr.Zero) return;

            IntPtr primary = Win32.MonitorFromPoint(new Win32.POINT { X = 0, Y = 0 }, Win32.MONITOR_DEFAULTTOPRIMARY);
            if (!Win32.TryGetMonitorInfo(primary, out Win32.MONITORINFO info)) return;

            Win32.RECT bounds = info.rcMonitor;
            Win32.MapWindowPoints(IntPtr.Zero, HostHandle, ref bounds, 2);

            if (IsRaisedDesktop)
            {
                // Z-sırası: SHELLDLL_DefView (simgeler) > duvar kağıdı > WorkerW (statik duvar kağıdı)
                Win32.SetWindowPos(windowHandle, _shellView, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                    Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);

                if (_workerW != IntPtr.Zero)
                {
                    Win32.SetWindowPos(_workerW, windowHandle, 0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            }
            else
            {
                Win32.SetWindowPos(windowHandle, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                    Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
            }
        }

        /// <summary>
        /// Windows'un mevcut duvar kağıdını kalıcı ayarlara dokunmadan yeniden uygular.
        /// Uygulama kapandıktan sonra masaüstünde son karenin asılı kalmasını engeller.
        /// </summary>
        public static void RefreshSystemWallpaper()
        {
            var current = new StringBuilder(Win32.MAX_PATH);
            if (Win32.SystemParametersInfo(Win32.SPI_GETDESKWALLPAPER, (uint)current.Capacity, current, 0))
            {
                Win32.SystemParametersInfo(Win32.SPI_SETDESKWALLPAPER, 0, current.ToString(), 0);
            }
        }
    }
}
