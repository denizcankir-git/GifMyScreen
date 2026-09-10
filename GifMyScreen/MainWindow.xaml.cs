using System.ComponentModel;
using System.Windows;
using GifMyScreen.Core;

namespace GifMyScreen
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            ConfigManager.Load();
            TxtFilePath.Text = ConfigManager.Current.LastWallpaperPath;
            ChkAutoStart.IsChecked = ConfigManager.Current.RunAtStartup;
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

        private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkAutoStart.IsChecked ?? false;
            ConfigManager.SetStartup(isChecked);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}