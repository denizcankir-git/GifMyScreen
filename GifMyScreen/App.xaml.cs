using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using GifMyScreen.Core;
using GifMyScreen.Views;
using Application = System.Windows.Application;

namespace GifMyScreen
{
    public partial class App : Application
    {
        public static WallpaperWindow? WallpaperHost { get; private set; }
        public static MainWindow? ControlPanel { get; private set; }

        private NotifyIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ayarlar tek noktadan yüklenir; pencereler ConfigManager.Current üzerinden okur
            ConfigManager.Load();

            // 1. Masaüstü katmanını arka planda başlat
            WallpaperHost = new WallpaperWindow();
            WallpaperHost.Show();

            // 2. Kontrol Panelini başlat ve ekrana getir
            ControlPanel = new MainWindow();
            ControlPanel.Show();

            // 3. Sistem Tepsisi İkonu ve Menüsü
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // İleride projenin .ico dosyası bağlanabilir
                Visible = true,
                Text = "Gif My Screen"
            };

            ContextMenuStrip contextMenu = new();
            contextMenu.Items.Add("Kontrol Panelini Aç", null, (s, args) => ShowControlPanel());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Çıkış", null, (s, args) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, args) => ShowControlPanel();
        }

        private void ShowControlPanel()
        {
            if (ControlPanel != null)
            {
                ControlPanel.Show();
                ControlPanel.WindowState = WindowState.Normal;
                ControlPanel.Activate();
            }
        }

        private void ExitApplication()
        {
            _notifyIcon?.Dispose();
            WallpaperHost?.Close();
            Shutdown();
        }
    }
}