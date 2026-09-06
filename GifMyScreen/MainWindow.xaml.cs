using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace GifMyScreen
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Title = "Bir Görsel, GIF veya Video Seçin",
                Filter = "Tüm Desteklenen Medyalar|*.gif;*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.mp4;*.webm;*.html|" +
                         "Görseller (*.gif, *.png, *.jpg, *.webp, *.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.webp;*.bmp|" +
                         "Videolar (*.mp4, *.webm)|*.mp4;*.webm|" +
                         "Tüm Dosyalar (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtFilePath.Text = openFileDialog.FileName;
                App.WallpaperHost?.SetWallpaper(openFileDialog.FileName);
            }
        }

        private void BtnApplyUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtUrl.Text.Trim();
            if (!string.IsNullOrEmpty(url) && url != "https://")
            {
                App.WallpaperHost?.SetWallpaper(url);
            }
        }

        // Pencere [X] ile kapatıldığında uygulamayı kapatma, sadece gizle (Tray'de kalsın)
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}