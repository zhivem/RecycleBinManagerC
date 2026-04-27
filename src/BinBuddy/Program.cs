using System.Diagnostics;
using System.NativeTray;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy
{
    internal static class Program
    {
        private static TrayIconHost? _trayIcon;
        private static AppSettings _settings = null!;
        private static System.Windows.Forms.Timer? _timer;
        private static bool _previousRecycleBinState;

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            _settings = SettingsManager.LoadSettings();
            _previousRecycleBinState = IsRecycleBinEmpty();

            InitializeTrayIcon();
            InitializeTimer();
            ApplyInitialSettings();

            _ = CheckForUpdatesAsync();

            Application.Run();
        }

        private static void InitializeTrayIcon()
        {
            _trayIcon = new TrayIconHost
            {
                ToolTipText = "BinBuddy",
                Icon = LoadTrayIcon(),
                ThemeMode = TrayThemeMode.System,
                Menu = CreateContextMenu()
            };

            _trayIcon.LeftDoubleClick += (_, _) => OpenRecycleBin();
        }

        private static void InitializeTimer()
        {
            _timer = new System.Windows.Forms.Timer { Interval = _settings.UpdateIntervalSeconds * 1000 };
            _timer.Tick += (_, _) => UpdateTrayIcon();
            _timer.Start();
        }

        private static void ApplyInitialSettings()
        {
            var currentPack = IconPackManager.LoadCurrentPack();
            IconPackManager.ApplyIconPack(currentPack, _trayIcon!);

            if (!_settings.ShowRecycleBinOnDesktop)
                RecycleBinVisibilityManager.HideRecycleBin();
        }

        private static IntPtr LoadTrayIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "BinBuddy", "app.ico");
            if (File.Exists(iconPath))
            {
                using var icon = new Icon(iconPath);
                return icon.Handle;
            }

            var defaultIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            return defaultIcon.Handle;
        }

        private static TrayMenu CreateContextMenu()
        {
            return new TrayMenu
            {
                CreateVersionMenuItem(),
                new TraySeparator(),
                CreateMenuItem("Открыть корзину", _ => OpenRecycleBin()),
                CreateMenuItem("Очистить корзину", _ =>
                {
                    EmptyRecycleBin();
                    UpdateTrayIcon();
                }),
                new TraySeparator(),
                CreateMenuItem("Настройки", null, CreateSettingsSubmenu()),
                new TraySeparator(),
                CreateMenuItem("Выход", _ => ExitApplication(), icon: CreateExitIconFromBase64())
            };
        }

        private static TrayMenuItem CreateMenuItem(string header, Action<object?>? command = null, TrayMenu? subMenu = null, Win32Image? icon = null)
        {
            return new TrayMenuItem
            {
                Header = header,
                Command = command != null ? new TrayCommand(command) : null,
                Menu = subMenu,
                Icon = icon,
                IsEnabled = command != null || subMenu != null
            };
        }

        private static TrayMenuItem CreateVersionMenuItem()
        {
            var item = new TrayMenuItem
            {
                Header = $"BinBuddy {GetVersion()}",
                IsEnabled = false
            };
            return item;
        }

        private static Win32Image? CreateExitIconFromBase64()
        {
            try
            {
                string base64Png = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAk0lEQVR4nO3STQ4BQRCG4SfBMRB3sHKQyXAG5xJu4RRY+LsFVmSSEjLpaWNn4d1W+q2u+opfY4Jupj5GLyfYYYVOojbFLSSNjHDGsiYpcQ3JR4Y4YhGSIh5XktYMcMAal5B8zRz3kKR2kqWIzpVk/zZOK8qY+fntfoxTX2ySWURVJnZyiohzd2ITkqZ0tnFsf7x4AMKtG5ek/9cNAAAAAElFTkSuQmCC";
                byte[] imageBytes = Convert.FromBase64String(base64Png);
                using var ms = new MemoryStream(imageBytes);
                return new Win32Image(ms) { ShowAsMonochrome = true };
            }
            catch
            {
                return null;
            }
        }

        private static TrayMenu CreateSettingsSubmenu()
            {
                return new TrayMenu
        {
            CreateCheckedMenuItem("Показывать уведомления", _settings.ShowNotifications, ToggleNotifications),
            CreateCheckedMenuItem("Автозапуск", AutoStartManager.IsEnabled(), ToggleAutoStart),
            CreateCheckedMenuItem("Отображать 🗑️ на рабочем столе", _settings.ShowRecycleBinOnDesktop, ToggleRecycleBinVisibility),
            new TraySeparator(),
            CreateMenuItem("Таймер обновления", null, CreateUpdateIntervalSubmenu()),
            CreateMenuItem("Выбрать иконку", null, CreateIconPackSubmenu())
        };
            }

        private static TrayMenuItem CreateCheckedMenuItem(string header, bool isChecked, Action<object?> command)
        {
            return new TrayMenuItem
            {
                Header = header,
                IsChecked = isChecked,
                Command = new TrayCommand(command)
            };
        }

        private static TrayMenu CreateUpdateIntervalSubmenu()
        {
            var subMenu = new TrayMenu();
            int[] intervals = [1, 3, 5];

            foreach (var interval in intervals)
            {
                var item = new TrayMenuItem
                {
                    Header = $"{interval} секунд",
                    IsChecked = _settings.UpdateIntervalSeconds == interval,
                    Command = new TrayCommand(_ => SetUpdateInterval(interval, subMenu))
                };
                subMenu.Add(item);
            }

            return subMenu;
        }

        private static void SetUpdateInterval(int interval, TrayMenu subMenu)
        {
            _settings.UpdateIntervalSeconds = interval;
            SettingsManager.SaveSettings(_settings);

            foreach (var item in subMenu.OfType<TrayMenuItem>())
                item.IsChecked = item.Header == $"{interval} секунд";

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Interval = interval * 1000;
                _timer.Start();
            }

            ShowNotification("Обновление корзины", $"Интервал обновления установлен на {interval} секунд.");
        }

        private static TrayMenu CreateIconPackSubmenu()
        {
            var subMenu = new TrayMenu();
            string iconDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");

            Directory.CreateDirectory(iconDirectory);

            var iconPacks = Directory.GetDirectories(iconDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            string currentPack = IconPackManager.LoadCurrentPack();

            if (iconPacks.Length == 0)
            {
                subMenu.Add(new TrayMenuItem { Header = "Нет наборов иконок", IsEnabled = false });
                return subMenu;
            }

            foreach (var packName in iconPacks)
            {
                var item = new TrayMenuItem
                {
                    Header = packName!,
                    IsChecked = currentPack == packName,
                    Command = new TrayCommand(_ => ApplyIconPack(packName!, subMenu)),
                    Icon = LoadPackPreviewIcon(packName!)
                };
                subMenu.Add(item);
            }

            return subMenu;
        }

        private static void ApplyIconPack(string packName, TrayMenu subMenu)
        {
            if (_trayIcon == null) return;

            IconPackManager.ApplyIconPack(packName, _trayIcon);
            bool isRecycleBinEmpty = IsRecycleBinEmpty();
            IconPackManager.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);

            foreach (var item in subMenu.OfType<TrayMenuItem>())
                item.IsChecked = item.Header == packName;

            ShowNotification("Иконки", $"Набор иконок '{packName}' применен.");
        }

        private static Win32Image? LoadPackPreviewIcon(string packName)
        {
            try
            {
                string fullIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName, "recycle-full.ico");
                if (!File.Exists(fullIconPath)) return null;

                using var icon = new Icon(fullIconPath);
                using var originalBitmap = icon.ToBitmap();

                var resizedBitmap = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(resizedBitmap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalBitmap, 0, 0, 16, 16);
                }

                using var ms = new MemoryStream();
                resizedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                return new Win32Image(ms) { ShowAsMonochrome = false };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки иконки для {packName}: {ex.Message}");
                return null;
            }
        }

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                bool hasUpdate = await UpdateChecker.IsUpdateAvailableAsync();
                string currentVersion = GetVersion();
                string? latestVersion = UpdateChecker.GetLatestVersion();

                if (_trayIcon?.Menu is TrayMenu menu && menu[0] is TrayMenuItem versionItem)
                {
                    if (hasUpdate && !string.IsNullOrEmpty(latestVersion))
                    {
                        versionItem.Header = $"Версия {latestVersion} доступна! (нажмите для загрузки)";
                        versionItem.IsEnabled = true;
                        versionItem.Command = new TrayCommand(_ => UpdateChecker.OpenReleasesPage());

                        ShowNotification("Доступно обновление!", $"Версия {latestVersion} уже доступна для скачивания.", 5000);
                    }
                    else
                    {
                        versionItem.Header = $"BinBuddy {currentVersion}";
                        versionItem.IsEnabled = false;
                        versionItem.Command = null;
                    }
                }
            }
            catch
            {
                if (_trayIcon?.Menu is TrayMenu menu && menu[0] is TrayMenuItem versionItem)
                {
                    versionItem.Header = $"BinBuddy {GetVersion()}";
                    versionItem.IsEnabled = false;
                }
            }
        }

        private static void ToggleNotifications(object? _)
        {
            _settings.ShowNotifications = !_settings.ShowNotifications;
            SettingsManager.SaveSettings(_settings);

            UpdateMenuItemCheckState("Показывать уведомления", _settings.ShowNotifications);
            ShowNotification("Уведомления", _settings.ShowNotifications ? "Уведомления включены." : "Уведомления отключены.");
        }

        private static void ToggleAutoStart(object? _)
        {
            bool enabled = !AutoStartManager.IsEnabled(); 

            if (enabled)
                AutoStartManager.Enable(); 
            else
                AutoStartManager.Disable(); 

            _settings.AutoStartEnabled = enabled;
            SettingsManager.SaveSettings(_settings);

            UpdateMenuItemCheckState("Автозапуск", enabled);
            ShowNotification("Автозапуск", enabled ? "Автозапуск включен." : "Автозапуск отключен.");
        }

        private static void ToggleRecycleBinVisibility(object? _)
        {
            _settings.ShowRecycleBinOnDesktop = !_settings.ShowRecycleBinOnDesktop;
            SettingsManager.SaveSettings(_settings);

            RecycleBinVisibilityManager.SetRecycleBinVisibility(_settings.ShowRecycleBinOnDesktop);
            UpdateMenuItemCheckState("Отображать 🗑️ на рабочем столе", _settings.ShowRecycleBinOnDesktop);
        }

        private static void UpdateMenuItemCheckState(string header, bool isChecked)
        {
            if (_trayIcon?.Menu == null) return;

            foreach (var item in GetAllMenuItems(_trayIcon.Menu))
            {
                if (item.Header == header)
                {
                    item.IsChecked = isChecked;
                    return;
                }
            }
        }

        private static IEnumerable<TrayMenuItem> GetAllMenuItems(TrayMenu menu)
        {
            foreach (var item in menu)
            {
                if (item is TrayMenuItem menuItem)
                {
                    yield return menuItem;
                    if (menuItem.Menu != null)
                    {
                        foreach (var subItem in GetAllMenuItems(menuItem.Menu))
                            yield return subItem;
                    }
                }
            }
        }

        private static void UpdateTrayIcon()
        {
            if (_trayIcon == null) return;

            bool isRecycleBinEmpty = IsRecycleBinEmpty();

            if (isRecycleBinEmpty != _previousRecycleBinState)
            {
                _previousRecycleBinState = isRecycleBinEmpty;
                IconPackManager.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);
            }

            UpdateTrayText();
        }

        private static void UpdateTrayText()
        {
            var rbInfo = GetRecycleBinInfo();
            _trayIcon!.ToolTipText = $"Менеджер Корзины\nЭлементов: {rbInfo.i64NumItems}\nЗанято: {FormatFileSize(rbInfo.i64Size)}";
        }

        private static SHQUERYRBINFO GetRecycleBinInfo()
        {
            var rbInfo = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
            SHQueryRecycleBin(null, ref rbInfo);
            return rbInfo;
        }

        private static bool IsRecycleBinEmpty() => GetRecycleBinInfo().i64NumItems == 0;

        private static string FormatFileSize(long sizeInBytes)
        {
            string[] sizes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
            double len = sizeInBytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private static void OpenRecycleBin()
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true });
        }

        private static void EmptyRecycleBin()
        {
            const uint flags = 0x00000001 | 0x00000002 | 0x00000004; // NOCONFIRMATION | NOPROGRESSUI | NOSOUND

            if (SHEmptyRecycleBin(IntPtr.Zero, null, flags) == 0)
            {
                ShowNotification("Корзина", "Корзина успешно очищена.");
                UpdateTrayIcon();
            }
            else
            {
                ShowNotification("Ошибка", "Не удалось очистить корзину.", 3000, TrayToolTipIcon.Error);
            }
        }

        private static void ShowNotification(string title, string message, int timeout = 3000, TrayToolTipIcon iconType = TrayToolTipIcon.Info)
        {
            if (_settings.ShowNotifications && _trayIcon != null)
                _trayIcon.ShowBalloonTip(timeout, title, message, iconType);
        }

        private static void ExitApplication()
        {
            IconPackManager.DisposeIcons();
            _timer?.Stop();
            _timer?.Dispose();
            _trayIcon?.Dispose();
            Application.Exit();
        }

        private static string GetVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0";
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }
    }
}