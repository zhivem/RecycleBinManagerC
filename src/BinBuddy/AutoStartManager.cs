using Microsoft.Win32;

namespace BinBuddy.src.BinBuddy
{
    public static class AutoStartManager
    {
        private const string AppName = "RecycleBinManager";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly string AppPath = Application.ExecutablePath;

        public static bool IsEnabled() => GetRegistryValue()?.Equals(AppPath, StringComparison.OrdinalIgnoreCase) == true;

        public static void Enable()
        {
            using var key = GetRegistryKey(true);
            key?.SetValue(AppName, AppPath, RegistryValueKind.String);
        }

        public static void Disable()
        {
            using var key = GetRegistryKey(true);
            key?.DeleteValue(AppName, false);
        }

        public static void Toggle()
        {
            if (IsEnabled())
                Disable();
            else
                Enable();
        }

        public static string GetStatus() => IsEnabled() ? "Включен" : "Отключен";

        private static string? GetRegistryValue()
        {
            using var key = GetRegistryKey(false);
            return key?.GetValue(AppName) as string;
        }

        private static RegistryKey? GetRegistryKey(bool writable) =>
            Registry.CurrentUser.OpenSubKey(RegistryPath, writable);
    }
}