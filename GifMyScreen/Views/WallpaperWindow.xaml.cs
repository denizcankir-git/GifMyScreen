using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GifMyScreen.Core;

namespace GifMyScreen.Views
{
    public partial class WallpaperWindow : Window
    {
        private readonly DesktopManager _desktopManager = new();
        private readonly PerformanceWatchdog _watchdog = new(checkIntervalMs: 500);

        private bool _isWebViewReady = false;
        private bool _isSuspended = false;

        public WallpaperWindow()
        {
            InitializeComponent();
            Loaded += WallpaperWindow_Loaded;
            Closed += WallpaperWindow_Closed;
        }

        private async void WallpaperWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            if (_desktopManager.InitializeDesktopHost())
            {
                _desktopManager.AttachWindow(windowHandle);
            }

            try
            {
                await WallpaperWebView.EnsureCoreWebView2Async(null);
                WallpaperWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                WallpaperWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        WallpaperWebView.Visibility = Visibility.Visible;
                        _isWebViewReady = true;
                        _watchdog.Start();
                    }
                };

                _watchdog.FullscreenStateChanged += async (isPaused) =>
                {
                    if (!_isWebViewReady || WallpaperWebView.CoreWebView2 == null) return;

                    try
                    {
                        if (isPaused && !_isSuspended)
                        {
                            bool success = await WallpaperWebView.CoreWebView2.TrySuspendAsync();
                            if (success) _isSuspended = true;
                        }
                        else if (!isPaused && _isSuspended)
                        {
                            WallpaperWebView.CoreWebView2.Resume();
                            _isSuspended = false;
                        }
                    }
                    catch (COMException) { }
                    catch (InvalidOperationException) { }
                };

                // Varsa son seçilen duvar kağıdını yükle
                ConfigManager.Load();
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

        public void SetWallpaper(string sourcePathOrUrl)
        {
            if (WallpaperWebView?.CoreWebView2 == null) return;

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
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Görsel yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Video yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // 3. Web Sayfaları (URL)
            else if (Uri.TryCreate(sourcePathOrUrl, UriKind.Absolute, out Uri? validUri))
            {
                WallpaperWebView.Source = validUri;
            }

            // SetWallpaper metodunun en altına ekleyin:
            ConfigManager.Current.LastWallpaperPath = sourcePathOrUrl;
            ConfigManager.Save(); 
        }

        private void WallpaperWindow_Closed(object? sender, EventArgs e)
        {
            _watchdog.Stop();
        }
    }
}