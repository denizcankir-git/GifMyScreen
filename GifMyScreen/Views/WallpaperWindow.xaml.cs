using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using GifMyScreen.Core;
using GifMyScreen.Core.Native;
using Microsoft.Win32;

namespace GifMyScreen.Views
{
    public partial class WallpaperWindow : Window
    {
        private readonly DesktopManager _desktopManager = new();
        private readonly PerformanceWatchdog _watchdog = new(checkIntervalMs: 500);

        private IntPtr _windowHandle = IntPtr.Zero;
        private bool _isAttached = false;
        private bool _isWebViewReady = false;
        private bool _isPaused = false;

        public WallpaperWindow()
        {
            InitializeComponent();
            SourceInitialized += WallpaperWindow_SourceInitialized;
            Loaded += WallpaperWindow_Loaded;
            Closed += WallpaperWindow_Closed;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        // HWND oluştuğu anda, pencere ekranda görünmeden önce masaüstüne gömülür
        private void WallpaperWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(_windowHandle)?.AddHook(WndProc);
            _isAttached = _desktopManager.InitializeDesktopHost() && _desktopManager.AttachWindow(_windowHandle);

            if (!_isAttached)
            {
                // Masaüstü katmanı bulunamadı: en azından diğer pencerelerin önüne çıkmasın
                System.Diagnostics.Debug.WriteLine("GifMyScreen: Masaüstü katmanı bulunamadı, pencere en alta alınıyor.");
                Win32.SetWindowPos(_windowHandle, Win32.HWND_BOTTOM, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Gömülü pencere için gereken stil bitlerini WPF'in stil yönetimine karşı koru
            if (msg == Win32.WM_STYLECHANGING)
            {
                var style = Marshal.PtrToStructure<Win32.STYLESTRUCT>(lParam);
                handled = _desktopManager.EnforceEmbeddedStyle(hwnd, (int)wParam.ToInt64(), ref style);
                Marshal.StructureToPtr(style, lParam, false);
            }
            return IntPtr.Zero;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // Çözünürlük veya ölçek değiştiğinde pencereyi yeni monitör sınırlarına oturt
            Dispatcher.BeginInvoke(() =>
            {
                if (_isAttached) _desktopManager.FitToPrimaryMonitor(_windowHandle);
            });
        }

        private async void WallpaperWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // WPF ilk gösterimde kendi Left/Top/Width/Height değerlerini uygulayabilir; fiziksel sınırları yeniden uygula
            if (_isAttached) _desktopManager.FitToPrimaryMonitor(_windowHandle);

            try
            {
                // Başlatmadan önce ayarlanır; ilk karede beyaz parlama olmaz, WPF arka planı görünür
                WallpaperWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                await WallpaperWebView.EnsureCoreWebView2Async(null);

                WallpaperWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        _isWebViewReady = true;
                        _watchdog.Start();
                    }
                };

                _watchdog.FullscreenStateChanged += async (isPaused) => await SetPausedAsync(isPaused);

                // Varsa son seçilen duvar kağıdını yükle
                if (!string.IsNullOrEmpty(ConfigManager.Current.LastWallpaperPath) &&
                    (File.Exists(ConfigManager.Current.LastWallpaperPath) || Uri.IsWellFormedUriString(ConfigManager.Current.LastWallpaperPath, UriKind.Absolute)))
                {
                    SetWallpaper(ConfigManager.Current.LastWallpaperPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"WebView2 başlatılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Tam ekran uygulama varken WebView2'yi gizler ve askıya alır; masaüstüne dönüldüğünde geri getirir.
        /// TrySuspendAsync yalnızca görünmez bir WebView2'de çalışır; görünürken çağrılırsa
        /// ERROR_INVALID_STATE (COMException 0x8007139F) fırlatır. Bu yüzden önce gizlenir.
        /// </summary>
        private async Task SetPausedAsync(bool pause)
        {
            if (!_isWebViewReady || WallpaperWebView.CoreWebView2 == null || pause == _isPaused) return;

            _isPaused = pause;
            try
            {
                if (pause)
                {
                    WallpaperWebView.Visibility = Visibility.Hidden;
                    bool suspended = await WallpaperWebView.CoreWebView2.TrySuspendAsync();

                    // Askıya alma sürerken masaüstüne dönüldüyse WebView2 askıda kalmasın
                    if (suspended && !_isPaused) WallpaperWebView.CoreWebView2.Resume();
                }
                else
                {
                    if (WallpaperWebView.CoreWebView2.IsSuspended) WallpaperWebView.CoreWebView2.Resume();
                    WallpaperWebView.Visibility = Visibility.Visible;
                }
            }
            catch (COMException) { }
            catch (InvalidOperationException) { }
        }

        public void SetWallpaper(string sourcePathOrUrl)
        {
            if (TryLoadWallpaper(sourcePathOrUrl))
            {
                ConfigManager.Current.LastWallpaperPath = sourcePathOrUrl;
                ConfigManager.Save();
            }
        }

        private bool TryLoadWallpaper(string sourcePathOrUrl)
        {
            if (WallpaperWebView?.CoreWebView2 == null) return false;

            string ext = Path.GetExtension(sourcePathOrUrl).ToLower();

            // GIF ve Statik Görseller
            if (ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".svg")
            {
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(sourcePathOrUrl);
                    string base64 = Convert.ToBase64String(imageBytes);
                    string mimeType = ext switch
                    {
                        ".gif" => "image/gif",
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        ".bmp" => "image/bmp",
                        ".svg" => "image/svg+xml",
                        _ => "image/jpeg"
                    };

                    string html = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"">
            <style>
                * {{ margin: 0; padding: 0; overflow: hidden; }}
                body {{ 
                    background: #0F172A; 
                    display: flex; 
                    align-items: center; 
                    justify-content: center; 
                    height: 100vh; 
                    width: 100vw; 
                }}
                img {{ 
                    width: 100vw; 
                    height: 100vh; 
                    object-fit: cover; 
                    user-select: none; 
                }}
            </style>
        </head>
        <body>
            <img src=""data:{mimeType};base64,{base64}"" />
        </body>
        </html>";

                    WallpaperWebView.NavigateToString(html);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Görsel yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            // 2. Video Dosyaları (.mp4, .webm)
            else if (ext == ".mp4" || ext == ".webm")
            {
                try
                {
                    string? folder = Path.GetDirectoryName(sourcePathOrUrl);
                    string fileName = Path.GetFileName(sourcePathOrUrl);

                    if (!string.IsNullOrEmpty(folder))
                    {
                        WallpaperWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                            "videohost.local",
                            folder,
                            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
                        );

                        string safeFileName = Uri.EscapeDataString(fileName);
                        string html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        * {{ margin: 0; padding: 0; overflow: hidden; }}
                        body {{ background: #000; height: 100vh; width: 100vw; }}
                        video {{ width: 100vw; height: 100vh; object-fit: cover; }}
                    </style>
                </head>
                <body>
                    <video autoplay loop muted playsinline src=""https://videohost.local/{safeFileName}""></video>
                </body>
                </html>";

                        WallpaperWebView.NavigateToString(html);
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Video yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            // 3. Web Sayfaları (URL)
            else if (Uri.TryCreate(sourcePathOrUrl, UriKind.Absolute, out Uri? validUri))
            {
                WallpaperWebView.Source = validUri;
                return true;
            }

            return false;
        }

        private void WallpaperWindow_Closed(object? sender, EventArgs e)
        {
            _watchdog.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            // Gömülü pencere kalktıktan sonra masaüstünde son karenin asılı kalmasını engelle
            if (_isAttached) DesktopManager.RefreshSystemWallpaper();
        }
    }
}