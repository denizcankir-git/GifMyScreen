using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace GifMyScreen.Core
{
    public class AppSettings
    {
        public string LastWallpaperPath { get; set; } = string.Empty;
        public bool RunAtStartup { get; set; } = false;
    }

    public static class ConfigManager
    {
        private const string AppName = "GifMyScreen";
        private const string RegistryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppName
        );

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

        public static AppSettings Current { get; private set; } = new();

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                Current = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }

        public static void SetStartup(bool enable)
        {
            Current.RunAtStartup = enable;
            Save();

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true);
                if (key == null) return;

                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                if (enable)
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }
    }
}