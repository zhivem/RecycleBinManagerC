using System.Diagnostics;
using System.Text.Json;

namespace BinBuddy.src.BinBuddy
{
    internal static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RecycleBinManager",
            "settings.json"
        );

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static AppSettings? _cachedSettings;
        private static readonly Lock _lock = new();

        public static AppSettings LoadSettings()
        {
            lock (_lock)
            {
                if (_cachedSettings is not null)
                    return _cachedSettings;

                try
                {
                    if (!File.Exists(SettingsFilePath))
                    {
                        _cachedSettings = new AppSettings();
                        SaveSettings(_cachedSettings);
                        return _cachedSettings;
                    }

                    var json = File.ReadAllText(SettingsFilePath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                    _cachedSettings.Normalize();
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
                    return _cachedSettings = new AppSettings();
                }
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            lock (_lock)
            {
                try
                {
                    string directory = Path.GetDirectoryName(SettingsFilePath)!;
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var json = JsonSerializer.Serialize(settings, _jsonOptions);
                    File.WriteAllText(SettingsFilePath, json);
                    _cachedSettings = settings;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
                }
            }
        }

        public static void ReloadSettings()
        {
            lock (_lock)
            {
                _cachedSettings = null;
                LoadSettings();
            }
        }

        public static void ResetToDefaults() => SaveSettings(new AppSettings());
    }

    public class AppSettings
    {
        public bool ShowNotifications { get; set; } = true;
        public bool ShowRecycleBinOnDesktop { get; set; } = true;
        public bool AutoStartEnabled { get; set; } = false;
        public string CurrentIconPack { get; set; } = "default";
        public int UpdateIntervalSeconds { get; set; } = 1;

        public AppSettings Clone() => new()
        {
            ShowNotifications = ShowNotifications,
            ShowRecycleBinOnDesktop = ShowRecycleBinOnDesktop,
            AutoStartEnabled = AutoStartEnabled,
            CurrentIconPack = CurrentIconPack,
            UpdateIntervalSeconds = UpdateIntervalSeconds
        };

        public void Normalize()
        {
            UpdateIntervalSeconds = Math.Clamp(UpdateIntervalSeconds, 1, 60);
            CurrentIconPack ??= "default";
        }
    }
}