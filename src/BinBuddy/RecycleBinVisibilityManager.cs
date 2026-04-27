using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy
{
    public static class RecycleBinVisibilityManager
    {
        private const string DesktopKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        private const string RecycleBinValue = "{645FF040-5081-101B-9F08-00AA002F954E}";

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_FLUSH = 0x1000;

        public static bool IsRecycleBinVisible() => Registry.GetValue(DesktopKey, RecycleBinValue, 0) is 0;

        public static void SetRecycleBinVisibility(bool isVisible)
        {
            Registry.SetValue(DesktopKey, RecycleBinValue, isVisible ? 0 : 1, RegistryValueKind.DWord);
            RefreshDesktop();
        }

        public static void ShowRecycleBin() => SetRecycleBinVisibility(true);
        public static void HideRecycleBin() => SetRecycleBinVisibility(false);
        public static void ToggleRecycleBin() => SetRecycleBinVisibility(!IsRecycleBinVisible());
        public static string GetVisibilityStatus() => IsRecycleBinVisible() ? "Видна" : "Скрыта";

        private static void RefreshDesktop() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}