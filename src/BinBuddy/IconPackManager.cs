using System.NativeTray;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy
{
    public static class IconPackManager
    {
        private static Icon? _emptyIcon;
        private static Icon? _fullIcon;
        private static readonly Lock _iconLock = new();

        public static void ApplyIconPack(string packName, TrayIconHost trayIcon)
        {
            ArgumentNullException.ThrowIfNull(trayIcon);

            string emptyIconPath = GetIconPath(packName, "recycle-empty.ico");
            string fullIconPath = GetIconPath(packName, "recycle-full.ico");

            if (!File.Exists(emptyIconPath) || !File.Exists(fullIconPath))
                return;

            lock (_iconLock)
            {
                _emptyIcon?.Dispose();
                _fullIcon?.Dispose();

                _emptyIcon = new Icon(emptyIconPath);
                _fullIcon = new Icon(fullIconPath);
            }

            trayIcon.Icon = (IsRecycleBinEmpty() ? _emptyIcon : _fullIcon).Handle;
            SaveCurrentPack(packName);
        }

        public static void UpdateIconsBasedOnState(TrayIconHost trayIcon, bool isEmpty)
        {
            ArgumentNullException.ThrowIfNull(trayIcon);

            if (isEmpty && _emptyIcon != null)
                trayIcon.Icon = _emptyIcon.Handle;
            else if (!isEmpty && _fullIcon != null)
                trayIcon.Icon = _fullIcon.Handle;
        }

        public static void DisposeIcons()
        {
            lock (_iconLock)
            {
                _emptyIcon?.Dispose();
                _fullIcon?.Dispose();
                _emptyIcon = _fullIcon = null;
            }
        }

        public static string LoadCurrentPack() =>
            SettingsManager.LoadSettings().CurrentIconPack ?? "default";

        private static string GetIconPath(string packName, string iconName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName, iconName);

        private static void SaveCurrentPack(string packName)
        {
            var settings = SettingsManager.LoadSettings();
            settings.CurrentIconPack = packName;
            SettingsManager.SaveSettings(settings);
        }

        private static bool IsRecycleBinEmpty()
        {
            var rbInfo = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
            SHQueryRecycleBin(null, ref rbInfo);
            return rbInfo.i64NumItems == 0;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }
    }
}