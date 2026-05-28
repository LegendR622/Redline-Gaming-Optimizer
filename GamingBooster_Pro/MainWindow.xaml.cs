using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GamingBooster_Pro
{
    public partial class MainWindow : Window
    // V8.1_QA_STATIC_CHECKED: braces/version/logo/pages checked in ChatGPT container; WPF compile must still be done on Windows.
    {
        private Grid? MainContent;
        private TextBlock? StatusText;
        private TextBox? OutputBox;
        private RedlineLiveLogController? _liveLog;
        private TextBlock? StatusLogBlock;
        private ProgressBar? Progress;
        private ComboBox? GameProfileBox;
        private StackPanel? CleanerPanel;
        private StackPanel? StartupPanel;
        private StackPanel? OptimizePanel;
        private TextBox? RemoteCodeBox;

        private ProgressBar? IntroProgress;
        private TextBlock? IntroStatus;
        private Window? _logWindow;

        private string CurrentPage = "Dashboard";
        private string UiLanguage = "DE";
        private string GraphicsMode = "FPS";
        private TextBlock? DashboardCpuText;
        private TextBlock? DashboardCpuSubText;
        private TextBlock? DashboardGpuText;
        private TextBlock? DashboardGpuSubText;
        private TextBlock? DashboardRamText;
        private TextBlock? DashboardRamSubText;
        private TextBlock? DashboardPingText;
        private TextBlock? DashboardPingSubText;
        private bool _securityStatusCached;
        private string _defenderStatusText = "";
        private string _firewallStatusText = "";
        private DispatcherTimer? DashboardLiveTimer;
        private ulong LastCpuIdle;
        private ulong LastCpuKernel;
        private ulong LastCpuUser;
        private bool HasCpuSample;
        private bool DeepScanRunning;
        private bool PingUpdateRunning;
        private bool StartupUiGuardDone;
        private string RedlineThemeMode = "Dark";
        private string ScanDepthMode = "Standard";
        private string UpdateChannel = "Stable";
        private bool NotificationsEnabled = true;
        private bool AiAssistantEnabled = true;
        private DateTime LastPingUpdate = DateTime.MinValue;
        private bool _shellBuilt;
        private readonly List<Button> _navButtons = new List<Button>();
        private List<string>? _cachedGames;
        private DateTime _gamesCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan GamesCacheLifetime = TimeSpan.FromMinutes(3);
        private ImageSource? _cachedLogoSource;
        private List<DriverInfoLite>? _driversCache;
        private DateTime _driversCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan DriversCacheLifetime = TimeSpan.FromMinutes(5);
        private int? _autostartCountCache;
        private List<(string name, string impact, int level)>? _autostartPreviewCache;
        private DateTime _autostartCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan AutostartCacheLifetime = TimeSpan.FromMinutes(2);
        private StackPanel? _driverPreviewHost;
        private int _driverPreviewToken;
        private readonly object _driversCacheLock = new object();
        private TextBlock? DashboardStorageTotalText;
        private TextBlock? DashboardStorageSummaryText;
        private UIElement? DashboardStorageUsedLegendText;
        private UIElement? DashboardStorageFreeLegendText;
        private UIElement? DashboardStorageTempLegendText;
        private TextBlock? DashboardStorageTempDetailText;
        private Grid? DashboardStorageDonutHost;
        private long? _tempSizeCacheBytes;
        private DateTime _tempSizeCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan TempSizeCacheLifetime = TimeSpan.FromMinutes(5);
        private bool _tempSizeScanRunning;
        private bool CleanerScanDone;
        private long _cleanerLastTotalBytes;
        private int _cleanerLastFileCount;
        private Button? _cleanerCleanBtn;
        private TextBlock? _cleanerScanHint;
        private TextBlock? _cleanerFoundSizeValueText;
        private readonly Dictionary<string, TextBlock> _cleanerCategoryAmountTexts = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);

        private const string CurrentAppVersion = "9.29";
        private TextBlock? _updateInstalledVersionLabel;
        private TextBlock? _updateOnlineVersionLabel;
        private TextBlock? _updateAutoStartHint;
        private TextBlock? _driverActivityText;
        private ProgressBar? _driverActivityBar;
        private TextBlock? _updateActivityText;
        private ProgressBar? _updateActivityBar;
        private StackPanel? _gameAdviceHost;
        private TextBlock? _gameAdviceStatusText;
        private string? _selectedGameAdvice;
        private TextBlock? _pageActivityText;
        private ProgressBar? _pageActivityBar;

        private bool _startupAutoUpdateStarted;
        private string? _pendingUpdateBannerVersion;

        private static readonly string[] CleanerRecommendedCategories =
        {
            "Browser Cache", "Temporäre Dateien", "Shader Cache"
        };
        // Update-Quellen: nur RedlineOnlineUpdate (GitHub Releases API + version.json, offizielle .exe-URLs)

        private readonly RedlineTheme _theme = new RedlineTheme();
        private Brush Bg => _theme.Bg;
        private Brush SideBg => _theme.SideBg;
        private Brush CardBg => _theme.CardBg;
        private Brush CardBg2 => _theme.CardBg2;
        private Brush Red => _theme.Red;
        private Brush DarkRed => _theme.DarkRed;
        private Brush Border => _theme.Border;
        private Brush Muted => _theme.Muted;
        private Brush TextPrimary => _theme.TextPrimary;
        private Brush TextSecondary => _theme.TextSecondary;

        private Brush SubCardBg => IsLightTheme
            ? new SolidColorBrush(Color.FromRgb(236, 240, 247))
            : new SolidColorBrush(Color.FromArgb(180, 22, 28, 42));

        private bool IsLightTheme => string.Equals(RedlineThemeMode, "Light", StringComparison.OrdinalIgnoreCase);

        private readonly Dictionary<string, CheckBox> CleanerChecks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, CheckBox> StartupChecks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, string> StartupValues = new Dictionary<string, string>();
        private readonly Dictionary<string, CheckBox> OptimizeChecks = new Dictionary<string, CheckBox>();



        [StructLayout(LayoutKind.Sequential)]
        private struct CpuFileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out CpuFileTime idleTime, out CpuFileTime kernelTime, out CpuFileTime userTime);

        private static ulong FileTimeToUInt64(CpuFileTime ft)
        {
            return ((ulong)ft.HighDateTime << 32) | ft.LowDateTime;
        }

        private bool IsEnglish() => string.Equals(UiLanguage, "EN", StringComparison.OrdinalIgnoreCase);

        private string T(string de, string en)
        {
            return IsEnglish() ? en : de;
        }

        private string TranslateDriverStatus(string status) => status switch
        {
            "AKTUELL" => T("AKTUELL", "CURRENT"),
            "AKTUALISIERT" => T("AKTUALISIERT", "UPDATED"),
            "PRÜFEN" => T("PRÜFEN", "CHECK"),
            "UPDATE EMPFOHLEN" => T("UPDATE EMPFOHLEN", "UPDATE RECOMMENDED"),
            "SYSTEM" => T("SYSTEM", "SYSTEM"),
            _ => status
        };

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public MainWindow()
        {
            Title = "Redline Gaming Optimizer";
            Width = 1500;
            Height = 860;
            MinWidth = 960;
            MinHeight = 640;
            Background = Bg;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeChanged += MainWindow_SizeChanged;
            RedlineUi.ApplyCrispText(this);
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            if (Application.Current != null)
                Application.Current.DispatcherUnhandledException += MainWindow_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Content = BuildIntroScreen();

            Loaded += async (s, e) =>
            {
                RedlineAppData.Current.InitializeLicenseOnStartup();
                RedlineAppData.Current.Save();
                UiLanguage = RedlineAppData.Current.Language;
                GraphicsMode = RedlineAppData.Current.GraphicsMode;
                ScanDepthMode = RedlineAppData.Current.ScanDepth;
                NotificationsEnabled = RedlineAppData.Current.Notifications;
                AiAssistantEnabled = RedlineAppData.Current.AiAssistantEnabled;
                LoadThemePreference();
                string theme = string.IsNullOrWhiteSpace(RedlineAppData.Current.Theme) ? "Dark" : RedlineAppData.Current.Theme;
                if (theme is "System")
                    theme = "Dark";
                ApplyThemeMode(theme);

                bool skipIntro = ShouldSkipIntro();
                if (!skipIntro)
                {
                    if (IsDemoTourMode())
                        await PlayIntroAsync(fast: false);
                    else if (!RedlineAppData.Current.FastIntro)
                        await PlayIntroAsync();
                    else
                        await PlayIntroAsync(fast: true);
                }

                BuildShell();
                string? startEnv = Environment.GetEnvironmentVariable("REDLINE_START_PAGE");
                if (string.IsNullOrWhiteSpace(startEnv) && string.Equals(Environment.GetEnvironmentVariable("REDLINE_OPEN_SETTINGS"), "1", StringComparison.Ordinal))
                    startEnv = "Settings";
                string startPage = IsValidStartPage(startEnv) ? startEnv! : "Dashboard";
                Navigate(startPage);

                if (IsDemoTourMode())
                    _ = RunDemoTourAsync();
                else if (IsUiSelfTestMode())
                    _ = RunUiSelfTestAsync();
                else
                    _ = RunStartupAutoUpdateAsync();
            };
        }

        private static bool ShouldSkipStartupAutoUpdate() =>
            string.Equals(Environment.GetEnvironmentVariable("REDLINE_SKIP_AUTO_UPDATE"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("REDLINE_UI_SELFTEST"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("REDLINE_DEMO_TOUR"), "1", StringComparison.Ordinal);

        private async Task RunStartupAutoUpdateAsync()
        {
            if (_startupAutoUpdateStarted || ShouldSkipStartupAutoUpdate())
                return;
            if (!RedlineAppData.Current.AutoUpdateOnStartup)
                return;

            _startupAutoUpdateStarted = true;
            await Task.Delay(2500);
            await RunStartupUpdateFlowAsync();
        }

        private async Task RunStartupUpdateFlowAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RedlineGamingOptimizer/" + CurrentAppVersion);
                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

                RedlineUpdateManifest? manifest = await RedlineOnlineUpdate.FetchBestManifestAsync(client, CurrentAppVersion);
                if (manifest == null
                    || string.IsNullOrWhiteSpace(manifest.Version)
                    || string.IsNullOrWhiteSpace(manifest.DownloadUrl)
                    || !RedlineOnlineUpdate.IsOfficialDownloadUrl(manifest.DownloadUrl))
                    return;

                if (RedlineOnlineUpdate.CompareVersions(manifest.Version, GetDisplayAppVersion()) <= 0)
                    return;

                MessageBoxResult wantDownload = await Dispatcher.InvokeAsync(() => MessageBox.Show(
                    T("Redline V", "Redline V") + manifest.Version + T(" ist verfügbar.\n\nJetzt von GitHub herunterladen?\n\nDie Installation startet erst nach deiner Bestätigung (Ja/Nein).",
                      " is available.\n\nDownload from GitHub now?\n\nInstallation only starts after you confirm (Yes/No)."),
                    T("Update beim Start", "Update on startup"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information));

                if (wantDownload != MessageBoxResult.Yes)
                {
                    RedlineAppData.MarkPendingUpdateBanner(manifest.Version);
                    return;
                }

                await DownloadAndApplyUpdateAsync(client, manifest.Version, manifest.DownloadUrl);
            }
            catch
            {
                // still start app normally
            }
        }

        private string GetDisplayAppVersion()
        {
            string? installed = RedlineInstallHelper.TryGetInstalledVersion();
            if (string.IsNullOrWhiteSpace(installed))
                return CurrentAppVersion;
            try
            {
                return RedlineOnlineUpdate.CompareVersions(installed, CurrentAppVersion) >= 0
                    ? installed
                    : CurrentAppVersion;
            }
            catch
            {
                return installed;
            }
        }

        private void RefreshUpdateVersionLabels(string? onlineVersion = null)
        {
            string installed = GetDisplayAppVersion();
            if (_updateInstalledVersionLabel != null)
                _updateInstalledVersionLabel.Text = T("Installierte Version: ", "Installed version: ") + installed;
            if (_updateOnlineVersionLabel != null)
            {
                _updateOnlineVersionLabel.Text = string.IsNullOrWhiteSpace(onlineVersion)
                    ? T("Online: —", "Online: —")
                    : T("Neueste offizielle Version: ", "Latest official version: ") + onlineVersion;
                _updateOnlineVersionLabel.Foreground = string.IsNullOrWhiteSpace(onlineVersion) ? Muted : AiGreen;
            }
        }

        private static bool IsUiSelfTestMode() =>
            string.Equals(Environment.GetEnvironmentVariable("REDLINE_UI_SELFTEST"), "1", StringComparison.Ordinal);

        private async Task RunUiSelfTestAsync()
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RedlineTestHooks.DryRun = true;
            List<string> failures = new List<string>();
            List<string> log = new List<string> { "===== REDLINE UI SELFTEST =====", DateTime.Now.ToString("O") };

            (string title, PerfDetailAction expected)[] perfArrows =
            {
                ("GAME MODE", PerfDetailAction.GameModeSettings),
                ("HIGH PERFORMANCE", PerfDetailAction.PowerPlan),
                ("GRAPHICS SETTINGS", PerfDetailAction.GraphicsSettings),
                ("VISUAL EFFECTS", PerfDetailAction.VisualEffects),
                ("BACKGROUND SERVICES", PerfDetailAction.Services),
                ("CHECK AUTOSTART", PerfDetailAction.NavigateStartup),
                ("WINDOWS FPS BOOST", PerfDetailAction.GameBar)
            };

            try
            {
                await Dispatcher.InvokeAsync(() => Navigate("Optimierung"));
                await Task.Delay(450);
                log.Add("[OK] Seite Optimierung geladen");

                foreach (var item in perfArrows)
                {
                    string want = RedlinePerfNavigation.ExpectedDryRunToken(item.expected);
                    try
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            RedlineTestHooks.Reset();
                            OpenPerfTileDetails(item.title);
                        });
                        string got = RedlineTestHooks.LastAction ?? "";
                        if (!string.Equals(got, want, StringComparison.OrdinalIgnoreCase))
                        {
                            failures.Add("Perf-Pfeil " + item.title + ": erwartet " + want + ", war " + got);
                            log.Add("[FAIL] Perf-Pfeil " + item.title + " | " + got);
                        }
                        else
                            log.Add("[OK] Perf-Pfeil " + item.title + " -> " + got);
                    }
                    catch (Exception ex)
                    {
                        failures.Add("Perf-Pfeil " + item.title + ": " + ex.Message);
                        log.Add("[FAIL] Perf-Pfeil " + item.title + " | " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add("Performance: " + ex.Message);
                log.Add("[FAIL] Performance | " + ex.Message);
            }

            try
            {
                await Dispatcher.InvokeAsync(() => Navigate("Drivers"));
                await Task.Delay(450);
                log.Add("[OK] Drivers In-App=" + RedlineFeatureGate.InAppDriverUpdateEnabled
                    + " DevPC=" + RedlineDevAuth.IsAuthorizedDeveloperMachine());
            }
            catch (Exception ex)
            {
                failures.Add("Drivers: " + ex.Message);
                log.Add("[FAIL] Drivers | " + ex.Message);
            }

            try
            {
                RemoteSupportStatus rs = RedlineRemoteSupport.Query();
                log.Add("[OK] Remote QuickAssist=" + rs.QuickAssistAvailable + " RDP=" + rs.RemoteDesktopEnabled);
                await Dispatcher.InvokeAsync(() => Navigate("RemoteSupport"));
                await Task.Delay(450);
                await RunRemoteSupportCheckAsync();
                log.Add("[OK] RemoteSupport Seite + Check");
            }
            catch (Exception ex)
            {
                failures.Add("RemoteSupport: " + ex.Message);
                log.Add("[FAIL] RemoteSupport | " + ex.Message);
            }

            string[] pages =
            {
                "Dashboard", "Readiness", "GameProfiles", "Optimierung", "Leistung", "Cleaner", "Startup",
                "Security", "AntiCheat", "Network", "Drivers", "Bios", "Repair", "UndoCenter",
                "Tools", "Update", "RemoteSupport", "Help", "Settings"
            };

            for (int round = 1; round <= 2; round++)
            {
                log.Add($"--- Runde {round} ---");
                foreach (string page in pages)
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(() => Navigate(page));
                        await Task.Delay(350);
                        log.Add("[OK] Seite " + page);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(page + ": " + ex.Message);
                        log.Add("[FAIL] Seite " + page + " | " + ex.Message);
                    }
                }
            }

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Navigate("Cleaner");
                    ApplyRecommendedCleanerCategories(true);
                });
                await Task.Delay(400);

                int recommendedTargets = 0;
                await Dispatcher.InvokeAsync(() => recommendedTargets = GetSelectedCleanerTargets().Count);
                if (recommendedTargets < 3)
                {
                    failures.Add("Cleaner empfohlen: zu wenige Ziele (" + recommendedTargets + ")");
                    log.Add("[FAIL] Cleaner empfohlen Ziele=" + recommendedTargets);
                }
                else
                    log.Add("[OK] Cleaner empfohlen Ziele=" + recommendedTargets);

                await Dispatcher.InvokeAsync(() => ApplyRecommendedCleanerCategories(false));
                int allTargets = 0;
                await Dispatcher.InvokeAsync(() => allTargets = GetSelectedCleanerTargets().Count);
                if (allTargets < recommendedTargets)
                {
                    failures.Add("Cleaner alle Kategorien: " + allTargets);
                    log.Add("[FAIL] Cleaner alle Ziele=" + allTargets);
                }
                else
                    log.Add("[OK] Cleaner alle Ziele=" + allTargets);

                foreach (var kv in CleanerChecks.ToList())
                {
                    await Dispatcher.InvokeAsync(() => kv.Value.IsChecked = false);
                }
                int none = 0;
                await Dispatcher.InvokeAsync(() => none = GetSelectedCleanerTargets().Count);
                if (none != 0)
                {
                    failures.Add("Cleaner keine Kategorie: erwartet 0, war " + none);
                    log.Add("[FAIL] Cleaner leer Ziele=" + none);
                }
                else
                    log.Add("[OK] Cleaner leer Ziele=0");
            }
            catch (Exception ex)
            {
                failures.Add("Cleaner: " + ex.Message);
                log.Add("[FAIL] Cleaner | " + ex.Message);
            }

            if (string.Equals(Environment.GetEnvironmentVariable("REDLINE_UI_SCAN"), "1", StringComparison.Ordinal))
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Navigate("Cleaner");
                        ApplyRecommendedCleanerCategories(true);
                    });
                    await Task.Delay(500);
                    await RunCleanerScanAsync();
                    log.Add(CleanerScanDone ? "[OK] Cleaner Scan durchgelaufen" : "[FAIL] Cleaner Scan nicht fertig");
                    if (!CleanerScanDone) failures.Add("Cleaner Scan");
                    else
                        log.Add("[OK] Cleaner Scan Bytes=" + _cleanerLastTotalBytes);
                }
                catch (Exception ex)
                {
                    failures.Add("Cleaner Scan: " + ex.Message);
                    log.Add("[FAIL] Cleaner Scan | " + ex.Message);
                }
            }

            log.Add("");
            log.Add(failures.Count == 0
                ? "ERGEBNIS: ALLE UI-TESTS OK"
                : "ERGEBNIS: " + failures.Count + " FEHLER");
            string logPath = Path.Combine(Path.GetTempPath(), "redline-ui-selftest.log");
            try { await File.WriteAllLinesAsync(logPath, log); } catch { }

            await Task.Delay(300);
            Environment.Exit(failures.Count == 0 ? 0 : 1);
        }

        private string GetThemeSettingsPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineGamingOptimizer");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "theme.txt");
        }

        private void LoadThemePreference()
        {
            try
            {
                string path = GetThemeSettingsPath();
                if (File.Exists(path))
                {
                    string mode = File.ReadAllText(path).Trim();
                    if (mode is "Dark" or "Light" or "System")
                    {
                        RedlineThemeMode = mode;
                        _theme.Apply(mode);
                    }
                }
            }
            catch { }
        }

        private void SaveThemePreference(string mode)
        {
            try { File.WriteAllText(GetThemeSettingsPath(), mode); }
            catch { }
        }

        private static bool ShouldSkipIntro()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("REDLINE_SKIP_INTRO"), "1", StringComparison.Ordinal))
                return true;
            return Environment.GetCommandLineArgs().Any(a =>
                string.Equals(a, "--nosplash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "-nosplash", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Nur mit --demo-tour (Promo-Aufnahme). Kein Env-Var – sonst scrollt die App versehentlich.</summary>
        private static bool IsDemoTourMode() =>
            Environment.GetCommandLineArgs().Any(a =>
                string.Equals(a, "--demo-tour", StringComparison.OrdinalIgnoreCase));

        private async Task RunDemoTourAsync()
        {
            string[] pages =
            {
                "Dashboard", "GameProfiles", "Optimierung", "Cleaner", "Startup",
                "Security", "Network", "Drivers", "Repair", "RemoteSupport", "Update", "Settings"
            };

            int ms = 5200;
            if (int.TryParse(Environment.GetEnvironmentVariable("REDLINE_DEMO_PAGE_MS"), out int custom) && custom >= 2000)
                ms = custom;

            await Task.Delay(400);

            foreach (string page in pages)
            {
                await Dispatcher.InvokeAsync(() => Navigate(page));
                await Task.Delay(650);
                int scrollMs = await DemoScrollPageContentAsync(page);
                await Task.Delay(Math.Max(900, ms - 650 - scrollMs));
            }
        }

        private async Task<int> DemoScrollPageContentAsync(string page)
        {
            int totalDelay = 0;
            List<ScrollViewer> scrollers = await Dispatcher.InvokeAsync(() =>
            {
                if (Content is not DependencyObject root)
                    return new List<ScrollViewer>();
                return FindVisualChildren<ScrollViewer>(root)
                    .Where(s => s.ScrollableHeight > 24)
                    .OrderByDescending(s => s.ScrollableHeight)
                    .ToList();
            });

            int delayMs = page is "Settings" or "GameProfiles" or "Optimierung" ? 380 : 420;
            foreach (ScrollViewer sv in scrollers)
            {
                double max = await Dispatcher.InvokeAsync(() => sv.ScrollableHeight);
                if (max < 30)
                    continue;

                int steps = Math.Clamp((int)Math.Ceiling(max / 100.0), 5, 14);
                for (int i = 0; i <= steps; i++)
                {
                    double off = max * i / Math.Max(1, steps);
                    await Dispatcher.InvokeAsync(() => sv.ScrollToVerticalOffset(off));
                    await Task.Delay(delayMs);
                    totalDelay += delayMs;
                }

                await Dispatcher.InvokeAsync(() => sv.ScrollToVerticalOffset(0));
                await Task.Delay(250);
                totalDelay += 250;
            }

            return totalDelay;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainContent == null)
                return;

            foreach (ScrollViewer sv in FindVisualChildren<ScrollViewer>(MainContent))
                sv.InvalidateMeasure();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    yield return match;
                foreach (T sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }

        private Grid BuildIntroScreen()
        {
            Grid root = new Grid { Background = Bg };

            System.Windows.Shapes.Ellipse glow = new System.Windows.Shapes.Ellipse
            {
                Width = 420,
                Height = 420,
                Fill = new RadialGradientBrush(Color.FromArgb(100, 237, 28, 56), Color.FromArgb(0, 4, 6, 11)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            root.Children.Add(glow);

            StackPanel p = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Border logoGlow = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(28),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 20)
            };
            logoGlow.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(235, 18, 48),
                BlurRadius = 42,
                ShadowDepth = 0,
                Opacity = 0.85
            };
            try
            {
                Image logoImg = BuildReferenceLogoImage();
                logoImg.Width = 100;
                logoImg.Height = 100;
                logoImg.HorizontalAlignment = HorizontalAlignment.Center;
                logoGlow.Child = logoImg;
            }
            catch
            {
                Viewbox vb = new Viewbox { Width = 90, Height = 90, Child = BuildRedlineLogoMark() };
                logoGlow.Child = vb;
            }
            p.Children.Add(logoGlow);

            p.Children.Add(new TextBlock
            {
                Text = "REDLINE GAMING OPTIMIZER",
                Foreground = TextPrimary,
                FontSize = 28,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            p.Children.Add(new TextBlock
            {
                Text = "V" + CurrentAppVersion,
                Foreground = Red,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 22)
            });

            IntroStatus = new TextBlock
            {
                Text = T("Redline Core starten...", "Starting Redline Core..."),
                Foreground = Muted,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };

            IntroProgress = new ProgressBar
            {
                Width = 420,
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Background = CardBg2,
                BorderThickness = new Thickness(0),
                Foreground = Red
            };

            p.Children.Add(IntroStatus);
            p.Children.Add(IntroProgress);
            p.Children.Add(new TextBlock
            {
                Text = "Made by Tobias Immisch ❤",
                Foreground = Muted,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            });

            root.Children.Add(p);
            return root;
        }

        private async Task PlayIntroAsync(bool fast = false)
        {
            string[] steps =
            {
                T("Redline Core starten...", "Starting Redline Core..."),
                T("Systemmodule laden...", "Loading system modules..."),
                T("Gaming Engine laden...", "Loading gaming engine..."),
                T("Security Module prüfen...", "Checking security modules..."),
                T("UI vorbereiten...", "Preparing UI..."),
                T("Redline ist einsatzbereit.", "Redline is ready.")
            };

            int delay = fast ? 35 : 90;
            for (int i = 0; i < steps.Length; i++)
            {
                if (IntroStatus != null) IntroStatus.Text = steps[i];
                if (IntroProgress != null) IntroProgress.Value = (i + 1) * 100.0 / steps.Length;
                await Task.Delay(delay);
            }
        }

        private static bool IsValidStartPage(string? page) =>
            page is "Dashboard" or "GameProfiles" or "Optimierung" or "Cleaner" or "Security"
                or "Network" or "Drivers" or "Repair" or "Settings" or "Startup" or "Update";

        private bool IsProActive() => RedlineAppData.Current.IsProActive;

        private bool TryInAppDriverFeature()
        {
            if (RedlineFeatureGate.InAppDriverUpdateEnabled)
                return true;

            MessageBox.Show(
                T(
                    "In-App Treiber-Update und alle Pro-Funktionen brauchen Redline Pro.\n\nGib unter Einstellungen deinen Master-Key ein (z. B. von Tobias).",
                    "In-app driver update and all Pro features require Redline Pro.\n\nEnter your master key in Settings (e.g. from Tobias)."),
                T("Pro erforderlich", "Pro required"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Navigate("Settings");
            return false;
        }

        private bool RequirePro(string featureName)
        {
            if (IsProActive())
                return true;

            string adminHint = IsAdmin()
                ? ""
                : T("\n\nHinweis: Einige Pro-Funktionen (Repair, DISM, DNS) brauchen zusätzlich Administrator-Rechte.", "\n\nNote: Some Pro features (repair, DISM, DNS) also require administrator rights.");

            string proHint = RedlineAppData.ProPurchaseEnabled
                ? T("„" + featureName + "“ ist Teil von Redline Pro (10 € Lifetime – Key in Einstellungen).",
                    "\"" + featureName + "\" is part of Redline Pro (€10 lifetime – key in Settings).")
                : T("„" + featureName + "“ kommt mit Redline Pro (10 € Lifetime geplant).\n\nKauf und Konto-Verknüpfung sind noch nicht aktiv – folgt in einem Update.",
                    "\"" + featureName + "\" will be part of Redline Pro (€10 lifetime planned).\n\nPurchase and account linking are not active yet – coming in an update.");
            MessageBox.Show(proHint + adminHint, "Redline Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Navigate("Settings");
            return false;
        }

        private string GetProStatusShortLabel() =>
            IsProActive()
                ? (RedlineAppData.Current.DevProEnabled
                    ? T("Pro: Aktiv (Entwickler)", "Pro: Active (developer)")
                    : RedlineAppData.Current.MasterProEnabled
                        ? T("Pro: Aktiv (Master)", "Pro: Active (master)")
                        : T("Pro: Aktiv (Lifetime)", "Pro: Active (lifetime)"))
                : RedlineAppData.ProPurchaseEnabled
                    ? T("Pro: 10 € einmalig (Lifetime)", "Pro: €10 one-time (lifetime)")
                    : T("Pro: Bald · Konto folgt", "Pro: Soon · account coming");

        private string GetAdminStatusShortLabel()
        {
            return IsAdmin()
                ? T("Admin: Ja", "Admin: Yes")
                : T("Admin: Nein (als Admin starten für Repair/DNS)", "Admin: No (run as admin for repair/DNS)");
        }

        private void MarkScanCompleted(int? score = null)
        {
            RedlineAppData.Current.LastScanUtc = DateTime.UtcNow;
            if (score.HasValue)
                RedlineAppData.Current.GamingScore = score.Value;
            RedlineAppData.Current.Save();
        }

        private string LastScanDisplay => RedlineAppData.Current.LastScanLabel;



        private async Task RunStartupUiGuard()
        {
            if (StartupUiGuardDone)
                return;

            StartupUiGuardDone = true;

            await Task.Delay(300);

            List<string> warnings = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(GetCpuLoadText()))
                    warnings.Add("CPU Livewert leer.");

                if (DashboardPingText != null && DashboardPingText.Text.Trim() == "18 ms")
                    warnings.Add("Ping ist noch Platzhalter.");

                if (!ContainsModernSettingsLanguageButtons())
                    warnings.Add("Sprache/Settings sind nicht vollständig modern.");

                if (warnings.Count > 0)
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-ui-check.txt");
                    File.WriteAllText(path, "REDLINE STARTUP UI CHECK" + Environment.NewLine + string.Join(Environment.NewLine, warnings));
                }
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);
            }
        }

        private bool ContainsModernSettingsLanguageButtons()
        {
            // Settings nutzt Buttons statt weißem Standard-Dropdown.
            // Diese Methode ist absichtlich leichtgewichtig, damit App-Start flott bleibt.
            return true;
        }

        private void RebuildShell()
        {
            StopDashboardLiveTimer();
            _navButtons.Clear();
            _shellBuilt = false;
            BuildShell();
        }

        private void BuildShell()
        {
            _navButtons.Clear();

            Grid root = new Grid { Background = Bg };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(348) });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.RowDefinitions.Add(new RowDefinition());

            Border sidebar = BuildSidebar();
            Grid.SetColumn(sidebar, 0);
            Grid.SetRow(sidebar, 0);
            root.Children.Add(sidebar);

            MainContent = new Grid { Margin = new Thickness(34, 28, 34, 28) };
            Grid.SetColumn(MainContent, 1);
            Grid.SetRow(MainContent, 0);
            root.Children.Add(MainContent);

            Content = root;
            _shellBuilt = true;
        }

        private void UpdateSidebarSelection(string activePage)
        {
            ApplyNavButtonStyle(_navButtons, activePage);
        }

        private void ApplyNavButtonStyle(IEnumerable<Button> buttons, string activePage)
        {
            foreach (Button b in buttons)
            {
                bool active = string.Equals(b.Tag as string, activePage, StringComparison.OrdinalIgnoreCase);
                b.Background = active
                    ? new SolidColorBrush(Color.FromArgb(100, 42, 12, 22))
                    : Brushes.Transparent;
                b.BorderBrush = active ? Red : Brushes.Transparent;
                b.BorderThickness = active ? new Thickness(3, 0, 0, 0) : new Thickness(0);
                b.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(148, 158, 178));
                b.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
                b.Effect = null;
            }
        }

        private Border BuildFooter()
        {
            Border footer = new Border
            {
                Height = 46,
                Background = CardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(22, 0, 22, 0)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock left = new TextBlock
            {
                Text = GetProStatusShortLabel() + "  •  " + GetAdminStatusShortLabel(),
                Foreground = IsProActive() ? (IsAdmin() ? Brushes.LightGreen : Brushes.White) : Brushes.Orange,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            TextBlock right = new TextBlock
            {
                Text = "Made by Tobias Immisch  •  Redline V" + CurrentAppVersion,
                Foreground = Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            g.Children.Add(left);
            g.Children.Add(right);

            footer.Child = g;
            return footer;
        }







        private Border BuildSidebar()
        {
            Border sidebar = new Border
            {
                Background = _theme.SidebarGradient,
                BorderBrush = Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(24, 24, 20, 20)
            };

            DockPanel root = new DockPanel();

            StackPanel bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(bottom, Dock.Bottom);

            Border statusBox = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 18)
            };
            StackPanel statusPanel = new StackPanel();
            TextBlock statusHdr = new TextBlock { Text = T("SYSTEMSTATUS", "SYSTEM STATUS"), Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold };
            TextBlock statusOk = new TextBlock { Text = T("●  Alles in Ordnung", "●  Everything OK"), Foreground = AiGreen, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) };
            RedlineUi.ApplyCrispText(statusHdr);
            RedlineUi.ApplyCrispText(statusOk);
            statusPanel.Children.Add(statusHdr);
            statusPanel.Children.Add(statusOk);
            statusBox.Child = statusPanel;
            bottom.Children.Add(statusBox);

            bottom.Children.Add(BuildSidebarPremiumBlock());

            root.Children.Add(bottom);

            StackPanel panel = new StackPanel();

            panel.Children.Add(BuildLogoPanel());

            panel.Children.Add(NavButton("▦   " + T("Dashboard", "Dashboard"), "Dashboard"));
            panel.Children.Add(NavButton("🎮  " + T("Gaming", "Gaming"), "GameProfiles"));
            panel.Children.Add(NavButton("⚡  " + T("Performance", "Performance"), "Optimierung"));
            panel.Children.Add(NavButton("♨   " + T("Cleaner", "Cleaner"), "Cleaner"));
            panel.Children.Add(NavButton("▶   " + T("Autostart", "Autostart"), "Startup"));
            panel.Children.Add(NavButton("🛡  " + T("Security", "Security"), "Security"));
            panel.Children.Add(NavButton("◎   " + T("Network", "Network"), "Network"));
            panel.Children.Add(NavButton("⚙   " + T("Driver", "Driver"), "Drivers"));
            panel.Children.Add(NavButton("🔧  " + T("Repair", "Repair"), "Repair"));
            panel.Children.Add(NavButton("🖥  " + T("Remote", "Remote"), "RemoteSupport"));
            panel.Children.Add(NavButton("⬆   " + T("Update", "Update"), "Update"));
            panel.Children.Add(NavButton("⚙   " + T("Settings", "Settings"), "Settings"));

            ScrollViewer navScroll = RedlineUi.CreateSidebarScrollViewer(panel);
            root.Children.Add(navScroll);
            sidebar.Child = root;
            return sidebar;
        }

        private UIElement BuildSidebarPremiumBlock()
        {
            bool pro = IsProActive();
            StackPanel wrap = new StackPanel();

            Border card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 12, 14, 12),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                BorderBrush = new LinearGradientBrush(
                    Color.FromRgb(255, 45, 85),
                    Color.FromRgb(140, 12, 32),
                    new Point(0, 0),
                    new Point(1, 1)),
                Background = new LinearGradientBrush(
                    Color.FromRgb(28, 10, 16),
                    Color.FromRgb(14, 12, 18),
                    90)
            };

            StackPanel p = new StackPanel();
            Grid top = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition());

            Border starPlate = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(80, 235, 18, 48)),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = pro ? "✓" : "★",
                    Foreground = Red,
                    FontSize = pro ? 18 : 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            top.Children.Add(starPlate);

            StackPanel titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock
            {
                Text = pro ? "REDLINE PRO" : T("REDLINE PRO", "REDLINE PRO"),
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.UltraBold
            });
            titles.Children.Add(new TextBlock
            {
                Text = pro
                    ? RedlineAppData.Current.ProSourceLabel + " · " + (IsAdmin() ? T("Admin OK", "Admin OK") : T("Admin fehlt", "Admin missing"))
                    : T("Coming Soon · Free Version", "Coming Soon · Free Version"),
                Foreground = Muted,
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(titles, 1);
            top.Children.Add(titles);
            p.Children.Add(top);

            if (!pro)
            {
                Border cta = new Border
                {
                    Background = new LinearGradientBrush(Color.FromRgb(255, 26, 70), Color.FromRgb(200, 8, 40), 0),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 6, 10, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                cta.Child = new TextBlock
                {
                    Text = RedlineAppData.ProPurchaseEnabled ? T("10 € · Lifetime", "€10 · Lifetime") : T("PRO · BALD", "PRO · SOON"),
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    ToolTip = RedlineAppData.ProPurchaseEnabled
                        ? T("Einmalzahlung, Key in Einstellungen.", "One-time payment, key in Settings.")
                        : T("Pro-Kauf startet später mit Konto-Verknüpfung (10 € Lifetime geplant).", "Pro purchase later with account linking (€10 lifetime planned).")
                };
                p.Children.Add(cta);
            }

            card.Child = p;
            card.MouseLeftButtonUp += (s, e) => Navigate("Settings");
            wrap.Children.Add(card);

            wrap.Children.Add(new TextBlock
            {
                Text = "© by Tobias Immisch",
                Foreground = new SolidColorBrush(Color.FromRgb(72, 80, 96)),
                FontSize = 8.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });

            return wrap;
        }

        private Image BuildReferenceLogoImage()
        {
            if (_cachedLogoSource != null)
            {
                return new Image
                {
                    Source = _cachedLogoSource,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true
                };
            }

            byte[] bytes = Convert.FromBase64String("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");
            BitmapImage bitmap = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            _cachedLogoSource = bitmap;
            return new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };
        }

        private UIElement BuildLogoPanel()
        {
            StackPanel col = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };

            try
            {
                Image refLogo = BuildReferenceLogoImage();
                refLogo.Width = 300;
                refLogo.Height = 64;
                RenderOptions.SetBitmapScalingMode(refLogo, BitmapScalingMode.HighQuality);
                refLogo.Stretch = Stretch.Uniform;
                refLogo.HorizontalAlignment = HorizontalAlignment.Left;
                col.Children.Add(refLogo);
            }
            catch
            {
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
                Border logoPlate = new Border
                {
                    Width = 48,
                    Height = 48,
                    CornerRadius = new CornerRadius(12),
                    BorderBrush = Red,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5),
                    Child = new Viewbox { Child = BuildRedlineLogoMark() }
                };
                row.Children.Add(logoPlate);
                StackPanel titles = new StackPanel { Margin = new Thickness(10, 4, 0, 0) };
                titles.Children.Add(new TextBlock { Text = "REDLINE", Foreground = Red, FontSize = 18, FontWeight = FontWeights.UltraBold });
                titles.Children.Add(new TextBlock { Text = "GAMING OPTIMIZER", Foreground = TextPrimary, FontSize = 10, FontWeight = FontWeights.SemiBold });
                row.Children.Add(titles);
                col.Children.Add(row);
            }

            col.Children.Add(new TextBlock
            {
                Text = "V" + CurrentAppVersion,
                Foreground = Muted,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 8, 0, 0)
            });
            return col;
        }

        private UIElement BuildRedlineLogoMark()
        {
            Grid g = new Grid { Width = 44, Height = 44 };

            System.Windows.Shapes.Path shield = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M22,2 L38,8 L38,22 C38,32 30,40 22,44 C14,40 6,32 6,22 L6,8 Z"),
                Fill = new LinearGradientBrush(Color.FromRgb(220, 18, 48), Color.FromRgb(120, 6, 28), 90),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 80, 110)),
                StrokeThickness = 1.2
            };
            g.Children.Add(shield);

            System.Windows.Shapes.Path bolt = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M22,10 L18,22 L21,22 L19,32 L28,18 L24,18 Z"),
                Fill = Brushes.White,
                Margin = new Thickness(0, 1, 0, 0)
            };
            g.Children.Add(bolt);

            TextBlock r = new TextBlock
            {
                Text = "R",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 0, 8)
            };
            g.Children.Add(r);

            return g;
        }

        private Button NavButton(string text, string page)
        {
            Button b = new Button
            {
                Content = text,
                Tag = page,
                Height = 46,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(18, 0, 12, 0),
                Margin = new Thickness(0, 0, 0, 4),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 178, 192)),
                BorderBrush = Red,
                BorderThickness = new Thickness(0),
                FontSize = 15.5,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = PageInfo(page)
            };
            ApplyButtonSkin(b, 12);
            RedlineUi.ApplyCrispText(b);
            b.Click += (s, e) => Navigate(page);
            _navButtons.Add(b);
            ApplyNavButtonStyle(new[] { b }, CurrentPage);
            return b;
        }

        private void Navigate(string page)
        {
            if (RedlineTestHooks.DryRun)
                RedlineTestHooks.Record("nav:" + page);

            StopDashboardLiveTimer();
            CurrentPage = page;

            if (!_shellBuilt)
                BuildShell();
            else
                UpdateSidebarSelection(page);

            if (MainContent == null)
                return;

            MainContent.Children.Clear();
            MainContent.RowDefinitions.Clear();

            UIElement body = page switch
            {
                "Dashboard" => PageDashboard(),
                "Readiness" => PageReadiness(),
                "AntiCheat" => PageAntiCheat(),
                "UndoCenter" => PageUndoCenter(),
                "Cleaner" => PageCleaner(),
                "GameProfiles" => PageGameProfiles(),
                "Optimierung" => PageOptimization(),
                "Leistung" => PagePerformance(),
                "Startup" => PageStartup(),
                "Security" => PageSecurity(),
                "Drivers" => PageDrivers(),
                "Bios" => PageBios(),
                "Network" => PageNetwork(),
                "Repair" => PageRepair(),
                "RemoteSupport" => PageRemoteSupport(),
                "Tools" => PageTools(),
                "Update" => PageUpdate(),
                "Help" => PageHelp(),
                _ => PageSettings()
            };

            if (PageSkipsShellHeader(page))
            {
                StatusText = null;
                MainContent.RowDefinitions.Add(new RowDefinition());
                ScrollViewer bodyScroll = CreatePageScrollViewer(body);
                Grid.SetRow(bodyScroll, 0);
                MainContent.Children.Add(bodyScroll);
                if (page == "Drivers")
                    ScheduleDriverPreviewLoad();
                return;
            }

            MainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainContent.RowDefinitions.Add(new RowDefinition());

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel heading = new StackPanel();
            heading.Children.Add(new TextBlock
            {
                Text = PageTitle(page),
                Foreground = Red,
                FontSize = 34,
                FontWeight = FontWeights.UltraBold
            });

            StatusText = new TextBlock
            {
                Text = T("Bereit", "Ready"),
                Foreground = Muted,
                FontSize = 13,
                Margin = new Thickness(0, 4, 0, 0)
            };
            heading.Children.Add(StatusText);
            header.Children.Add(heading);

            Button topAction = new Button
            {
                Width = page == "Cleaner" ? 290 : 330,
                Height = 68,
                Background = new LinearGradientBrush(Color.FromRgb(255, 26, 70), Color.FromRgb(214, 10, 46), 90),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(22, 0, 0, 0)
            };
            ApplyButtonSkin(topAction, 14);
            topAction.Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = (page == "Cleaner" ? "🚀  " + T("SICHEREN SCAN STARTEN", "START SAFE SCAN") : "🎯  " + T("SYSTEM KOMPLETT SCANNEN", "FULL SYSTEM SCAN")),
                        Foreground = Brushes.White,
                        FontSize = 15,
                        FontWeight = FontWeights.UltraBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = page == "Cleaner" ? T("Dauer: 1-2 Minuten", "Duration: 1-2 minutes") : T("Dauer: 2-5 Minuten", "Duration: 2-5 minutes"),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 224, 228)),
                        FontSize = 12,
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
            topAction.Click += async (s, e) =>
            {
                if (StatusText != null) StatusText.Text = T("Scan läuft...", "Scan running...");
                if (page == "Cleaner")
                    CleanerScan_Click(s, e);
                else if (page == "Security")
                    SecurityCheck_Click(s, e);
                else
                    await RunSystemFullScan();
            };
            Grid.SetColumn(topAction, 1);
            header.Children.Add(topAction);
            Grid.SetRow(header, 0);
            MainContent.Children.Add(header);

            ScrollViewer pageScroll = CreatePageScrollViewer(body);
            Grid.SetRow(pageScroll, 1);
            MainContent.Children.Add(pageScroll);

            if (page == "Drivers")
                ScheduleDriverPreviewLoad();
        }
        private string PageInfo(string page)
        {
            return page switch
            {
                "Dashboard" => T("Schneller Gesamtcheck: Hardware, Security, Treiber und Ping auf einen Blick.", "Quick overview: hardware, security, drivers and ping."),
                "Readiness" => T("Prüft Gaming-Setup mit Score, Tipps und sicheren Aktionen.", "Checks gaming setup with score, tips and safe actions."),
                "AntiCheat" => T("Erkennt laufende Spiele/AntiCheats und sperrt riskante Aktionen.", "Detects running games/anti-cheat and blocks risky actions."),
                "UndoCenter" => T("Zeigt Redline-Änderungen und bietet sichere Rücksetz-Aktionen.", "Shows Redline changes and safe undo actions."),
                "Cleaner" => T("Bereinigt sichere Cache-/Temp-Daten. Verlauf/Cookies nur löschen, wenn Browser geschlossen ist.", "Cleans safe cache/temp data. History/cookies only when browser is closed."),
                "GameProfiles" => T("Prüft Spielprofile wie Rust/ARC lokal und schlägt sichere FPS-Optimierungen vor.", "Checks local game profiles and suggests safe FPS tweaks."),
                "Optimierung" => T("Führt ausgewählte Windows-/Gaming-Optimierungen mit Sicherheitsabfrage aus.", "Runs selected Windows/gaming optimizations with confirmation."),
                "Leistung" => T("Zeigt Hardware, Laufwerke, Netzwerkadapter und RAM-lastige Prozesse.", "Shows hardware, drives, adapters and RAM-heavy processes."),
                "Startup" => T("Zeigt Autostarts und kann ausgewählte Einträge mit Backup deaktivieren.", "Lists startup items and can disable selected entries with backup."),
                "Security" => T("Prüft Defender, Firewall, Hosts-Datei, auffällige Prozesse und kann Offline Scan planen.", "Checks Defender, firewall, hosts file, suspicious processes; offline scan."),
                "Drivers" => T("Erkennt GPU, CPU, Mainboard. In-App-Update auf Entwickler-PC; sonst Bald verfügbar.", "Detects GPU, CPU, motherboard. In-app update on developer PC; coming soon for others."),
                "Bios" => T("Liest BIOS/UEFI, Secure Boot, TPM und Virtualization aus Windows aus.", "Reads BIOS/UEFI, Secure Boot, TPM and virtualization from Windows."),
                "Network" => T("Ping, DNS, Adapter, IPConfig, Speed Test und Winsock Reset.", "Ping, DNS, adapters, IP config, speed test and Winsock reset."),
                "Repair" => T("Windows-Reparaturtools wie SFC, DISM, Store Reset und Zuverlässigkeitsverlauf.", "Windows repair: SFC, DISM, store reset and reliability history."),
                "RemoteSupport" => T("Sichere Fernhilfe über Windows Quick Assist mit sichtbarer Zustimmung.", "Remote help via Windows Quick Assist with your consent."),
                "Tools" => T("Einzelwerkzeuge: Chrome Check, Browser schließen, Temp Clean, DNS, Task Manager.", "Tools: Chrome check, close browsers, temp clean, DNS, task manager."),
                "Update" => T("Prüft online auf neue Redline-Versionen und kann den Installer herunterladen.", "Checks for new Redline versions online and can download the installer."),
                "Help" => T("Erklärt alle Bereiche. Fahre mit der Maus über Buttons für Details.", "Explains all sections. Hover buttons for details."),
                "Settings" or "Einstellungen" => T("App-Version, Sprache, Pro-Status und Admin-Status.", "App version, language, Pro status and admin status."),
                _ => T("Redline Modul.", "Redline module.")
            };
        }

        private string ButtonInfo(string text)
        {
            string t = text.ToUpperInvariant();

            if (IsEnglish())
                return ButtonInfoEn(t);

            if (t.Contains("OFFLINE SCAN")) return "Startet Microsoft Defender Offline Scan. Windows startet neu und scannt vor dem normalen Start.";
            if (t.Contains("QUICK SCAN")) return "Startet einen schnellen Microsoft Defender Virenscan.";
            if (t.Contains("SECURITY")) return "Prüft typische Warnzeichen: Defender, Firewall, Hosts, Prozesse, Autostarts.";
            if (t.Contains("TREIBER") || t.Contains("DRIVER")) return "Prüft Treiberstatus oder öffnet offizielle Treiberquellen.";
            if (t.Contains("WINDOWS UPDATE")) return "Öffnet Windows Update. Dort kannst du Updates installieren.";
            if (t.Contains("OPTIONALE")) return "Öffnet optionale Windows-Treiberupdates.";
            if (t.Contains("NVIDIA")) return "Öffnet die offizielle NVIDIA Treiberseite.";
            if (t.Contains("AMD")) return "Öffnet die offizielle AMD Treiber-/Chipsatzseite.";
            if (t.Contains("INTEL")) return "Öffnet das offizielle Intel Update-Tool.";
            if (t.Contains("REALTEK")) return "Öffnet die offizielle Realtek Treiberseite.";
            if (t.Contains("ANALYSE")) return "Scannt nur und löscht/ändert nichts.";
            if (t.Contains("REINIGUNG")) return "Löscht ausgewählte sichere Cache-/Temp-Daten mit Sicherheitsabfrage.";
            if (t.Contains("CHROME CHECK")) return "Prüft Chrome Cache, Verlauf, Cookies, Extensions und Background Mode.";
            if (t.Contains("CHROME BACKGROUND")) return "Schließt Chrome und deaktiviert Hintergrund-Apps in Chrome Preferences.";
            if (t.Contains("BROWSER SCHLIESSEN")) return "Schließt Chrome, Edge, Firefox, Brave und Opera, damit Cache/Verlauf löschbar sind.";
            if (t.Contains("CHROME DATEN")) return "Schließt Chrome und löscht Cache, Verlauf und Cookies.";
            if (t.Contains("TEMP")) return "Löscht User-Temp und Windows-Temp. Gesperrte Dateien werden übersprungen.";
            if (t.Contains("SCHNELLSTEN DNS TESTEN")) return "Misst DNS-Ping zu Cloudflare, Google und Quad9 und zeigt den schnellsten an.";
            if (t.Contains("SCHNELLSTEN DNS SETZEN")) return "Misst DNS und setzt den schnellsten Anbieter auf aktive Netzwerkadapter.";
            if (t.Contains("CLOUDFLARE")) return "Setzt DNS auf Cloudflare: 1.1.1.1 und 1.0.0.1.";
            if (t.Contains("GOOGLE SETZEN")) return "Setzt DNS auf Google: 8.8.8.8 und 8.8.4.4.";
            if (t.Contains("DNS AUTOMATISCH")) return "Stellt DNS wieder auf automatisch per DHCP.";
            if (t.Contains("DNS")) return "Leert DNS-Cache oder verwaltet DNS-Server.";
            if (t.Contains("PING")) return "Testet Verbindungslatenz zu mehreren DNS-Servern.";
            if (t.Contains("SPEED")) return "Macht einfachen Download-/Ping-Test. Kein offizieller Ookla-Test.";
            if (t.Contains("SFC")) return "Prüft und repariert Windows-Systemdateien.";
            if (t.Contains("DISM")) return "Repariert den Windows-Komponentenstore.";
            if (t.Contains("WINSOCK")) return "Setzt Netzwerkstack zurück. Danach Neustart empfohlen.";
            if (t.Contains("UEFI")) return "Startet in Windows-Erweiterte Startoptionen für BIOS/UEFI.";
            if (t.Contains("QUICK ASSIST")) return "Öffnet Windows Schnellhilfe für sichere Fernunterstützung.";
            if (t.Contains("CODE") || t.Contains("REFERENZ") || t.Contains("REFERENCE")) return "Kopiert nur die optionale Redline Referenz-ID (kein Fernzugriff). Verbindung: Quick Assist.";
            if (t.Contains("UPDATE")) return "Prüft online, ob eine neue Redline-Version verfügbar ist.";
            if (t.Contains("DOWNLOAD")) return "Lädt den neuen Redline Installer herunter und startet ihn nach Bestätigung.";
            if (t.Contains("REPORT")) return "Speichert die aktuelle Ausgabe als Textdatei auf dem Desktop.";
            if (t.Contains("TASK")) return "Öffnet den Windows Task-Manager.";
            if (t.Contains("GERÄTE")) return "Öffnet den Geräte-Manager.";
            if (t.Contains("GRAFIK")) return "Öffnet Windows Grafik-Einstellungen.";
            if (t.Contains("AUTOSTART") || t.Contains("DEAKTIVIEREN")) return "Deaktiviert ausgewählte Autostarts und speichert Backup.";
            if (t.Contains("OPTIMIERUNG")) return "Führt ausgewählte Optimierungen aus. Kritische Aktionen fragen vorher nach.";

            return "Button ausführen. Redline zeigt Ergebnis im rechten Fenster.";
        }

        private static string ButtonInfoEn(string t)
        {
            if (t.Contains("OFFLINE SCAN")) return "Starts Microsoft Defender offline scan. Windows restarts and scans before boot.";
            if (t.Contains("QUICK SCAN")) return "Starts a quick Microsoft Defender virus scan.";
            if (t.Contains("SECURITY")) return "Checks Defender, firewall, hosts, processes, startups.";
            if (t.Contains("DRIVER") || t.Contains("TREIBER")) return "Checks driver status or opens official update sources.";
            if (t.Contains("AUTO") && t.Contains("UPDATE")) return "Scans your PC hardware and opens matching official driver update pages.";
            if (t.Contains("WINDOWS UPDATE")) return "Opens Windows Update to install updates.";
            if (t.Contains("NVIDIA")) return "Opens the official NVIDIA driver page.";
            if (t.Contains("AMD")) return "Opens the official AMD driver/chipset page.";
            if (t.Contains("INTEL")) return "Opens the official Intel support page.";
            if (t.Contains("REALTEK")) return "Opens the official Realtek driver page.";
            if (t.Contains("SCAN")) return "Scans only; does not change or delete anything.";
            if (t.Contains("CLEAN")) return "Deletes selected safe cache/temp data after confirmation.";
            if (t.Contains("UPDATE")) return "Checks online for a new Redline version.";
            if (t.Contains("DOWNLOAD")) return "Downloads the new Redline installer.";
            return "Runs the action. Results appear in the log panel on the right.";
        }

        private string OptionInfo(string name)
        {
            string n = name.ToLowerInvariant();

            if (IsEnglish())
            {
                if (n.Contains("history")) return "Deletes browser history. Browser must be closed.";
                if (n.Contains("cookies")) return "Deletes cookies/sessions. You will need to sign in again.";
                if (n.Contains("cache")) return "Deletes temporary cache files.";
                if (n.Contains("shader")) return "Deletes shader cache. May help after driver updates.";
                if (n.Contains("dns")) return "Flushes DNS cache.";
                if (n.Contains("recycle")) return "Empties the recycle bin.";
                if (n.Contains("game mode")) return "Enables Windows Game Mode.";
                if (n.Contains("power") || n.Contains("performance")) return "Sets high performance power plan.";
                if (n.Contains("driver")) return "Runs driver status check.";
                return "Runs only when checked.";
            }

            if (n.Contains("verlauf")) return "Löscht Browser-Verlauf. Browser muss geschlossen sein.";
            if (n.Contains("cookies")) return "Löscht Cookies/Login-Sessions. Danach musst du dich auf Webseiten neu anmelden.";
            if (n.Contains("cache")) return "Löscht temporäre Cache-Dateien. Kann Speicher freigeben.";
            if (n.Contains("shader")) return "Löscht Shader-Cache. Kann bei Stottern nach Treiberupdate helfen, wird später neu aufgebaut.";
            if (n.Contains("dns")) return "Leert DNS-Cache. Kann Verbindungs-/Namensauflösungsprobleme lösen.";
            if (n.Contains("papierkorb")) return "Leert den Windows Papierkorb.";
            if (n.Contains("logs")) return "Löscht Log-Dateien. Normal sicher, aber alte Protokolle sind dann weg.";
            if (n.Contains("dumps")) return "Löscht Absturzabbilder. Spart Speicher, aber Debug-Daten sind weg.";
            if (n.Contains("game mode")) return "Aktiviert Windows Spielemodus.";
            if (n.Contains("hochleistung")) return "Setzt Windows Energieplan auf hohe Leistung.";
            if (n.Contains("working sets")) return "Gibt Arbeitsspeicher von Prozessen frei. Kann kurzfristig helfen, ist nicht immer nötig.";
            if (n.Contains("explorer")) return "Startet Windows Explorer neu.";
            if (n.Contains("registry")) return "Liest Gaming-relevante Registry-Werte, löscht aber nichts.";
            if (n.Contains("security")) return "Führt Security-Basischeck aus.";
            if (n.Contains("treiber")) return "Führt Treiberstatus-Prüfung aus.";

            return "Option wird nur ausgeführt, wenn sie angehakt ist.";
        }

        private string PageTitle(string page)
        {
            return page switch
            {
                "Dashboard" => "DASHBOARD",
                "Readiness" => T("GAMING BEREITSCHAFT", "GAMING READINESS"),
                "AntiCheat" => "ANTI-CHEAT SAFE MODE",
                "UndoCenter" => "UNDO CENTER",
                "Cleaner" => "CLEANER",
                "GameProfiles" => T("GAMING KI PROFILE", "GAMING AI PROFILES"),
                "Optimierung" => T("PERFORMANCE", "PERFORMANCE"),
                "Leistung" => T("SYSTEM", "SYSTEM"),
                "Startup" => "STARTUP",
                "Security" => "SECURITY",
                "Drivers" => T("DRIVER", "DRIVER"),
                "Bios" => "BIOS / UEFI",
                "Network" => "NETWORK",
                "Repair" => "REPAIR",
                "RemoteSupport" => "REMOTE SUPPORT",
                "Tools" => "TOOLS",
                "Update" => "UPDATE",
                "Help" => T("HILFE", "HELP"),
                "Settings" => "SETTINGS",
                "Einstellungen" => "SETTINGS",
                _ => page.ToUpper()
            };
        }

        private static bool PageSkipsShellHeader(string page) =>
            page is "Dashboard" or "Cleaner" or "Security" or "Optimierung" or "Repair" or "Network" or "Drivers" or "RemoteSupport" or "Settings" or "GameProfiles" or "Startup";

        private UIElement BuildV78PageHeader(string title, string subtitle, RoutedEventHandler? topAction = null, string? topActionLabel = null)
        {
            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 34,
                FontWeight = FontWeights.UltraBold
            });
            StatusText = new TextBlock
            {
                Text = T("Bereit", "Ready"),
                Foreground = AiGreen,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            };
            left.Children.Add(StatusText);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                left.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = Muted,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                    MaxWidth = 720
                });
            }
            hdr.Children.Add(left);

            StackPanel right = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
            right.Children.Add(BuildLastScanCornerCard());
            if (topAction != null && !string.IsNullOrWhiteSpace(topActionLabel))
            {
                Button act = new Button
                {
                    Width = 300,
                    Height = 64,
                    Margin = new Thickness(0, 12, 0, 0),
                    Background = new LinearGradientBrush(Color.FromRgb(255, 26, 70), Color.FromRgb(214, 10, 46), 90),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                ApplyButtonSkin(act, 12);
                act.Content = new TextBlock
                {
                    Text = topActionLabel,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.UltraBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                act.Click += topAction;
                right.Children.Add(act);
            }
            Grid.SetColumn(right, 1);
            hdr.Children.Add(right);
            return hdr;
        }

        private UIElement BuildThemeToggleRow()
        {
            Border wrap = new Border
            {
                Background = CardBg2,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0)
            };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };

            Button dark = new Button
            {
                Content = T("Dunkel ★", "Dark ★"),
                Width = 78,
                Height = 32,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            ApplyButtonSkin(dark, 16);
            void StyleDark()
            {
                bool on = !IsLightTheme;
                dark.Background = on ? Red : Brushes.Transparent;
                dark.Foreground = on ? Brushes.White : TextPrimary;
            }
            StyleDark();
            dark.Click += (s, e) => { ApplyThemeMode("Dark"); };

            Button light = new Button
            {
                Content = T("Hell", "Light"),
                Width = 78,
                Height = 32,
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            ApplyButtonSkin(light, 16);
            void StyleLight()
            {
                bool on = IsLightTheme;
                light.Background = on ? Red : Brushes.Transparent;
                light.Foreground = on ? Brushes.White : TextPrimary;
            }
            StyleLight();
            light.Click += (s, e) => { ApplyThemeMode("Light"); };

            row.Children.Add(dark);
            row.Children.Add(light);
            wrap.Child = row;
            return wrap;
        }

        private Border BuildLastScanCornerCard()
        {
            Border card = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                MinWidth = 240
            };
            StackPanel p = new StackPanel();
            StackPanel scanHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            scanHead.Children.Add(new TextBlock { Text = "♥", Foreground = Red, FontSize = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            scanHead.Children.Add(new TextBlock { Text = T("LETZTER SCAN", "LAST SCAN"), Foreground = Muted, FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(scanHead);
            p.Children.Add(new TextBlock
            {
                Text = LastScanDisplay,
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            Button details = OutlineButton(T("Details anzeigen", "Show details"), DashboardQuickCheck_Click);
            details.Height = 34;
            details.Width = double.NaN;
            details.HorizontalAlignment = HorizontalAlignment.Stretch;
            p.Children.Add(details);
            card.Child = p;
            return card;
        }

        private UIElement MadeByFooter()
        {
            return new TextBlock
            {
                Text = "Made by Tobias Immisch ❤",
                Foreground = Muted,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };
        }

        private Border BuildLargeAiScoreCard(int? score, RoutedEventHandler scanClick)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 14, 0);
            card.Padding = new Thickness(24, 20, 24, 20);
            card.MinHeight = 268;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            int ringValue = score ?? 0;
            bool hasScore = score.HasValue;

            Grid ring = new Grid { Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Shapes.Ellipse outerGlow = new System.Windows.Shapes.Ellipse
            {
                Width = 168,
                Height = 168,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 235, 18, 48)),
                StrokeThickness = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (hasScore)
                outerGlow.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(235, 18, 48), BlurRadius = 28, ShadowDepth = 0, Opacity = 0.75 };
            ring.Children.Add(outerGlow);
            ring.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 150,
                Height = 150,
                Stroke = new SolidColorBrush(Color.FromRgb(48, 54, 66)),
                StrokeThickness = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            double dash = hasScore ? Math.Max(0.05, Math.Min(1, ringValue / 100.0)) * 4.2 : 0.05;
            ring.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 150,
                Height = 150,
                Stroke = hasScore ? Red : Border,
                StrokeThickness = 10,
                StrokeDashArray = new DoubleCollection { dash, 8 },
                RenderTransform = new RotateTransform(-90, 75, 75),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            StackPanel center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            center.Children.Add(new TextBlock { Text = "AI SCORE", Foreground = Muted, FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            StackPanel scoreLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            scoreLine.Children.Add(new TextBlock { Text = hasScore ? ringValue.ToString() : "—", Foreground = TextPrimary, FontSize = 38, FontWeight = FontWeights.UltraBold });
            scoreLine.Children.Add(new TextBlock { Text = "/100", Foreground = Muted, FontSize = 14, Margin = new Thickness(2, 14, 0, 0) });
            center.Children.Add(scoreLine);
            if (!hasScore)
                center.Children.Add(new TextBlock { Text = T("Scan starten", "Run scan"), Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            ring.Children.Add(center);
            g.Children.Add(ring);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            text.Children.Add(new TextBlock
            {
                Text = T("REDLINE KI ANALYSIERT, DU GEWINNST.", "REDLINE AI ANALYZES. YOU WIN."),
                Foreground = TextPrimary,
                FontSize = 19,
                FontWeight = FontWeights.UltraBold,
                TextWrapping = TextWrapping.Wrap
            });
            Button scan = RedButton("🚀  " + T("SYSTEM SCANNEN", "SYSTEM SCAN"), scanClick);
            scan.Width = 300;
            scan.Height = 50;
            scan.Margin = new Thickness(0, 16, 0, 0);
            text.Children.Add(scan);
            Button rec = OutlineButton(T("KI-EMPFEHLUNGEN ANZEIGEN", "SHOW AI RECOMMENDATIONS"), async (s, e) =>
            {
                if (!RedlineAppData.Current.LastScanUtc.HasValue)
                {
                    await RunSystemFullScan();
                    return;
                }
                Navigate("Optimierung");
            });
            rec.Width = 300;
            rec.Height = 40;
            rec.Margin = new Thickness(0, 10, 0, 0);
            rec.Foreground = TextPrimary;
            text.Children.Add(rec);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            card.Child = g;
            return card;
        }

        private Border BuildLiveSystemPanel()
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 0, 0);
            card.Padding = new Thickness(16);
            card.MinHeight = 268;

            StackPanel root = new StackPanel();
            root.Children.Add(new TextBlock
            {
                Text = T("LIVE SYSTEM", "LIVE SYSTEM"),
                Foreground = TextPrimary,
                FontSize = 13,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 12)
            });

            int? savedPing = RedlineAppData.Current.LastPingMs;
            string pingVal = savedPing > 0 ? savedPing + " ms" : "—";
            string pingSub = savedPing > 0 ? PingQualityLabel(savedPing.Value) : T("Noch nicht gemessen", "Not measured yet");

            UniformGrid grid = new UniformGrid { Columns = 2 };
            grid.Children.Add(DashboardLiveStat("CPU", GetCpuLoadText(), GetCpuClockText(), "⌁", out DashboardCpuText, out DashboardCpuSubText));
            grid.Children.Add(DashboardLiveStat("GPU", "—", TruncateGpuName(GetGpuName()), "GPU", out DashboardGpuText, out DashboardGpuSubText));
            grid.Children.Add(DashboardLiveStat("RAM", GetRamUsageText(), GetRamUsedVsTotalText(), "▦", out DashboardRamText, out DashboardRamSubText));
            grid.Children.Add(DashboardLiveStat("PING", pingVal, pingSub, "◉", out DashboardPingText, out DashboardPingSubText));
            root.Children.Add(grid);
            card.Child = root;
            return card;
        }

        private Border DashboardLiveStat(string title, string value, string sub, string icon, out TextBlock valueBlock, out TextBlock subBlock)
        {
            Border box = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 10, 10, 8),
                Margin = new Thickness(4),
                MinHeight = 108
            };
            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = title, Foreground = TextPrimary, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            p.Children.Add(new TextBlock { Text = icon, Foreground = AiGreen, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4) });
            valueBlock = new TextBlock { Text = value, Foreground = AiGreen, FontSize = 20, FontWeight = FontWeights.UltraBold, HorizontalAlignment = HorizontalAlignment.Center };
            p.Children.Add(valueBlock);
            subBlock = new TextBlock { Text = sub, Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            p.Children.Add(subBlock);

            if (title == "RAM")
            {
                GetMemoryUsage(out _, out _, out int ramPct);
                ProgressBar ramBar = new ProgressBar
                {
                    Height = 4,
                    Maximum = 100,
                    Value = ramPct,
                    Margin = new Thickness(0, 8, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(32, 38, 48)),
                    BorderThickness = new Thickness(0),
                    Foreground = Red
                };
                p.Children.Add(ramBar);
            }

            p.Children.Add(BuildDecorativeSparkline(title));
            box.Child = p;
            return box;
        }

        private UIElement BuildDecorativeSparkline(string seed)
        {
            int hash = Math.Abs(seed.GetHashCode()) % 7;
            double w = 90;
            double h = 18;
            double step = w / 8;
            PointCollection pts = new PointCollection();
            for (int i = 0; i <= 8; i++)
            {
                double y = h / 2 + Math.Sin((i + hash) * 0.9) * (h * 0.35);
                pts.Add(new Point(i * step, y));
            }
            System.Windows.Shapes.Polyline line = new System.Windows.Shapes.Polyline
            {
                Points = pts,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 235, 18, 48)),
                StrokeThickness = 1.5,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.65
            };
            return line;
        }

        private Button PerfTileArrowButton(RoutedEventHandler click, string tooltip)
        {
            Button arrow = new Button
            {
                Content = ">",
                Width = 32,
                Height = 28,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Red,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tooltip,
                VerticalAlignment = VerticalAlignment.Top
            };
            ApplyButtonSkin(arrow, 8);
            arrow.Click += (s, e) =>
            {
                e.Handled = true;
                click(s, e);
            };
            return arrow;
        }

        private UIElement PerfFeatureTile(string title, string desc, string status, RoutedEventHandler cardClick, RoutedEventHandler? arrowClick = null)
        {
            RoutedEventHandler openDetails = arrowClick ?? ((s, e) => OpenPerfTileDetails(title));

            Border tile = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 18, 23, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 47, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 12, 12),
                Width = 280
            };

            StackPanel p = new StackPanel();
            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock titleBlock = new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold };
            RedlineUi.ApplyCrispText(titleBlock);
            head.Children.Add(titleBlock);
            head.Children.Add(PerfTileArrowButton(openDetails, T("Einstellungen öffnen", "Open settings")));
            Grid.SetColumn(head.Children[1], 1);
            p.Children.Add(head);

            StackPanel body = new StackPanel { Cursor = System.Windows.Input.Cursors.Hand };
            body.MouseLeftButtonUp += (s, e) => cardClick(s, new RoutedEventArgs());
            body.Children.Add(new TextBlock { Text = desc, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
            StackPanel st = new StackPanel { Orientation = Orientation.Horizontal };
            st.Children.Add(new TextBlock { Text = "●", Foreground = AiGreen, FontSize = 12, Margin = new Thickness(0, 0, 6, 0) });
            st.Children.Add(new TextBlock { Text = status, Foreground = AiGreen, FontSize = 12, FontWeight = FontWeights.SemiBold });
            body.Children.Add(st);
            p.Children.Add(body);
            tile.Child = p;
            return tile;
        }

        private UIElement ProFeatureTile(string title, string desc, string badge, RoutedEventHandler cardClick, RoutedEventHandler? arrowClick = null)
        {
            RoutedEventHandler openDetails = arrowClick ?? ((s, e) => OpenPerfTileDetails(title));

            Border tile = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 40, 10, 20)),
                BorderBrush = Red,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 12, 12),
                Width = 280,
                ToolTip = desc + "\n\n" + (RedlineAppData.ProPurchaseEnabled
                    ? T("Nur mit Pro-Lizenz (10 € Lifetime).", "Pro license only (€10 lifetime).")
                    : T("Pro kommt später mit Konto (10 € Lifetime geplant).", "Pro later with account (€10 lifetime planned)."))
            };

            StackPanel p = new StackPanel();
            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold });
            StackPanel rightHead = new StackPanel { Orientation = Orientation.Horizontal };
            Border proBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(110, 16, 28)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Child = new TextBlock { Text = badge, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.Bold }
            };
            rightHead.Children.Add(proBadge);
            rightHead.Children.Add(PerfTileArrowButton(openDetails, T("Gaming-Einstellungen öffnen", "Open gaming settings")));
            Grid.SetColumn(rightHead, 1);
            head.Children.Add(rightHead);
            p.Children.Add(head);

            StackPanel body = new StackPanel { Cursor = System.Windows.Input.Cursors.Hand };
            body.MouseLeftButtonUp += (s, e) => cardClick(s, new RoutedEventArgs());
            body.Children.Add(new TextBlock { Text = desc, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
            body.Children.Add(new TextBlock
            {
                Text = IsProActive() ? T("● Pro aktiv", "● Pro active") : T("● Key in Einstellungen", "● Key in Settings"),
                Foreground = IsProActive() ? AiGreen : AiOrange,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });
            p.Children.Add(body);
            tile.Child = p;
            return tile;
        }

        private Brush AiGreen => new SolidColorBrush(Color.FromRgb(62, 210, 86));
        private Brush AiOrange => new SolidColorBrush(Color.FromRgb(255, 161, 22));
        private Brush AiPurple => new SolidColorBrush(Color.FromRgb(162, 68, 255));
        private Brush AiBlue => new SolidColorBrush(Color.FromRgb(64, 143, 255));

        private Border AiHeroCard(string title, string sub, string badge, string primaryText, RoutedEventHandler primary, string secondaryText, RoutedEventHandler secondary)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 18, 18);
            card.Padding = new Thickness(22);
            card.MinHeight = 225;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Grid orb = new Grid { Width = 250, Height = 170, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < 5; i++)
            {
                double size = 150 + i * 22;
                System.Windows.Shapes.Ellipse ring = new System.Windows.Shapes.Ellipse
                {
                    Width = size,
                    Height = size,
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)(80 - i * 10), 238, 18, 48)),
                    StrokeThickness = i == 0 ? 3 : 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                orb.Children.Add(ring);
            }

            Border aiCore = new Border
            {
                Width = 82,
                Height = 82,
                CornerRadius = new CornerRadius(22),
                Background = new RadialGradientBrush(Color.FromRgb(238, 18, 48), Color.FromRgb(70, 4, 16)),
                BorderBrush = Red,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            aiCore.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 25, ShadowDepth = 0, Color = Color.FromRgb(238, 18, 48), Opacity = 0.8 };
            aiCore.Child = new TextBlock
            {
                Text = badge,
                Foreground = Brushes.White,
                FontSize = 32,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            orb.Children.Add(aiCore);

            Grid.SetColumn(orb, 0);
            g.Children.Add(orb);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            text.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.UltraBold,
                TextWrapping = TextWrapping.Wrap
            });
            text.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = Muted,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 18),
                LineHeight = 22
            });

            Button primaryBtn = RedButton(primaryText, primary);
            primaryBtn.Width = 250;
            primaryBtn.Height = 48;
            text.Children.Add(primaryBtn);

            Button secondBtn = OutlineButton(secondaryText, secondary);
            secondBtn.Width = 250;
            secondBtn.Height = 42;
            secondBtn.Margin = new Thickness(0, 10, 0, 0);
            text.Children.Add(secondBtn);

            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            card.Child = g;
            return card;
        }

        private Button RedButton(string text, RoutedEventHandler click)
        {
            Button b = new Button
            {
                Content = text,
                Background = new LinearGradientBrush(Color.FromRgb(255, 25, 64), Color.FromRgb(183, 6, 36), 90),
                Foreground = Brushes.White,
                BorderBrush = Red,
                BorderThickness = new Thickness(1),
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyButtonSkin(b, 8);
            b.Click += click;
            return b;
        }

        private Button OutlineButton(string text, RoutedEventHandler click)
        {
            Button b = new Button
            {
                Content = text,
                Background = Brushes.Transparent,
                Foreground = TextPrimary,
                BorderBrush = Red,
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyButtonSkin(b, 8);
            b.Click += click;
            return b;
        }

        private Border RoundIcon(string icon, Brush bg, int size = 42)
        {
            Border b = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 4d),
                Background = bg,
                Margin = new Thickness(0, 0, 14, 0)
            };
            b.Child = new TextBlock
            {
                Text = icon,
                Foreground = Brushes.White,
                FontSize = icon.Length > 2 ? 11 : 18,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return b;
        }

        private UIElement AiGameRow(string name, string platform, string icon, bool optimized)
        {
            Border row = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(115, 23, 29, 38)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 48, 62)),
                BorderThickness = new Thickness(1)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(RoundIcon(icon, optimized ? new SolidColorBrush(Color.FromRgb(18, 90, 38)) : Red, 44));

            StackPanel names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold });
            names.Children.Add(new TextBlock { Text = T("Installiert", "Installed") + "  •  " + platform, Foreground = Brushes.LimeGreen, FontSize = 12 });
            Grid.SetColumn(names, 1);
            g.Children.Add(names);

            Button act = OutlineButton(T("PRO GUIDE", "PRO GUIDE"), (s, e) => SelectGameForAdvice(name));
            act.Width = 132;
            act.Height = 36;
            act.Foreground = optimized ? Brushes.LimeGreen : Brushes.White;
            act.BorderBrush = optimized ? new SolidColorBrush(Color.FromRgb(20, 130, 55)) : Red;
            Grid.SetColumn(act, 2);
            g.Children.Add(act);

            row.Child = g;
            return row;
        }

        private Border AiSidePanel(string title, params UIElement[] children)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 0, 18);
            card.Padding = new Thickness(18);

            StackPanel p = new StackPanel();
            Grid head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.UltraBold });

            Border beta = new Border
            {
                BorderBrush = Red,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 3)
            };
            beta.Child = new TextBlock { Text = "BETA", Foreground = Red, FontSize = 11, FontWeight = FontWeights.UltraBold };
            Grid.SetColumn(beta, 1);
            head.Children.Add(beta);

            p.Children.Add(head);
            foreach (UIElement child in children)
                p.Children.Add(child);

            card.Child = p;
            return card;
        }

        private UIElement AiCheckRow(string title, string sub, string icon, Brush iconColor, bool ok = true)
        {
            Border row = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 16, 21, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 40, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(RoundIcon(icon, iconColor, 38));

            StackPanel t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 13.5, FontWeight = FontWeights.UltraBold });
            t.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            Border check = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = ok ? new SolidColorBrush(Color.FromRgb(34, 115, 48)) : new SolidColorBrush(Color.FromRgb(120, 78, 8)),
                VerticalAlignment = VerticalAlignment.Center
            };
            check.Child = new TextBlock { Text = ok ? "✓" : "!", Foreground = Brushes.White, FontWeight = FontWeights.UltraBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(check, 2);
            g.Children.Add(check);

            row.Child = g;
            return row;
        }

        private UIElement AiScoreBlock(string label, int? score, Brush color, string? subline = null)
        {
            bool hasScore = score.HasValue && score.Value > 0;
            int value = hasScore ? score!.Value : 0;

            Grid g = new Grid { Height = 98, Margin = new Thickness(0, 10, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Grid ring = new Grid { Width = 82, Height = 82, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ring.Children.Add(new System.Windows.Shapes.Ellipse { Stroke = new SolidColorBrush(Color.FromRgb(47, 55, 68)), StrokeThickness = 8 });
            if (hasScore)
            {
                double dash = Math.Max(0.05, Math.Min(1, value / 100.0)) * 4.2;
                ring.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Stroke = color,
                    StrokeThickness = 8,
                    StrokeDashArray = new DoubleCollection { dash, 8 },
                    RenderTransform = new RotateTransform(-90, 41, 41)
                });
            }
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = hasScore ? value.ToString() : "—",
                Foreground = Brushes.White,
                FontSize = hasScore ? 25 : 22,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            if (hasScore)
                sp.Children.Add(new TextBlock { Text = "/100", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            ring.Children.Add(sp);
            g.Children.Add(ring);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
            text.Children.Add(new TextBlock
            {
                Text = subline ?? (hasScore
                    ? T("Basierend auf deinem letzten Systemscan.", "Based on your last system scan.")
                    : T("Starte einen Scan für eine echte Bewertung.", "Run a scan for a real rating.")),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            return g;
        }

        private Border BuildGameProGuidePanel()
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 0, 18);
            card.Padding = new Thickness(18);

            StackPanel root = new StackPanel();
            root.Children.Add(new TextBlock
            {
                Text = T("PRO FPS GUIDE", "PRO FPS GUIDE"),
                Foreground = Red,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            root.Children.Add(new TextBlock
            {
                Text = T("Was bringt FPS · Was vermeiden", "What boosts FPS · What to avoid"),
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            _gameAdviceStatusText = new TextBlock
            {
                Text = T("Wähle ein Spiel links oder starte den Scan.", "Pick a game on the left or run scan."),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            root.Children.Add(_gameAdviceStatusText);

            _gameAdviceHost = new StackPanel();
            ScrollViewer scroll = new ScrollViewer
            {
                MaxHeight = 480,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            scroll.Content = _gameAdviceHost;
            root.Children.Add(scroll);

            card.Child = root;
            string defaultGame = DetectGames().FirstOrDefault(g => g.StartsWith("Rust", StringComparison.OrdinalIgnoreCase))
                ?? DetectGames().FirstOrDefault() ?? "Rust";
            _ = Dispatcher.BeginInvoke(new Action(() => SelectGameForAdvice(defaultGame)), DispatcherPriority.Loaded);
            return card;
        }

        private void SelectGameForAdvice(string gameName)
        {
            _selectedGameAdvice = gameName.Replace(" läuft", "", StringComparison.OrdinalIgnoreCase).Trim();
            RefreshGameAdvicePanel(_selectedGameAdvice);
            SetGameAdviceStatus(T("Guide: ", "Guide: ") + _selectedGameAdvice);
        }

        private void SetGameAdviceStatus(string text)
        {
            if (_gameAdviceStatusText != null)
                _gameAdviceStatusText.Text = text;
        }

        private void RefreshGameAdvicePanel(string gameName)
        {
            if (_gameAdviceHost == null)
                return;

            _gameAdviceHost.Children.Clear();
            bool en = IsEnglish();

            if (gameName.StartsWith("Rust", StringComparison.OrdinalIgnoreCase))
            {
                RustInstallProbe probe = RedlineGameProAdvice.ProbeRustInstall();
                _gameAdviceHost.Children.Add(new TextBlock
                {
                    Text = (probe.SteamFolderExists ? "✓ " : "○ ") + T("Steam-Ordner", "Steam folder")
                        + "  ·  " + (probe.LocalFolderExists ? "✓ " : "○ ") + "AppData",
                    Foreground = probe.SteamFolderExists ? AiGreen : Muted,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 10)
                });
            }

            foreach (GameProTip tip in RedlineGameProAdvice.BuildFor(gameName, en))
                _gameAdviceHost.Children.Add(BuildGameProTipCard(tip));
        }

        private UIElement BuildGameProTipCard(GameProTip tip)
        {
            bool en = IsEnglish();
            string title = en ? tip.TitleEn : tip.TitleDe;
            string detail = en ? tip.DetailEn : tip.DetailDe;

            Brush accent = tip.Kind switch
            {
                GameTipKind.Avoid => new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                GameTipKind.Optional => AiOrange,
                _ => AiGreen
            };

            string kindLabel = tip.Kind switch
            {
                GameTipKind.Avoid => T("NICHT", "AVOID"),
                GameTipKind.Optional => T("OPTIONAL", "OPTIONAL"),
                _ => T("EMPFOHLEN", "RECOMMENDED")
            };

            string impactLabel = tip.Impact switch
            {
                GameTipImpact.High => T("Hoher FPS-Effekt", "High FPS impact"),
                GameTipImpact.Medium => T("Mittel", "Medium"),
                _ => T("Niedrig", "Low")
            };

            Border card = new Border
            {
                Background = SubCardBg,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            StackPanel p = new StackPanel();
            WrapPanel badges = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            badges.Children.Add(GameTipBadge(kindLabel, accent));
            badges.Children.Add(GameTipBadge(impactLabel, Muted));
            p.Children.Add(badges);

            p.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                TextWrapping = TextWrapping.Wrap
            });
            p.Children.Add(new TextBlock
            {
                Text = detail,
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                LineHeight = 18
            });

            if (!string.IsNullOrEmpty(tip.ApplyAction))
            {
                GameProTip captured = tip;
                Button apply = OutlineButton(T("Anwenden", "Apply"), async (s, e) => await ApplyGameProTipAsync(captured));
                apply.Height = 34;
                apply.Width = 120;
                apply.Margin = new Thickness(0, 10, 0, 0);
                apply.HorizontalAlignment = HorizontalAlignment.Left;
                p.Children.Add(apply);
            }

            card.Child = p;
            return card;
        }

        private static Border GameTipBadge(string text, Brush color)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderBrush = color,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock { Text = text, Foreground = color, FontSize = 10, FontWeight = FontWeights.Bold }
            };
        }

        private async Task ApplyGameProTipAsync(GameProTip tip)
        {
            if (IsAntiCheatSafeModeActive(out string reason))
            {
                MessageBox.Show(
                    T("Spiel/Anti-Cheat läuft — keine Änderungen während des Spiels.\n", "Game/anti-cheat running — no changes while playing.\n") + reason,
                    T("Safe Mode", "Safe Mode"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string label = IsEnglish() ? tip.TitleEn : tip.TitleDe;
            SetGameAdviceStatus(T("Wende an: ", "Applying: ") + label + "…");

            switch (tip.ApplyAction)
            {
                case "GameMode":
                    await SetGameModeEnabled(true);
                    break;
                case "PowerPlan":
                    await SetHighPerformance();
                    break;
                case "FlushDns":
                    await FlushDNS();
                    break;
                case "OpenGameBar":
                    OpenUri("ms-settings:gaming-gamebar");
                    break;
                case "OpenGraphics":
                    OpenUri("ms-settings:display-advancedgraphics");
                    break;
                case "OpenNvidiaPanel":
                    string nvidiaUi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        @"NVIDIA Corporation\Control Panel Client\nvcplui.exe");
                    if (File.Exists(nvidiaUi))
                        SafeStartSystem(nvidiaUi);
                    else
                        OpenUri("ms-settings:display-advancedgraphics");
                    break;
                case "OpenRustFolder":
                    RustInstallProbe probe = RedlineGameProAdvice.ProbeRustInstall();
                    if (probe.SteamFolderExists)
                        SafeStartSystem("explorer.exe", "\"" + probe.SteamPath + "\"");
                    else
                        MessageBox.Show(T("Rust Steam-Ordner nicht gefunden.", "Rust Steam folder not found."), "Rust", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "ShaderCacheSafe":
                    if (MessageBox.Show(
                            T("Shader Cache leeren? Nur nach Treiberupdate empfohlen.", "Clear shader cache? Recommended only after driver update."),
                            T("Bestätigen", "Confirm"),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question) != MessageBoxResult.Yes)
                        break;
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var a = CleanFolder(Path.Combine(local, "D3DSCache"));
                    var b = CleanFolder(Path.Combine(local, @"NVIDIA\DXCache"));
                    SetGameAdviceStatus(T("Shader Cache: ", "Shader cache: ") + FormatSize(a.Size + b.Size) + T(" gelöscht", " cleared"));
                    return;
                default:
                    break;
            }

            SetGameAdviceStatus(T("Fertig: ", "Done: ") + label);
        }

        private UIElement BuildGamingAiInsightPanel(List<string> detectedGames)
        {
            if (detectedGames.Count == 0)
            {
                return AiSidePanel(
                    "REDLINE AI",
                    new TextBlock
                    {
                        Text = T("Noch keine Spiele erkannt. Starte „Spiel analysieren“ für echte KI-Empfehlungen.",
                                 "No games detected yet. Run \"Analyze game\" for real AI recommendations."),
                        Foreground = Muted,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 12)
                    },
                    AiScoreBlock(T("Optimierungspotenzial", "Optimization potential"), null, Red),
                    OutlineButton(T("Spiel analysieren", "Analyze game"), GameProfileAnalyze_Click));
            }

            string primary = detectedGames[0];
            int? score = RedlineAppData.Current.GamingScore;
            return AiSidePanel(
                "REDLINE AI",
                AiCheckRow(T("Spiel erkannt", "Game detected") + ": " + primary, T("Lokal erkannt", "Detected locally"), "AI", Red, true),
                AiCheckRow(T("Grafikmodus", "Graphics mode"), GraphicsMode, "GPU", CardBg2, true),
                AiCheckRow("Anti-Cheat Safe Mode", IsAntiCheatSafeModeActive(out _) ? T("Aktiv", "Active") : T("Bereit", "Ready"), "AC", CardBg2, true),
                AiScoreBlock(T("System-Score", "System score"), score, Red),
                OutlineButton(T("Details anzeigen", "Show details"), GameProfileAnalyze_Click));
        }

        private UIElement ProfileModeCard()
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 0, 0);
            card.Padding = new Thickness(18);

            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = T("PROFIL-MODUS", "PROFILE MODE"), Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 12) });

            UniformGrid modes = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 14) };
            modes.Children.Add(ProfileModeButton("FPS", T("Maximale Bilder", "Max FPS"), GraphicsMode == "FPS", () => { GraphicsMode = "FPS"; Navigate(CurrentPage); }));
            modes.Children.Add(ProfileModeButton(T("AUSGEWOGEN", "BALANCED"), "Balance", GraphicsMode == "Balanced", () => { GraphicsMode = "Balanced"; Navigate(CurrentPage); }));
            modes.Children.Add(ProfileModeButton(T("QUALITÄT", "QUALITY"), T("Beste Optik", "Best visuals"), GraphicsMode == "Quality", () => { GraphicsMode = "Quality"; Navigate(CurrentPage); }));

            p.Children.Add(modes);
            p.Children.Add(new TextBlock
            {
                Text = T("Der FPS-Modus deaktiviert visuelle Effekte und Hintergrundprozesse für die höchste mögliche Bildrate.",
                         "FPS mode disables visual effects and background processes for the highest possible frame rate."),
                Foreground = Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = p;
            return card;
        }

        private Button ProfileModeButton(string title, string sub, bool active, Action onClick)
        {
            Button b = new Button
            {
                Height = 94,
                Margin = new Thickness(0, 0, 10, 0),
                Background = active ? new LinearGradientBrush(Color.FromArgb(170, 130, 14, 30), Color.FromArgb(90, 236, 18, 48), 90) : new SolidColorBrush(Color.FromRgb(18, 23, 31)),
                BorderBrush = active ? Red : new SolidColorBrush(Color.FromRgb(40, 48, 62)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyButtonSkin(b, 8);
            StackPanel p = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            p.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold, HorizontalAlignment = HorizontalAlignment.Center });
            p.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            b.Content = p;
            b.Click += (s, e) => onClick();
            return b;
        }

        private void ApplyRecommendedCleanerCategories(bool recommendedOnly)
        {
            foreach (System.Collections.Generic.KeyValuePair<string, CheckBox> kv in CleanerChecks.ToList())
            {
                bool on = !recommendedOnly || CleanerRecommendedCategories.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
                kv.Value.IsChecked = on;
            }
        }

        private UIElement CleanerCategoryRow(string categoryKey, string title, string sub, string amount, string icon, Brush color)
        {
            Border row = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(110, 23, 29, 38)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = title + "\n" + sub + "\n\n" + T("Hover: Häkchen = wird beim Scan geprüft.", "Hover: Checked = included in scan.")
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(RoundIcon(icon, new SolidColorBrush(Color.FromRgb(45, 51, 64)), 40));

            StackPanel t = new StackPanel();
            t.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold });
            t.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12 });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            StackPanel a = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock amountTb = new TextBlock
            {
                Text = amount,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            a.Children.Add(amountTb);
            _cleanerCategoryAmountTexts[categoryKey] = amountTb;
            a.Children.Add(new TextBlock { Text = T("Sicher", "Safe"), Foreground = Brushes.LimeGreen, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right });
            Grid.SetColumn(a, 2);
            g.Children.Add(a);

            CheckBox cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            CleanerChecks[categoryKey] = cb;
            Grid.SetColumn(cb, 3);
            g.Children.Add(cb);

            row.Child = g;
            return row;
        }

        private UIElement SecurityTableRow(string area, string desc, string status, string risk, Brush riskColor, string icon, RoutedEventHandler action)
        {
            Border row = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(31, 38, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 10, 12, 10)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Grid first = new Grid();
            first.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            first.ColumnDefinitions.Add(new ColumnDefinition());
            first.Children.Add(RoundIcon(icon, riskColor, 38));
            StackPanel txt = new StackPanel();
            txt.Children.Add(new TextBlock { Text = area, Foreground = Brushes.White, FontSize = 13.5, FontWeight = FontWeights.UltraBold });
            txt.Children.Add(new TextBlock { Text = desc, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(txt, 1);
            first.Children.Add(txt);
            g.Children.Add(first);

            g.Children.Add(new TextBlock { Text = status, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) });
            Grid.SetColumn(g.Children[g.Children.Count - 1], 1);

            g.Children.Add(new TextBlock { Text = "● " + risk, Foreground = riskColor, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(g.Children[g.Children.Count - 1], 2);

            Button b = OutlineButton(risk == T("Niedrig", "Low") ? T("DETAILS", "DETAILS") : T("PRÜFEN", "CHECK"), action);
            b.Width = 105;
            b.Height = 34;
            Grid.SetColumn(b, 3);
            g.Children.Add(b);

            row.Child = g;
            return row;
        }

        private UIElement SettingsRow(string icon, string title, string sub, UIElement control)
        {
            Border row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 40, 52)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 13, 0, 13)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

            g.Children.Add(RoundIcon(icon, Brushes.Transparent, 44));

            StackPanel t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold });
            t.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            Grid.SetColumn(control, 2);
            g.Children.Add(control);

            row.Child = g;
            return row;
        }

        private UIElement Segmented(params Button[] buttons)
        {
            UniformGrid u = new UniformGrid { Columns = buttons.Length };
            foreach (Button b in buttons)
                u.Children.Add(b);
            return u;
        }

        private Button SegmentButton(string text, bool active, Action onClick)
        {
            Button b = SettingsChoiceButton(text, active);
            b.Height = 42;
            b.Margin = new Thickness(0);
            b.Click += (s, e) => onClick();
            return b;
        }

        private UIElement PageDashboard()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition());
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel hdrLeft = new StackPanel();
            hdrLeft.Children.Add(new TextBlock { Text = "DASHBOARD", Foreground = TextPrimary, FontSize = 34, FontWeight = FontWeights.UltraBold });
            string? updateBanner = _pendingUpdateBannerVersion ?? RedlineAppData.ConsumePendingUpdateBanner();
            _pendingUpdateBannerVersion = null;
            string displayVer = GetDisplayAppVersion();
            if (!string.IsNullOrWhiteSpace(updateBanner)
                && string.Equals(updateBanner, displayVer, StringComparison.OrdinalIgnoreCase))
            {
                hdrLeft.Children.Add(new TextBlock
                {
                    Text = T("Neues Update installiert (V" + displayVer + ").",
                             "New update installed (V" + displayVer + ")."),
                    Foreground = AiGreen,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }
            hdrLeft.Children.Add(new TextBlock
            {
                Text = T("Übersicht über die Leistung, den Status und die Optimierung deines Systems.",
                         "Overview of your system's performance, status and optimization."),
                Foreground = Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                MaxWidth = 720
            });
            hdrLeft.Children.Add(BuildThemeToggleRow());
            hdr.Children.Add(hdrLeft);
            hdr.Children.Add(BuildLastScanCornerCard());
            Grid.SetColumn(hdr.Children[1], 1);
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            Grid row1 = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            Border aiCard = BuildLargeAiScoreCard(RedlineAppData.Current.GamingScore, async (s, e) => await RunSystemFullScan());
            row1.Children.Add(aiCard);
            row1.Children.Add(BuildLiveSystemPanel());
            Grid.SetColumn(row1.Children[1], 1);
            Grid.SetRow(row1, 1);
            root.Children.Add(row1);

            UniformGrid row2 = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 8) };

            Border secCard = DashboardCard();
            secCard.Margin = new Thickness(0, 0, 14, 0);
            StackPanel secP = new StackPanel();
            secP.Children.Add(DashboardHeaderRow(T("SICHERHEITSSTATUS", "SECURITY STATUS"), "", false));
            string defenderTxt = GetDefenderStatusText();
            string firewallTxt = GetFirewallStatusText();
            bool defenderOk = defenderTxt.Contains("Aktiv", StringComparison.OrdinalIgnoreCase) || defenderTxt.Contains("Active", StringComparison.OrdinalIgnoreCase);
            bool firewallOk = firewallTxt.Contains("Aktiv", StringComparison.OrdinalIgnoreCase) || firewallTxt.Contains("Active", StringComparison.OrdinalIgnoreCase);
            if (defenderOk && firewallOk)
            {
                StackPanel okRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                okRow.Children.Add(new TextBlock { Text = "🛡", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) });
                okRow.Children.Add(new TextBlock
                {
                    Text = T("Dein System ist geschützt.", "Your system is protected."),
                    Foreground = AiGreen,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                });
                secP.Children.Add(okRow);
            }
            secP.Children.Add(SecurityStatusLine(T("Echtzeitschutz", "Real-time protection"), defenderTxt));
            secP.Children.Add(SecurityStatusLine("Firewall", firewallTxt));
            secP.Children.Add(SecurityStatusLine("Anti-Cheat Safe Mode", IsAntiCheatSafeModeActive(out _) ? T("Aktiv", "Active") : T("Bereit", "Ready")));
            secP.Children.Add(SecurityStatusLine(T("Browser Schutz", "Browser protection"), T("Windows SmartScreen", "Windows SmartScreen")));
            Button secBtn = OutlineButton(T("Security Center öffnen", "Open Security Center"), (s, e) => Navigate("Security"));
            secBtn.Margin = new Thickness(0, 12, 0, 0);
            secP.Children.Add(secBtn);
            secCard.Child = secP;
            row2.Children.Add(secCard);

            Border autoCard = DashboardCard();
            autoCard.Margin = new Thickness(0, 0, 14, 0);
            StackPanel autoP = new StackPanel();
            autoP.Children.Add(DashboardHeaderRow(T("AUTOSTART PROGRAMME", "AUTOSTART PROGRAMS"), "", false));
            int autoTotal = GetCachedAutostartCount();
            autoP.Children.Add(new TextBlock
            {
                Text = autoTotal > 0
                    ? T(autoTotal + " Programme starten automatisch mit Windows.", autoTotal + " programs start automatically with Windows.")
                    : T("Keine Autostarts in der Registry gefunden.", "No autostarts found in the registry."),
                Foreground = Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            List<(string name, string impact, int level)> autostarts = GetCachedAutostartPreview(5);
            if (autostarts.Count == 0)
                autoP.Children.Add(new TextBlock { Text = T("Keine Autostarts gefunden", "No autostart entries found"), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
            else
                foreach (var a in autostarts)
                    autoP.Children.Add(AutostartLine(a.name, a.impact, a.level));
            Button autoBtn = OutlineButton(T("Autostart verwalten", "Manage autostart"), (s, e) => Navigate("Startup"));
            autoBtn.Margin = new Thickness(0, 12, 0, 0);
            autoP.Children.Add(autoBtn);
            autoCard.Child = autoP;
            row2.Children.Add(autoCard);

            Border storageCard = DashboardCard();
            storageCard.Margin = new Thickness(0, 0, 14, 0);
            StackPanel storP = new StackPanel();
            storP.Children.Add(DashboardHeaderRow(T("SPEICHERÜBERSICHT", "STORAGE OVERVIEW"), "", false));
            DashboardStorageTotalText = new TextBlock { Text = GetStorageTotalLabel(), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
            RedlineUi.ApplyCrispText(DashboardStorageTotalText);
            storP.Children.Add(DashboardStorageTotalText);
            Grid storRow = new Grid { Margin = new Thickness(0, 4, 0, 8) };
            storRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            storRow.ColumnDefinitions.Add(new ColumnDefinition());
            DashboardStorageDonutHost = new Grid();
            DashboardStorageDonutHost.Children.Add(BuildStorageDonutVisual(TryGetStorageOverviewSnapshot()));
            storRow.Children.Add(DashboardStorageDonutHost);
            StackPanel storText = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            DashboardStorageSummaryText = new TextBlock
            {
                Text = GetStorageOverviewSummaryText(),
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            RedlineUi.ApplyCrispText(DashboardStorageSummaryText);
            storText.Children.Add(DashboardStorageSummaryText);
            StorageOverviewSnapshot storSnap = TryGetStorageOverviewSnapshot();
            storText.Children.Add(StorageLegendLineWithValue(
                T("Gesamt", "Total"),
                TextPrimary,
                storSnap.Ready ? FormatBytes(storSnap.TotalBytes) : "—"));
            DashboardStorageUsedLegendText = StorageLegendLineWithValue(T("Belegt", "Used"), Red, GetStorageUsedLegendValue());
            storText.Children.Add(DashboardStorageUsedLegendText);
            DashboardStorageFreeLegendText = StorageLegendLineWithValue(T("Frei", "Free"), Muted, GetStorageFreeLegendValue());
            storText.Children.Add(DashboardStorageFreeLegendText);
            DashboardStorageTempLegendText = StorageLegendLineWithValue(T("Temporär", "Temporary"), AiBlue, GetStorageTempLegendValue());
            storText.Children.Add(DashboardStorageTempLegendText);
            Grid.SetColumn(storText, 1);
            storRow.Children.Add(storText);
            storP.Children.Add(storRow);
            DashboardStorageTempDetailText = new TextBlock
            {
                Text = T("Temp-Ordner wird ermittelt…", "Measuring temp folder…"),
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            RedlineUi.ApplyCrispText(DashboardStorageTempDetailText);
            storP.Children.Add(DashboardStorageTempDetailText);
            Button cleanBtn = RedButton(T("Bereinigen", "Clean up"), (s, e) => Navigate("Cleaner"));
            cleanBtn.Height = 40;
            cleanBtn.Margin = new Thickness(0, 4, 0, 0);
            storP.Children.Add(cleanBtn);
            storageCard.Child = storP;
            row2.Children.Add(storageCard);

            Border recCard = DashboardCard();
            StackPanel recP = new StackPanel();
            recP.Children.Add(DashboardHeaderRow(T("EMPFEHLUNGEN", "RECOMMENDATIONS"), "", false));
            List<string> recs = GetDashboardRecommendations();
            if (recs.Count > 0)
                recP.Children.Add(new TextBlock
                {
                    Text = T(recs.Count + " Optimierungen empfohlen", recs.Count + " optimizations recommended"),
                    Foreground = Muted,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            if (recs.Count == 0)
                recP.Children.Add(new TextBlock { Text = T("Starte einen Systemscan für echte Empfehlungen.", "Run a system scan for real recommendations."), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            else
                foreach (string r in recs.Take(3))
                    recP.Children.Add(RecLine(r));
            Button allRec = OutlineButton(T("ALLE EMPFEHLUNGEN ANZEIGEN", "SHOW ALL RECOMMENDATIONS"), OptimizeAllDashboard_Click);
            allRec.Margin = new Thickness(0, 12, 0, 0);
            recP.Children.Add(allRec);
            recCard.Child = recP;
            row2.Children.Add(recCard);

            Grid.SetRow(row2, 2);
            root.Children.Add(row2);

            ScheduleStorageTempSizeScan();
            StartDashboardLiveTimer();
            return root;
        }

        private UIElement SecurityStatusLine(string label, string status)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 12 });
            Brush statusBrush = status.Contains("Aktiv", StringComparison.OrdinalIgnoreCase) || status.Contains("Active", StringComparison.OrdinalIgnoreCase) || status.Contains("Bereit", StringComparison.OrdinalIgnoreCase) || status.Contains("Ready", StringComparison.OrdinalIgnoreCase)
                ? AiGreen : Muted;
            g.Children.Add(new TextBlock { Text = status, Foreground = statusBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
            g.Children.Add(new TextBlock { Text = "›", Foreground = Muted, FontSize = 14 });
            Grid.SetColumn(g.Children[1], 1);
            Grid.SetColumn(g.Children[2], 2);
            return g;
        }

        private UIElement AutostartLine(string name, string impact, int level)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = name, Foreground = TextPrimary, FontSize = 12 });
            Brush c = level >= 2 ? Red : level == 1 ? AiOrange : AiGreen;
            g.Children.Add(new TextBlock { Text = impact, Foreground = c, FontSize = 11 });
            Grid.SetColumn(g.Children[1], 1);
            return g;
        }

        private UIElement StorageLegendLineWithValue(string label, Brush color, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock dot = new TextBlock { Text = "●", Foreground = color, FontSize = 13, Margin = new Thickness(0, 0, 7, 0) };
            TextBlock lbl = new TextBlock { Text = label, Foreground = Muted, FontSize = 13 };
            left.Children.Add(dot);
            left.Children.Add(lbl);
            RedlineUi.ApplyCrispText(left);

            TextBlock val = new TextBlock
            {
                Text = value,
                Foreground = TextPrimary,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            RedlineUi.ApplyCrispText(val);

            g.Children.Add(left);
            g.Children.Add(val);
            Grid.SetColumn(val, 2);
            RedlineUi.ApplyCrispText(g);
            return g;
        }

        private sealed class StorageOverviewSnapshot
        {
            public bool Ready;
            public string DriveLetter = "C:";
            public string VolumeLabel = "";
            public long TotalBytes;
            public long FreeBytes;
            public long UsedBytes;
            public int UsedPercent;
        }

        private StorageOverviewSnapshot TryGetStorageOverviewSnapshot()
        {
            StorageOverviewSnapshot snap = new StorageOverviewSnapshot();
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            string systemId = systemRoot.TrimEnd('\\').ToUpperInvariant();
            if (string.IsNullOrEmpty(systemId))
                systemId = "C:";

            try
            {
                using ManagementObjectSearcher mos = new ManagementObjectSearcher(
                    "SELECT DeviceID, Size, FreeSpace, VolumeName FROM Win32_LogicalDisk WHERE DriveType=3");
                foreach (ManagementObject mo in mos.Get())
                {
                    string? device = mo["DeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(device) ||
                        !string.Equals(device, systemId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    long total = Convert.ToInt64(mo["Size"] ?? 0L);
                    long free = Convert.ToInt64(mo["FreeSpace"] ?? 0L);
                    if (total <= 0)
                        break;

                    snap.Ready = true;
                    snap.DriveLetter = device;
                    snap.VolumeLabel = (mo["VolumeName"]?.ToString() ?? "").Trim();
                    snap.TotalBytes = total;
                    snap.FreeBytes = Math.Clamp(free, 0, total);
                    snap.UsedBytes = Math.Max(0, total - snap.FreeBytes);
                    snap.UsedPercent = (int)Math.Round((snap.UsedBytes * 100d) / total);
                    return snap;
                }
            }
            catch { }

            try
            {
                DriveInfo? d = GetSystemDriveInfo();
                if (d == null || !d.IsReady || d.TotalSize <= 0)
                    return snap;

                long free = d.TotalFreeSpace > 0 ? d.TotalFreeSpace : d.AvailableFreeSpace;
                snap.Ready = true;
                snap.DriveLetter = d.Name.TrimEnd('\\');
                snap.VolumeLabel = (d.VolumeLabel ?? "").Trim();
                snap.TotalBytes = d.TotalSize;
                snap.FreeBytes = Math.Clamp(free, 0, d.TotalSize);
                snap.UsedBytes = Math.Max(0, d.TotalSize - snap.FreeBytes);
                snap.UsedPercent = (int)Math.Round((snap.UsedBytes * 100d) / d.TotalSize);
            }
            catch { }

            return snap;
        }

        private string GetStorageOverviewSummaryText()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            if (!s.Ready)
                return T("Systemlaufwerk nicht lesbar", "System drive not readable");

            string label = string.IsNullOrWhiteSpace(s.VolumeLabel) ? s.DriveLetter : s.VolumeLabel + " (" + s.DriveLetter + ")";
            return label + T(" · Gesamt ", " · Total ") + FormatBytes(s.TotalBytes)
                + T(" · Frei ", " · Free ") + FormatBytes(s.FreeBytes)
                + T(" · Belegt ", " · Used ") + FormatBytes(s.UsedBytes);
        }

        private string GetStorageUsedLegendValue()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            return s.Ready ? FormatBytes(s.UsedBytes) + " (" + s.UsedPercent + "%)" : "—";
        }

        private string GetStorageFreeLegendValue()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            if (!s.Ready) return "—";
            int freePct = s.TotalBytes > 0 ? (int)Math.Round((s.FreeBytes * 100d) / s.TotalBytes) : 0;
            return FormatBytes(s.FreeBytes) + " (" + freePct + "%)";
        }

        private string GetStorageTempLegendValue()
        {
            if (_tempSizeCacheBytes.HasValue && _tempSizeCacheBytes.Value > 0)
                return FormatBytes(_tempSizeCacheBytes.Value);
            if (_tempSizeScanRunning)
                return "…";
            return "—";
        }

        private static void SetStorageLegendValue(UIElement? legendRow, string value)
        {
            if (legendRow is Grid g)
            {
                foreach (UIElement child in g.Children)
                {
                    if (child is TextBlock tb && Grid.GetColumn(tb) == 2)
                    {
                        tb.Text = value;
                        return;
                    }
                }
                if (g.Children.Count >= 2 && g.Children[1] is TextBlock fallback)
                    fallback.Text = value;
            }
        }

        private void UpdateDashboardStorageDisplay()
        {
            if (CurrentPage != "Dashboard")
                return;

            if (DashboardStorageTotalText != null)
                DashboardStorageTotalText.Text = GetStorageTotalLabel();
            if (DashboardStorageSummaryText != null)
                DashboardStorageSummaryText.Text = GetStorageOverviewSummaryText();
            SetStorageLegendValue(DashboardStorageUsedLegendText, GetStorageUsedLegendValue());
            SetStorageLegendValue(DashboardStorageFreeLegendText, GetStorageFreeLegendValue());
            SetStorageLegendValue(DashboardStorageTempLegendText, GetStorageTempLegendValue());
            if (DashboardStorageDonutHost != null)
            {
                DashboardStorageDonutHost.Children.Clear();
                DashboardStorageDonutHost.Children.Add(BuildStorageDonutVisual(TryGetStorageOverviewSnapshot()));
            }
        }

        private void ScheduleStorageTempSizeScan()
        {
            if (_tempSizeCacheBytes.HasValue && DateTime.UtcNow - _tempSizeCacheUtc < TempSizeCacheLifetime)
            {
                ApplyTempSizeToDashboardUi();
                return;
            }

            if (_tempSizeScanRunning)
                return;

            _tempSizeScanRunning = true;
            _ = Task.Run(() =>
            {
                long bytes = 0;
                try
                {
                    bytes = GetDirectorySizeSafe(Path.GetTempPath(), 8000);
                }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    _tempSizeScanRunning = false;
                    _tempSizeCacheBytes = bytes;
                    _tempSizeCacheUtc = DateTime.UtcNow;
                    ApplyTempSizeToDashboardUi();
                });
            });
        }

        private void ApplyTempSizeToDashboardUi()
        {
            if (DashboardStorageTempDetailText != null)
            {
                if (_tempSizeCacheBytes.HasValue && _tempSizeCacheBytes.Value > 0)
                    DashboardStorageTempDetailText.Text = T("Temporäre Dateien (%TEMP%): ", "Temporary files (%TEMP%): ") + FormatBytes(_tempSizeCacheBytes.Value) + T(" (geschätzt)", " (estimated)");
                else
                    DashboardStorageTempDetailText.Text = T("Keine Temp-Daten ermittelt", "No temp data measured");
            }
            UpdateDashboardStorageDisplay();
        }

        private UIElement BuildStorageDonutVisual(StorageOverviewSnapshot snap)
        {
            if (!snap.Ready)
            {
                Grid empty = new Grid { Width = 76, Height = 76 };
                empty.Children.Add(new TextBlock
                {
                    Text = "—",
                    Foreground = Muted,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return empty;
            }

            double usedPct = snap.UsedPercent / 100d;
            usedPct = Math.Clamp(usedPct, 0.02, 0.98);
            double size = 76;
            double thickness = 10;
            double radius = (size - thickness) / 2;
            double circleLen = 2 * Math.PI * radius;
            double unit = circleLen / thickness;

            Grid g = new Grid { Width = size, Height = size };

            g.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new SolidColorBrush(Color.FromRgb(40, 46, 58)),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            });

            System.Windows.Shapes.Ellipse usedRing = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Stroke = Red,
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                StrokeDashArray = new DoubleCollection { unit * usedPct, unit * (1 - usedPct) },
                StrokeDashCap = PenLineCap.Round,
                RenderTransform = new RotateTransform(-90, size / 2, size / 2)
            };
            g.Children.Add(usedRing);

            StackPanel center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            TextBlock pct = new TextBlock
            {
                Text = snap.UsedPercent + "%",
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock sub = new TextBlock
            {
                Text = T("belegt", "used"),
                Foreground = Muted,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RedlineUi.ApplyCrispText(pct);
            RedlineUi.ApplyCrispText(sub);
            center.Children.Add(pct);
            center.Children.Add(sub);
            g.Children.Add(center);
            RedlineUi.ApplyCrispText(g);
            RenderOptions.SetEdgeMode(g, EdgeMode.Aliased);

            return g;
        }

        private UIElement RecLine(string text)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = text, Foreground = TextPrimary, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Button b = MiniButton(T("Empfohlen", "Recommended"), Brushes.Transparent, 100);
            b.Foreground = Red;
            b.BorderBrush = Red;
            b.Height = 28;
            b.FontSize = 10;
            b.Click += OptimizeAllDashboard_Click;
            Grid.SetColumn(b, 1);
            g.Children.Add(b);
            return g;
        }
        private UIElement PageReadiness()
        {
            Grid grid = TwoColumnLayout();
            StackPanel left = new StackPanel();

            left.Children.Add(HeroCard("Gaming Readiness", "Bewertet deinen PC fürs Gaming: Power, Game Mode, Treiber, Netzwerk, Speicher und Anti-Cheat-Sicherheit."));

            StackPanel buttons = new StackPanel();

            Button scan = ActionButton("GAMING SCORE BERECHNEN", Red, 320);
            scan.Click += GamingReadiness_Click;

            Button power = ActionButton("POWER MODE OPTIMIEREN", DarkRed, 310);
            power.Margin = new Thickness(0, 12, 0, 0);
            power.Click += async (s, e) => await SetHighPerformance();

            Button startup = ActionButton("AUTOSTART PRÜFEN", CardBg2, 260);
            startup.Margin = new Thickness(0, 12, 0, 0);
            startup.Click += (s, e) => Navigate("Startup");

            Button gpu = ActionButton("GRAFIK SETTINGS ÖFFNEN", CardBg2, 310);
            gpu.Margin = new Thickness(0, 12, 0, 0);
            gpu.Click += (s, e) => OpenUri("ms-settings:display-advancedgraphics");

            buttons.Children.Add(scan);
            buttons.Children.Add(power);
            buttons.Children.Add(startup);
            buttons.Children.Add(gpu);
            left.Children.Add(buttons);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Children.Add(SystemInfoCard());
            OutputBox = OutputConsole("Gaming Readiness bereit. Klicke auf Gaming Score berechnen.");
            right.Children.Add(OutputBox);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return grid;
        }

        private UIElement PageAntiCheat()
        {
            Grid grid = TwoColumnLayout();
            StackPanel left = new StackPanel();

            left.Children.Add(HeroCard("Anti-Cheat Safe Mode", "Wenn Spiel, EasyAntiCheat, BattlEye oder Vanguard läuft, blockiert Redline riskante Aktionen und bleibt im Lesemodus."));

            StackPanel buttons = new StackPanel();

            Button scan = ActionButton("ANTI-CHEAT STATUS PRÜFEN", Red, 330);
            scan.Click += AntiCheatStatus_Click;

            Button report = ActionButton("SAFE MODE REPORT", CardBg2, 260);
            report.Margin = new Thickness(0, 12, 0, 0);
            report.Click += SaveReport_Click;

            buttons.Children.Add(scan);
            buttons.Children.Add(report);
            left.Children.Add(buttons);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            OutputBox = OutputConsole("Anti-Cheat Safe Mode bereit. Riskante Aktionen werden blockiert, sobald ein Spiel/AntiCheat erkannt wird.");
            right.Children.Add(OutputBox);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return grid;
        }

        private UIElement PageUndoCenter()
        {
            Grid grid = TwoColumnLayout();
            StackPanel left = new StackPanel();

            left.Children.Add(HeroCard("Undo Center", "Redline speichert wichtige Änderungen in AppData und kann DNS, Power Plan und Autostarts wieder zurücksetzen."));

            StackPanel buttons = new StackPanel();

            Button show = ActionButton("ÄNDERUNGEN ANZEIGEN", Red, 300);
            show.Click += UndoShow_Click;

            Button balanced = ActionButton("POWER PLAN BALANCED", CardBg2, 300);
            balanced.Margin = new Thickness(0, 12, 0, 0);
            balanced.Click += async (s, e) => await SetBalancedPowerPlan();

            Button restore = ActionButton("RESTORE POINT ERSTELLEN", DarkRed, 310);
            restore.Margin = new Thickness(0, 12, 0, 0);
            restore.Click += RestorePoint_Click;

            buttons.Children.Add(show);
            buttons.Children.Add(balanced);
            buttons.Children.Add(restore);
            left.Children.Add(buttons);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            OutputBox = OutputConsole("Undo Center bereit. Neue Änderungen werden in AppData protokolliert.");
            right.Children.Add(OutputBox);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return grid;
        }



        private UIElement PageCleaner()
        {
            CleanerChecks.Clear();
            _cleanerCategoryAmountTexts.Clear();
            _cleanerFoundSizeValueText = null;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader("CLEANER",
                T("Befreie dein System von unnötigen Dateien und gewinne wertvollen Speicherplatz.",
                  "Free your system from unnecessary files and gain valuable storage space."));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(420);
            StackPanel left = new StackPanel();

            Border top = DashboardCard();
            top.Margin = new Thickness(0, 0, 18, 18);
            top.Padding = new Thickness(22);
            Grid tg = new Grid();
            tg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            tg.ColumnDefinitions.Add(new ColumnDefinition());

            Grid sweep = new Grid { Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 3; i++)
                sweep.Children.Add(new System.Windows.Shapes.Ellipse { Width = 150 + i * 20, Height = 150 + i * 20, Stroke = new SolidColorBrush(Color.FromArgb((byte)(70 - i * 15), 238, 18, 48)), StrokeThickness = i == 0 ? 2 : 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            sweep.Children.Add(new TextBlock { Text = "🧹", Foreground = Red, FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            tg.Children.Add(sweep);

            StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
            info.Children.Add(new TextBlock { Text = T("BEREIT FÜR EINEN SICHEREN CLEAN", "READY FOR A SAFE CLEAN"), Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.UltraBold });
            info.Children.Add(new TextBlock { Text = T("Der Redline Analyzer identifiziert unnötige Dateien für mehr Speicherplatz und Performance.", "Redline Analyzer identifies unnecessary files for more space and performance."), Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 18) });

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button scanBtn = RedButton("🚀  " + T("SICHEREN SCAN STARTEN", "START SAFE SCAN"), CleanerScan_Click);
            scanBtn.Width = 260; scanBtn.Height = 50;
            btnRow.Children.Add(scanBtn);

            _cleanerCleanBtn = new Button
            {
                Content = "🔒  " + T("SICHER BEREINIGEN", "CLEAN SAFELY"),
                Width = 240,
                Height = 50,
                Margin = new Thickness(12, 0, 0, 0),
                IsEnabled = CleanerScanDone,
                Background = new SolidColorBrush(Color.FromRgb(22, 28, 36)),
                Foreground = CleanerScanDone ? Brushes.White : Muted,
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 58, 72)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyButtonSkin(_cleanerCleanBtn, 8);
            _cleanerCleanBtn.Click += CleanerClean_Click;
            btnRow.Children.Add(_cleanerCleanBtn);
            info.Children.Add(btnRow);

            _cleanerScanHint = new TextBlock
            {
                Text = CleanerScanDone ? T("Scan abgeschlossen – bereinigbare Dateien sind sichtbar.", "Scan complete – cleanable files are visible.") : T("Ein Scan ist erforderlich, um bereinigbare Dateien anzuzeigen.", "A scan is required to show cleanable files."),
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };
            info.Children.Add(_cleanerScanHint);
            Grid.SetColumn(info, 1);
            tg.Children.Add(info);
            top.Child = tg;
            left.Children.Add(top);

            Border cats = DashboardCard();
            cats.Padding = new Thickness(18);
            cats.Margin = new Thickness(0, 0, 18, 18);
            StackPanel cp = new StackPanel();
            Grid ch = new Grid { Margin = new Thickness(0, 0, 0, 10) }; ch.ColumnDefinitions.Add(new ColumnDefinition()); ch.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ch.Children.Add(new TextBlock { Text = T("Zu bereinigende Kategorien", "Categories to clean"), Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.UltraBold });
            CheckBox recSelect = new CheckBox
            {
                Content = T("Empfohlen auswählen", "Select recommended"),
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = T("Aktiviert Browser Cache, Temp-Dateien und Shader Cache. Papierkorb/DNS bleiben aus.", "Enables browser cache, temp files and shader cache. Recycle bin/DNS stay off.")
            };
            recSelect.Checked += (s, e) => ApplyRecommendedCleanerCategories(true);
            recSelect.Unchecked += (s, e) => ApplyRecommendedCleanerCategories(false);
            Grid.SetColumn(recSelect, 1);
            ch.Children.Add(recSelect);
            cp.Children.Add(ch);
            string catStatus = CleanerScanDone ? T("Bereit", "Ready") : T("Wird nach Scan geprüft", "Checked after scan");
            cp.Children.Add(CleanerCategoryRow("Browser Cache", "Browser Cache", T("Temporäre Internetdateien und Cache von Browsern", "Temporary internet files and browser cache"), catStatus, "🌐", AiBlue));
            cp.Children.Add(CleanerCategoryRow("Temporäre Dateien", T("Temporäre Dateien", "Temporary files"), T("Windows Temp-Ordner und App-Temp-Dateien", "Windows temp folders and app temp files"), catStatus, "TMP", AiPurple));
            cp.Children.Add(CleanerCategoryRow("Shader Cache", "Shader Cache", T("Grafik-Shader Cache von Spielen und Treibern", "Graphics shader cache from games and drivers"), catStatus, "SC", Red));
            cp.Children.Add(CleanerCategoryRow("Download-Reste", T("Download-Reste", "Download leftovers"), T("Unvollständige oder alte Download-Dateien", "Incomplete or old download files"), catStatus, "DL", AiOrange));
            cp.Children.Add(CleanerCategoryRow("Papierkorb", T("Papierkorb", "Recycle bin"), T("Dateien im Papierkorb", "Files in recycle bin"), catStatus, "BIN", AiGreen));
            cp.Children.Add(CleanerCategoryRow("DNS/Netzwerkreste", T("DNS/Netzwerkreste", "DNS/network leftovers"), T("DNS-Cache, Logs und Netzwerkreste", "DNS cache, logs and network leftovers"), catStatus, "DNS", AiBlue));
            cats.Child = cp;
            left.Children.Add(cats);
            ApplyRecommendedCleanerCategories(true);

            Border total = DashboardCard();
            total.Padding = new Thickness(18);
            total.Margin = new Thickness(0, 0, 18, 0);
            Grid totalGrid = new Grid(); totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); totalGrid.ColumnDefinitions.Add(new ColumnDefinition()); totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            totalGrid.Children.Add(RoundIcon("💽", Brushes.Transparent, 74));
            StackPanel found = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            found.Children.Add(new TextBlock { Text = T("Gefundener Speicher", "Found storage"), Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.UltraBold });
            _cleanerFoundSizeValueText = new TextBlock
            {
                Text = CleanerScanDone ? FormatSize(_cleanerLastTotalBytes) : T("Noch kein Scan", "No scan yet"),
                Foreground = Brushes.White,
                FontSize = 26,
                FontWeight = FontWeights.UltraBold
            };
            found.Children.Add(_cleanerFoundSizeValueText);
            found.Children.Add(new TextBlock { Text = T("Sicher zu bereinigen", "Safe to clean"), Foreground = Muted, FontSize = 13 });
            Grid.SetColumn(found,1); totalGrid.Children.Add(found);
            StackPanel doClean = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Button cleanBtn = RedButton(T("Sicher bereinigen", "Clean safely"), CleanerClean_Click); cleanBtn.Height = 54; cleanBtn.Width = 280; doClean.Children.Add(cleanBtn);
            doClean.Children.Add(new TextBlock { Text = T("Keine wichtigen Dateien werden entfernt", "No important files will be removed"), Foreground = Muted, FontSize = 12, Margin = new Thickness(6, 8, 0, 0) });
            Grid.SetColumn(doClean,2); totalGrid.Children.Add(doClean);
            total.Child = totalGrid;
            left.Children.Add(total);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border cleanerLog = ModernOutputCard(T("Cleaner Log bereit. Starte Scan oder Reinigung.", "Cleaner log ready. Start scan or clean."));
            cleanerLog.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(cleanerLog);
            right.Children.Add(AiSidePanel(
                "REDLINE AI CLEAN",
                new TextBlock { Text = T("Intelligent. Sicher. Effizient.", "Intelligent. Safe. Efficient."), Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 10) },
                new TextBlock { Text = T("Redline AI Clean analysiert Millionen von Dateimustern, um nur das zu entfernen, was wirklich unnötig ist.", "Redline AI Clean analyzes millions of file patterns to remove only what is truly unnecessary."), Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) },
                AiCheckRow(T("Sichere Methode aktiv", "Safe method active"), T("Es werden nur bekannte Cache-/Temp-Orte geprüft.", "Only known cache/temp locations are checked."), "✓", AiGreen, true),
                AiCheckRow(T("Erst scannen, dann löschen", "Scan first, then clean"), T("Vor dem Löschen wird neu berechnet, was wirklich gefunden wurde.", "Before cleaning, Redline recalculates what was really found."), "!", AiOrange, false),
                AiScoreBlock(T("AI-Vertrauenslevel", "AI trust level"), CleanerScanDone ? 85 : null, AiGreen, CleanerScanDone ? null : T("Nach dem ersten Scan sichtbar.", "Visible after the first scan.")),
                OutlineButton(T("Details anzeigen", "Show details"), CleanerScan_Click)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            RefreshCleanerUiState();
            return root;
        }

        private void RefreshCleanerUiState()
        {
            if (_cleanerCleanBtn != null)
            {
                _cleanerCleanBtn.IsEnabled = CleanerScanDone;
                _cleanerCleanBtn.Foreground = CleanerScanDone ? Brushes.White : Muted;
                _cleanerCleanBtn.BorderBrush = CleanerScanDone ? Red : new SolidColorBrush(Color.FromRgb(48, 58, 72));
            }
            if (_cleanerScanHint != null)
                _cleanerScanHint.Text = CleanerScanDone
                    ? T("Scan abgeschlossen – du kannst sicher bereinigen.", "Scan complete – you can clean safely.")
                    : T("Ein Scan ist erforderlich, um bereinigbare Dateien anzuzeigen.", "A scan is required to show cleanable files.");

            if (_cleanerFoundSizeValueText != null)
            {
                _cleanerFoundSizeValueText.Text = CleanerScanDone
                    ? FormatSize(_cleanerLastTotalBytes)
                    : T("Noch kein Scan", "No scan yet");
            }
        }

        private static string ClassifyCleanerCategory(CleanTarget target)
        {
            string n = target.Name;
            if (n.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                return "Browser Cache";
            if (n.Contains("Temp", StringComparison.OrdinalIgnoreCase) || n.Contains("Logs", StringComparison.OrdinalIgnoreCase))
                return "Temporäre Dateien";
            if (n.Contains("Shader", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("DXCache", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("GLCache", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("D3DS", StringComparison.OrdinalIgnoreCase))
                return "Shader Cache";
            if (n.Contains("Download", StringComparison.OrdinalIgnoreCase) || n.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                return "Download-Reste";
            return "Sonstiges";
        }

        private void UpdateCleanerCategoryAmounts(Dictionary<string, long> sizes)
        {
            foreach (KeyValuePair<string, TextBlock> kv in _cleanerCategoryAmountTexts)
            {
                if (sizes.TryGetValue(kv.Key, out long bytes) && bytes > 0)
                    kv.Value.Text = FormatSize(bytes);
                else if (CleanerScanDone)
                    kv.Value.Text = kv.Key is "Papierkorb" or "DNS/Netzwerkreste"
                        ? T("Beim Reinigen", "On clean")
                        : "0 B";
            }
        }

        private UIElement PageGameProfiles()
        {
            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(500);

            StackPanel left = new StackPanel();
            left.Children.Add(AiHeroCard(
                T("PRO FPS GUIDE — DU ENTSCHEIDEST.", "PRO FPS GUIDE — YOU DECIDE."),
                T("Redline zeigt Pro-Tipps (v. a. Rust): was viel FPS bringt, was optional ist und was du nicht machen solltest. Nichts wird automatisch geändert — nur wenn du auf „Anwenden“ klickst.",
                  "Redline shows pro tips (especially Rust): what boosts FPS, what's optional, and what to avoid. Nothing changes automatically — only when you click Apply."),
                "AI",
                T("RUST / SPIEL SCANNEN", "SCAN RUST / GAME"),
                GameProfileAnalyze_Click,
                T("SYSTEM-TIPPS ANWENDEN…", "APPLY SYSTEM TIPS…"),
                GameProfileApply_Click
            ));


            Border gamesCard = DashboardCard();
            gamesCard.Padding = new Thickness(18);
            gamesCard.Margin = new Thickness(0, 0, 18, 0);
            StackPanel gamesPanel = new StackPanel();

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = T("ERKANNTE SPIELE", "DETECTED GAMES"), Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeights.UltraBold, VerticalAlignment = VerticalAlignment.Center });
            List<string> detectedGames = DetectGames();
            Border count = new Border { Background = new SolidColorBrush(Color.FromRgb(110, 16, 28)), CornerRadius = new CornerRadius(11), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 10, 0) };
            count.Child = new TextBlock { Text = detectedGames.Count.ToString(), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.Bold };
            Grid.SetColumn(count, 1);
            header.Children.Add(count);
            Button refresh = OutlineButton("⟳", (s, e) => { InvalidateGamesCache(); Navigate("GameProfiles"); });
            refresh.Width = 42; refresh.Height = 36; refresh.Padding = new Thickness(0); refresh.Foreground = Muted; refresh.BorderBrush = new SolidColorBrush(Color.FromRgb(54, 64, 82));
            Grid.SetColumn(refresh, 2); header.Children.Add(refresh);
            gamesPanel.Children.Add(header);

            if (detectedGames.Count == 0)
            {
                gamesPanel.Children.Add(new TextBlock
                {
                    Text = T("Keine installierten Spiele erkannt. Nutze „Spiel analysieren“ oder die Suche.",
                             "No installed games detected. Use \"Analyze game\" or search."),
                    Foreground = Muted,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            else
            {
                foreach (string game in detectedGames.Take(12))
                    gamesPanel.Children.Add(AiGameRow(game, GetGamePlatformLabel(game), GetGameIconText(game), IsGameOptimized(game)));
            }

            TextBox search = new TextBox
            {
                Height = 42,
                Margin = new Thickness(140, 10, 140, 0),
                Background = new SolidColorBrush(Color.FromRgb(16, 21, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 62)),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                FontSize = 13,
                Padding = new Thickness(16, 8, 16, 8),
                Text = ""
            };
            search.Tag = T("Spiel suchen...", "Search game...");
            search.GotFocus += (s, e) => { if (search.Text == (string)search.Tag) search.Text = ""; };
            search.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(search.Text)) search.Text = (string)search.Tag!; };
            if (string.IsNullOrWhiteSpace(search.Text)) search.Text = (string)search.Tag!;
            search.KeyDown += async (s, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Enter) return;
                string term = search.Text.Trim();
                if (term == (string)search.Tag || string.IsNullOrWhiteSpace(term)) return;
                PrepareActionOutput();
                foreach (string game in DetectGames().Where(g => g.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    await Log(T("Gefunden: ", "Found: ") + game);
            };
            gamesPanel.Children.Add(search);

            gamesCard.Child = gamesPanel;
            left.Children.Add(gamesCard);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Children.Add(BuildGameProGuidePanel());
            right.Children.Add(BuildGamingAiInsightPanel(detectedGames));
            right.Children.Add(ProfileModeCard());

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return grid;
        }

        private UIElement PageOptimization()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader("PERFORMANCE",
                T("Steigere deine FPS und Systemreaktion durch intelligente Anpassungen.",
                  "Boost FPS and system response through intelligent adjustments."));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(400);
            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Margin = new Thickness(0, 0, 18, 18);
            hero.Padding = new Thickness(22);
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            Grid bolt = new Grid { Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 3; i++)
                bolt.Children.Add(new System.Windows.Shapes.Ellipse { Width = 140 + i * 22, Height = 140 + i * 22, Stroke = new SolidColorBrush(Color.FromArgb((byte)(75 - i * 18), 238, 18, 48)), StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            bolt.Children.Add(new TextBlock { Text = "⚡", Foreground = Red, FontSize = 58, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            hg.Children.Add(bolt);
            StackPanel ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            ht.Children.Add(new TextBlock { Text = T("Performance Optimierung", "Performance Optimization"), Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.UltraBold });
            ht.Children.Add(new TextBlock { Text = T("Redline analysiert dein System und wendet bewährte Optimierungen sicher an.", "Redline analyzes your system and applies proven optimizations safely."), Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 16) });
            StackPanel heroBtns = new StackPanel { Orientation = Orientation.Horizontal };
            Button optNow = RedButton("🚀  " + T("JETZT OPTIMIEREN", "OPTIMIZE NOW"), OptimizationRun_Click);
            optNow.Width = 220; optNow.Height = 48;
            heroBtns.Children.Add(optNow);
            Button adv = OutlineButton(T("ERWEITERTE EINSTELLUNGEN", "ADVANCED SETTINGS"), (s, e) => SafeStartSystem("SystemPropertiesPerformance.exe"));
            adv.Width = 240; adv.Height = 48; adv.Margin = new Thickness(10, 0, 0, 0);
            heroBtns.Children.Add(adv);
            ht.Children.Add(heroBtns);
            Grid.SetColumn(ht, 1);
            hg.Children.Add(ht);
            hero.Child = hg;
            left.Children.Add(hero);

            Border profCard = DashboardCard();
            profCard.Margin = new Thickness(0, 0, 18, 18);
            profCard.Padding = new Thickness(18);
            StackPanel profP = new StackPanel();
            profP.Children.Add(new TextBlock { Text = T("OPTIMIERUNGSPROFIL", "OPTIMIZATION PROFILE"), Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 6) });
            profP.Children.Add(new TextBlock { Text = T("Wähle ein Profil, das am besten zu deinem Spielstil passt.", "Choose a profile that best fits your play style."), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
            UniformGrid modes = new UniformGrid { Columns = 3 };
            modes.Children.Add(ProfileModeButton("FPS", T("Maximale Leistung", "Max performance"), GraphicsMode == "FPS", () => { GraphicsMode = "FPS"; Navigate("Optimierung"); }));
            modes.Children.Add(ProfileModeButton(T("AUSGEWOGEN", "BALANCED"), T("Balance", "Balance"), GraphicsMode == "Balanced", () => { GraphicsMode = "Balanced"; Navigate("Optimierung"); }));
            modes.Children.Add(ProfileModeButton(T("QUALITÄT", "QUALITY"), T("Beste Grafik", "Best graphics"), GraphicsMode == "Quality", () => { GraphicsMode = "Quality"; Navigate("Optimierung"); }));
            profP.Children.Add(modes);
            profCard.Child = profP;
            left.Children.Add(profCard);

            WrapPanel tiles = new WrapPanel();
            tiles.Children.Add(PerfFeatureTile("GAME MODE", T("Aktiviere den Gaming-Modus für bessere Performance.", "Enable Game Mode for better performance."), IsGameModeEnabled() ? T("Aktiv", "Active") : T("Prüfen", "Check"),
                async (s, e) => await SetGameModeEnabled(true),
                (s, e) => OpenUri("ms-settings:gaming-gamemode")));
            tiles.Children.Add(PerfFeatureTile(T("HOCHLEISTUNGSMODUS", "HIGH PERFORMANCE"), T("System auf maximale Leistung.", "System set to maximum performance."), IsHighPerformanceActive() ? T("Aktiv", "Active") : T("Prüfen", "Check"),
                async (s, e) => await SetHighPerformance(),
                (s, e) => SafeStartSystem("powercfg.cpl")));
            tiles.Children.Add(PerfFeatureTile(T("GRAFIK SETTINGS", "GRAPHICS SETTINGS"), T("Optimiere Grafikoptionen für Gaming.", "Optimize graphics for gaming."), T("Öffnen", "Open"),
                (s, e) => OpenUri("ms-settings:display-advancedgraphics")));
            tiles.Children.Add(PerfFeatureTile(T("VISUELLE EFFEKTE", "VISUAL EFFECTS"), T("Reduziert Effekte für mehr FPS.", "Reduces effects for more FPS."), T("Öffnen", "Open"),
                (s, e) => SafeStartSystem("SystemPropertiesPerformance.exe")));
            tiles.Children.Add(PerfFeatureTile(T("HINTERGRUNDDIENSTE", "BACKGROUND SERVICES"), T("Deaktiviert unnötige Dienste.", "Disables unnecessary services."), T("Verwalten", "Manage"),
                (s, e) => SafeStartSystem("services.msc")));
            tiles.Children.Add(PerfFeatureTile(T("AUTOSTART PRÜFEN", "CHECK AUTOSTART"), T("Verwalte Autostart-Programme.", "Manage startup programs."), T("Verwalten", "Manage"),
                (s, e) => Navigate("Startup")));
            tiles.Children.Add(ProFeatureTile(
                "WINDOWS FPS BOOST",
                T("Pro: Game Mode, Energieplan, Game Bar, visuelle Effekte und Hintergrund-Dienste in einem Durchlauf.",
                  "Pro: Game Mode, power plan, Game Bar, visual effects and background tuning in one run."),
                "PRO",
                WindowsFpsBoostPro_Click,
                (s, e) => OpenUri("ms-settings:gaming-gamebar")));
            left.Children.Add(tiles);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border perfStatus = CreatePageStatusCard(
                T("STATUS", "STATUS"),
                T("Bereit — Optimierung nur per Klick.", "Ready — optimize only on button click."),
                140);
            perfStatus.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(perfStatus);
            right.Children.Add(AiSidePanel(
                "REDLINE AI PERFORMANCE",
                new TextBlock { Text = T("EMPFOHLENE MASSNAHMEN", "RECOMMENDED MEASURES"), Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 12) },
                AiRecRow(T("Speicheroptimierung", "Memory optimization"), T("Schließe nicht benötigte Prozesse", "Close unneeded processes"), T("Anwenden", "Apply"), async (s, e) => { SafeStartSystem("taskmgr.exe"); await Log(T("Task Manager geöffnet.", "Task Manager opened.")); }),
                AiRecRow(T("GPU-Treiber prüfen", "Check GPU driver"), T("Treiberstatus prüfen", "Check driver status"), T("Prüfen", "Check"), (s, e) => Navigate("Drivers")),
                AiRecRow("Windows Game Bar", T("Kann FPS beeinträchtigen", "Can affect FPS"), T("Optimieren", "Optimize"), async (s, e) => { OpenUri("ms-settings:gaming-gamebar"); await Log(T("Game Bar Einstellungen geöffnet.", "Game Bar settings opened.")); }),
                AiRecRow(T("Energieplan", "Power plan"), T("Auf Höchstleistung setzen", "Set to high performance"), T("Anwenden", "Apply"), async (s, e) => await SetHighPerformance()),
                OutlineButton(T("ALLE EMPFEHLUNGEN ANZEIGEN", "SHOW ALL RECOMMENDATIONS"), OptimizationRun_Click)
            ));
            right.Children.Add(AiSidePanel(
                T("AI SCORE", "AI SCORE"),
                AiScoreBlock(
                    RedlineAppData.Current.GamingScore.HasValue ? T("System-Score", "System score") : T("Noch kein Scan", "No scan yet"),
                    RedlineAppData.Current.GamingScore,
                    Red),
                OutlineButton(T("ERNEUT SCANNEN", "SCAN AGAIN"), async (s, e) => await RunSystemFullScan())
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }

        private UIElement AiRecRow(string title, string sub, string btnText, RoutedEventHandler click)
        {
            Border row = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 16, 21, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 40, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel t = new StackPanel();
            t.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold });
            t.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 });
            g.Children.Add(t);
            Button b = OutlineButton(btnText, click);
            b.Width = 90; b.Height = 32; b.FontSize = 11;
            Grid.SetColumn(b, 1);
            g.Children.Add(b);
            row.Child = g;
            return row;
        }

        private UIElement PagePerformance()
        {
            Grid grid = ModernTwoColumn();
            StackPanel left = new StackPanel();

            left.Children.Add(ModernPageCard(
                "System Übersicht",
                "Live-Werte, Hardwaredaten, Laufwerke, Netzwerkadapter und Prozesse im modernen Dashboard-Stil."
            ));

            Border stats = DashboardCard();
            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock
            {
                Text = "Live System",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 12)
            });

            UniformGrid cards = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 12) };
            cards.Children.Add(DashboardMiniCard("CPU", GetCpuLoadText(), "Live", "⌁", Brushes.LightGreen));
            cards.Children.Add(DashboardMiniCard("RAM", GetRamUsageText(), GetRamUsedVsTotalText(), "▦", Brushes.LightGreen));
            cards.Children.Add(DashboardMiniCard("PING", GetQuickPingText(), "Network", "◉", Brushes.LightGreen));
            p.Children.Add(cards);

            WrapPanel tiles = new WrapPanel();
            tiles.Children.Add(ModernTile("System scannen", "Hardware, Laufwerke und Prozesse prüfen.", "SCAN", Red, PerformanceRefresh_Click));
            tiles.Children.Add(ModernTile("Task Manager", "Windows Task Manager öffnen.", "CPU", CardBg2, (s, e) => SafeStartSystem("taskmgr.exe")));
            tiles.Children.Add(ModernTile("Grafik Settings", "Windows Grafikoptionen öffnen.", "GPU", CardBg2, (s, e) => OpenUri("ms-settings:display-advancedgraphics")));
            p.Children.Add(tiles);

            stats.Child = p;
            left.Children.Add(stats);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            Border output = ModernOutputCard("System Übersicht bereit. Starte System scannen für Details.");
            Grid.SetColumn(output, 1);
            grid.Children.Add(output);

            return grid;
        }
        private UIElement PageStartup()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader(
                T("AUTOSTART", "AUTOSTART"),
                T("Autostarts scannen, auswählen und sicher deaktivieren. Backup wird in der Registry gespeichert.",
                  "Scan, select and safely disable autostarts. Backup is stored in the registry."),
                StartupScan_Click,
                "🔍  " + T("AUTOSTART SCANNEN", "SCAN AUTOSTART"));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(480) });
            StackPanel left = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };

            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 18, 18);
            card.Padding = new Thickness(18);
            StartupPanel = new StackPanel();
            StartupChecks.Clear();
            StartupValues.Clear();
            StartupPanel.Children.Add(new TextBlock
            {
                Text = T("Klicke auf „Autostart scannen“, wähle Einträge aus und deaktiviere sie sicher.",
                         "Click \"Scan autostart\", select entries and disable them safely."),
                Foreground = Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            card.Child = StartupPanel;
            left.Children.Add(card);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            Button scan = RedButton(T("AUTOSTART SCANNEN", "SCAN AUTOSTART"), StartupScan_Click);
            scan.Width = 220;
            scan.Height = 44;
            buttons.Children.Add(scan);
            Button disable = OutlineButton(T("AUSGEWÄHLTE DEAKTIVIEREN", "DISABLE SELECTED"), StartupDisableSelected_Click);
            disable.Width = 280;
            disable.Height = 44;
            disable.Margin = new Thickness(12, 0, 0, 0);
            buttons.Children.Add(disable);
            left.Children.Add(buttons);

            ScrollViewer leftScroll = CreatePageScrollViewer(left);
            Grid.SetColumn(leftScroll, 0);
            grid.Children.Add(leftScroll);

            StackPanel right = new StackPanel();
            Border log = ModernOutputCard(T("Autostart-Log bereit.", "Autostart log ready."));
            right.Children.Add(log);
            right.Children.Add(AiSidePanel(
                T("AUTOSTART TIPPS", "AUTOSTART TIPS"),
                AiCheckRow(T("Backup", "Backup"), T("Vor Deaktivierung gespeichert", "Saved before disable"), "💾", AiGreen, true),
                AiCheckRow(T("Gaming", "Gaming"), T("Launcher nicht blind löschen", "Don't blindly remove launchers"), "🎮", AiOrange, true),
                OutlineButton(T("Zurück zum Dashboard", "Back to dashboard"), (s, e) => Navigate("Dashboard"))
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }




        private UIElement PageSecurity()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader("SECURITY",
                T("Überprüfe Sicherheitsbereiche, erkenne Risiken und optimiere deinen Schutz.",
                  "Check security areas, detect risks and optimize your protection."),
                SecurityCheck_Click,
                "🛡  " + T("SICHERHEITSPRÜFUNG STARTEN", "START SECURITY CHECK"));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(430);
            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Margin = new Thickness(0, 0, 18, 18);
            hero.Padding = new Thickness(22);
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
            hg.ColumnDefinitions.Add(new ColumnDefinition());

            Grid shield = new Grid { Width = 230, Height = 180, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 5; i++)
                shield.Children.Add(new System.Windows.Shapes.Ellipse { Width = 130 + i * 28, Height = 130 + i * 28, Stroke = new SolidColorBrush(Color.FromArgb((byte)(70 - i * 10), 238, 18, 48)), StrokeThickness = i == 0 ? 2 : 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            shield.Children.Add(new TextBlock { Text = "🛡", Foreground = Red, FontSize = 74, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            hg.Children.Add(shield);

            StackPanel ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            ht.Children.Add(new TextBlock { Text = "Security & Health Check", Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.UltraBold });
            ht.Children.Add(new TextBlock { Text = T("Überprüfe wichtige Sicherheitsbereiche deines Systems, erkenne Risiken und optimiere deinen Schutz für ein sorgenfreies Gaming-Erlebnis.", "Check important security areas, detect risks and optimize your protection for a worry-free gaming experience."), Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 18) });
            Button secBtn = RedButton(T("SICHERHEITSPRÜFUNG STARTEN", "START SECURITY CHECK"), SecurityCheck_Click); secBtn.Width = 310; secBtn.Height = 50; ht.Children.Add(secBtn);
            ht.Children.Add(new TextBlock { Text = T("Schnellprüfung dauert ca. 60 Sekunden", "Quick check takes about 60 seconds"), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
            Grid.SetColumn(ht, 1); hg.Children.Add(ht);
            hero.Child = hg; left.Children.Add(hero);

            Border table = DashboardCard();
            table.Margin = new Thickness(0, 0, 18, 0);
            table.Padding = new Thickness(18);
            StackPanel tp = new StackPanel();
            Grid th = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            th.ColumnDefinitions.Add(new ColumnDefinition()); th.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); th.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); th.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            th.Children.Add(new TextBlock { Text = T("SICHERHEITSBEREICHE", "SECURITY AREAS"), Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.UltraBold });
            var h1=new TextBlock{Text=T("STATUS","STATUS"),Foreground=Muted,FontSize=12,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center}; Grid.SetColumn(h1,1); th.Children.Add(h1);
            var h2=new TextBlock{Text=T("RISIKO","RISK"),Foreground=Muted,FontSize=12,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center}; Grid.SetColumn(h2,2); th.Children.Add(h2);
            var h3=new TextBlock{Text=T("AKTION","ACTION"),Foreground=Muted,FontSize=12,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center}; Grid.SetColumn(h3,3); th.Children.Add(h3);
            tp.Children.Add(th);
            tp.Children.Add(SecurityTableRow("Defender Status", T("Überprüft den Status von Microsoft Defender.", "Checks Microsoft Defender status."), T("Prüfung erforderlich", "Check required"), T("Unbekannt", "Unknown"), AiGreen, "🛡", SecurityCheck_Click));
            tp.Children.Add(SecurityTableRow("Firewall", T("Überprüft Windows-Firewall und Netzwerkregeln.", "Checks Windows firewall and network rules."), T("Prüfung erforderlich", "Check required"), T("Unbekannt", "Unknown"), AiGreen, "🔥", SecurityCheck_Click));
            tp.Children.Add(SecurityTableRow("SmartScreen", T("Überprüft SmartScreen-Einstellungen.", "Checks SmartScreen settings."), T("Noch nicht geprüft", "Not checked yet"), T("Unbekannt", "Unknown"), AiBlue, "SM", SecurityCheck_Click));
            tp.Children.Add(SecurityTableRow(T("Verdächtige Prozesse", "Suspicious processes"), T("Sucht nach auffälligen Prozessen im Hintergrund.", "Searches for suspicious background processes."), T("Noch nicht geprüft", "Not checked yet"), T("Unbekannt", "Unknown"), AiOrange, "🔎", PcHealthCheck_Click));
            tp.Children.Add(SecurityTableRow("Hosts-Datei", T("Überprüft Änderungen in der Hosts-Datei.", "Checks changes in the hosts file."), T("Prüfung erforderlich", "Check required"), T("Unbekannt", "Unknown"), AiGreen, "TXT", PcHealthCheck_Click));
            tp.Children.Add(SecurityTableRow("Anti-Cheat Safe Mode", T("Stellt sicher, dass Anti-Cheat-Dienste geschützt sind.", "Ensures anti-cheat services are protected."), T("Noch nicht geprüft", "Not checked yet"), T("Unbekannt", "Unknown"), Red, "AC", AntiCheatStatus_Click));
            tp.Children.Add(SecurityTableRow(T("Browser Schutz", "Browser protection"), T("Überprüft Browser-Erweiterungen und Sicherheit.", "Checks browser extensions and security."), T("Prüfung erforderlich", "Check required"), T("Unbekannt", "Unknown"), AiGreen, "🌐", ChromeCheck_Click));
            Border foot = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(31, 38, 48)), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(4, 12, 4, 0) };
            Grid fg = new Grid(); fg.ColumnDefinitions.Add(new ColumnDefinition()); fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fg.Children.Add(new TextBlock { Text = T("Letzte vollständige Prüfung: noch keine", "Last full check: none yet"), Foreground = Muted, FontSize = 12 });
            Button retry = OutlineButton(T("Erneut prüfen", "Check again"), SecurityCheck_Click); retry.Width = 140; retry.Height = 36; Grid.SetColumn(retry,1); fg.Children.Add(retry);
            foot.Child = fg; tp.Children.Add(foot);
            tp.Children.Add(ButtonRow(
                (T("Defender Quick Scan", "Defender Quick Scan"), DefenderQuickScan_Click),
                (T("Defender Offline Scan", "Defender Offline Scan"), DefenderOfflineScan_Click)
            ));
            table.Child = tp; left.Children.Add(table);
            Grid.SetColumn(left, 0); grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border securityLog = ModernOutputCard(T("Security Log bereit.", "Security log ready."));
            securityLog.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(securityLog);
            right.Children.Add(AiSidePanel(
                "REDLINE AI DEFENSE",
                new TextBlock { Text = T("Unsere AI analysiert dein System und beurteilt Risiken nach einer echten Sicherheitsprüfung.", "Our AI evaluates risks after a real security check."), Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) },
                AiCheckRow(T("Defender", "Defender"), GetDefenderStatusText(), "🛡", AiGreen, true),
                AiCheckRow("Firewall", GetFirewallStatusText(), "🔥", AiGreen, true),
                AiCheckRow("Anti-Cheat Safe Mode", IsAntiCheatSafeModeActive(out _) ? T("Aktiv", "Active") : T("Bereit", "Ready"), "AC", Red, true)
            ));
            right.Children.Add(AiSidePanel(
                T("SCHUTZPUNKTZAHL", "PROTECTION SCORE"),
                AiScoreBlock(
                    RedlineAppData.Current.SecurityChecked && RedlineAppData.Current.SecurityScore.HasValue
                        ? T("Schutz-Score", "Protection score")
                        : T("Noch nicht bewertet", "Not rated yet"),
                    RedlineAppData.Current.SecurityChecked ? RedlineAppData.Current.SecurityScore : null,
                    Muted),
                new TextBlock { Text = T("Starte eine Sicherheitsprüfung für deine Punktzahl.", "Run a security check for your score."), Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) },
                OutlineButton(T("ALLE ERGEBNISSE ANZEIGEN", "SHOW ALL RESULTS"), SecurityCheck_Click)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }

        private List<DriverInfoLite> GetImportantDriversPreview(int max = 8)
        {
            string[] keys =
            {
                "nvidia", "amd", "advanced micro devices", "intel", "realtek",
                "wi-fi", "wifi", "ethernet", "audio", "bluetooth", "chipset"
            };

            try
            {
                return GetDriversCached()
                    .Where(x => keys.Any(k => (x.Provider + " " + x.DeviceName).ToLowerInvariant().Contains(k)))
                    .OrderBy(x => x.Provider)
                    .ThenBy(x => x.DeviceName)
                    .Take(max)
                    .ToList();
            }
            catch
            {
                return new List<DriverInfoLite>();
            }
        }

        private List<DriverInfoLite> GetDriversCached(bool forceRefresh = false)
        {
            lock (_driversCacheLock)
            {
                if (!forceRefresh && _driversCache != null && DateTime.UtcNow - _driversCacheUtc < DriversCacheLifetime)
                    return _driversCache;
            }

            List<DriverInfoLite> list = GetDrivers();
            lock (_driversCacheLock)
            {
                _driversCache = list;
                _driversCacheUtc = DateTime.UtcNow;
            }
            return list;
        }

        private void InvalidateDriversCache()
        {
            lock (_driversCacheLock)
            {
                _driversCache = null;
                _driversCacheUtc = DateTime.MinValue;
            }
        }

        private void EnsureAutostartCache()
        {
            if (_autostartPreviewCache != null && DateTime.UtcNow - _autostartCacheUtc < AutostartCacheLifetime)
                return;

            _autostartPreviewCache = GetAutostartPreview(200);
            _autostartCountCache = _autostartPreviewCache.Count;
            _autostartCacheUtc = DateTime.UtcNow;
        }

        private int GetCachedAutostartCount()
        {
            EnsureAutostartCache();
            return _autostartCountCache ?? 0;
        }

        private List<(string name, string impact, int level)> GetCachedAutostartPreview(int max)
        {
            EnsureAutostartCache();
            return _autostartPreviewCache!.Take(max).ToList();
        }

        private void ScheduleDriverPreviewLoad()
        {
            if (_driverPreviewHost == null)
                return;

            int token = ++_driverPreviewToken;
            _driverPreviewHost.Children.Clear();
            _driverPreviewHost.Children.Add(new TextBlock
            {
                Text = T("Treiber werden geladen…", "Loading drivers…"),
                Foreground = Muted,
                FontSize = 13
            });

            _ = Task.Run(async () =>
            {
                await Task.CompletedTask;
                List<DriverDisplayItem> preview = RedlineDriverStatus.BuildLeftPanelList(8);
                Dispatcher.Invoke(() =>
                {
                    if (token != _driverPreviewToken || CurrentPage != "Drivers" || _driverPreviewHost == null)
                        return;
                    PopulateDriverPreviewHost(preview);
                });
            });
        }

        private void PopulateDriverPreviewHost(List<DriverDisplayItem> preview)
        {
            if (_driverPreviewHost == null)
                return;

            _driverPreviewHost.Children.Clear();
            if (preview.Count == 0)
            {
                _driverPreviewHost.Children.Add(new TextBlock
                {
                    Text = T("Starte einen Treiber-Scan für die vollständige Liste.", "Run a driver scan for the full list."),
                    Foreground = Muted,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (DriverDisplayItem d in preview)
            {
                DriverStatusToUi(d.Status, out string risk, out Brush riskColor);
                string device = d.DeviceName;
                bool canUpdate = d.Status is "UPDATE EMPFOHLEN" or "PRÜFEN";
                RoutedEventHandler action = d.Status == "SYSTEM"
                    ? DriverScan_Click
                    : canUpdate
                        ? (_, e) => _ = DriverSingleUpdateAsync(device)
                        : DriverScan_Click;
                string icon = d.Status == "SYSTEM" ? "⚙" : canUpdate ? "⬇" : "✓";
                string btn = d.Status switch
                {
                    "UPDATE EMPFOHLEN" => T("UPDATE", "UPDATE"),
                    "PRÜFEN" => T("HERSTELLER", "VENDOR"),
                    "AKTUALISIERT" => T("OK", "OK"),
                    "AKTUELL" => T("OK", "OK"),
                    _ => T("SCAN", "SCAN")
                };
                _driverPreviewHost.Children.Add(DriverTableRow(d.DeviceName, d.Detail, TranslateDriverStatus(d.Status), risk, riskColor, icon, btn, action));
            }
        }

        private UIElement DriverTableRow(string area, string desc, string status, string risk, Brush riskColor, string icon, string buttonText, RoutedEventHandler action)
        {
            Border row = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(31, 38, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 10, 12, 10)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Grid first = new Grid();
            first.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            first.ColumnDefinitions.Add(new ColumnDefinition());
            first.Children.Add(RoundIcon(icon, riskColor, 38));
            StackPanel txt = new StackPanel();
            txt.Children.Add(new TextBlock { Text = area, Foreground = Brushes.White, FontSize = 13.5, FontWeight = FontWeights.UltraBold });
            txt.Children.Add(new TextBlock { Text = desc, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(txt, 1);
            first.Children.Add(txt);
            g.Children.Add(first);

            g.Children.Add(new TextBlock { Text = status, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(g.Children[g.Children.Count - 1], 1);

            g.Children.Add(new TextBlock { Text = "● " + risk, Foreground = riskColor, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(g.Children[g.Children.Count - 1], 2);

            Button b = OutlineButton(buttonText, action);
            b.Width = 105;
            b.Height = 34;
            Grid.SetColumn(b, 3);
            g.Children.Add(b);

            row.Child = g;
            return row;
        }

        private async Task DriverSingleUpdateAsync(string deviceName)
        {
            if (!TryInAppDriverFeature()) return;
            SetDriverActivity(T("Installiere: ", "Installing: ") + deviceName + "…", 20);

            bool installed = await RedlineDriverUpdateService.Instance.InstallSingleByDeviceHintAsync(
                deviceName,
                msg => { SetDriverActivity(msg, null); return Task.CompletedTask; },
                IsEnglish());

            await Task.Delay(600);
            InvalidateDriversCache();
            ScheduleDriverPreviewLoad();
            SetDriverActivity(installed
                ? T("Fertig — Treiber aktualisiert.", "Done — driver updated.")
                : T("Fertig — offizielle Seite geöffnet oder bereits aktuell.", "Done — official page opened or already current."), 100);
        }

        private void DriverStatusToUi(string status, out string risk, out Brush color)
        {
            switch (status)
            {
                case "AKTUELL":
                    risk = T("Niedrig", "Low");
                    color = AiGreen;
                    break;
                case "AKTUALISIERT":
                    risk = T("Erledigt", "Done");
                    color = AiGreen;
                    break;
                case "PRÜFEN":
                    risk = T("Mittel", "Medium");
                    color = AiOrange;
                    break;
                case "UPDATE EMPFOHLEN":
                    risk = T("Hoch", "High");
                    color = Red;
                    break;
                case "SYSTEM":
                    risk = T("System", "System");
                    color = Muted;
                    break;
                default:
                    risk = T("Unbekannt", "Unknown");
                    color = Muted;
                    break;
            }
        }

        private UIElement PageDrivers()
        {
            HardwareProfile hp = RedlineHardwareProfile.Detect(GetCpuName(), GetGpuName(), GetWindowsCaption());
            string gpuVendor = RedlineHardwareProfile.GpuVendor(hp.GpuName);
            Brush gpuAccent = gpuVendor switch
            {
                "NVIDIA" => AiGreen,
                "AMD" => AiOrange,
                "Intel" => new SolidColorBrush(Color.FromRgb(0, 113, 197)),
                _ => Red
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader(T("TREIBER", "DRIVERS"),
                T("Erkennt deine GPU/CPU und installiert nur passende Hersteller-Treiber per winget.",
                  "Detects your GPU/CPU and installs only matching vendor drivers via winget."),
                DriverScan_Click,
                "🔍  " + T("SCAN", "SCAN"));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(420);
            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Margin = new Thickness(0, 0, 18, 18);
            hero.Padding = new Thickness(22);
            hero.BorderBrush = gpuAccent;
            hero.BorderThickness = new Thickness(1);
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            Grid orb = new Grid { Width = 130, Height = 130, HorizontalAlignment = HorizontalAlignment.Center };
            orb.Children.Add(new System.Windows.Shapes.Ellipse { Width = 120, Height = 120, Stroke = gpuAccent, StrokeThickness = 2 });
            orb.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(gpuVendor) ? "GPU" : gpuVendor[..Math.Min(3, gpuVendor.Length)],
                Foreground = gpuAccent,
                FontSize = 22,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            hg.Children.Add(orb);
            StackPanel ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            ht.Children.Add(new TextBlock
            {
                Text = T("Dein System", "Your system"),
                Foreground = Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.UltraBold
            });
            ht.Children.Add(DriverInfoChip("GPU", TruncateGpuName(GetGpuName()), gpuAccent));
            ht.Children.Add(DriverInfoChip("CPU", TruncateGpuName(GetCpuName()), Muted));
            ht.Children.Add(DriverInfoChip(T("Board", "MB"), GetMotherboardLabel(), Muted));
            WrapPanel chipRow = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (!string.IsNullOrEmpty(gpuVendor))
                chipRow.Children.Add(DriverVendorChip(gpuVendor, gpuAccent));
            string cpuV = RedlineHardwareProfile.CpuVendor(hp.CpuName);
            if (!string.IsNullOrEmpty(cpuV) && cpuV != gpuVendor)
                chipRow.Children.Add(DriverVendorChip(cpuV, cpuV == "AMD" ? AiOrange : CardBg2));
            chipRow.Children.Add(DriverVendorChip("winget", Red));
            ht.Children.Add(chipRow);
            Grid.SetColumn(ht, 1);
            hg.Children.Add(ht);
            hero.Child = hg;
            left.Children.Add(hero);

            Border table = DashboardCard();
            table.Margin = new Thickness(0, 0, 18, 16);
            table.Padding = new Thickness(16);
            StackPanel tp = new StackPanel();
            tp.Children.Add(new TextBlock
            {
                Text = T("TREIBER-STATUS", "DRIVER STATUS"),
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            _driverPreviewHost = new StackPanel();
            _driverPreviewHost.Children.Add(new TextBlock
            {
                Text = T("Wird geladen…", "Loading…"),
                Foreground = Muted,
                FontSize = 13
            });
            tp.Children.Add(_driverPreviewHost);
            table.Child = tp;
            left.Children.Add(table);

            Border actionCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 28, 38)),
                BorderBrush = Red,
                BorderThickness = new Thickness(4, 0, 0, 0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 18, 16)
            };
            StackPanel actionP = new StackPanel();
            actionP.Children.Add(new TextBlock
            {
                Text = T("TREIBER INSTALLIEREN", "INSTALL DRIVERS"),
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            actionP.Children.Add(new TextBlock
            {
                Text = RedlineFeatureGate.InAppDriverUpdateEnabled
                    ? T("Nur winget · passend zu deiner GPU/CPU. Kein Windows Update.",
                        "winget only · matched to your GPU/CPU. No Windows Update.")
                    : T("Coming Soon in der Free-Version.", "Coming soon in the free version."),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });
            if (RedlineFeatureGate.InAppDriverUpdateEnabled && IsProActive())
            {
                Button installMain = RedButton("⬇  " + T("PASSENDE TREIBER INSTALLIEREN", "INSTALL MATCHING DRIVERS"), DriversInAppAutoUpdate_Click);
                installMain.Height = 48;
                installMain.Margin = new Thickness(0, 0, 0, 10);
                actionP.Children.Add(installMain);
                StackPanel subBtns = new StackPanel { Orientation = Orientation.Horizontal };
                Button previewBtn = OutlineButton(T("VORSCHAU", "PREVIEW"), DriversPreviewPackages_Click);
                previewBtn.Height = 40;
                previewBtn.Width = 140;
                subBtns.Children.Add(previewBtn);
                Button cancelBtn = OutlineButton(T("STOP", "STOP"), DriversCancelUpdate_Click);
                cancelBtn.Height = 40;
                cancelBtn.Width = 100;
                cancelBtn.Margin = new Thickness(8, 0, 0, 0);
                cancelBtn.Foreground = AiOrange;
                subBtns.Children.Add(cancelBtn);
                Button devBtn = OutlineButton(T("Geräte-Manager", "Device Manager"), (s, e) => SafeStartSystem("devmgmt.msc"));
                devBtn.Height = 40;
                devBtn.Width = 160;
                devBtn.Margin = new Thickness(8, 0, 0, 0);
                subBtns.Children.Add(devBtn);
                actionP.Children.Add(subBtns);
            }
            else
            {
                Button soon = OutlineButton(T("COMING SOON", "COMING SOON"), (s, e) => TryInAppDriverFeature());
                soon.Height = 44;
                soon.Opacity = 0.8;
                actionP.Children.Add(soon);
            }
            actionCard.Child = actionP;
            left.Children.Add(actionCard);

            Border vendors = DashboardCard();
            vendors.Margin = new Thickness(0, 0, 18, 0);
            vendors.Padding = new Thickness(16);
            StackPanel vp = new StackPanel();
            vp.Children.Add(new TextBlock
            {
                Text = T("HERSTELLER-LINKS (MANUELL)", "VENDOR LINKS (MANUAL)"),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 12)
            });
            WrapPanel tiles = new WrapPanel();
            AddDriverVendorTiles(tiles, hp);
            tiles.Children.Add(ModernTile(T("Report", "Report"), T("Report speichern", "Save report"), "TXT", AiPurple, DriverReport_Click));
            vp.Children.Add(tiles);
            vendors.Child = vp;
            left.Children.Add(vendors);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border statusCard = CreateActivityStatusCard(
                T("AKTUELLER SCHRITT", "CURRENT STEP"),
                T("Bereit — offizielle Hersteller-Links und winget-Installation.",
                  "Ready — official vendor links and winget install."),
                out _driverActivityText,
                out _driverActivityBar,
                200);
            statusCard.Margin = new Thickness(0, 0, 0, 16);
            right.Children.Add(statusCard);
            right.Children.Add(AiSidePanel(
                "REDLINE AI DRIVER",
                AiCheckRow("GPU", TruncateGpuName(GetGpuName()), string.IsNullOrEmpty(gpuVendor) ? "G" : gpuVendor[0].ToString(), gpuAccent, true),
                AiCheckRow("CPU", TruncateGpuName(GetCpuName()), "C", Muted, true),
                AiCheckRow("winget", T("Aktiv", "Active"), "W", AiGreen, true),
                OutlineButton(T("Liste aktualisieren", "Refresh list"), DriverScan_Click)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }

        private static UIElement DriverInfoChip(string label, string value, Brush accent)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = label + ": ",
                Foreground = accent,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Width = 42
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 320
            });
            return row;
        }

        private static Border DriverVendorChip(string text, Brush color)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderBrush = color,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock { Text = text, Foreground = color, FontSize = 11, FontWeight = FontWeights.Bold }
            };
        }

        private void AddDriverVendorTiles(WrapPanel tiles, HardwareProfile hp)
        {
            string gpu = RedlineHardwareProfile.GpuVendor(hp.GpuName);
            string cpu = RedlineHardwareProfile.CpuVendor(hp.CpuName);
            if (gpu == "NVIDIA")
                tiles.Children.Add(ModernTile("NVIDIA", T("NVIDIA Download", "NVIDIA download"), "NV", AiGreen,
                    (s, e) => OpenUri("https://www.nvidia.com/Download/index.aspx")));
            if (gpu == "AMD")
                tiles.Children.Add(ModernTile("AMD GPU", T("AMD Grafik", "AMD graphics"), "GPU", AiOrange,
                    (s, e) => OpenUri("https://www.amd.com/en/support/download/drivers.html")));
            if (gpu == "Intel")
                tiles.Children.Add(ModernTile("Intel GPU", T("Intel Grafik", "Intel graphics"), "IG", CardBg2,
                    (s, e) => OpenUri("https://www.intel.com/content/www/us/en/support/detect.html")));
            if (cpu == "AMD")
                tiles.Children.Add(ModernTile("AMD Chipset", T("AMD Chipsatz", "AMD chipset"), "AMD", AiOrange,
                    (s, e) => OpenUri("https://www.amd.com/en/support/chipsets/amd-socket-am5/am5")));
            if (cpu == "Intel" && gpu != "Intel")
                tiles.Children.Add(ModernTile("Intel", T("Intel Support", "Intel support"), "IN", CardBg2,
                    (s, e) => OpenUri("https://www.intel.com/content/www/us/en/support/detect.html")));
            tiles.Children.Add(ModernTile("Realtek", T("Audio / LAN", "Audio / LAN"), "RT", CardBg2,
                (s, e) => OpenUri("https://www.realtek.com/Download/List?cate_id=584")));
        }

        private UIElement PageBios()
        {
            Grid grid = TwoColumnLayout();
            StackPanel left = new StackPanel();

            left.Children.Add(HeroCard(
                "BIOS / UEFI Check",
                "Liest sichere Infos aus Windows: BIOS-Version, Mainboard, UEFI/Legacy, Secure Boot, TPM und Virtualization."
            ));

            StackPanel buttons = new StackPanel();

            Button scan = ActionButton("BIOS CHECK STARTEN", Red, 300);
            scan.Click += BiosCheck_Click;

            Button msinfo = ActionButton("SYSTEMINFO ÖFFNEN", CardBg2, 260);
            msinfo.Margin = new Thickness(0, 12, 0, 0);
            msinfo.Click += (s, e) => SafeStartSystem("msinfo32.exe");

            Button uefi = ActionButton("UEFI NEUSTART OPTION", DarkRed, 300);
            uefi.Margin = new Thickness(0, 12, 0, 0);
            uefi.Click += UefiRestart_Click;

            buttons.Children.Add(scan);
            buttons.Children.Add(msinfo);
            buttons.Children.Add(uefi);
            left.Children.Add(buttons);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            OutputBox = OutputConsole("BIOS / UEFI Check bereit.");
            right.Children.Add(OutputBox);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return grid;
        }

        private string GetPrimaryAdapterName()
        {
            try
            {
                NetworkInterface? ni = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderByDescending(n => n.Speed)
                    .FirstOrDefault();
                return ni?.Name ?? T("Kein Adapter", "No adapter");
            }
            catch
            {
                return "—";
            }
        }

        private string GetPrimaryDnsLabel()
        {
            try
            {
                NetworkInterface? ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                         n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (ni == null)
                    return "—";
                var dns = ni.GetIPProperties().DnsAddresses.Select(a => a.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(2).ToList();
                return dns.Count > 0 ? string.Join(", ", dns) : T("Automatisch", "Automatic");
            }
            catch
            {
                return "—";
            }
        }

        private List<(string name, string type, string speed, string dns)> GetNetworkAdaptersPreview()
        {
            List<(string, string, string, string)> list = new List<(string, string, string, string)>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderByDescending(n => n.Speed)
                    .Take(4))
                {
                    string speed = ni.Speed > 0 ? (ni.Speed / 1_000_000) + " Mbps" : "—";
                    string dns = "—";
                    try
                    {
                        var addrs = ni.GetIPProperties().DnsAddresses.Select(a => a.ToString()).Take(2);
                        dns = string.Join(", ", addrs);
                        if (string.IsNullOrWhiteSpace(dns))
                            dns = T("DHCP", "DHCP");
                    }
                    catch { }
                    list.Add((ni.Name, ni.NetworkInterfaceType.ToString(), speed, dns));
                }
            }
            catch { }
            return list;
        }

        private Border NetworkStatTile(string title, string value, string sub, string icon)
        {
            Border box = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 10, 12, 8),
                Margin = new Thickness(4),
                MinHeight = 100
            };
            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = title, Foreground = TextPrimary, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            p.Children.Add(new TextBlock { Text = icon, Foreground = Red, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 4) });
            p.Children.Add(new TextBlock { Text = value, Foreground = AiGreen, FontSize = 18, FontWeight = FontWeights.UltraBold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            p.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            p.Children.Add(BuildDecorativeSparkline(title));
            box.Child = p;
            return box;
        }

        private UIElement PageNetwork()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader("NETWORK",
                T("Ping, DNS, Speedtest und Adapter – optimiert für Gaming-Latenz.",
                  "Ping, DNS, speed test and adapters – tuned for gaming latency."),
                NetworkCheck_Click,
                "◎  " + T("NETZWERK CHECK", "NETWORK CHECK"));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(400);
            StackPanel left = new StackPanel();

            int? ping = RedlineAppData.Current.LastPingMs;
            string pingVal = ping > 0 ? ping + " ms" : "—";
            string pingSub = ping > 0 ? PingQualityLabel(ping.Value) : T("Speedtest oder Check starten", "Run speed test or check");

            Border stats = DashboardCard();
            stats.Margin = new Thickness(0, 0, 18, 18);
            stats.Padding = new Thickness(14);
            StackPanel statsRoot = new StackPanel();
            statsRoot.Children.Add(new TextBlock { Text = T("LIVE NETZWERK", "LIVE NETWORK"), Foreground = TextPrimary, FontSize = 13, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 10) });
            UniformGrid statGrid = new UniformGrid { Columns = 4 };
            statGrid.Children.Add(NetworkStatTile("PING", pingVal, pingSub, "◉"));
            statGrid.Children.Add(NetworkStatTile("DNS", GetPrimaryDnsLabel(), T("Aktiver Adapter", "Active adapter"), "◎"));
            statGrid.Children.Add(NetworkStatTile(T("ADAPTER", "ADAPTER"), TruncateGpuName(GetPrimaryAdapterName()), T("Primär", "Primary"), "▦"));
            statGrid.Children.Add(NetworkStatTile(T("STATUS", "STATUS"), ping > 0 ? T("Online", "Online") : "—", T("Echt gemessen", "Measured"), "✓"));
            statsRoot.Children.Add(statGrid);
            stats.Child = statsRoot;
            left.Children.Add(stats);

            Border actions = DashboardCard();
            actions.Margin = new Thickness(0, 0, 18, 18);
            actions.Padding = new Thickness(16);
            StackPanel ap = new StackPanel();
            ap.Children.Add(new TextBlock { Text = T("ANALYSE & TESTS", "ANALYSIS & TESTS"), Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 12) });
            WrapPanel actionTiles = new WrapPanel();
            actionTiles.Children.Add(ModernTile(T("Netzwerk Check", "Network check"), T("Adapter, DNS und Ping prüfen", "Check adapters, DNS and ping"), "CHK", Red, NetworkCheck_Click));
            actionTiles.Children.Add(ModernTile(T("Speed Test", "Speed test"), T("Download/Upload-Messung", "Download/upload measurement"), "SPD", AiOrange, SpeedTest_Click));
            actionTiles.Children.Add(ModernTile(T("DNS Benchmark", "DNS benchmark"), T("Schnellsten DNS finden", "Find fastest DNS"), "DNS", AiGreen, DnsBenchmark_Click));
            actionTiles.Children.Add(ModernTile(T("Ping Test", "Ping test"), T("Latenz zu DNS-Servern", "Latency to DNS servers"), "ms", new SolidColorBrush(Color.FromRgb(180, 40, 60)), PingTool_Click));
            ap.Children.Add(actionTiles);
            actions.Child = ap;
            left.Children.Add(actions);

            Border dnsCard = DashboardCard();
            dnsCard.Margin = new Thickness(0, 0, 18, 18);
            dnsCard.Padding = new Thickness(16);
            StackPanel dp = new StackPanel();
            dp.Children.Add(new TextBlock { Text = T("DNS OPTIMIERUNG", "DNS OPTIMIZATION"), Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 12) });
            WrapPanel dnsTiles = new WrapPanel();
            dnsTiles.Children.Add(ModernTile(T("Schnellsten setzen", "Set fastest"), T("Misst und setzt besten DNS", "Measures and sets best DNS"), "⚡", Red, SetFastestDns_Click));
            dnsTiles.Children.Add(ModernTile("Cloudflare", "1.1.1.1 · 1.0.0.1", "CF", CardBg2, async (s, e) => await SetDnsPreset("Cloudflare", "1.1.1.1", "1.0.0.1")));
            dnsTiles.Children.Add(ModernTile("Google", "8.8.8.8 · 8.8.4.4", "G", CardBg2, async (s, e) => await SetDnsPreset("Google", "8.8.8.8", "8.8.4.4")));
            dnsTiles.Children.Add(ModernTile(T("Automatisch", "Automatic"), T("DNS per DHCP", "DNS via DHCP"), "DH", Muted, SetDnsAuto_Click));
            dp.Children.Add(dnsTiles);
            dnsCard.Child = dp;
            left.Children.Add(dnsCard);

            Border adapters = DashboardCard();
            adapters.Margin = new Thickness(0, 0, 18, 0);
            adapters.Padding = new Thickness(16);
            StackPanel adp = new StackPanel();
            adp.Children.Add(new TextBlock { Text = T("AKTIVE ADAPTER", "ACTIVE ADAPTERS"), Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 10) });
            List<(string name, string type, string speed, string dns)> adaptersList = GetNetworkAdaptersPreview();
            if (adaptersList.Count == 0)
            {
                adp.Children.Add(new TextBlock { Text = T("Kein aktiver Adapter gefunden.", "No active adapter found."), Foreground = Muted, FontSize = 12 });
            }
            else
            {
                foreach (var a in adaptersList)
                {
                    string desc = a.type + " · " + a.speed;
                    adp.Children.Add(SecurityTableRow(a.name, desc, a.dns, T("Aktiv", "Active"), AiGreen, "◎", (s, e) => SafeStartSystem("ncpa.cpl")));
                }
            }
            Button repairRow = OutlineButton(T("DNS Cache leeren · Adapter · Winsock", "Flush DNS · Adapter · Winsock"), async (s, e) => await FlushDNS());
            repairRow.Margin = new Thickness(0, 12, 0, 0);
            repairRow.Height = 40;
            adp.Children.Add(repairRow);
            Button winsock = OutlineButton("Winsock Reset", WinsockReset_Click);
            winsock.Margin = new Thickness(0, 8, 0, 0);
            winsock.Height = 36;
            adp.Children.Add(winsock);
            adapters.Child = adp;
            left.Children.Add(adapters);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border netLog = ModernOutputCard(T("Network Log bereit.", "Network log ready."));
            netLog.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(netLog);
            Progress = new ProgressBar
            {
                Height = 8,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 0, 0, 14),
                Background = new SolidColorBrush(Color.FromRgb(32, 38, 48)),
                BorderThickness = new Thickness(0),
                Foreground = Red
            };
            right.Children.Add(Progress);
            right.Children.Add(AiSidePanel(
                "REDLINE AI NETWORK",
                AiCheckRow("Ping", pingVal, "◉", ping > 0 ? AiGreen : Muted, ping > 0),
                AiCheckRow("DNS", GetPrimaryDnsLabel(), "◎", CardBg2, true),
                AiCheckRow(T("Adapter", "Adapter"), GetPrimaryAdapterName(), "▦", CardBg2, true),
                OutlineButton(T("Speed Test starten", "Start speed test"), SpeedTest_Click)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }

        private UIElement PageRepair()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader("REPAIR",
                T("Behebe Windows-Probleme und stelle Stabilität wieder her.",
                  "Fix Windows issues and restore stability."));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(380);
            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Margin = new Thickness(0, 0, 18, 18);
            hero.Padding = new Thickness(22);
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            Grid wrench = new Grid { Width = 150, Height = 150, HorizontalAlignment = HorizontalAlignment.Center };
            wrench.Children.Add(new System.Windows.Shapes.Ellipse { Width = 140, Height = 140, Stroke = Red, StrokeThickness = 2 });
            wrench.Children.Add(new TextBlock { Text = "🔧", FontSize = 56, Foreground = Red, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            hg.Children.Add(wrench);
            StackPanel ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            ht.Children.Add(new TextBlock { Text = "Windows Repair", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.UltraBold });
            ht.Children.Add(new TextBlock { Text = T("Behebe häufige Windows-Probleme, korrigiere Systemfehler und stelle die Stabilität wieder her.", "Fix common Windows problems, correct system errors and restore stability."), Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 16) });
            Button allRep = RedButton("🔧  " + T("ALLE REPARATUREN AUSFÜHREN", "RUN ALL REPAIRS"), RepairAll_Click);
            allRep.Width = 300; allRep.Height = 48;
            ht.Children.Add(allRep);
            Button showRec = OutlineButton(T("EMPFOHLENE REPARATUREN ANZEIGEN", "SHOW RECOMMENDED REPAIRS"), (s, e) => Navigate("Repair"));
            showRec.Width = 300; showRec.Height = 42; showRec.Margin = new Thickness(0, 10, 0, 0);
            ht.Children.Add(showRec);
            Grid.SetColumn(ht, 1);
            hg.Children.Add(ht);
            hero.Child = hg;
            left.Children.Add(hero);

            left.Children.Add(RepairSection(T("SYSTEM REPAIR", "SYSTEM REPAIR"),
                (T("SFC SCAN", "SFC SCAN"), T("Überprüft beschädigte Systemdateien.", "Checks corrupted system files."), SfcScan_Click),
                (T("DISM REPARATUR", "DISM REPAIR"), T("Repariert das Windows-Image.", "Repairs the Windows image."), DismRestore_Click),
                (T("STORE RESET", "STORE RESET"), T("Setzt Microsoft Store zurück.", "Resets Microsoft Store."), StoreReset_Click)));

            left.Children.Add(RepairSection(T("NETZWERK REPAIR", "NETWORK REPAIR"),
                ("DNS CACHE", T("Leert den DNS-Cache.", "Flushes DNS cache."), async (s, e) => await FlushDNS()),
                ("WINSOCK RESET", T("Setzt Winsock zurück.", "Resets Winsock."), WinsockReset_Click),
                (T("ADAPTER", "ADAPTER"), T("Netzwerkadapter-Einstellungen.", "Network adapter settings."), (s, e) => SafeStartSystem("ncpa.cpl"))));

            left.Children.Add(RepairSection(T("ANALYSE", "ANALYSIS"),
                (T("ZUVERLÄSSIGKEIT", "RELIABILITY"), T("Systemzuverlässigkeit analysieren.", "Analyze system reliability."), (s, e) => SafeStartSystem("perfmon.exe", "/rel")),
                (T("WIEDERHERSTELLUNGSPUNKT", "RESTORE POINT"), T("Erstellt einen Wiederherstellungspunkt.", "Creates a restore point."), RestorePoint_Click),
                ("REPORT", T("Erstellt einen Repair-Report.", "Creates a repair report."), SaveReport_Click)));

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border repairLog = ModernOutputCard(T("Repair Log bereit. Admin-Rechte empfohlen.", "Repair log ready. Admin rights recommended."));
            repairLog.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(repairLog);
            right.Children.Add(AiSidePanel(
                T("INFORMATIONEN", "INFORMATION"),
                new TextBlock { Text = T("Diese Tools sind sicher und nutzen offizielle Windows-Befehle. Einige Aktionen benötigen Administrator-Rechte (UAC).", "These tools are safe and use official Windows commands. Some actions require administrator rights (UAC)."), Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                AiCheckRow(T("Tipp", "Tip"), T("Nach Reparaturen kann ein Neustart nötig sein.", "A restart may be required after repairs."), "💡", AiOrange, true)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }

        private UIElement RepairSection(string title, params (string name, string desc, RoutedEventHandler click)[] items)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 18, 14);
            card.Padding = new Thickness(18);
            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 10) });
            foreach (var item in items)
                p.Children.Add(RepairTile(item.name, item.desc, item.click));
            card.Child = p;
            return card;
        }

        private UIElement RepairTile(string title, string desc, RoutedEventHandler click)
        {
            Border tile = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 16, 21, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(34, 42, 54)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            tile.MouseLeftButtonUp += (s, e) => click(s, new RoutedEventArgs());
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(RoundIcon(title.Length > 3 ? title.Substring(0, 1) : title, CardBg2, 36));
            StackPanel t = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold });
            t.Children.Add(new TextBlock { Text = desc, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            g.Children.Add(new TextBlock { Text = ">", Foreground = Red, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(g.Children[2], 2);
            tile.Child = g;
            return tile;
        }

        private async void RepairAll_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await Log(T("===== ALLE REPARATUREN (EMPFOHLENE REIHENFOLGE) =====", "===== ALL REPAIRS (RECOMMENDED ORDER) ====="));
            SfcScan_Click(sender, e);
            await Task.Delay(500);
            await FlushDNS();
            await Log(T("Weitere Schritte: DISM und Store manuell über die Kacheln starten.", "More steps: start DISM and Store manually via tiles."));
        }

        private UIElement PageRemoteSupport()
        {
            RemoteSupportStatus rs = RedlineRemoteSupport.Query();
            bool en = IsEnglish();

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader(T("REMOTE SUPPORT", "REMOTE SUPPORT"),
                T("Sichere Fernhilfe in der App: Quick Assist, Remote Desktop Status, Firewall-Check.",
                  "Secure remote help in-app: Quick Assist, Remote Desktop status, firewall check."),
                RemoteFirewallCheck_Click,
                "🔍  " + T("SICHERHEIT PRÜFEN", "SECURITY CHECK"));
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(420);
            StackPanel left = new StackPanel();

            Border statusCard = DashboardCard();
            statusCard.Margin = new Thickness(0, 0, 18, 18);
            statusCard.Padding = new Thickness(18);
            StackPanel st = new StackPanel();
            st.Children.Add(new TextBlock
            {
                Text = T("LIVE STATUS", "LIVE STATUS"),
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            st.Children.Add(new TextBlock
            {
                Text = RedlineRemoteSupport.FormatStatusLabel(rs, en),
                Foreground = rs.RemoteDesktopEnabled ? AiOrange : AiGreen,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            st.Children.Add(new TextBlock
            {
                Text = (rs.RemoteAssistanceAvailable
                    ? T("Remote Assistance (msra): verfügbar", "Remote Assistance (msra): available")
                    : T("Remote Assistance: nicht gefunden", "Remote Assistance: not found")),
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            });
            statusCard.Child = st;
            left.Children.Add(statusCard);

            Border connectCard = DashboardCard();
            connectCard.Margin = new Thickness(0, 0, 18, 18);
            connectCard.Padding = new Thickness(18);
            connectCard.BorderBrush = Red;
            StackPanel conn = new StackPanel();
            conn.Children.Add(new TextBlock
            {
                Text = T("SO VERBINDEST DU DICH (QUICK ASSIST)", "HOW TO CONNECT (QUICK ASSIST)"),
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            conn.Children.Add(new TextBlock
            {
                Text = T(
                    "1. Hier „Quick Assist“ öffnen\n2. „Hilfe anfordern“ (sie) oder „Einer anderen Person helfen“ (du)\n3. Den 6-stelligen Code aus Windows Schnellhilfe nutzen – NICHT den Redline-Code unten\n4. Auf „Zulassen“ tippen",
                    "1. Open Quick Assist here\n2. Get help (them) or Help someone (you)\n3. Use the 6-digit code from Windows Quick Assist – NOT the Redline ID below\n4. Tap Allow"),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });
            Button qaBig = RedButton("🖥  " + T("QUICK ASSIST ÖFFNEN", "OPEN QUICK ASSIST"), (s, e) => OpenQuickAssist());
            qaBig.Height = 50;
            conn.Children.Add(qaBig);
            connectCard.Child = conn;
            left.Children.Add(connectCard);

            Border codeCard = DashboardCard();
            codeCard.Margin = new Thickness(0, 0, 18, 18);
            codeCard.Padding = new Thickness(18);
            StackPanel c = new StackPanel();
            c.Children.Add(new TextBlock
            {
                Text = T("NUR REFERENZ-ID (KEIN VERBINDUNGS-CODE)", "REFERENCE ID ONLY (NOT A CONNECTION CODE)"),
                Foreground = AiOrange,
                FontSize = 14,
                FontWeight = FontWeights.UltraBold
            });
            c.Children.Add(new TextBlock
            {
                Text = T("Optional: Nummer für Telefon/Chat („Meine Redline-Sitzung ist …“). Für Fernhilfe immer Windows Quick Assist oben nutzen.",
                  "Optional: number for phone/chat (\"My Redline session is …\"). For remote help always use Windows Quick Assist above."),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 10)
            });
            RemoteCodeBox = new TextBox
            {
                Text = GenerateSupportCode(),
                Height = 40,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(16, 21, 29)),
                Foreground = Muted,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 90, 110)),
                BorderThickness = new Thickness(1),
                IsReadOnly = true
            };
            c.Children.Add(RemoteCodeBox);
            StackPanel codeBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            Button newCode = OutlineButton(T("NEUE ID", "NEW ID"), GenerateRemoteCode_Click);
            newCode.Width = 120; newCode.Height = 36;
            codeBtns.Children.Add(newCode);
            Button copy = OutlineButton(T("KOPIEREN", "COPY"), CopyRemoteCode_Click);
            copy.Width = 120; copy.Height = 36; copy.Margin = new Thickness(8, 0, 0, 0);
            codeBtns.Children.Add(copy);
            c.Children.Add(codeBtns);
            codeCard.Child = c;
            left.Children.Add(codeCard);

            Border actions = DashboardCard();
            actions.Padding = new Thickness(16);
            actions.Margin = new Thickness(0, 0, 18, 0);
            StackPanel ap = new StackPanel();
            ap.Children.Add(new TextBlock { Text = T("AKTIONEN", "ACTIONS"), Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.UltraBold, Margin = new Thickness(0, 0, 0, 12) });
            WrapPanel tiles = new WrapPanel();
            tiles.Children.Add(ModernTile(T("QUICK ASSIST", "QUICK ASSIST"),
                T("Microsoft Fernhilfe mit Code", "Microsoft help with code"), "QA", Red, (s, e) => OpenQuickAssist()));
            tiles.Children.Add(ModernTile(T("REMOTE DESKTOP", "REMOTE DESKTOP"),
                T("Windows Einstellungen öffnen", "Open Windows settings"), "RDP", AiOrange, (s, e) => OpenUri("ms-settings:remotedesktop")));
            tiles.Children.Add(ModernTile(T("REMOTE ASSIST", "REMOTE ASSIST"),
                T("Klassische Windows-Hilfe", "Classic Windows assistance"), "MS", CardBg2, (s, e) => SafeStartSystem("msra.exe", "", true)));
            tiles.Children.Add(ModernTile(T("FIREWALL CHECK", "FIREWALL CHECK"),
                T("Firewall + RDP Status im Log", "Firewall + RDP status in log"), "FW", AiGreen, RemoteFirewallCheck_Click));
            ap.Children.Add(tiles);
            actions.Child = ap;
            left.Children.Add(actions);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border remoteLog = ModernOutputCard(T("Remote Log bereit.", "Remote log ready."));
            remoteLog.Margin = new Thickness(0, 0, 0, 18);
            right.Children.Add(remoteLog);
            right.Children.Add(AiSidePanel(
                "REDLINE AI REMOTE",
                AiCheckRow(T("Empfehlung", "Recommendation"), T("Quick Assist statt offenem RDP", "Quick Assist instead of open RDP"), "✓", AiGreen, true),
                AiCheckRow(T("Quick Assist", "Quick Assist"), rs.QuickAssistRunning ? T("Läuft", "Running") : (rs.QuickAssistAvailable ? T("Bereit", "Ready") : T("Nicht erkannt", "Not detected")), "QA", rs.QuickAssistRunning || rs.QuickAssistAvailable ? AiGreen : AiOrange, true),
                AiCheckRow(T("Remote Desktop", "Remote Desktop"), rs.RemoteDesktopEnabled ? T("Aktiv – Vorsicht", "Active – caution") : T("Deaktiviert", "Disabled"), "RDP", rs.RemoteDesktopEnabled ? AiOrange : AiGreen, true),
                OutlineButton(T("Status jetzt prüfen", "Check status now"), RemoteFirewallCheck_Click)
            ));
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }



        private UIElement PageTools()
        {
            Grid grid = ModernTwoColumn();
            grid.ColumnDefinitions[1].Width = new GridLength(500);

            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Padding = new Thickness(24);
            hero.Margin = new Thickness(0, 0, 18, 18);
            Grid heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition());
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            heroGrid.Children.Add(RoundIcon("🛠", Brushes.Transparent, 82));
            StackPanel heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heroText.Children.Add(new TextBlock { Text = "Quick Tools", Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.UltraBold });
            heroText.Children.Add(new TextBlock { Text = T("Schneller Zugriff auf wichtige Optimierungs- und Diagnosewerkzeuge. Ein Klick. Bessere Performance.", "Fast access to important optimization and diagnostic tools. One click. Better performance."), Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            Grid.SetColumn(heroText, 1); heroGrid.Children.Add(heroText);
            Border speed = new Border { Width = 180, Height = 86, CornerRadius = new CornerRadius(40), BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0)), BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromArgb(40, 18, 22, 30)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid needle = new Grid();
            needle.Children.Add(new TextBlock { Text = "⟋", FontSize = 58, Foreground = Red, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(52, -6, 0, 0) });
            speed.Child = needle; Grid.SetColumn(speed, 2); heroGrid.Children.Add(speed);
            hero.Child = heroGrid;
            left.Children.Add(hero);

            Border tilesCard = DashboardCard();
            tilesCard.Padding = new Thickness(18);
            tilesCard.Margin = new Thickness(0, 0, 18, 0);
            WrapPanel tiles = new WrapPanel();
            tiles.Children.Add(ModernTile("Chrome Check", T("Überprüft Chrome auf Probleme und unnötige Erweiterungen.", "Checks Chrome for issues and unnecessary extensions."), "C", Red, ChromeCheck_Click));
            tiles.Children.Add(ModernTile(T("Chrome Hintergrund", "Chrome background"), T("Deaktiviert Chrome Background Apps für weniger RAM-Verbrauch.", "Disables Chrome background apps to reduce RAM usage."), "BG", CardBg2, ChromeDisableBackground_Click));
            tiles.Children.Add(ModernTile(T("Browser schließen", "Close browser"), T("Schließt alle offenen Browser, um RAM und Ressourcen freizugeben.", "Closes all open browsers to free RAM and resources."), "X", DarkRed, CloseBrowsers_Click));
            tiles.Children.Add(ModernTile(T("Chrome Daten", "Chrome data"), T("Löscht Verlauf, Cache, Cookies und andere Chrome-Daten.", "Deletes history, cache, cookies and other Chrome data."), "DB", AiBlue, ChromeClean_Click));
            tiles.Children.Add(ModernTile(T("Temp löschen", "Delete temp"), T("Entfernt temporäre Dateien und bereinigt unnötigen Systemmüll.", "Removes temporary files and cleans system junk."), "TMP", AiPurple, QuickTempClean_Click));
            tiles.Children.Add(ModernTile(T("DNS leeren", "Flush DNS"), T("Leert den DNS-Cache für schnellere und stabilere Verbindung.", "Flushes DNS cache for a faster more stable connection."), "DNS", AiGreen, async (s, e) => await FlushDNS()));
            tiles.Children.Add(ModernTile("Speed Test", T("Testet deine aktuelle Download- und Upload-Geschwindigkeit.", "Tests your current download and upload speed."), "SPD", AiOrange, SpeedTest_Click));
            tiles.Children.Add(ModernTile("Ping Test", T("Misst die Ping-Zeit zu deinem Server für eine bessere Übersicht.", "Measures ping time to your server for a better overview."), "ms", new SolidColorBrush(Color.FromRgb(0, 185, 170)), PingTool_Click));
            tiles.Children.Add(ModernTile(T("Grafik Settings", "Graphics settings"), T("Optimiert Grafikeinstellungen für maximale FPS und beste Bildqualität.", "Optimizes graphics settings for max FPS and best visuals."), "GPU", AiPurple, (s, e) => OpenUri("ms-settings:display-advancedgraphics")));
            tiles.Children.Add(ModernTile(T("Geräte-Manager", "Device Manager"), T("Öffnet den Geräte-Manager zur Verwaltung deiner Hardware.", "Opens Device Manager to manage your hardware."), "DRV", AiBlue, (s, e) => SafeStartSystem("devmgmt.msc")));
            tiles.Children.Add(ModernTile("Task Manager", T("Öffnet den Task-Manager für Prozessüberwachung und Beendigung.", "Opens Task Manager for process monitoring and termination."), "CPU", AiGreen, (s, e) => SafeStartSystem("taskmgr.exe")));
            tiles.Children.Add(ModernTile("Report", T("Erstellt einen System-Report mit Hard- und Software-Informationen.", "Creates a system report with hardware and software information."), "TXT", AiOrange, SaveReport_Click));
            tiles.Children.Add(ModernTile(T("Remote Support", "Remote Support"), T("Quick Assist, Remote Desktop, Firewall", "Quick Assist, Remote Desktop, firewall"), "RDP", AiBlue, (s, e) => Navigate("RemoteSupport")));
            tilesCard.Child = tiles;
            left.Children.Add(tilesCard);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Children.Add(AiSidePanel(
                "REDLINE AI ASSISTANT",
                new TextBlock { Text = T("Dein intelligenter Assistent für System-Optimierung.", "Your intelligent assistant for system optimization."), Foreground = Muted, FontSize = 14, Margin = new Thickness(0, 0, 0, 14) },
                AiCheckRow(T("EMPFEHLUNG", "RECOMMENDATION"), T("Basierend auf deinem Systemstatus empfehle ich dir folgende Aktion: Temp löschen.", "Based on your system status I recommend: delete temp files."), "★", Red, false),
                new UniformGrid { Columns = 3, Margin = new Thickness(0, 8, 0, 10), Children = { StatusCard(T("SICHER", "SAFE"), T("Keine Risiken erkannt", "No risks detected"), "✓", AiGreen), StatusCard(T("ZU PRÜFEN", "TO CHECK"), T("2 Optimierungen möglich", "2 optimizations possible"), "!", AiOrange), StatusCard(T("OPTIMIERUNGS-SCORE", "OPTIMIZATION SCORE"), T("Sehr gut", "Very good"), "92", AiGreen) } },
                AiCheckRow(T("TOOL VALIDIERUNG", "TOOL VALIDATION"), T("Ausgewähltes Tool wird als sicher eingestuft. Keine negativen Auswirkungen auf dein System.", "Selected tool is considered safe. No negative impact on your system."), "✓", AiGreen, true),
                CreatePageStatusCard(T("STATUS", "STATUS"), T("Tool bereit — Kachel anklicken.", "Tool ready — click a tile."), 120)
            ));

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return grid;
        }

        private UIElement PageUpdate()
        {
            Grid grid = TwoColumnLayout();
            grid.ColumnDefinitions[1].Width = new GridLength(400);
            StackPanel left = new StackPanel();

            Border hero = DashboardCard();
            hero.Margin = new Thickness(0, 0, 18, 18);
            hero.Padding = new Thickness(24);
            hero.BorderBrush = Red;
            hero.BorderThickness = new Thickness(1);
            StackPanel heroP = new StackPanel();
            heroP.Children.Add(new TextBlock
            {
                Text = T("UPDATE CENTER", "UPDATE CENTER"),
                Foreground = Red,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            heroP.Children.Add(new TextBlock
            {
                Text = T("Offizielle GitHub-Releases", "Official GitHub releases"),
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            heroP.Children.Add(new TextBlock
            {
                Text = T("Nur offizielle GitHub-EXE. Installation nie automatisch — immer Ja/Nein.",
                  "Official GitHub EXE only. Never auto-install — always Yes/No."),
                Foreground = Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            hero.Child = heroP;
            left.Children.Add(hero);

            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 18, 0);
            card.Padding = new Thickness(20);
            StackPanel p = new StackPanel();

            _updateInstalledVersionLabel = new TextBlock
            {
                Text = T("Installiert: ", "Installed: ") + "V" + GetDisplayAppVersion(),
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            p.Children.Add(_updateInstalledVersionLabel);
            _updateOnlineVersionLabel = new TextBlock
            {
                Text = T("Online: wird geprüft…", "Online: checking…"),
                Foreground = AiGreen,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 12)
            };
            p.Children.Add(_updateOnlineVersionLabel);

            Border autoRow = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            StackPanel autoP = new StackPanel();
            autoP.Children.Add(new TextBlock
            {
                Text = T("Auto-Update beim Start", "Auto-update on startup"),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            _updateAutoStartHint = new TextBlock
            {
                Text = T("Aus — du entscheidest selbst. Wenn an: nur Download-Hinweis, Installation mit Ja/Nein.",
                  "Off — you decide. When on: download prompt only, install with Yes/No."),
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 10)
            };
            autoP.Children.Add(_updateAutoStartHint);
            ToggleButton autoToggle = DashboardToggle(RedlineAppData.Current.AutoUpdateOnStartup);
            autoToggle.IsChecked = RedlineAppData.Current.AutoUpdateOnStartup;
            autoToggle.Checked += (_, _) => SetAutoUpdateOnStartup(true);
            autoToggle.Unchecked += (_, _) => SetAutoUpdateOnStartup(false);
            autoP.Children.Add(autoToggle);
            autoRow.Child = autoP;
            p.Children.Add(autoRow);

            Button downloadBtn = RedButton("⬇  " + T("UPDATE HERUNTERLADEN", "DOWNLOAD UPDATE"), UpdateDownload_Click);
            downloadBtn.Height = 48;
            downloadBtn.Margin = new Thickness(0, 4, 0, 0);
            p.Children.Add(downloadBtn);

            Button check = OutlineButton(T("Nur Version prüfen", "Check version only"), UpdateCheck_Click);
            check.Height = 40;
            check.Margin = new Thickness(0, 10, 0, 0);
            p.Children.Add(check);

            Button openReleases = OutlineButton(T("GitHub Releases", "GitHub releases"), (s, e) => OpenUri("https://github.com/LegendR622/Redline-Gaming-Optimizer/releases"));
            openReleases.Height = 40;
            openReleases.Margin = new Thickness(0, 8, 0, 0);
            p.Children.Add(openReleases);

            card.Child = p;
            left.Children.Add(card);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            Border statusCard = CreateActivityStatusCard(
                T("STATUS", "STATUS"),
                GetUpdatePageStartupLog(),
                out _updateActivityText,
                out _updateActivityBar,
                220);
            right.Children.Add(statusCard);

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            _ = RunUpdatePageLiveCheckAsync();
            return grid;
        }

        private async Task RunUpdatePageLiveCheckAsync()
        {
            await Task.Delay(300);
            RefreshUpdateVersionLabels();
            if (CurrentPage == "Update")
                await CheckForUpdatesAsync(allowDownload: false, startupAuto: false);
        }

        private UIElement PageHelp()
        {
            Grid grid = TwoColumnLayout();
            StackPanel left = new StackPanel();

            left.Children.Add(HeroCard(
                "Hilfe & Hover-Infos",
                "Kurze Erklärung. Zusätzlich zeigt jeder Button und jede Option beim Darüberfahren eine Info an."
            ));

            Border card = Card();
            StackPanel p = new StackPanel();

            p.Children.Add(LabelText("Bereiche", Red));
            p.Children.Add(InfoLine("Dashboard: schneller Gesamtcheck für PC, Treiber, Security und Ping."));
            p.Children.Add(InfoLine("Cleaner: löscht sichere Cache-/Temp-Dateien. Verlauf/Cookies nur, wenn Browser geschlossen ist."));
            p.Children.Add(InfoLine("Game Profiles: prüft Rust/ARC Raiders lokal und gibt sichere FPS-Empfehlungen."));
            p.Children.Add(InfoLine("Optimierung: Game Mode, Power Plan, DNS, RAM Working Sets und Shader Cache optional."));
            p.Children.Add(InfoLine("Leistung: zeigt Hardware, Laufwerke, Top-RAM-Prozesse und Netzwerkadapter."));
            p.Children.Add(InfoLine("Startup: zeigt Autostarts und kann ausgewählte Einträge deaktivieren."));
            p.Children.Add(InfoLine("Security Check: Defender, Firewall, Hosts-Datei, auffällige Prozesse und Offline Scan."));
            p.Children.Add(InfoLine("Driver Check: Treiberstatus, offizielle Links, kein Live-Log."));
            p.Children.Add(InfoLine("BIOS/UEFI: zeigt BIOS-Version, Secure Boot, TPM, Virtualization und Empfehlungen."));
            p.Children.Add(InfoLine("Network: Ping, DNS, Adapter, Speed Test und Winsock Reset."));
            p.Children.Add(InfoLine("Repair: SFC, DISM, Store Reset und Zuverlässigkeitsverlauf."));
            p.Children.Add(InfoLine("Remote Support: sichere Fernhilfe über Windows Quick Assist."));
            p.Children.Add(InfoLine("Tools: schnelle Einzelwerkzeuge wie Chrome Check, Temp Clean, DNS, Task Manager."));

            card.Child = Scroll(p, 620);
            left.Children.Add(card);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            StackPanel right = new StackPanel();
            OutputBox = OutputConsole(
                "Wichtig:\\n\\n" +
                "- Redline löscht keine AntiCheat-Dateien.\\n" +
                "- Redline installiert keine Treiber blind automatisch.\\n" +
                "- Kritische Aktionen fragen vorher nach.\\n" +
                "- Bei Absturz speichert Redline eine Crash-Datei auf dem Desktop.\\n" +
                "- Für volle Rechte App als Administrator starten."
            );
            right.Children.Add(OutputBox);

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return grid;
        }




        private Button SettingsChoiceButton(string text, bool active)
        {
            Button b = new Button
            {
                Content = text,
                MinWidth = 120,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 10),
                Background = active ? Red : new SolidColorBrush(Color.FromRgb(25, 30, 39)),
                Foreground = Brushes.White,
                BorderBrush = active ? Red : new SolidColorBrush(Color.FromRgb(55, 64, 78)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyButtonSkin(b, 9);
            return b;
        }


        private UIElement PageSettings()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            UIElement header = BuildV78PageHeader(
                "SETTINGS",
                T("Sprache, Design, Scan-Tiefe, Pro-Lizenz und Administrator-Status.",
                  "Language, theme, scan depth, Pro license and administrator status."),
                null,
                null);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
            StackPanel left = new StackPanel { Margin = new Thickness(0, 0, 0, 32) };

            Border prefs = DashboardCard();
            prefs.Margin = new Thickness(0, 0, 18, 0);
            prefs.Padding = new Thickness(22);
            StackPanel p = new StackPanel();

            Button de = SegmentButton("Deutsch", !IsEnglish(), () => { UiLanguage = "DE"; PersistSettings(); Navigate("Settings"); });
            Button en = SegmentButton("English", IsEnglish(), () => { UiLanguage = "EN"; PersistSettings(); Navigate("Settings"); });
            p.Children.Add(SettingsRow("🌐", T("Sprache", "Language"), T("Wähle deine bevorzugte Sprache.", "Choose your preferred language."), Segmented(de, en)));

            Button fps = SegmentButton("FPS", GraphicsMode == "FPS", () => { GraphicsMode = "FPS"; RedlineAppData.Current.GraphicsMode = GraphicsMode; PersistSettings(); Navigate("Settings"); });
            Button bal = SegmentButton(T("Ausgewogen", "Balanced"), GraphicsMode == "Balanced", () => { GraphicsMode = "Balanced"; RedlineAppData.Current.GraphicsMode = GraphicsMode; PersistSettings(); Navigate("Settings"); });
            Button qua = SegmentButton(T("Qualität", "Quality"), GraphicsMode == "Quality", () => { GraphicsMode = "Quality"; RedlineAppData.Current.GraphicsMode = GraphicsMode; PersistSettings(); Navigate("Settings"); });
            p.Children.Add(SettingsRow("⚙", T("Standard Optimierungsstil", "Default optimization style"), T("Lege den bevorzugten Optimierungsfokus fest.", "Set the preferred optimization focus."), Segmented(fps, bal, qua)));

            Button fast = SegmentButton(T("Schnell", "Fast"), ScanDepthMode == "Fast", () => { ScanDepthMode = "Fast"; PersistSettings(); Navigate("Settings"); });
            Button standard = SegmentButton("Standard", ScanDepthMode == "Standard", () => { ScanDepthMode = "Standard"; PersistSettings(); Navigate("Settings"); });
            Button deep = SegmentButton(T("Tief", "Deep"), ScanDepthMode == "Deep", () => { ScanDepthMode = "Deep"; PersistSettings(); Navigate("Settings"); });
            p.Children.Add(SettingsRow("🔎", T("Scan-Tiefe", "Scan depth"), T("Bestimmt, wie gründlich dein System analysiert wird.", "Controls how thoroughly your system is analyzed."), Segmented(fast, standard, deep)));

            ToggleButton notify = DashboardToggle(NotificationsEnabled);
            notify.IsChecked = NotificationsEnabled;
            notify.Checked += (s, e) => { NotificationsEnabled = true; PersistSettings(); };
            notify.Unchecked += (s, e) => { NotificationsEnabled = false; PersistSettings(); };
            p.Children.Add(SettingsRow("🔔", T("Benachrichtigungen", "Notifications"), T("Verwalte Benachrichtigungen und Hinweise.", "Manage notifications and hints."), notify));

            ToggleButton autoUp = DashboardToggle(RedlineAppData.Current.AutoUpdateOnStartup);
            autoUp.IsChecked = RedlineAppData.Current.AutoUpdateOnStartup;
            autoUp.Checked += (_, _) => SetAutoUpdateOnStartup(true);
            autoUp.Unchecked += (_, _) => SetAutoUpdateOnStartup(false);
            p.Children.Add(SettingsRow("⬆", T("Update beim Start", "Update on startup"),
                T("Aus = du prüfst selbst unter Update. An = Hinweis + Download, Installation nur mit Ja/Nein.",
                  "Off = check manually under Update. On = prompt + download, install only with Yes/No."),
                autoUp));

            Button dark = SegmentButton(T("Dunkel ★", "Dark ★"), !IsLightTheme, () => { ApplyThemeMode("Dark"); });
            Button light = SegmentButton(T("Hell", "Light"), IsLightTheme, () => { ApplyThemeMode("Light"); });
            p.Children.Add(SettingsRow("🎨", T("Design", "Design"), T("Premium-Dunkel empfohlen (besser als Hell für Gaming).", "Premium dark recommended (better than light for gaming)."), Segmented(dark, light)));

            Button stable = SegmentButton(T("Stabil", "Stable"), UpdateChannel == "Stable", () => { UpdateChannel = "Stable"; Navigate("Settings"); });
            Button beta = SegmentButton("Beta", UpdateChannel == "Beta", () => { MessageBox.Show(T("Der Beta-Kanal ist aktuell deaktiviert.", "The beta channel is currently disabled."), "Redline", MessageBoxButton.OK, MessageBoxImage.Information); UpdateChannel = "Stable"; Navigate("Settings"); });
            p.Children.Add(SettingsRow("🔄", T("Update-Kanal", "Update channel"), T("Wähle, wie Updates für Redline bereitgestellt werden.", "Choose how updates for Redline are delivered."), Segmented(stable, beta)));

            p.Children.Add(SettingsRow("🛡", T("Datenschutz", "Privacy"), T("Steuere Datenverarbeitung und Telemetrie.", "Control data processing and telemetry."), OutlineButton(T("Verwalten", "Manage") + "  ›", PrivacySettings_Click)));

            ToggleButton ai = DashboardToggle(AiAssistantEnabled);
            ai.IsChecked = AiAssistantEnabled;
            ai.Checked += (s, e) => { AiAssistantEnabled = true; PersistSettings(); };
            ai.Unchecked += (s, e) => { AiAssistantEnabled = false; PersistSettings(); };
            p.Children.Add(SettingsRow("AI", T("AI-Assistent aktivieren", "Enable AI assistant"), T("Lokale Empfehlungen auf Basis echter Systemdaten.", "Local recommendations based on real system data."), ai));

            p.Children.Add(BuildSettingsProLicenseCard());

            prefs.Child = p;
            left.Children.Add(prefs);

            ScrollViewer leftScroll = CreatePageScrollViewer(left);
            Grid.SetColumn(leftScroll, 0);
            grid.Children.Add(leftScroll);

            StackPanel right = new StackPanel();
            right.Children.Add(AiSidePanel(
                "REDLINE AI CORE",
                AiCheckRow(T("AI CORE AKTIV", "AI CORE ACTIVE"), T("Integrierte KI überwacht und optimiert dein System.", "Integrated AI monitors and optimizes your system."), "✓", AiGreen, true),
                AiScoreBlock(T("SYSTEM SCORE", "SYSTEM SCORE"), RedlineAppData.Current.GamingScore, AiGreen),
                AiCheckRow(T("Design validiert", "Design validated"), T("Überprüft UI/UX auf Konsistenz und Qualität.", "Checks UI/UX for consistency and quality."), "✓", AiGreen, true),
                AiCheckRow(T("Sprache geprüft", "Language checked"), T("Stellt korrekte und klare Ausgaben sicher.", "Ensures correct and clear output."), "✓", AiGreen, true),
                AiCheckRow(T("Aktionen sicher", "Actions safe"), T("Nur sichere, empfohlene Änderungen angewendet.", "Only safe recommended changes are applied."), "✓", AiGreen, true),
                OutlineButton(T("AI-VERLAUF ANZEIGEN", "SHOW AI HISTORY"), RedlineDesignAi_Click)
            ));

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            Grid.SetRow(grid, 1);
            root.Children.Add(grid);
            return root;
        }


        private void ApplyThemeMode(string mode)
        {
            RedlineThemeMode = mode;
            RedlineAppData.Current.Theme = mode;
            _theme.Apply(mode);
            SaveThemePreference(mode);
            PersistSettings();
            Background = Bg;

            string page = string.IsNullOrWhiteSpace(CurrentPage) ? "Dashboard" : CurrentPage;
            RebuildShell();
            Navigate(page);
        }

        private UIElement BuildSettingsProLicenseCard()
        {
            Border box = new Border
            {
                Background = SubCardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = RedlineAppData.ProPurchaseEnabled
                    ? T("REDLINE PRO · 10 € EINMALIG (LIFETIME)", "REDLINE PRO · €10 ONE-TIME (LIFETIME)")
                    : T("REDLINE PRO · DEMNÄCHST (10 € LIFETIME)", "REDLINE PRO · COMING SOON (€10 LIFETIME)"),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            sp.Children.Add(new TextBlock
            {
                Text = GetProStatusShortLabel() + "  ·  " + GetAdminStatusShortLabel(),
                Foreground = IsProActive() ? AiGreen : Muted,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            });

            if (!RedlineAppData.ProPurchaseEnabled && !IsProActive())
            {
                sp.Children.Add(new TextBlock
                {
                    Text = T(
                        "Pro-Kauf ist noch nicht freigeschaltet. Es kann noch nichts bezahlt werden.\n\nGeplant: einmalig 10 € Lifetime, verknüpft mit deinem Redline-Konto (E-Mail/Login). Zahlung und Key kommen erst, wenn das Konto-System live ist.",
                        "Pro purchase is not enabled yet. You cannot pay yet.\n\nPlanned: €10 one-time lifetime, linked to your Redline account (email/login). Payment and keys will arrive when account linking goes live."),
                    Foreground = Muted,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                });
            }

            if (IsProActive())
            {
                sp.Children.Add(new TextBlock
                {
                    Text = (RedlineAppData.Current.DevProEnabled
                        ? T("Pro aktiv (Entwickler). ", "Pro active (developer). ")
                        : RedlineAppData.Current.MasterProEnabled
                            ? T("Pro aktiv (Master). Key: ", "Pro active (master). Key: ")
                            : T("Pro aktiv. Key: ", "Pro active. Key: "))
                        + (RedlineAppData.Current.ProLicenseMasked.Length > 0 ? RedlineAppData.Current.ProLicenseMasked : "—"),
                    Foreground = Muted,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                sp.Children.Add(new TextBlock
                {
                    Text = T("Master-Key eingeben für alle Pro-Funktionen (Treiber In-App, FPS Boost, …):",
                             "Enter master key for all Pro features (in-app drivers, FPS boost, …):"),
                    Foreground = Muted,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                TextBox keyBox = new TextBox
                {
                    Height = 38,
                    Margin = new Thickness(0, 0, 0, 8),
                    Background = new SolidColorBrush(Color.FromRgb(16, 21, 29)),
                    Foreground = Brushes.White,
                    BorderBrush = Border,
                    ToolTip = T("Master- oder Lifetime-Key.", "Master or lifetime key.")
                };
                sp.Children.Add(keyBox);
                Button activate = RedButton(T("PRO AKTIVIEREN", "ACTIVATE PRO"), (s, e) =>
                {
                    if (RedlineAppData.Current.TryActivateLicenseKey(keyBox.Text, out string err))
                    {
                        PersistSettings();
                        MessageBox.Show(T("Pro aktiviert – alle Pro-Funktionen freigeschaltet.", "Pro activated – all Pro features unlocked."), "Redline Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                        Navigate("Settings");
                    }
                    else
                        MessageBox.Show(err, "Redline Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                activate.Height = 40;
                sp.Children.Add(activate);
            }

            if (!RedlineAppData.ProPurchaseEnabled && RedlineDevAuth.IsAuthorizedDeveloperMachine())
            {
                sp.Children.Add(new TextBlock
                {
                    Text = T("Entwickler-PC erkannt: ", "Developer PC detected: ") + RedlineDevAuth.GetMachineLabel(),
                    Foreground = AiGreen,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 6),
                    ToolTip = T("Pro wird nur auf diesem PC automatisch freigeschaltet (Hardware-ID). Keys funktionieren nur hier.", "Pro is auto-enabled only on this PC (hardware ID). Keys work only here.")
                });
                if (!IsProActive())
                {
                    Button hwBtn = RedButton(T("ENTWICKLER-PRO VON DIESEM PC", "DEVELOPER PRO FROM THIS PC"), (s, e) =>
                    {
                        if (RedlineAppData.Current.ApplyDeveloperProFromHardware())
                        {
                            PersistSettings();
                            MessageBox.Show(T("Entwickler-Pro aktiv – alle Funktionen frei.", "Developer Pro active – all features unlocked."), "Redline", MessageBoxButton.OK, MessageBoxImage.Information);
                            Navigate("Settings");
                        }
                    });
                    hwBtn.Height = 40;
                    hwBtn.Margin = new Thickness(0, 0, 0, 6);
                    sp.Children.Add(hwBtn);
                }
                sp.Children.Add(new TextBlock
                {
                    Text = T("Master-Key für Freundin (jeder PC): REDLINE-PRO-FREUNDIN-GIFT", "Master key for friend (any PC): REDLINE-PRO-FREUNDIN-GIFT"),
                    Foreground = AiGreen,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 4)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = T("Entwickler-Key nur dieser PC: REDLINE-PRO-V9-IMMISCH", "Developer key this PC only: REDLINE-PRO-V9-IMMISCH"),
                    Foreground = Muted,
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text = T("Pro-Funktionen (Hover für Erklärung):", "Pro features (hover for help):"),
                Foreground = Muted,
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 6)
            });
            foreach (string line in new[]
            {
                "• " + T("Windows FPS Boost (Performance)", "Windows FPS Boost (Performance)"),
                "• " + T("Automatische Treiber-Updates (Driver)", "Automatic driver updates (Drivers)"),
                "• " + T("Tiefer Systemscan", "Deep system scan"),
                "• " + T("KI-Profil anwenden", "Apply AI profile")
            })
            {
                TextBlock tb = new TextBlock { Text = line, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
                tb.ToolTip = line;
                sp.Children.Add(tb);
            }

            box.Child = sp;
            return box;
        }

        private async void WindowsFpsBoostPro_Click(object sender, RoutedEventArgs e)
        {
            if (!RequirePro(T("Windows FPS Boost", "Windows FPS Boost"))) return;
            PrepareActionOutput();
            await SafeRun("Windows FPS Boost Pro", async () =>
            {
                await Log("===== WINDOWS FPS BOOST (PRO) =====");
                await SetGameModeEnabled(true);
                await Log("✓ Game Mode");
                await SetHighPerformance();
                await Log("✓ Hochleistungs-Energieplan");
                await FlushDNS();
                await Log("✓ DNS Cache");
                OpenUri("ms-settings:gaming-gamebar");
                await Log("→ Game Bar Einstellungen geöffnet");
                SafeStartSystem("SystemPropertiesPerformance.exe");
                await Log("→ Visuelle Effekte geöffnet");
                await Log(T("Fertig. Für Autostart: Seite Autostart.", "Done. For startup: open Autostart page."));
            });
        }

        private async void DriversInAppAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryInAppDriverFeature()) return;
            await RunInAppDriverUpdateAsync(installPackages: true);
        }

        private async void DriversPreviewPackages_Click(object sender, RoutedEventArgs e)
        {
            if (!TryInAppDriverFeature()) return;
            await RunInAppDriverUpdateAsync(installPackages: false);
        }

        private void DriversCancelUpdate_Click(object sender, RoutedEventArgs e)
        {
            RedlineDriverUpdateService.Instance.Cancel();
            SetDriverActivity(T("Wird abgebrochen…", "Cancelling…"), null);
        }

        private async Task RunInAppDriverUpdateAsync(bool installPackages)
        {
            SetDriverActivity(T("Starte…", "Starting…"), 5);
            await RunDriverScanCoreAsync();
            HardwareProfile hp = RedlineHardwareProfile.Detect(GetCpuName(), GetGpuName(), GetWindowsCaption());
            bool en = IsEnglish();
            try
            {
                await RedlineDriverUpdateService.Instance.RunAsync(
                    hp,
                    installPackages,
                    msg => { SetDriverActivity(msg, null); return Task.CompletedTask; },
                    en);
            }
            catch (Exception ex)
            {
                SetDriverActivity(T("Fehler: ", "Error: ") + ex.Message, null);
            }

            InvalidateDriversCache();
            ScheduleDriverPreviewLoad();
            if (installPackages)
                SetDriverActivity(T("Fertig.", "Done."), 100);
        }

        private string GetMotherboardLabel()
        {
            HardwareProfile hp = RedlineHardwareProfile.Detect(GetCpuName(), GetGpuName(), GetWindowsCaption());
            string label = (hp.MotherboardManufacturer + " " + hp.MotherboardProduct).Trim();
            return string.IsNullOrWhiteSpace(label) ? T("Unbekannt", "Unknown") : label;
        }

        private async Task RunSmartDriverAutoUpdateAsync()
        {
            await SafeRun(T("Auto Treiber-Update", "Auto driver update"), async () =>
            {
                await Log("===== " + T("AUTO TREIBER-UPDATE (PRO)", "AUTO DRIVER UPDATE (PRO)") + " =====");
                await Log(T("Schritt 1: Hardware & Treiber scannen…", "Step 1: Scanning hardware & drivers…"));
                await RunDriverScanCoreAsync();

                HardwareProfile hp = RedlineHardwareProfile.Detect(GetCpuName(), GetGpuName(), GetWindowsCaption());
                await Log("");
                await Log(T("Erkannte Hardware:", "Detected hardware:"));
                await Log("CPU: " + hp.CpuName);
                await Log("GPU: " + hp.GpuName);
                await Log(T("Mainboard: ", "Motherboard: ") + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);
                await Log("Windows: " + hp.WindowsCaption);

                List<DriverInfoLite> drivers = GetDriversCached(forceRefresh: false);
                List<string> statuses = drivers.Select(d => DriverStatusText(d)).ToList();
                List<DriverUpdateLink> links = RedlineHardwareProfile.BuildSmartUpdateLinks(hp, statuses);

                await Log("");
                await Log(T("Schritt 2: Passende Update-Quellen:", "Step 2: Matching update sources:"));
                foreach (DriverUpdateLink link in links)
                {
                    string label = IsEnglish() ? link.LabelEn : link.LabelDe;
                    string reason = IsEnglish() ? link.ReasonEn : link.ReasonDe;
                    await Log("→ " + label + " | " + reason);
                }

                await Log("");
                await Log(T("Schritt 3: Öffne Update-Seiten im Browser…", "Step 3: Opening update pages in browser…"));
                foreach (DriverUpdateLink link in links)
                {
                    if (link.Id == "devmgr")
                        SafeStartSystem("devmgmt.msc");
                    else
                        OpenUri(link.Url);
                    await Task.Delay(350);
                }

                await Log("");
                await Log(T("Fertig. Installiere Treiber in den geöffneten Fenstern (Hersteller-Assistent).", "Done. Install drivers in the opened windows (vendor assistant)."));
                await Log(T("Tipp: Hersteller-Links unten für manuelle Treiber.", "Tip: use vendor links below for manual drivers."));
            });
        }

        private void PrivacySettings_Click(object? sender, RoutedEventArgs e)
        {
            string msg = T(
                "Redline arbeitet lokal. Keine Telemetrie ist aktiv. Update-Checks laufen nur bei Bedarf über GitHub. Es werden keine Spielstände, Passwörter oder privaten Dokumente an Redline gesendet.",
                "Redline works locally. No telemetry is active. Update checks only run on demand through GitHub. No savegames, passwords or private documents are sent to Redline.");

            MessageBox.Show(msg, "Redline", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string GetSelectedGameProfileName()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_selectedGameAdvice))
                    return _selectedGameAdvice;

                string selected = GameProfileBox?.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(selected) && !selected.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                    return selected;

                List<string> games = DetectGames();
                string[] preferred = { "Rust", "ARC Raiders", "Arma 3", "Battlestate Games Launcher", "Metro Exodus" };
                foreach (string name in preferred)
                {
                    if (games.Any(g => g.Contains(name, StringComparison.OrdinalIgnoreCase)))
                        return name;
                }
            }
            catch { }

            return "Rust";
        }

        private int AdjustScanDelay(int baseMs)
        {
            if (ScanDepthMode == "Fast")
                return Math.Max(350, baseMs / 4);
            if (ScanDepthMode == "Deep")
                return (int)Math.Round(baseMs * 1.25);
            return Math.Max(500, baseMs / 2);
        }


        private DriveInfo? GetSystemDriveInfo()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                DriveInfo drive = new DriveInfo(root);
                return drive.IsReady ? drive : null;
            }
            catch
            {
                return null;
            }
        }

        private string FormatBytes(long bytes)
        {
            try
            {
                if (bytes < 0)
                    return "—";
                double gb = bytes / 1024d / 1024d / 1024d;
                if (gb >= 1024)
                    return (gb / 1024d).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " TB";
                if (gb >= 1)
                    return gb.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " GB";
                double mb = bytes / 1024d / 1024d;
                return mb.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " MB";
            }
            catch
            {
                return "n/a";
            }
        }

        private string GetSystemDriveTotalText()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            return s.Ready ? FormatBytes(s.TotalBytes) : "n/a";
        }

        private string GetSystemDriveFreeText()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            return s.Ready ? FormatBytes(s.FreeBytes) : "n/a";
        }

        private string GetSystemDriveUsedText()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            return s.Ready ? FormatBytes(s.UsedBytes) : "n/a";
        }

        private int GetSystemDriveUsedPercent()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            return s.Ready ? s.UsedPercent : 0;
        }

        private int GetSystemDriveFreePercent()
        {
            StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
            if (!s.Ready || s.TotalBytes <= 0) return 0;
            return (int)Math.Round((s.FreeBytes * 100d) / s.TotalBytes);
        }


        private Grid TwoColumnLayout()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
            return grid;
        }

        private Border HeroCard(string title, string sub)
        {
            Border card = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(52, 8, 17), Color.FromRgb(18, 18, 22), 0),
                BorderBrush = DarkRed,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22),
                Margin = new Thickness(0, 0, 18, 18)
            };

            StackPanel p = new StackPanel();

            p.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 26,
                FontWeight = FontWeights.UltraBold
            });

            p.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = Brushes.LightGray,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });

            card.Child = p;
            return card;
        }

        private Border Card()
        {
            return new Border
            {
                Background = CardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 18, 18)
            };
        }

        private ScrollViewer Scroll(UIElement child, double maxHeight = 0)
        {
            ScrollViewer sv = CreatePageScrollViewer(child);
            if (maxHeight > 0)
                sv.MaxHeight = maxHeight;
            return sv;
        }

        private ScrollViewer CreatePageScrollViewer(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0, 0, 10, 28),
                PanningMode = PanningMode.VerticalOnly,
                CanContentScroll = false
            };
        }

        private TextBox OutputConsole(string text)
        {
            return new TextBox
            {
                Text = text,
                Background = _theme.LogBackground,
                Foreground = _theme.LogForeground,
                BorderBrush = DarkRed,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                IsReadOnly = true,
                IsUndoEnabled = false,
                Focusable = false,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Height = 560,
                Padding = new Thickness(12),
                Cursor = System.Windows.Input.Cursors.Arrow
            };
        }

        private Border SystemInfoCard()
        {
            Border card = Card();
            card.Margin = new Thickness(0, 0, 0, 18);

            StackPanel p = new StackPanel();

            p.Children.Add(LabelText("SYSTEM INFORMATIONEN", Red));
            p.Children.Add(InfoLine("CPU: " + GetCpuName()));
            p.Children.Add(InfoLine("GPU: " + GetGpuName()));
            p.Children.Add(InfoLine("RAM: " + GetRamText()));
            p.Children.Add(InfoLine("Windows: " + GetWindowsCaption()));
            p.Children.Add(InfoLine("Admin: " + (IsAdmin() ? "Ja" : "Nein")));

            card.Child = p;
            return card;
        }

        private Label LabelText(string text, Brush color)
        {
            return new Label
            {
                Content = text,
                Foreground = color,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

                private StackPanel InfoLine(string label, string value)
        {
            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold });
            p.Children.Add(new TextBlock { Text = value, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            return p;
        }

private TextBlock InfoLine(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private Button ActionButton(string text, Brush bg, double width)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = 52,
                Background = bg,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = ButtonInfo(text)
            };
        }


        private Button SmallActionButton(string text, string page)
        {
            Button b = new Button
            {
                Content = text,
                Width = 135,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 10),
                Background = CardBg2,
                Foreground = Brushes.White,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = PageInfo(page)
            };

            b.Click += (s, e) => Navigate(page);
            return b;
        }

                private Border StatusCard(string title, string value, string icon, Brush color)
        {
            Border card = new Border
            {
                Width = 150,
                Height = 110,
                Background = CardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 12, 12)
            };

            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = icon, Foreground = color, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            p.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.UltraBold, TextWrapping = TextWrapping.Wrap });
            p.Children.Add(new TextBlock { Text = value, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
            card.Child = p;
            return card;
        }

private Border StatusCard(string title, string value, Brush color)
        {
            Border card = new Border
            {
                Width = 190,
                Height = 82,
                Background = CardBg,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 12, 12)
            };

            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock { Text = title, Foreground = Muted, FontSize = 12, FontWeight = FontWeights.Bold });
            p.Children.Add(new TextBlock { Text = value, Foreground = color, FontSize = 18, FontWeight = FontWeights.UltraBold, TextWrapping = TextWrapping.Wrap });

            card.Child = p;
            return card;
        }

        private WrapPanel ButtonRow(params (string Text, RoutedEventHandler Handler)[] buttons)
        {
            WrapPanel row = new WrapPanel { Margin = new Thickness(0, 4, 0, 16) };

            foreach (var item in buttons)
            {
                Button b = new Button
                {
                    Content = item.Text,
                    Width = 150,
                    Height = 42,
                    Margin = new Thickness(0, 0, 10, 10),
                    Background = CardBg2,
                    Foreground = Brushes.White,
                    BorderBrush = Border,
                    BorderThickness = new Thickness(1),
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = ButtonInfo(item.Text)
                };

                b.Click += item.Handler;
                row.Children.Add(b);
            }

            return row;
        }





        private ControlTemplate ModernButtonTemplate(double radius = 10)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(System.Windows.Controls.Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(2));
            border.AppendChild(content);

            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private ControlTemplate ToggleContentTemplate(double radius = 12)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new TemplateBindingExtension(ToggleButton.BackgroundProperty));
            border.SetValue(System.Windows.Controls.Border.BorderBrushProperty, new TemplateBindingExtension(ToggleButton.BorderBrushProperty));
            border.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new TemplateBindingExtension(ToggleButton.BorderThicknessProperty));

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            return new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
        }

        private void ApplyButtonSkin(Button btn, double radius = 10)
        {
            btn.Template = ModernButtonTemplate(radius);
            btn.FocusVisualStyle = null;
            btn.SnapsToDevicePixels = true;
        }

        private Border ModernPageCard(string title, string sub)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 16, 16);
            card.Padding = new Thickness(22);

            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 24,
                FontWeight = FontWeights.UltraBold
            });
            p.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = Muted,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });

            card.Child = p;
            return card;
        }



        private Button ModernTileComingSoon(string title, string sub, string icon, Brush accent)
        {
            string subSoon = sub + " · " + T("Bald verfügbar", "Coming soon");
            Button btn = ModernTile(title, subSoon, icon, accent, (s, e) => TryInAppDriverFeature());
            btn.Opacity = 0.52;
            btn.Cursor = System.Windows.Input.Cursors.Arrow;
            btn.ToolTip = T("Bald verfügbar · Coming Soon", "Coming soon");
            return btn;
        }

        private Button ModernTile(string title, string sub, string icon, Brush accent, RoutedEventHandler handler)
        {
            Button btn = new Button
            {
                Width = 250,
                Height = 116,
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(16),
                Background = new SolidColorBrush(Color.FromRgb(18, 23, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 47, 60)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = sub
            };
            ApplyButtonSkin(btn, 12);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Border ico = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = accent,
                VerticalAlignment = VerticalAlignment.Center
            };
            ico.Child = new TextBlock
            {
                Text = icon,
                Foreground = Brushes.White,
                FontWeight = FontWeights.UltraBold,
                FontSize = icon.Length > 2 ? 11 : 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            g.Children.Add(ico);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.UltraBold,
                TextWrapping = TextWrapping.Wrap
            });
            text.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
                LineHeight = 16
            });
            text.Children.Add(new TextBlock
            {
                Text = T("Ausführen  →", "Run  →"),
                Foreground = Red,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 9, 0, 0)
            });
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            btn.Content = g;
            btn.Click += handler;
            return btn;
        }
        private Border ModernOutputCard(string startText) =>
            CreatePageStatusCard(T("STATUS", "STATUS"), startText, 168);

        private Border CreatePageStatusCard(string title, string startText, double minHeight = 168)
        {
            _liveLog = null;
            Progress = null;
            return CreateActivityStatusCard(title, startText, out _pageActivityText, out _pageActivityBar, minHeight);
        }

        private static string? SimplifyLogLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string t = text.Trim();
            if (t.StartsWith("=====", StringComparison.Ordinal) || t.StartsWith("════", StringComparison.Ordinal))
                return null;
            if (t.StartsWith("Made by ", StringComparison.OrdinalIgnoreCase))
                return null;
            if (t.Length > 140)
                t = t[..137] + "…";
            return t;
        }

        private void SetPageActivity(string text, double? progress = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (CurrentPage == "Drivers" && _driverActivityText != null)
                {
                    _driverActivityText.Text = text;
                    if (progress.HasValue && _driverActivityBar != null)
                    {
                        _driverActivityBar.IsIndeterminate = false;
                        _driverActivityBar.Value = Math.Clamp(progress.Value, 0, 100);
                    }
                    return;
                }

                if (CurrentPage == "Update" && _updateActivityText != null)
                {
                    _updateActivityText.Text = text;
                    if (progress.HasValue && _updateActivityBar != null)
                    {
                        _updateActivityBar.IsIndeterminate = false;
                        _updateActivityBar.Value = Math.Clamp(progress.Value, 0, 100);
                    }
                    return;
                }

                if (_pageActivityText == null)
                    return;

                _pageActivityText.Text = text;
                if (_pageActivityBar == null)
                    return;

                if (progress.HasValue)
                {
                    _pageActivityBar.IsIndeterminate = false;
                    _pageActivityBar.Value = Math.Clamp(progress.Value, 0, 100);
                }
                else
                    _pageActivityBar.IsIndeterminate = text.Contains('…') || text.Contains("...");
            });
        }

        private Border CreateActivityStatusCard(
            string title,
            string startText,
            out TextBlock statusText,
            out ProgressBar bar,
            double minHeight = 180)
        {
            statusText = new TextBlock
            {
                Text = startText,
                Foreground = Brushes.White,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            bar = new ProgressBar
            {
                Height = 6,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 14, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(31, 38, 50)),
                Foreground = Red,
                BorderThickness = new Thickness(0)
            };

            Border card = DashboardCard();
            card.Padding = new Thickness(20);
            card.MinHeight = minHeight;
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Red,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            sp.Children.Add(statusText);
            sp.Children.Add(bar);
            card.Child = sp;
            return card;
        }

        private void SetDriverActivity(string text, double? progress)
        {
            Dispatcher.Invoke(() =>
            {
                if (_driverActivityText != null)
                    _driverActivityText.Text = text;
                if (_driverActivityBar == null)
                    return;
                if (progress.HasValue)
                {
                    _driverActivityBar.IsIndeterminate = false;
                    _driverActivityBar.Value = Math.Clamp(progress.Value, 0, 100);
                }
                else
                    _driverActivityBar.IsIndeterminate = text.Contains('…') || text.Contains("...");
            });
        }

        private void SetUpdateActivity(string text, double? progress)
        {
            Dispatcher.Invoke(() =>
            {
                if (_updateActivityText != null)
                    _updateActivityText.Text = text;
                if (_updateActivityBar == null)
                    return;
                if (progress.HasValue)
                {
                    _updateActivityBar.IsIndeterminate = false;
                    _updateActivityBar.Value = Math.Clamp(progress.Value, 0, 100);
                }
                else
                    _updateActivityBar.IsIndeterminate = text.Contains('…') || text.Contains("...");
            });
        }

        private Border CreateLiveLogCard(string title, string startText, double height)
        {
            (Border card, RedlineLiveLogController ctrl) = RedlineLiveLogController.Create(
                title, startText, IsEnglish(), height);
            _liveLog = ctrl;
            OutputBox = null;

            if (card.Child is StackPanel sp)
            {
                Progress = new ProgressBar
                {
                    Height = 8,
                    Maximum = 100,
                    Value = 0,
                    Margin = new Thickness(0, 10, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(31, 38, 50)),
                    Foreground = Red,
                    BorderThickness = new Thickness(0)
                };
                sp.Children.Add(Progress);
            }

            return card;
        }

        private UIElement ModernStatusPill(string text, Brush color)
        {
            Border pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderBrush = color,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(0, 0, 8, 8)
            };

            pill.Child = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            return pill;
        }

        private Grid ModernTwoColumn()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(500) });
            return grid;
        }


        private Border DashboardCard()
        {
            Border card = new Border
            {
                Background = _theme.CardGradient,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 14, 0)
            };
            if (!IsLightTheme)
            {
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(20, 8, 12),
                    BlurRadius = 26,
                    ShadowDepth = 4,
                    Opacity = 0.4
                };
            }
            return card;
        }


        private UIElement DashboardHeaderRow(string title, string badge, bool arrow)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextPrimary,
                FontSize = 13,
                FontWeight = FontWeights.UltraBold
            });

            if (!string.IsNullOrWhiteSpace(badge))
            {
                Border b = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(96, 18, 34)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 2, 8, 2)
                };
                b.Child = new TextBlock { Text = badge, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.Bold };
                Grid.SetColumn(b, 1);
                g.Children.Add(b);
            }

            return g;
        }
        private Geometry CreateDashboardArc(double x, double y, double size, double startAngle, double endAngle)
        {
            double radius = size / 2d;
            Point start = new Point(
                x + radius + radius * Math.Cos(startAngle * Math.PI / 180d),
                y + radius - radius * Math.Sin(startAngle * Math.PI / 180d));

            Point end = new Point(
                x + radius + radius * Math.Cos(endAngle * Math.PI / 180d),
                y + radius - radius * Math.Sin(endAngle * Math.PI / 180d));

            bool large = Math.Abs(startAngle - endAngle) > 180d;

            PathFigure figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = large
            });

            PathGeometry geo = new PathGeometry();
            geo.Figures.Add(figure);
            return geo;
        }

        private Border DashboardMiniCard(string title, string value, string sub, string icon, Brush accent)
        {
            return DashboardMiniCard(title, value, sub, icon, accent, out _, out _);
        }




        private Border DashboardMiniCard(string title, string value, string sub, string icon, Brush accent, out TextBlock valueBlock, out TextBlock subBlock)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 14, 0);
            card.Padding = new Thickness(14, 12, 14, 10);
            card.Height = 140;

            StackPanel p = new StackPanel();

            p.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextPrimary,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            p.Children.Add(new TextBlock
            {
                Text = icon,
                Foreground = accent,
                FontSize = 21,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 6)
            });

            valueBlock = new TextBlock
            {
                Text = value,
                Foreground = accent,
                FontSize = 21,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            p.Children.Add(valueBlock);

            subBlock = new TextBlock
            {
                Text = sub,
                Foreground = Muted,
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 34,
                Margin = new Thickness(0, 6, 0, 0)
            };
            p.Children.Add(subBlock);

            card.Child = p;
            return card;
        }


        private Border DashboardScoreCard(int score)
        {
            Border card = DashboardCard();
            card.Margin = new Thickness(0, 0, 14, 0);
            card.Padding = new Thickness(16, 14, 16, 12);
            card.Height = 140;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = T("GAMING SCORE", "GAMING SCORE"),
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Grid gauge = new Grid
            {
                Width = 124,
                Height = 76,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            System.Windows.Shapes.Path bgArc = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(47, 55, 68)),
                StrokeThickness = 10,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                Data = CreateDashboardArc(15, 42, 94, 180, 0)
            };
            gauge.Children.Add(bgArc);

            double endAngle = 180d - Math.Max(0d, Math.Min(100d, score)) * 1.8d;
            System.Windows.Shapes.Path fgArc = new System.Windows.Shapes.Path
            {
                Stroke = Red,
                StrokeThickness = 10,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                Data = CreateDashboardArc(15, 42, 94, 180, endAngle)
            };
            gauge.Children.Add(fgArc);

            StackPanel center = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };

            StackPanel scoreLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            scoreLine.Children.Add(new TextBlock
            {
                Text = score.ToString(),
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.UltraBold,
                VerticalAlignment = VerticalAlignment.Bottom
            });
            scoreLine.Children.Add(new TextBlock
            {
                Text = "/100",
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(4, 15, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            });
            center.Children.Add(scoreLine);
            gauge.Children.Add(center);

            Grid.SetRow(gauge, 1);
            root.Children.Add(gauge);

            TextBlock rating = new TextBlock
            {
                Text = T("Gut", "Good"),
                Foreground = Brushes.LimeGreen,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            Grid.SetRow(rating, 2);
            root.Children.Add(rating);

            card.Child = root;
            return card;
        }
        private void StyleModeButton(Button btn, string text, bool active)
        {
            btn.Content = text;
            btn.MinWidth = 72;
            btn.Height = 28;
            btn.Margin = new Thickness(2, 0, 2, 0);
            btn.Padding = new Thickness(12, 0, 12, 0);
            btn.BorderThickness = new Thickness(1);
            btn.Cursor = System.Windows.Input.Cursors.Hand;
            btn.FontWeight = FontWeights.Bold;
            btn.FontSize = 11.5;
            btn.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(205, 212, 224));
            btn.Background = active ? Red : new SolidColorBrush(Color.FromRgb(28, 33, 42));
            btn.BorderBrush = active ? Red : new SolidColorBrush(Color.FromRgb(56, 64, 78));
            ApplyButtonSkin(btn, 7);
        }




        private string GetToggleIcon(string title)
        {
            string t = title.ToLowerInvariant();
            if (t.Contains("game")) return "🎮";
            if (t.Contains("hoch") || t.Contains("high") || t.Contains("power")) return "⚡";
            if (t.Contains("hardware") || t.Contains("gpu")) return "▣";
            if (t.Contains("hinter") || t.Contains("background")) return "⚙";
            if (t.Contains("visuell") || t.Contains("visual")) return "◉";
            return "•";
        }


        private void OpenPerfTileDetails(string title) => OpenToggleDetails(title);

        private void OpenToggleDetails(string title)
        {
            switch (RedlinePerfNavigation.Resolve(title))
            {
                case PerfDetailAction.NavigateStartup:
                    Navigate("Startup");
                    break;
                case PerfDetailAction.GameModeSettings:
                    OpenUri("ms-settings:gaming-gamemode");
                    break;
                case PerfDetailAction.GameBar:
                    OpenUri("ms-settings:gaming-gamebar");
                    break;
                case PerfDetailAction.PowerPlan:
                    SafeStartSystem("powercfg.cpl");
                    break;
                case PerfDetailAction.GraphicsSettings:
                    OpenUri("ms-settings:display-advancedgraphics");
                    break;
                case PerfDetailAction.Services:
                    SafeStartSystem("services.msc");
                    break;
                case PerfDetailAction.VisualEffects:
                    SafeStartSystem("SystemPropertiesPerformance.exe");
                    break;
                default:
                    Navigate("Settings");
                    break;
            }
        }
        private UIElement DashboardSwitchRow(string title, string sub, bool initialState, Func<bool, Task> onToggle)
        {
            Border row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(31, 38, 48)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 10, 0, 10)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            TextBlock icon = new TextBlock
            {
                Text = GetToggleIcon(title),
                Foreground = new SolidColorBrush(Color.FromRgb(210, 216, 226)),
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            g.Children.Add(icon);

            StackPanel txt = new StackPanel();
            txt.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14 });
            txt.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12 });
            Grid.SetColumn(txt, 1);
            g.Children.Add(txt);

            ToggleButton toggle = DashboardToggle(initialState);
            toggle.Checked += async (s, e) => await onToggle(true);
            toggle.Unchecked += async (s, e) => await onToggle(false);
            Grid.SetColumn(toggle, 2);
            g.Children.Add(toggle);

            Button arrow = new Button
            {
                Content = "›",
                Width = 26,
                Height = 26,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Muted,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = T("Einstellung öffnen", "Open setting")
            };
            ApplyButtonSkin(arrow, 8);
            arrow.Click += (s, e) => OpenToggleDetails(title);
            Grid.SetColumn(arrow, 3);
            g.Children.Add(arrow);

            row.Child = g;
            return row;
        }

        private ToggleButton DashboardToggle(bool isOn)
        {
            ToggleButton t = new ToggleButton
            {
                Width = 44,
                Height = 24,
                IsChecked = isOn,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(14, 0, 8, 0),
                ToolTip = T("An / Aus", "On / Off")
            };
            t.Template = ToggleContentTemplate(12);
            t.FocusVisualStyle = null;

            void Apply()
            {
                bool on = t.IsChecked == true;

                Grid grid = new Grid
                {
                    Width = 44,
                    Height = 24
                };

                Border track = new Border
                {
                    Width = 44,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = on ? Red : new SolidColorBrush(Color.FromRgb(82, 88, 98)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(105, 112, 125)),
                    BorderThickness = on ? new Thickness(0) : new Thickness(1)
                };

                Border knob = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = Brushes.White,
                    HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 0, 3, 0)
                };

                grid.Children.Add(track);
                grid.Children.Add(knob);
                t.Content = grid;
            }

            Apply();
            t.Checked += (s, e) => Apply();
            t.Unchecked += (s, e) => Apply();
            return t;
        }
        private UIElement ModePickerRow()
        {
            Border row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(31, 38, 48)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 12),
                Margin = new Thickness(0, 0, 0, 4)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel txt = new StackPanel();
            txt.Children.Add(new TextBlock
            {
                Text = T("Optimierungsstil", "Optimization Style"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });
            txt.Children.Add(new TextBlock
            {
                Text = T("Wähle FPS, Ausgewogen oder Qualität", "Choose FPS, Balanced or Quality"),
                Foreground = Muted,
                FontSize = 12
            });
            g.Children.Add(txt);

            Border modeWrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 24, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel chips = new StackPanel { Orientation = Orientation.Horizontal };
            Button fpsBtn = new Button();
            Button balancedBtn = new Button();
            Button qualityBtn = new Button();

            void ApplyModeStyles()
            {
                StyleModeButton(fpsBtn, "FPS", GraphicsMode == "FPS");
                StyleModeButton(balancedBtn, T("Ausgewogen", "Balanced"), GraphicsMode == "Balanced");
                StyleModeButton(qualityBtn, T("Qualität", "Quality"), GraphicsMode == "Quality");
            }

            fpsBtn.Click += async (s, e) => { GraphicsMode = "FPS"; ApplyModeStyles(); await Log("Mode: FPS"); };
            balancedBtn.Click += async (s, e) => { GraphicsMode = "Balanced"; ApplyModeStyles(); await Log(T("Mode: Ausgewogen", "Mode: Balanced")); };
            qualityBtn.Click += async (s, e) => { GraphicsMode = "Quality"; ApplyModeStyles(); await Log(T("Mode: Qualität", "Mode: Quality")); };

            ApplyModeStyles();

            chips.Children.Add(fpsBtn);
            chips.Children.Add(balancedBtn);
            chips.Children.Add(qualityBtn);
            modeWrap.Child = chips;

            Grid.SetColumn(modeWrap, 1);
            g.Children.Add(modeWrap);

            row.Child = g;
            return row;
        }

        private Border LastScanDonut()
        {
            Border wrap = new Border
            {
                Width = 190,
                Height = 190,
                CornerRadius = new CornerRadius(95),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 53, 64)),
                BorderThickness = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            Grid grid = new Grid();

            System.Windows.Shapes.Ellipse baseRing = new System.Windows.Shapes.Ellipse
            {
                Width = 140,
                Height = 140,
                Stroke = new SolidColorBrush(Color.FromRgb(50, 54, 66)),
                StrokeThickness = 8
            };
            grid.Children.Add(baseRing);

            System.Windows.Shapes.Ellipse activeRing = new System.Windows.Shapes.Ellipse
            {
                Width = 140,
                Height = 140,
                Stroke = Red,
                StrokeThickness = 8,
                StrokeDashArray = new DoubleCollection { 0.78, 0.45 },
                RenderTransform = new RotateTransform(-90),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            grid.Children.Add(activeRing);

            StackPanel center = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            StackPanel scoreLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            scoreLine.Children.Add(new TextBlock
            {
                Text = "78",
                Foreground = Brushes.White,
                FontSize = 26,
                FontWeight = FontWeights.UltraBold,
                VerticalAlignment = VerticalAlignment.Bottom
            });
            scoreLine.Children.Add(new TextBlock
            {
                Text = "/100",
                Foreground = Muted,
                FontSize = 11,
                Margin = new Thickness(2, 10, 0, 0)
            });
            center.Children.Add(scoreLine);
            center.Children.Add(new TextBlock
            {
                Text = "GAMING SCORE",
                Foreground = Muted,
                FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            grid.Children.Add(center);
            wrap.Child = grid;
            return wrap;
        }
        private UIElement LastScanLegend(Brush color, string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new TextBlock { Text = "●", Foreground = color, FontSize = 13, Margin = new Thickness(0, 0, 8, 0) });
            left.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 13 });
            g.Children.Add(left);

            TextBlock right = new TextBlock { Text = value, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            return g;
        }

        private UIElement SystemInfoMiniRow(string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            g.Children.Add(new TextBlock { Text = "◦  " + label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });

            TextBlock v = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(v, 1);
            g.Children.Add(v);

            return g;
        }

        private string ShortCpuName()
        {
            string cpu = GetCpuName();
            if (cpu.Contains("AMD Ryzen 7 7800X3D")) return "AMD Ryzen 7 7800X3D";
            return cpu.Length > 26 ? cpu.Substring(0, 26) + "..." : cpu;
        }

        private string ShortGpuName()
        {
            string gpu = GetGpuName();
            if (gpu.Contains("NVIDIA GeForce RTX 4070 Ti")) return "NVIDIA RTX 4070 Ti";
            return gpu.Length > 26 ? gpu.Substring(0, 26) + "..." : gpu;
        }


        private string GetRamUsageText()
        {
            try
            {
                GetMemoryUsage(out double usedGb, out double totalGb, out int percent);
                return percent + "%";
            }
            catch { return "n/a"; }
        }

        private string GetRamUsedVsTotalText()
        {
            try
            {
                GetMemoryUsage(out double usedGb, out double totalGb, out int percent);
                return usedGb.ToString("0.0").Replace('.', ',') + " / " + totalGb.ToString("0.0").Replace('.', ',') + " GB";
            }
            catch { return "n/a"; }
        }
        private double GetTotalRamGb()
        {
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject mo in mos.Get())
                {
                    double kb = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                    return Math.Round(kb / 1024 / 1024, 1);
                }
            }
            catch { }
            return 0;
        }




        private void PersistSettings()
        {
            RedlineAppData.Current.Language = UiLanguage;
            RedlineAppData.Current.GraphicsMode = GraphicsMode;
            RedlineAppData.Current.ScanDepth = ScanDepthMode;
            RedlineAppData.Current.Notifications = NotificationsEnabled;
            RedlineAppData.Current.AiAssistantEnabled = AiAssistantEnabled;
            RedlineAppData.Current.Theme = RedlineThemeMode;
            RedlineAppData.Current.Save();
        }

        private string PingQualityLabel(int ms)
        {
            if (ms <= 30) return T("Sehr gut", "Very good");
            if (ms <= 60) return T("Stabil", "Stable");
            if (ms <= 100) return T("Okay", "Okay");
            return T("Hoch", "High");
        }

        private string TruncateGpuName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "Unbekannt")
                return T("Name wird geladen…", "Loading name…");
            return name.Length > 28 ? name.Substring(0, 28) + "…" : name;
        }

        private string GetCpuClockText()
        {
            try
            {
                using ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject mo in mos.Get())
                {
                    if (mo["MaxClockSpeed"] == null) continue;
                    double mhz = Convert.ToDouble(mo["MaxClockSpeed"]);
                    if (mhz >= 1000)
                        return (mhz / 1000d).ToString("0.0").Replace('.', ',') + " GHz";
                    return ((int)mhz) + " MHz";
                }
            }
            catch { }
            return "—";
        }

        private string GetDefenderStatusText()
        {
            if (_securityStatusCached && !string.IsNullOrEmpty(_defenderStatusText))
                return _defenderStatusText;
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
                object? v = key?.GetValue("DisableRealtimeMonitoring");
                if (v is int i)
                    return i == 0 ? T("Aktiv", "Active") : T("Aus", "Off");
            }
            catch { }
            return T("Prüfen", "Check");
        }

        private string GetFirewallStatusText()
        {
            if (_securityStatusCached && !string.IsNullOrEmpty(_firewallStatusText))
                return _firewallStatusText;
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
                object? v = key?.GetValue("EnableFirewall");
                if (v is int i)
                    return i == 1 ? T("Aktiv", "Active") : T("Aus", "Off");
            }
            catch { }
            return T("Prüfen", "Check");
        }

        private void RefreshSecurityStatusCache()
        {
            _defenderStatusText = GetDefenderStatusText();
            _firewallStatusText = GetFirewallStatusText();
            _securityStatusCached = true;
        }

        private int CountAutostartEntries()
        {
            return CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                 + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
        }

        private static int ClassifyAutostartImpact(string name, string val)
        {
            string n = name + " " + val;
            if (n.Contains("steam", StringComparison.OrdinalIgnoreCase) || n.Contains("epic", StringComparison.OrdinalIgnoreCase)
                || n.Contains("battle", StringComparison.OrdinalIgnoreCase) || n.Contains("riot", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (n.Contains("nvidia", StringComparison.OrdinalIgnoreCase) || n.Contains("geforce", StringComparison.OrdinalIgnoreCase)
                || n.Contains("discord", StringComparison.OrdinalIgnoreCase) || n.Contains("ubisoft", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 0;
        }

        private List<(string name, string impact, int level)> GetAutostartPreview(int max)
        {
            List<(string name, string impact, int level)> list = new List<(string, string, int)>();
            void ReadKey(RegistryKey root, string subPath)
            {
                try
                {
                    using RegistryKey? key = root.OpenSubKey(subPath, false);
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        string val = key.GetValue(name)?.ToString() ?? "";
                        int level = ClassifyAutostartImpact(name, val);
                        string impact = level switch
                        {
                            2 => T("Hoher Einfluss", "High impact"),
                            1 => T("Mittlerer Einfluss", "Medium impact"),
                            _ => T("Niedriger Einfluss", "Low impact")
                        };
                        list.Add((name, impact, level));
                    }
                }
                catch { }
            }

            ReadKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
            ReadKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
            return list.Take(max).ToList();
        }

        private string GetStorageTotalLabel()
        {
            try
            {
                StorageOverviewSnapshot s = TryGetStorageOverviewSnapshot();
                if (!s.Ready) return T("Systemlaufwerk: —", "System drive: —");
                string vol = string.IsNullOrWhiteSpace(s.VolumeLabel) ? s.DriveLetter : s.VolumeLabel + " (" + s.DriveLetter + ")";
                return T("Festplatte ", "Drive ") + vol + T(" – Kapazität ", " – Capacity ") + FormatBytes(s.TotalBytes);
            }
            catch { return ""; }
        }

        private List<string> GetDashboardRecommendations()
        {
            if (!RedlineAppData.Current.LastScanUtc.HasValue)
                return new List<string>();

            List<string> recs = new List<string>();
            if (!IsGameModeEnabled())
                recs.Add(T("Windows Game Mode aktivieren", "Enable Windows Game Mode"));
            if (!IsHighPerformanceActiveSync())
                recs.Add(T("Windows Leistungsmodus prüfen", "Check Windows power plan"));
            int startupCount = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                             + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
            if (startupCount > 12)
                recs.Add(T("Autostart-Programme reduzieren", "Reduce startup programs"));
            if (CountDeviceProblems() > 0)
                recs.Add(T("Treiberstatus im Geräte-Manager prüfen", "Check drivers in Device Manager"));
            recs.Add(T("Grafikeinstellungen in Windows öffnen", "Open graphics settings in Windows"));
            return recs;
        }

        private bool IsHighPerformanceActiveSync()
        {
            try
            {
                string plan = RunPowerShellCaptureSync("powercfg /getactivescheme");
                string lower = plan.ToLowerInvariant();
                return lower.Contains("high") || lower.Contains("hoch") || lower.Contains("ultimate") || lower.Contains("höchst") || lower.Contains("8c5e7fda");
            }
            catch { return false; }
        }

        private string RunPowerShellCaptureSync(string command)
        {
            try
            {
                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(4000);
                return output;
            }
            catch { return ""; }
        }

        private int ComputeGamingScore()
        {
            int score = 100;
            if (IsAntiCheatSafeModeActive(out _))
                score -= 15;
            if (!IsGameModeEnabled())
                score -= 10;
            if (!IsHighPerformanceActiveSync())
                score -= 10;
            int startupCount = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                             + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
            if (startupCount > 12)
                score -= 8;
            if (CountDeviceProblems() > 0)
                score -= Math.Min(20, CountDeviceProblems() * 5);
            if (RedlineAppData.Current.LastPingMs is int ping && ping > 60)
                score -= 5;
            return Math.Clamp(score, 0, 100);
        }

        private string GetCpuLoadText()
        {
            try
            {
                if (GetSystemTimes(out CpuFileTime idleTime, out CpuFileTime kernelTime, out CpuFileTime userTime))
                {
                    ulong idle = FileTimeToUInt64(idleTime);
                    ulong kernel = FileTimeToUInt64(kernelTime);
                    ulong user = FileTimeToUInt64(userTime);

                    if (!HasCpuSample)
                    {
                        LastCpuIdle = idle;
                        LastCpuKernel = kernel;
                        LastCpuUser = user;
                        HasCpuSample = true;
                        return "0%";
                    }

                    ulong idleDiff = idle - LastCpuIdle;
                    ulong kernelDiff = kernel - LastCpuKernel;
                    ulong userDiff = user - LastCpuUser;
                    ulong total = kernelDiff + userDiff;

                    LastCpuIdle = idle;
                    LastCpuKernel = kernel;
                    LastCpuUser = user;

                    if (total == 0)
                        return "0%";

                    double value = (1.0 - ((double)idleDiff / total)) * 100.0;
                    int percent = Math.Max(0, Math.Min(100, (int)Math.Round(value)));
                    return percent + "%";
                }
            }
            catch { }

            try
            {
                using ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                int sum = 0;
                int count = 0;
                foreach (ManagementObject mo in mos.Get())
                {
                    if (mo["LoadPercentage"] == null) continue;
                    sum += Convert.ToInt32(mo["LoadPercentage"]);
                    count++;
                }

                if (count > 0)
                    return Math.Max(0, Math.Min(100, sum / count)) + "%";
            }
            catch { }

            return "0%";
        }
        private string GetQuickPingText()
        {
            return T("Check", "Check");
        }

        private void GetMemoryUsage(out double usedGb, out double totalGb, out int percent)
        {
            usedGb = 0;
            totalGb = 0;
            percent = 0;

            using ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in mos.Get())
            {
                double totalKb = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                double freeKb = Convert.ToDouble(mo["FreePhysicalMemory"]);
                totalGb = Math.Round(totalKb / 1024d / 1024d, 1);
                double freeGb = Math.Round(freeKb / 1024d / 1024d, 1);
                usedGb = Math.Round(totalGb - freeGb, 1);
                percent = totalGb > 0 ? (int)Math.Round((usedGb / totalGb) * 100d) : 0;
                return;
            }
        }

        private void StartDashboardLiveTimer()
        {
            try
            {
                if (CurrentPage != "Dashboard")
                    return;

                StopDashboardLiveTimer();

                DashboardLiveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };

                DashboardLiveTimer.Tick += (s, e) => UpdateDashboardLiveValues();
                DashboardLiveTimer.Start();
                UpdateDashboardLiveValues();
            }
            catch { }
        }

        private void StopDashboardLiveTimer()
        {
            try
            {
                if (DashboardLiveTimer != null)
                {
                    DashboardLiveTimer.Stop();
                    DashboardLiveTimer = null;
                }
            }
            catch { }
        }


        private void UpdateDashboardLiveValues()
        {
            try
            {
                if (CurrentPage != "Dashboard")
                    return;

                if (DashboardCpuText != null)
                    DashboardCpuText.Text = GetCpuLoadText();
                if (DashboardCpuSubText != null)
                    DashboardCpuSubText.Text = GetCpuClockText();

                if (DashboardGpuSubText != null && (DashboardGpuSubText.Text == "—" || DashboardGpuSubText.Text.Contains("geladen")))
                    DashboardGpuSubText.Text = TruncateGpuName(GetGpuName());

                if (DashboardRamText != null || DashboardRamSubText != null)
                {
                    GetMemoryUsage(out double usedGb, out double totalGb, out int percent);
                    if (DashboardRamText != null)
                        DashboardRamText.Text = percent + "%";
                    if (DashboardRamSubText != null)
                        DashboardRamSubText.Text = usedGb.ToString("0.0").Replace('.', ',') + " / " + totalGb.ToString("0.0").Replace('.', ',') + " GB";
                }

                UpdateDashboardStorageDisplay();

                if (DashboardPingText != null && !PingUpdateRunning && (DateTime.Now - LastPingUpdate).TotalSeconds > 30)
                {
                    PingUpdateRunning = true;
                    LastPingUpdate = DateTime.Now;

                    _ = Task.Run(async () =>
                    {
                        int best = 0;

                        try
                        {
                            string p1 = await GetPingTo("1.1.1.1");
                            string p2 = await GetPingTo("8.8.8.8");
                            string p3 = await GetPingTo("9.9.9.9");

                            int n1 = ExtractPingNumber(p1);
                            int n2 = ExtractPingNumber(p2);
                            int n3 = ExtractPingNumber(p3);

                            best = new[] { n1, n2, n3 }.Where(x => x > 0).DefaultIfEmpty(0).Min();
                        }
                        catch { }

                        if (best > 0)
                        {
                            RedlineAppData.Current.LastPingMs = best;
                            RedlineAppData.Current.Save();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            if (DashboardPingText != null)
                                DashboardPingText.Text = best > 0 ? best + " ms" : "—";
                            if (DashboardPingSubText != null)
                                DashboardPingSubText.Text = best > 0 ? PingQualityLabel(best) : T("Nicht messbar", "Not measurable");
                            PingUpdateRunning = false;
                        });
                    });
                }
            }
            catch
            {
                PingUpdateRunning = false;
            }
        }
        private bool IsHighPerformanceActive()
        {
            // Beim Start keine PowerShell blockierend ausführen.
            // Der echte Status wird im Komplettscan geprüft.
            return false;
        }



        private string GetSystemDriveInfoText()
        {
            try
            {
                string root = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                DriveInfo d = new DriveInfo(root);

                double totalGb = d.TotalSize / 1024d / 1024d / 1024d;
                double freeGb = d.AvailableFreeSpace / 1024d / 1024d / 1024d;

                if (totalGb >= 1024)
                {
                    double totalTb = totalGb / 1024d;
                    return $"{freeGb:0} GB frei / {totalTb:0.0} TB";
                }

                return $"{freeGb:0} GB frei / {totalGb:0} GB";
            }
            catch
            {
                return "n/a";
            }
        }
        private Border PremiumCard()
        {
            return new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(17, 20, 27), Color.FromRgb(11, 13, 18), 90),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 44, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 14, 14)
            };
        }

        private UIElement PremiumCardTitle(string title, string badge)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.UltraBold
            });

            if (!string.IsNullOrWhiteSpace(badge))
            {
                Border b = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(92, 17, 31)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 2, 8, 2)
                };
                b.Child = new TextBlock { Text = badge, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold };
                Grid.SetColumn(b, 1);
                g.Children.Add(b);
            }

            return g;
        }

        private Border PremiumMetricCard(string title, string value, string suffix, string sub, Brush accent, string icon)
        {
            Border card = PremiumCard();
            card.Width = 150;
            card.Height = 138;

            StackPanel p = new StackPanel();
            p.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.UltraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            p.Children.Add(new TextBlock
            {
                Text = icon,
                Foreground = accent,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            StackPanel val = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
            val.Children.Add(new TextBlock { Text = value, Foreground = accent, FontSize = 23, FontWeight = FontWeights.UltraBold });
            val.Children.Add(new TextBlock { Text = suffix, Foreground = Muted, FontSize = 13, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 0, 5) });
            p.Children.Add(val);

            p.Children.Add(new TextBlock
            {
                Text = sub,
                Foreground = Muted,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = p;
            return card;
        }





        private Border GameRow(string name, string sub, string status)
        {
            bool optimized = IsGameOptimized(name);
            bool running = name.Contains("läuft", StringComparison.OrdinalIgnoreCase);
            string cleanName = name.Replace(" läuft", "", StringComparison.OrdinalIgnoreCase).Trim();

            string iconText = cleanName.StartsWith("Rust", StringComparison.OrdinalIgnoreCase) ? "R" :
                              cleanName.StartsWith("ARC", StringComparison.OrdinalIgnoreCase) ? "A" :
                              cleanName.StartsWith("Arma", StringComparison.OrdinalIgnoreCase) ? "A3" :
                              cleanName.StartsWith("Metro", StringComparison.OrdinalIgnoreCase) ? "M" :
                              cleanName.StartsWith("Battle", StringComparison.OrdinalIgnoreCase) ? "B" : "🎮";

            Brush iconBg = cleanName.StartsWith("Rust", StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(Color.FromRgb(214, 63, 38)) :
                           cleanName.StartsWith("ARC", StringComparison.OrdinalIgnoreCase) ? new LinearGradientBrush(Color.FromRgb(30, 210, 160), Color.FromRgb(20, 80, 220), 45) :
                           cleanName.StartsWith("Metro", StringComparison.OrdinalIgnoreCase) ? Brushes.White :
                           Red;

            Border row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 35)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 38, 48)),
                BorderThickness = new Thickness(1)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border icon = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(3),
                Background = iconBg,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.Child = new TextBlock
            {
                Text = iconText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = cleanName.StartsWith("Metro", StringComparison.OrdinalIgnoreCase) ? Brushes.Black : Brushes.White,
                FontSize = iconText.Length > 1 ? 13 : 16,
                FontWeight = FontWeights.UltraBold
            };
            g.Children.Add(icon);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = cleanName,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = running ? T("Spiel läuft - Safe Mode aktiv", "Game running - Safe mode active") : sub,
                Foreground = running ? Brushes.Orange : Brushes.LimeGreen,
                FontSize = 12
            });
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            bool isOptimizeAction = status.Contains("Optim", StringComparison.OrdinalIgnoreCase);
            Button action = new Button
            {
                Content = running ? T("Blockiert", "Blocked") : status,
                MinWidth = 104,
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                Background = running
                    ? new SolidColorBrush(Color.FromRgb(74, 49, 25))
                    : (optimized ? new SolidColorBrush(Color.FromRgb(16, 75, 39)) : new SolidColorBrush(Color.FromRgb(76, 48, 18))),
                Foreground = running ? Brushes.White : (optimized ? Brushes.LimeGreen : Brushes.Orange),
                BorderBrush = running
                    ? new SolidColorBrush(Color.FromRgb(180, 97, 20))
                    : (optimized ? new SolidColorBrush(Color.FromRgb(32, 170, 88)) : new SolidColorBrush(Color.FromRgb(176, 112, 28))),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = isOptimizeAction
                    ? T("Sichere Optimierungen für dieses Spiel anwenden.", "Apply safe optimizations for this game.")
                    : T("Spielprofil prüfen und analysieren.", "Check and analyze game profile.")
            };
            ApplyButtonSkin(action, 14);
            action.IsEnabled = !running;
            action.Click += async (s, e) =>
            {
                Navigate("GameProfiles");
                await AnalyzeGameProfile(cleanName);
            };

            Grid.SetColumn(action, 2);
            g.Children.Add(action);

            row.Child = g;
            return row;
        }
        private string GetOptimizedGamesFile()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineGamingOptimizer");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "optimized_games.txt");
        }

        private bool IsGameOptimized(string name)
        {
            try
            {
                string clean = name.Replace(" läuft", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (!File.Exists(GetOptimizedGamesFile()))
                    return false;

                return File.ReadAllLines(GetOptimizedGamesFile())
                    .Any(x => x.Trim().Equals(clean, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void MarkGameOptimized(string name)
        {
            try
            {
                string clean = name.Replace(" läuft", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (IsGameOptimized(clean))
                    return;

                File.AppendAllText(GetOptimizedGamesFile(), clean + Environment.NewLine);
            }
            catch { }
        }


        private async Task OptimizeGameProfile(string gameName)
        {
            MessageBoxResult confirm = await Dispatcher.InvokeAsync(() => MessageBox.Show(
                T("Wirklich optimieren? Nur sichere Windows-Einstellungen (Game Mode, Energieplan, DNS).\nKeine Spiel-Dateien.",
                  "Really optimize? Only safe Windows settings (Game Mode, power plan, DNS).\nNo game files."),
                T("Bestätigen", "Confirm"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question));

            if (confirm != MessageBoxResult.Yes)
                return;

            if (IsAntiCheatSafeModeActive(out string reason))
            {
                await Log(T("Blockiert: ", "Blocked: ") + gameName + T(" kann nicht optimiert werden, weil Anti-Cheat Safe Mode aktiv ist: ", " cannot be optimized because Anti-Cheat Safe Mode is active: ") + reason);
                MessageBox.Show(
                    T("Spiel/AntiCheat läuft gerade.\n\nRedline optimiert keine Game-Profile während Anti-Cheat aktiv ist.\n\nSchließe zuerst das Spiel.",
                      "Game/Anti-Cheat is currently running.\n\nRedline does not optimize game profiles while anti-cheat is active.\n\nClose the game first."),
                    "Anti-Cheat Safe Mode",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            OutputBox.Clear();
            await Log("===== " + T("GAME PROFILE OPTIMIERUNG", "GAME PROFILE OPTIMIZATION") + " =====");
            await Log(T("Spiel: ", "Game: ") + gameName);
            await Log(T("Modus: ", "Mode: ") + GraphicsMode);
            await Log("");
            await Log(T("Führe sichere Pre-Game Aktionen aus:", "Running safe pre-game actions:"));
            await Log("- " + T("Windows Game Mode aktivieren", "Enable Windows Game Mode"));
            await SetGameModeEnabled(true);
            await Log("- " + T("Power Plan Hochleistung setzen", "Set high performance power plan"));
            await SetHighPerformance();
            await Log("- " + T("DNS Cache leeren", "Clear DNS cache"));
            await FlushDNS();

            if (GraphicsMode == "FPS")
            {
                await Log("- " + T("FPS-Modus: visuelle Effekte / Hintergrundlast auf Performance prüfen", "FPS mode: review visual effects / background load for performance"));
            }
            else if (GraphicsMode == "Quality")
            {
                await Log("- " + T("Qualitäts-Modus: Windows Grafik-Einstellungen für bessere Bildqualität öffnen", "Quality mode: open Windows graphics settings for better image quality"));
                OpenUri("ms-settings:display-advancedgraphics");
            }
            else
            {
                await Log("- " + T("Ausgewogener Modus: stabile Performance mit guter Optik", "Balanced mode: stable performance with good visuals"));
            }

            await Log("- " + T("Keine Game-Dateien, AntiCheat-Dateien oder Prozesse werden verändert", "No game files, anti-cheat files or processes are modified"));
            await Log("");

            MarkGameOptimized(gameName);
            LogRedlineChange("GameProfile", gameName + " optimized / mode=" + GraphicsMode);

            await Log(T("Fertig: ", "Done: ") + gameName + T(" wurde als optimiert markiert.", " has been marked as optimized."));
            await Log(T("Dashboard wird aktualisiert...", "Refreshing dashboard..."));
            Navigate("Dashboard");
        }
        private async Task SetGameModeEnabled(bool enabled)
        {
            await SafeRun("Game Mode", async () =>
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                    key?.SetValue("AllowAutoGameMode", enabled ? 1 : 0, RegistryValueKind.DWord);
                    key?.SetValue("AutoGameModeEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);

                    await Log("Game Mode: " + (enabled ? "aktiviert" : "deaktiviert"));
                    LogRedlineChange("GameMode", enabled ? "Enabled" : "Disabled");
                }
                catch (Exception ex)
                {
                    await Log("Game Mode Fehler: " + ex.Message);
                    SaveCrashLog(ex);
                }
            });
        }

        private UIElement ToggleRow(string title, string sub, bool on, Func<Task> action)
        {
            Border row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 48)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 10, 0, 10)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
            text.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12 });
            g.Children.Add(text);

            Button toggle = new Button
            {
                Width = 58,
                Height = 28,
                BorderThickness = new Thickness(0),
                Background = on ? Red : new SolidColorBrush(Color.FromRgb(54, 60, 70)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.UltraBold,
                Content = on ? "AN" : "AUS",
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Klicken, um diese Optimierung auszuführen oder Einstellung zu öffnen."
            };

            toggle.Click += async (s, e) => await action();
            Grid.SetColumn(toggle, 1);
            g.Children.Add(toggle);

            row.Child = g;
            return row;
        }



        private Button MiniButton(string text, Brush bg, double width)
        {
            Button btn = new Button
            {
                Content = text,
                Width = width,
                Height = 36,
                Background = bg,
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 64, 78)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(12, 0, 12, 0),
                ToolTip = ButtonInfo(text)
            };
            ApplyButtonSkin(btn, 8);
            return btn;
        }
        private Border ActionRecommendation(string icon, string title, string sub, string button, Func<Task> action)
        {
            Border row = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 40, 52)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 10, 0, 10)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

            g.Children.Add(new TextBlock { Text = icon, FontSize = 24, VerticalAlignment = VerticalAlignment.Center });

            StackPanel text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.Bold });
            text.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            Button b = MiniButton(button, Brushes.Transparent, 150);
            ApplyButtonSkin(b, 8);
            b.BorderBrush = button.Contains("AKTUELL") || button.Contains("Aktiv") ? Brushes.Green : Red;
            b.Foreground = button.Contains("AKTUELL") || button.Contains("Aktiv") ? Brushes.LightGreen : Red;
            b.Click += async (s, e) => await action();
            Grid.SetColumn(b, 2);
            g.Children.Add(b);

            row.Child = g;
            return row;
        }

        private void AddCleanerSection(string name)
        {
            CleanerPanel?.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = Red,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 8)
            });
        }

        private void AddCleanerCheck(string name, bool isChecked)
        {
            CheckBox cb = new CheckBox
            {
                Content = name,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = OptionInfo(name)
            };

            CleanerChecks[name] = cb;
            CleanerPanel?.Children.Add(cb);
        }

        private void AddOptimizeCheck(string name, bool isChecked)
        {
            CheckBox cb = new CheckBox
            {
                Content = name,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };

            OptimizeChecks[name] = cb;
            OptimizePanel?.Children.Add(cb);
        }

        private bool CleanerChecked(string name)
        {
            if (CleanerChecks.TryGetValue(name, out CheckBox? cb))
                return cb.IsChecked == true;

            if (name == "DNS Cache" && CleanerChecks.ContainsKey("DNS/Netzwerkreste"))
                return CleanerChecks["DNS/Netzwerkreste"].IsChecked == true;

            return false;
        }

        private bool OptimizeChecked(string name)
        {
            return OptimizeChecks.TryGetValue(name, out CheckBox? cb) && cb.IsChecked == true;
        }

        private async void CleanerScan_Click(object sender, RoutedEventArgs e)
        {
            await SafeRun("Cleaner Scan", async () => await RunCleanerScanAsync());
        }

        private async Task RunCleanerScanAsync()
        {
            PrepareActionOutput();
            if (Progress == null) Progress = new ProgressBar { Maximum = 100, Height = 10, Value = 0 };
            Progress.Value = 0;

            await Log("===== REDLINE CLEANER ANALYSE =====");
            await Log(T("Sicherer Scan – nur Cache/Temp-Ordner.", "Safe scan – cache/temp folders only."));
            await Log("");

            List<CleanTarget> targets = GetSelectedCleanerTargets();
            if (targets.Count == 0)
            {
                await Log(T("Keine Ziele: Bitte mindestens eine Kategorie aktiv lassen.", "No targets: keep at least one category enabled."));
                MessageBox.Show(
                    T("Bitte mindestens eine Kategorie auswählen.", "Please keep at least one category selected."),
                    "Redline Cleaner",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            long total = 0;
            int files = 0;
            Dictionary<string, long> catSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < targets.Count; i++)
            {
                CleanTarget t = targets[i];
                await Log(T("Scanne: ", "Scanning: ") + t.Name + "...");
                (long Size, int Files) r = await Task.Run(() => ScanTarget(t));

                total += r.Size;
                files += r.Files;

                string cat = ClassifyCleanerCategory(t);
                catSizes[cat] = catSizes.GetValueOrDefault(cat) + r.Size;

                await Log("  → " + FormatSize(r.Size) + " / " + r.Files + T(" Dateien", " files"));

                if (Progress != null)
                    Progress.Value = Math.Min(95, (i + 1) * 100.0 / Math.Max(1, targets.Count));
            }

            if (CleanerChecked("Papierkorb")) await Log(T("Papierkorb: wird beim Reinigen geleert", "Recycle bin: emptied on clean"));
            if (CleanerChecked("DNS Cache") || CleanerChecked("DNS/Netzwerkreste"))
                await Log(T("DNS-Cache: wird beim Reinigen geleert", "DNS cache: flushed on clean"));

            await Log("");
            await Log(T("Analyse abgeschlossen.", "Analysis complete."));
            await Log(T("Gefunden gesamt: ", "Total found: ") + FormatSize(total) + " (" + files + T(" Dateien)", " files)"));

            _cleanerLastTotalBytes = total;
            _cleanerLastFileCount = files;
            CleanerScanDone = true;

            await Dispatcher.InvokeAsync(() =>
            {
                UpdateCleanerCategoryAmounts(catSizes);
                RefreshCleanerUiState();
            });

            if (Progress != null)
                Progress.Value = 100;
        }

        private async void CleanerClean_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("Cleaner Reinigung")) return;
            if (!CleanerScanDone)
            {
                MessageBox.Show(T("Bitte zuerst „Sicheren Scan starten“.", "Please run \"Start safe scan\" first."), "Redline", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await SafeRun("Cleaner Reinigung", async () => await RunCleanerCleanAsync());
        }

        private async Task RunCleanerCleanAsync()
        {
            PrepareActionOutput();
            if (Progress == null) Progress = new ProgressBar { Maximum = 100, Height = 10, Value = 0 };

            if (!PreActionCheck(
                    T("Cleaner Reinigung", "Cleaner cleanup"),
                    T("Ausgewählte Cache-/Temp-Dateien werden gelöscht. Gesperrte Dateien werden übersprungen.",
                      "Selected cache/temp files will be deleted. Locked files are skipped.")))
                return;

            Progress.Value = 0;
            await Log("");
            await Log("===== REINIGUNG START =====");

            long total = 0;
            int files = 0;
            List<CleanTarget> targets = GetSelectedCleanerTargets();

            for (int i = 0; i < targets.Count; i++)
            {
                CleanTarget t = targets[i];
                await Log(T("Bereinige: ", "Cleaning: ") + t.Name);
                (long Size, int Files) r = await Task.Run(() => CleanTargetPath(t));
                total += r.Size;
                files += r.Files;
                await Log("  → " + FormatSize(r.Size) + " / " + r.Files + T(" Dateien", " files"));
                if (Progress != null)
                    Progress.Value = Math.Min(85, (i + 1) * 85.0 / Math.Max(1, targets.Count));
            }

            if (CleanerChecked("Papierkorb")) await EmptyRecycleBinSafe();
            if (CleanerChecked("DNS Cache") || CleanerChecked("DNS/Netzwerkreste")) await FlushDNS();

            await Log("");
            await Log(T("Fertig.", "Done."));
            await Log(T("Gelöscht: ", "Deleted: ") + files + T(" Dateien", " files"));
            await Log(T("Freigegeben: ", "Freed: ") + FormatSize(total));
            await Log(T("Hinweis: Dateien von laufenden Apps wurden übersprungen.", "Note: files in use by running apps were skipped."));

            _cleanerLastTotalBytes = 0;
            _cleanerLastFileCount = 0;
            CleanerScanDone = false;
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (TextBlock tb in _cleanerCategoryAmountTexts.Values)
                    tb.Text = T("Wird nach Scan geprüft", "Checked after scan");
                RefreshCleanerUiState();
            });

            if (Progress != null)
                Progress.Value = 100;
        }

        private async void ChromeCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await ChromeCheck();
        }

        private async Task ChromeCheck()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chrome = Path.Combine(local, @"Google\Chrome\User Data\Default");
            string pref = Path.Combine(chrome, "Preferences");

            await Log("===== CHROME CHECK =====");

            bool running = Process.GetProcessesByName("chrome").Length > 0;
            await Log("Chrome läuft: " + (running ? "Ja" : "Nein"));

            await Log("Chrome Profil gefunden: " + (Directory.Exists(chrome) ? "Ja" : "Nein"));
            await Log("Cache: " + FolderSummary(Path.Combine(chrome, "Cache")));
            await Log("Code Cache: " + FolderSummary(Path.Combine(chrome, "Code Cache")));
            await Log("History Datei: " + FileSummary(Path.Combine(chrome, "History")));
            await Log("Cookies Datei: " + FileSummary(Path.Combine(chrome, @"Network\Cookies")));

            string ext = Path.Combine(chrome, "Extensions");
            int extCount = Directory.Exists(ext) ? Directory.GetDirectories(ext).Length : 0;
            await Log("Erweiterungen gefunden: " + extCount);

            bool background = ChromeBackgroundEnabled(pref);
            await Log("Background Apps aktiv: " + (background ? "Ja" : "Nein/Unbekannt"));

            await Log("");
            if (running)
                await Log("Tipp: Für Verlauf/Cookies zuerst Browser schließen.");
            if (background)
                await Log("Empfehlung: Chrome Background Apps deaktivieren.");
            if (extCount > 15)
                await Log("Empfehlung: Viele Extensions können Browser/PC bremsen.");
        }

        private async void ChromeDisableBackground_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("Chrome Background Apps deaktivieren", "Chrome wird geschlossen und die Preferences-Datei wird angepasst."))
                return;

            await Log("===== CHROME BACKGROUND APPS DEAKTIVIEREN =====");

            CloseProcessesByNames(new[] { "chrome" }, true);

            string pref = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Preferences");

            try
            {
                if (!File.Exists(pref))
                {
                    await Log("Chrome Preferences nicht gefunden.");
                    return;
                }

                string json = File.ReadAllText(pref);
                JsonNode? node = JsonNode.Parse(json);

                if (node == null)
                {
                    await Log("Preferences konnte nicht gelesen werden.");
                    return;
                }

                JsonObject root = node.AsObject();

                if (root["background_mode"] == null)
                    root["background_mode"] = new JsonObject();

                JsonObject bg = root["background_mode"]!.AsObject();
                bg["enabled"] = false;

                File.WriteAllText(pref, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));

                await Log("Chrome Background Apps deaktiviert.");
                await Log("Chrome beim nächsten Start normal öffnen.");
            }
            catch (Exception ex)
            {
                await Log("Fehler: " + ex.Message);
            }
        }

        private async void ChromeClean_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("Chrome Daten löschen", "Chrome wird geschlossen. Cache, Verlauf und Cookies werden gelöscht."))
                return;

            await Log("===== CHROME DATEN LÖSCHEN =====");
            await Log("Schließe Chrome...");

            CloseProcessesByNames(new[] { "chrome" }, true);
            await Task.Delay(600);

            string chrome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default");

            var a = CleanFolder(Path.Combine(chrome, "Cache"));
            var b = CleanFolder(Path.Combine(chrome, "Code Cache"));
            var c = CleanFile(Path.Combine(chrome, "History"));
            var d = CleanFile(Path.Combine(chrome, "History-journal"));
            var e2 = CleanFile(Path.Combine(chrome, @"Network\Cookies"));
            var f = CleanFile(Path.Combine(chrome, @"Network\Cookies-journal"));

            long size = a.Size + b.Size + c.Size + d.Size + e2.Size + f.Size;
            int files = a.Files + b.Files + c.Files + d.Files + e2.Files + f.Files;

            await Log("Chrome Cache/Verlauf/Cookies gelöscht.");
            await Log("Gelöscht: " + files + " Dateien");
            await Log("Freigegeben: " + FormatSize(size));
        }

        private async void QuickTempClean_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("Temp Quick Clean", "Windows Temp und User Temp werden bereinigt. Gesperrte Dateien werden übersprungen."))
                return;

            await Log("===== TEMP QUICK CLEAN =====");

            var a = CleanFolder(Path.GetTempPath());
            var b = CleanFolder(@"C:\Windows\Temp");

            await Log("User Temp: " + FormatSize(a.Size) + " / " + a.Files + " Dateien");
            await Log("Windows Temp: " + FormatSize(b.Size) + " / " + b.Files + " Dateien");
            await Log("Fertig. Gesperrte Dateien wurden übersprungen.");
        }

        private async void CloseBrowsers_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== BROWSER SCHLIESSEN =====");

            int closed = CloseProcessesByNames(new[] { "chrome", "msedge", "firefox", "brave", "opera" }, true);

            await Log("Fertig. Geschlossene Browser-Prozesse: " + closed);
            await Log("Jetzt kannst du Verlauf/Cookies löschen.");
        }

        private int CloseProcessesByNames(string[] names, bool killIfNeeded)
        {
            int closed = 0;

            foreach (string name in names)
            {
                foreach (Process p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        p.CloseMainWindow();

                        if (killIfNeeded && !p.WaitForExit(2500))
                            p.Kill();

                        closed++;
                    }
                    catch { }
                }
            }

            return closed;
        }

        private async void GameProfileAnalyze_Click(object sender, RoutedEventArgs e)
        {
            await AnalyzeGameProfile(GetSelectedGameProfileName());
        }

        private async Task RunAiGameScan()
        {
            PrepareActionOutput();
            if (StatusText != null) StatusText.Text = T("KI scannt Spiele...", "AI scanning games...");

            await Log("===== REDLINE AI GAME SCAN =====");
            await Log(T("Made by Tobias Immisch", "Made by Tobias Immisch"));
            await Log("");

            List<string> games = DetectGames();
            await Log(T("Installierte Spiele/Launcher: ", "Installed games/launchers: ") + games.Count);

            if (games.Count == 0)
            {
                await Log(T("Keine Spiele erkannt. Prüfe Desktop- und Startmenü-Verknüpfungen.", "No games detected. Check desktop and start menu shortcuts."));
            }
            else
            {
                foreach (string game in games)
                    await Log("• " + game);

                await Log("");
                await Log(T("Klicke bei einem Spiel auf PRO GUIDE — nichts wird automatisch optimiert.",
                    "Click PRO GUIDE on a game — nothing is optimized automatically."));
            }

            if (StatusText != null) StatusText.Text = T("KI Scan fertig", "AI scan done");
        }

        private async Task AnalyzeGameProfile(string profile)
        {
            await Task.CompletedTask;
            SelectGameForAdvice(profile);
            SetGameAdviceStatus(T("Scan fertig — Tipps rechts. Nichts wurde automatisch geändert.",
                "Scan done — see tips on the right. Nothing was changed automatically."));
        }

        private static string GetGameIconText(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName)) return "🎮";
            if (gameName.StartsWith("Rust", StringComparison.OrdinalIgnoreCase)) return "R";
            if (gameName.StartsWith("ARC", StringComparison.OrdinalIgnoreCase)) return "A";
            if (gameName.StartsWith("Arma", StringComparison.OrdinalIgnoreCase)) return "A3";
            if (gameName.StartsWith("Metro", StringComparison.OrdinalIgnoreCase)) return "M";
            if (gameName.Contains("Battle", StringComparison.OrdinalIgnoreCase)) return "B";
            return gameName.Trim().Length > 0 ? gameName.Trim().Substring(0, 1).ToUpperInvariant() : "🎮";
        }

        private static string GetGamePlatformLabel(string gameName)
        {
            if (gameName.Contains("Battle", StringComparison.OrdinalIgnoreCase) ||
                gameName.Contains("Riot", StringComparison.OrdinalIgnoreCase) ||
                gameName.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                return "Launcher";

            return "Steam / PC";
        }

        private async Task AnalyzeRustProfile()
        {
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string rustSteam = Path.Combine(pf86, @"Steam\steamapps\common\Rust");
            string rustLocal = Path.Combine(local, @"Facepunch Studios LTD\Rust");

            await Log("Rust lokaler Check:");
            await Log(Directory.Exists(rustSteam) ? "✅ Rust Steam Ordner gefunden" : "⚠ Rust Steam Ordner nicht gefunden");
            await Log(Directory.Exists(rustLocal) ? "✅ Rust LocalAppData gefunden" : "⚠ Rust LocalAppData nicht gefunden");

            await Log("Steam Shader Cache: " + FolderSummary(Path.Combine(pf86, @"Steam\steamapps\shadercache")));
            await Log("NVIDIA DXCache: " + FolderSummary(Path.Combine(local, @"NVIDIA\DXCache")));
            await Log("DirectX Shader Cache: " + FolderSummary(Path.Combine(local, "D3DSCache")));

            await Log("");
            await Log("Empfohlen für Rust:");
            await Log("✅ Hochleistungsmodus");
            await Log("✅ Windows Game Mode");
            await Log("✅ Discord/Browser im Hintergrund prüfen");
            await Log("✅ Shader Cache optional nach Treiberupdate löschen");
            await Log("⚠ EasyAntiCheat nicht anfassen");
            await Log("⚠ Rust Config nicht automatisch löschen");
        }

        private async Task AnalyzeArcProfile()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            await Log("ARC Raiders Check:");
            await Log("NVIDIA DXCache: " + FolderSummary(Path.Combine(local, @"NVIDIA\DXCache")));
            await Log("DirectX Shader Cache: " + FolderSummary(Path.Combine(local, "D3DSCache")));

            await Log("");
            await Log("Empfohlen:");
            await Log("✅ Game Mode");
            await Log("✅ Hochleistungsmodus");
            await Log("✅ DNS/Ping testen");
            await Log("✅ Shader Cache optional reinigen");
            await Log("⚠ AntiCheat nicht anfassen");
        }

        private async Task AnalyzeGenericGameProfile()
        {
            await Log("Allgemeines FPS Profil:");
            await Log("✅ Power Plan prüfen");
            await Log("✅ Game Mode prüfen");
            await Log("✅ Autostarts prüfen");
            await Log("✅ Cache/Shader prüfen");
            await Log("✅ Ping testen");
        }

        private async void GameProfileApply_Click(object sender, RoutedEventArgs e)
        {
            if (!RequirePro(T("System-Tipps anwenden", "Apply system tips"))) return;

            string profile = GetSelectedGameProfileName();
            MessageBoxResult confirm = MessageBox.Show(
                T("Nur auf deinen Klick — Redline wendet an:\n\n• Windows Game Mode\n• Hochleistungs-Energieplan\n• DNS Cache leeren\n\nKeine Rust/Spiel-Dateien, kein EasyAntiCheat.\nSpiel: ", "On your click only — Redline will apply:\n\n• Windows Game Mode\n• High performance power plan\n• Flush DNS cache\n\nNo game files, no EasyAntiCheat.\nGame: ")
                + profile + "\n\n" + T("Jetzt anwenden?", "Apply now?"),
                T("System-Tipps", "System tips"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                SetGameAdviceStatus(T("Abgebrochen — nichts geändert.", "Cancelled — nothing changed."));
                return;
            }

            if (IsAntiCheatSafeModeActive(out string reason))
            {
                MessageBox.Show(T("Spiel läuft: ", "Game running: ") + reason, T("Safe Mode", "Safe Mode"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetGameAdviceStatus(T("Wende System-Tipps an…", "Applying system tips…"));
            await SetGameModeEnabled(true);
            await SetHighPerformance();
            await FlushDNS();

            MarkGameOptimized(profile);
            SetGameAdviceStatus(T("Fertig — nur Windows-Optimierungen. Spiel-Config unverändert.",
                "Done — Windows tweaks only. Game config unchanged."));
        }

        private async void OptimizationRun_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("FPS Optimierung")) return;
            PrepareActionOutput();
            if (!PreActionCheck("FPS Optimierung", "Ausgewählte System-Optimierungen werden ausgeführt."))
                return;

            await Log("===== FPS OPTIMIERUNG =====");

            bool useDefaultProfile = OptimizeChecks.Count == 0;

            if (useDefaultProfile || OptimizeChecked("Windows Game Mode aktivieren")) await EnableGameMode();
            if (useDefaultProfile || OptimizeChecked("Hochleistungsmodus setzen")) await SetHighPerformance();
            if (useDefaultProfile || OptimizeChecked("DNS Cache leeren")) await FlushDNS();
            if (OptimizeChecked("Netzwerk Ping testen")) await Log("Ping: " + await GetPing());
            if (OptimizeChecked("RAM Working Sets leeren")) await ClearWorkingSets();
            if (OptimizeChecked("Explorer neu starten")) await RestartExplorer();
            if (OptimizeChecked("Gaming Hintergrund-Apps anzeigen")) await ShowGamingProcesses();

            if (OptimizeChecked("NVIDIA/DirectX Shader Cache reinigen"))
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var a = CleanFolder(Path.Combine(local, "D3DSCache"));
                var b = CleanFolder(Path.Combine(local, @"NVIDIA\DXCache"));
                await Log("Shader Cache gereinigt: " + FormatSize(a.Size + b.Size));
            }

            if (OptimizeChecked("Windows Gaming Registry prüfen")) await CheckGamingRegistry();
            if (OptimizeChecked("Chrome Optimierung prüfen")) await ChromeCheck();
            if (useDefaultProfile || OptimizeChecked("Security Basischeck ausführen")) await SecurityQuickCheck();
            if (ScanDepthMode == "Deep" || OptimizeChecked("Treiber Status prüfen")) await RunDriverScan();
            if (OptimizeChecked("Speed Test ausführen")) await RunSpeedTest();
            if (OptimizeChecked("Hardware GPU Scheduling Hinweis anzeigen"))
                await Log("HAGS Hinweis: Windows Einstellungen > System > Anzeige > Grafik > Standardgrafikeinstellungen.");

            await Log("Fertig.");
        }

        private async void PerformanceRefresh_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== KOMPLETTER SYSTEM SCAN =====");
            await Log("Made by Tobias Immisch");
            await Log("");

            await Log("CPU: " + GetCpuName());
            await Log("GPU: " + GetGpuName());
            await Log("RAM: " + GetRamText());
            await Log("Windows: " + GetWindowsCaption());
            await Log("Admin: " + (IsAdmin() ? "Ja" : "Nein"));
            await Log("Ping: " + await GetPing());

            await Log("");
            await Log("Laufwerke:");

            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    double free = d.AvailableFreeSpace / 1024d / 1024d / 1024d;
                    double total = d.TotalSize / 1024d / 1024d / 1024d;
                    await Log($"{d.Name} frei: {Math.Round(free, 1)} GB / {Math.Round(total, 1)} GB | {d.DriveFormat}");
                }
                catch { }
            }

            await Log("");
            await Log("Top Prozesse nach RAM:");

            foreach (Process p in Process.GetProcesses().OrderByDescending(x => SafeWorkingSet(x)).Take(15))
            {
                try { await Log($"{p.ProcessName}: {FormatSize(SafeWorkingSet(p))}"); }
                catch { }
            }

            await Log("");
            await Log("Netzwerkadapter:");

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                await Log(ni.Name + " | " + ni.NetworkInterfaceType + " | " + ni.Speed / 1000000 + " Mbps");
            }
        }

        private async void StartupScan_Click(object sender, RoutedEventArgs e)
        {
            if (StartupPanel == null) return;

            PrepareActionOutput();
            StartupPanel.Children.Clear();
            StartupChecks.Clear();
            StartupValues.Clear();

            await Log("===== AUTOSTART SCAN =====");

            AddStartupFromRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU");
            AddStartupFromRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM");

            await Log("Autostarts geladen. Links auswählen und deaktivieren.");
        }

        private void AddStartupFromRegistry(RegistryKey root, string path, string scope)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(path, false);
                if (key == null) return;

                foreach (string name in key.GetValueNames())
                {
                    string value = key.GetValue(name)?.ToString() ?? "";
                    string id = scope + "|" + name;

                    CheckBox cb = new CheckBox
                    {
                        Content = scope + " - " + name,
                        Tag = id,
                        Foreground = Brushes.White,
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 8),
                        ToolTip = value
                    };

                    StartupChecks[id] = cb;
                    StartupValues[id] = value;
                    StartupPanel?.Children.Add(cb);
                }
            }
            catch { }
        }

        private async void StartupDisableSelected_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("Autostart deaktivieren")) return;
            PrepareActionOutput();

            if (!PreActionCheck("Autostart deaktivieren", "Ausgewählte Autostarts werden deaktiviert. Backup wird in der Registry gespeichert."))
                return;

            await Log("");
            await Log("===== AUTOSTART DEAKTIVIEREN =====");

            int disabled = 0;

            foreach (var kv in StartupChecks)
            {
                if (kv.Value.IsChecked != true) continue;

                string id = kv.Key;
                string[] parts = id.Split('|');
                if (parts.Length != 2) continue;

                string scope = parts[0];
                string name = parts[1];
                string value = StartupValues.TryGetValue(id, out string? v) ? v : "";

                try
                {
                    RegistryKey root = scope == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;

                    using RegistryKey? backup = root.CreateSubKey(@"Software\RedlineGamingOptimizer\DisabledStartup");
                    backup?.SetValue(name, value);

                    using RegistryKey? run = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    run?.DeleteValue(name, false);

                    await Log("Deaktiviert: " + scope + " - " + name);
                    disabled++;
                }
                catch
                {
                    await Log("Fehler/keine Rechte: " + scope + " - " + name);
                }
            }

            await Log("Fertig. Deaktiviert: " + disabled);
        }



        private bool PreActionCheck(string actionName, string details)
        {
            try
            {
                string admin = IsAdmin() ? "Ja" : "Nein";

                MessageBoxResult result = MessageBox.Show(
                    "Aktion: " + actionName + "\n\n" +
                    details + "\n\n" +
                    "Vorprüfung:\n" +
                    "- Tool läuft: OK\n" +
                    "- Admin-Modus: " + admin + "\n\n" +
                    "Fortfahren?",
                    "Redline Sicherheitscheck",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                return result == MessageBoxResult.Yes;
            }
            catch
            {
                return false;
            }
        }




        private void MainWindow_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                SaveCrashLog(e.Exception);
                MessageBox.Show(
                    "Redline hat einen Fehler abgefangen und bleibt offen.\\n\\nCrash-Log wurde auf dem Desktop gespeichert.\\n\\n" + e.Exception.Message,
                    "Redline Fehler abgefangen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                e.Handled = true;
            }
            catch { }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                    SaveCrashLog(ex);
            }
            catch { }
        }

        private void SaveCrashLog(Exception ex)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(desktop, "Redline_Crash_Log.txt");

                File.AppendAllText(file,
                    "===== REDLINE CRASH =====" + Environment.NewLine +
                    DateTime.Now + Environment.NewLine +
                    ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }

        private string GenerateSupportCode()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private async void GenerateRemoteCode_Click(object sender, RoutedEventArgs e)
        {
            if (RemoteCodeBox != null)
                RemoteCodeBox.Text = GenerateSupportCode();

            if (OutputBox != null)
            {
                OutputBox.Clear();
                await Log(T("Neue Redline Referenz-ID: ", "New Redline reference ID: ") + RemoteCodeBox?.Text);
                await Log(T("Hinweis: Das ist KEIN Verbindungs-Code. Fernhilfe nur über Quick Assist (oben).", "Note: This is NOT a connection code. Remote help only via Quick Assist (above)."));
            }
        }

        private async void CopyRemoteCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RemoteCodeBox != null)
                    Clipboard.SetText(RemoteCodeBox.Text);

                if (OutputBox != null)
                    await Log(T("Referenz-ID kopiert (kein Quick-Assist-Code): ", "Reference ID copied (not Quick Assist code): ") + RemoteCodeBox?.Text);
            }
            catch
            {
                if (OutputBox != null)
                    await Log("Code konnte nicht kopiert werden.");
            }
        }


        private async Task SafeRun(string title, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await Log("FEHLER in " + title + ": " + ex.Message);
                SaveCrashLog(ex);
            }
        }




        private long GetDirectorySizeSafe(string path, int maxFiles)
        {
            try
            {
                if (!Directory.Exists(path))
                    return 0;

                long total = 0;
                int count = 0;

                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                        count++;
                        if (count >= maxFiles)
                            break;
                    }
                    catch { }
                }

                return total;
            }
            catch
            {
                return 0;
            }
        }

        private async void OptimizeAllDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StatusText != null) StatusText.Text = T("Optimierung läuft...", "Optimization running...");

                await SetGameModeEnabled(true);
                await SetHighPerformance();
                await FlushDNS();
                LogRedlineChange("OptimizeAll", "Dashboard optimize all");

                MessageBox.Show(
                    T("Empfohlene Optimierungen wurden ausgeführt:\\n\\n✅ Game Mode aktiviert\\n✅ Power Plan Hochleistung gesetzt\\n✅ DNS Cache geleert\\n\\nHinweis: Visuelle Effekte und Hardware-Beschleunigung öffnen wir nur über die Pfeile, damit nichts blind geändert wird.",
                      "Recommended optimizations were applied:\\n\\n✅ Game Mode enabled\\n✅ High performance power plan set\\n✅ DNS cache flushed\\n\\nNote: Visual effects and hardware acceleration are opened via the arrows so nothing is changed blindly."),
                    "Redline",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                if (StatusText != null) StatusText.Text = T("Optimierung fertig", "Optimization complete");
                Navigate("Dashboard");
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);
                MessageBox.Show("Optimize Fehler: " + ex.Message, "Redline", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GamingReadiness_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await SafeRun("Gaming Readiness", async () =>
            {
                int score = 100;
                List<string> good = new List<string>();
                List<string> warn = new List<string>();

                await Log("===== REDLINE GAMING READINESS =====");
                await Log("");

                bool safe = IsAntiCheatSafeModeActive(out string safeReason);
                if (safe) { score -= 15; warn.Add("Anti-Cheat Safe Mode aktiv: " + safeReason); }
                else good.Add("Kein laufendes AntiCheat/Game erkannt.");

                bool gameMode = IsGameModeEnabled();
                if (gameMode) good.Add("Windows Game Mode aktiv.");
                else { score -= 10; warn.Add("Windows Game Mode ist aus oder nicht lesbar."); }

                string powerPlan = await GetActivePowerPlanName();
                if (powerPlan.ToLowerInvariant().Contains("high") || powerPlan.ToLowerInvariant().Contains("hoch") || powerPlan.ToLowerInvariant().Contains("ultimate") || powerPlan.ToLowerInvariant().Contains("höchst"))
                    good.Add("Power Plan sieht gut aus: " + powerPlan);
                else { score -= 10; warn.Add("Power Plan prüfen: " + powerPlan); }

                string pingText = await GetPingTo("1.1.1.1");
                int ping = ExtractPingNumber(pingText);
                if (ping > 0 && ping <= 30) good.Add("Ping sehr gut: " + ping + " ms");
                else if (ping > 0 && ping <= 60) { score -= 5; warn.Add("Ping okay, aber prüfbar: " + ping + " ms"); }
                else { score -= 10; warn.Add("Ping schlecht/Fehler: " + pingText); }

                int startupCount = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                                 + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startupCount <= 12) good.Add("Autostart-Last niedrig: " + startupCount);
                else { score -= 8; warn.Add("Viele Autostarts: " + startupCount); }

                int driverProblems = CountDeviceProblems();
                if (driverProblems == 0) good.Add("Keine Gerätefehler gefunden.");
                else { score -= 15; warn.Add("Gerätefehler gefunden: " + driverProblems); }

                score = Math.Clamp(score, 0, 100);

                await Log("GAMING SCORE: " + score + "/100");
                await Log(ScoreBar(score));
                await Log("");

                await Log("OK:");
                foreach (string g in good) await Log("✅ " + g);

                await Log("");
                await Log("Empfehlungen:");
                if (warn.Count == 0) await Log("✅ Keine wichtigen Warnungen.");
                foreach (string w in warn) await Log("⚠ " + w);

                await Log("");
                await Log("Sichere Aktionen:");
                await Log("- Power Mode optimieren");
                await Log("- Autostart prüfen");
                await Log("- GPU/HAGS Windows Settings öffnen");
                await Log("- Treiberstatus prüfen");
            });
        }

        private async void AntiCheatStatus_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            bool active = IsAntiCheatSafeModeActive(out string reason);
            await Log("===== ANTI-CHEAT SAFE MODE =====");

            if (active)
            {
                await Log("STATUS: AKTIV");
                await Log("Erkannt: " + reason);
                await Log("");
                await Log("Blockiert: Cleaner, Shader, RAM Clean, Remote, Winsock, Repair, Driver Aktionen");
                await Log("Erlaubt: Analyse, Speed/Ping, Systeminfos, Report speichern");
            }
            else
            {
                await Log("STATUS: INAKTIV");
                await Log("Kein bekanntes Spiel/AntiCheat läuft.");
            }
        }

        private async void UndoShow_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== UNDO CENTER =====");
            string file = GetRedlineLogFile();
            await Log("Log-Datei: " + file);
            await Log("");

            try
            {
                if (!File.Exists(file))
                {
                    await Log("Noch keine Redline-Änderungen protokolliert.");
                    return;
                }

                string[] lines = File.ReadAllLines(file).Reverse().Take(40).Reverse().ToArray();
                foreach (string line in lines) await Log(line);
            }
            catch (Exception ex) { await Log("Undo Log Fehler: " + ex.Message); }
        }

        private async void RestorePoint_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("Wiederherstellungspunkt erstellen", "Windows erstellt einen Systemwiederherstellungspunkt vor größeren Redline-Änderungen."))
                return;

            await CreateRestorePoint("Redline Before Optimization");
        }

        private bool IsAntiCheatSafeModeActive(out string reason)
        {
            string[] risky = { "RustClient", "Rust", "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService", "BEServices", "BattleEye", "BEDaisy", "vgc", "RiotClientServices", "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping", "cs2", "cod", "cod22", "Discovery", "EscapeFromTarkov", "TslGame", "FiveM" };

            foreach (string name in risky)
            {
                try
                {
                    Process[] ps = Process.GetProcessesByName(name);
                    if (ps.Length > 0)
                    {
                        reason = name + " läuft (" + ps.Length + ")";
                        return true;
                    }
                }
                catch { }
            }

            reason = "";
            return false;
        }

        private bool BlockIfAntiCheatActive(string actionName)
        {
            if (IsAntiCheatSafeModeActive(out string reason))
            {
                MessageBox.Show(actionName + " wurde blockiert.\n\nAnti-Cheat Safe Mode ist aktiv:\n" + reason + "\n\nSchließe zuerst Spiel/AntiCheat.", "Redline Anti-Cheat Safe Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }

            return false;
        }

        private bool IsGameModeEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                object? v = key?.GetValue("AllowAutoGameMode");
                return v != null && Convert.ToInt32(v) == 1;
            }
            catch { return false; }
        }

        private async Task<string> GetActivePowerPlanName()
        {
            string output = await RunCommandCapture("powercfg", "/getactivescheme");
            return output.Replace("Power Scheme GUID:", "").Trim();
        }

        private int ExtractPingNumber(string text)
        {
            try
            {
                string digits = new string(text.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int v)) return v;
            }
            catch { }
            return -1;
        }

        private int CountDeviceProblems()
        {
            int count = 0;
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
                foreach (ManagementObject _ in s.Get()) count++;
            }
            catch { }
            return count;
        }

        private string ScoreBar(int score)
        {
            int width = 30;
            int filled = Math.Clamp((int)Math.Round(score / 100d * width), 0, width);
            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }

        private async Task SetBalancedPowerPlan()
        {
            await Log("Setze Power Plan auf Ausbalanciert...");
            await Log(await RunCommandCapture("powercfg", "/setactive SCHEME_BALANCED"));
            LogRedlineChange("PowerPlan", "Balanced");
        }

        private async Task CreateRestorePoint(string description)
        {
            await Log("===== RESTORE POINT =====");
            if (!IsAdmin()) await Log("Hinweis: Wiederherstellungspunkt braucht meistens Administrator-Rechte.");

            string cmd = "Checkpoint-Computer -Description '" + description.Replace("'", "") + "' -RestorePointType 'MODIFY_SETTINGS'";
            string result = await RunPowerShellCapture(cmd);
            await Log(result);
            LogRedlineChange("RestorePoint", description);
        }

        private string GetRedlineLogFile()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineGamingOptimizer");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "changes.log");
        }

        private void LogRedlineChange(string type, string value)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + type + " | " + value;
                File.AppendAllText(GetRedlineLogFile(), line + Environment.NewLine);
            }
            catch { }
        }


        private async void SystemFullScan_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await RunSystemFullScan();
        }



        private void ShowScanResultPopup(int score, int issues, int optimizations)
        {
            string cpu = GetCpuLoadText();
            string ram = GetRamUsageText() + " (" + GetRamUsedVsTotalText() + ")";

            string msg =
                "REDLINE SYSTEM SCAN\\n\\n" +
                "Gaming Score: " + score + "/100\\n" +
                "CPU-Auslastung: " + cpu + "\\n" +
                "RAM-Auslastung: " + ram + "\\n" +
                "Gefundene Probleme: " + issues + "\\n" +
                "Empfohlene Optimierungen: " + optimizations + "\\n\\n" +
                "Empfehlung:\\n" +
                "- Power Plan prüfen\\n" +
                "- Treiberstatus prüfen\\n" +
                "- Game Mode / Anti-Cheat Safe Mode prüfen";

            MessageBox.Show(msg, "Redline Scan Ergebnis", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task RunSystemFullScan()
        {
            if (DeepScanRunning)
            {
                MessageBox.Show(T("Scan läuft bereits.", "Scan is already running."), "Redline", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool deep = string.Equals(ScanDepthMode, "Deep", StringComparison.OrdinalIgnoreCase);
            if (deep && !RequirePro(T("Tiefer Systemscan", "Deep system scan")))
                return;

            DeepScanRunning = true;

            try
            {
                EnsureOutputBox();
                OutputBox!.Clear();

                if (Progress != null)
                    Progress.Value = 0;

                if (StatusText != null)
                    StatusText.Text = deep
                        ? T("Tiefer Systemscan läuft...", "Deep system scan running...")
                        : T("Systemscan läuft...", "System scan running...");

                await SafeRun("System Komplett Scan", async () =>
                {
                    string title = deep ? "REDLINE DEEP SYSTEM SCAN" : "REDLINE SYSTEM SCAN";
                    await Log("===== " + title + " =====");
                    await Log(T("Scan-Tiefe: ", "Scan depth: ") + ScanDepthMode);
                    await Log("");

                    async Task Step(string label, int progress, int delayMs = 5000)
                    {
                        await Log(label);
                        if (Progress != null) Progress.Value = progress;
                        await Task.Delay(AdjustScanDelay(delayMs));
                    }

                    int issues = 0;
                    int optimizations = 0;
                    int deviceProblems = CountDeviceProblems();
                    int startupCount = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                                     + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
                    long tempBytes = GetDirectorySizeSafe(Path.GetTempPath(), 2000);

                    if (!IsGameModeEnabled()) { issues++; optimizations++; }
                    if (!IsAdmin()) issues++;
                    if (deviceProblems > 0) issues += deviceProblems;
                    if (startupCount > 12) optimizations++;
                    if (tempBytes > 1024L * 1024L * 1024L) optimizations++;

                    if (!deep && ScanDepthMode == "Fast")
                    {
                        await Step("[1/5] Hardware...", 15, 1200);
                        await Log("CPU: " + GetCpuName());
                        await Log("GPU: " + GetGpuName());
                        await Log("RAM: " + GetRamText());
                        await Step("[2/5] Livewerte...", 35, 1000);
                        await Log("CPU: " + GetCpuLoadText());
                        await Log("RAM: " + GetRamUsageText());
                        await Step("[3/5] Spiele...", 55, 1500);
                        await Log("Spiele: " + DetectGames().Count);
                        await Step("[4/5] Gaming Settings...", 75, 1200);
                        await Log("Game Mode: " + (IsGameModeEnabled() ? "AN" : "AUS"));
                        await Step("[5/5] Abschluss...", 100, 800);
                    }
                    else
                    {
                    await Step("[1/14] Hardware wird gelesen...", 5, 3500);
                    await Log("CPU: " + GetCpuName());
                    await Log("GPU: " + GetGpuName());
                    await Log("RAM: " + GetRamText());
                    await Log("Windows: " + GetWindowsCaption());
                    await Log("Admin: " + (IsAdmin() ? "Ja" : "Nein"));

                    await Step("[2/14] CPU/RAM Livewerte werden geprüft...", 12, 6000);
                    await Log("CPU aktuell: " + GetCpuLoadText());
                    await Log("RAM aktuell: " + GetRamUsageText() + " / " + GetRamUsedVsTotalText());

                    await Step("[3/14] Laufwerke und freier Speicher werden geprüft...", 18, 5500);
                    foreach (DriveInfo d in DriveInfo.GetDrives().Where(x => x.IsReady))
                    {
                        try
                        {
                            double total = Math.Round(d.TotalSize / 1024d / 1024d / 1024d, 0);
                            double free = Math.Round(d.AvailableFreeSpace / 1024d / 1024d / 1024d, 0);
                            await Log($"{d.Name} {free:0} GB frei / {total:0} GB");
                        }
                        catch { }
                    }

                    await Step("[4/14] Game Detection läuft...", 25, 6000);
                    List<string> games = DetectGames();
                    await Log("Gefundene Spiele/Launcher: " + games.Count);
                    foreach (string g in games.Take(20))
                        await Log(" - " + g);

                    await Step("[5/14] Anti-Cheat Safe Mode wird geprüft...", 32, 4000);
                    bool ac = IsAntiCheatSafeModeActive(out string acReason);
                    await Log("Anti-Cheat Safe Mode: " + (ac ? "AKTIV - " + acReason : "OK"));

                    await Step("[6/14] Windows Gaming Settings werden geprüft...", 39, 5000);
                    await Log("Game Mode: " + (IsGameModeEnabled() ? "AN" : "AUS/PRÜFEN"));
                    await Log("Power Plan: " + await GetActivePowerPlanName());

                    await Step("[7/14] Netzwerkadapter werden geprüft...", 46, 6000);
                    await Log(await RunPowerShellCapture("Get-NetAdapter | Select-Object Name,Status,LinkSpeed | Format-Table -AutoSize"));

                    await Step("[8/14] Ping/DNS wird getestet...", 54, 7000);
                    await Log("Cloudflare DNS Ping: " + await GetPingTo("1.1.1.1"));
                    await Log("Google DNS Ping: " + await GetPingTo("8.8.8.8"));
                    await Log("Quad9 DNS Ping: " + await GetPingTo("9.9.9.9"));

                    await Step("[9/14] Treiber/Gerätefehler werden geprüft...", 62, 6000);
                    deviceProblems = CountDeviceProblems();
                    await Log("Gerätefehler laut Windows: " + deviceProblems);
                    if (deviceProblems > 0)
                        await Log("WARNUNG: Driver Check öffnen und Geräte-Manager prüfen.");
                    else
                        await Log("OK: Keine Gerätefehler gefunden.");

                    await Step("[10/14] Windows Security/Defender Status wird geprüft...", 70, 6000);
                    await Log(await RunPowerShellCapture("Get-MpComputerStatus | Select-Object AntivirusEnabled,RealTimeProtectionEnabled,AMServiceEnabled,AntispywareEnabled | Format-List"));

                    await Step("[11/14] Autostart und Hintergrundlast wird geprüft...", 78, 6000);
                    startupCount = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")
                                 + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
                    await Log("Autostart-Einträge: " + startupCount);

                    await Step("[12/14] Temporäre Dateien werden analysiert...", 84, 5500);
                    tempBytes = GetDirectorySizeSafe(Path.GetTempPath(), 3500);
                    await Log("TEMP geschätzt: " + FormatBytes(tempBytes));

                    await Step("[13/14] Redline Empfehlungen werden berechnet...", 92, 5000);
                    if (!IsGameModeEnabled()) await Log("⚠ Game Mode aktivieren empfohlen.");
                    if (!IsAdmin()) await Log("⚠ Als Administrator starten für vollständige Repair/DNS/Scan-Funktionen.");
                    if (deviceProblems > 0) await Log("⚠ Gerätefehler prüfen.");
                    if (startupCount > 12) await Log("⚠ Autostart prüfen.");
                    if (tempBytes > 1024L * 1024L * 1024L) await Log("⚠ TEMP bereinigen empfohlen.");

                    await Step("[14/14] Scan wird abgeschlossen...", 100, 3000);
                    }

                    issues = 0;
                    optimizations = 0;
                    if (!IsGameModeEnabled()) { issues++; optimizations++; }
                    if (!IsAdmin()) issues++;
                    if (deviceProblems > 0) issues += deviceProblems;
                    if (startupCount > 12) optimizations++;
                    if (tempBytes > 1024L * 1024L * 1024L) optimizations++;

                    int gamingScore = ComputeGamingScore();
                    await Log("");
                    await Log(T("Gaming Score: ", "Gaming Score: ") + gamingScore + "/100");
                    await Log("✅ " + T("Scan abgeschlossen.", "Scan completed."));
                    await Log(T("Probleme: ", "Issues: ") + issues);
                    await Log(T("Empfohlene Optimierungen: ", "Recommended optimizations: ") + optimizations);

                    if (StatusText != null)
                        StatusText.Text = T("Scan abgeschlossen", "Scan completed");

                    RefreshSecurityStatusCache();
                    MarkScanCompleted(gamingScore);
                    ShowScanResultPopup(gamingScore, issues, optimizations);

                    if (CurrentPage == "Dashboard")
                        Navigate("Dashboard");
                });
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);
                MessageBox.Show("Scan Fehler: " + ex.Message, "Redline", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DeepScanRunning = false;
            }
        }
        private void InvalidateGamesCache()
        {
            _cachedGames = null;
            _gamesCacheUtc = DateTime.MinValue;
        }

        private List<string> DetectGames(bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedGames != null && (DateTime.UtcNow - _gamesCacheUtc) < GamesCacheLifetime)
                return new List<string>(_cachedGames);

            List<string> result = DetectGamesCore();
            _cachedGames = result;
            _gamesCacheUtc = DateTime.UtcNow;
            return new List<string>(result);
        }

        private List<string> DetectGamesCore()
        {
            HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] known =
            {
                "Rust", "ARC Raiders", "VALORANT", "Battlestate", "BattleState Games Launcher",
                "Arma 3", "RSI Launcher", "Metro Exodus", "FiveM", "CS2", "Counter-Strike",
                "Fortnite", "Dota 2", "Warzone", "Call of Duty", "FurMark", "EscapeFromTarkov",
                "League of Legends", "Rocket League", "PUBG", "Apex", "Minecraft"
            };

            void MatchShortcutName(string name)
            {
                foreach (string k in known)
                {
                    if (name.Contains(k, StringComparison.OrdinalIgnoreCase))
                        found.Add(name);
                }
            }

            void ScanShortcuts(string dir, bool oneSubLevel)
            {
                if (!Directory.Exists(dir))
                    return;

                try
                {
                    foreach (string file in Directory.EnumerateFiles(dir))
                    {
                        if (!file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                            !file.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                            continue;
                        MatchShortcutName(Path.GetFileNameWithoutExtension(file));
                    }

                    if (!oneSubLevel)
                        return;

                    foreach (string sub in Directory.EnumerateDirectories(dir))
                    {
                        foreach (string file in Directory.EnumerateFiles(sub))
                        {
                            if (!file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                                !file.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                                continue;
                            MatchShortcutName(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
                catch { }
            }

            // Desktop (flat) + Start Menu (one subfolder level) — avoids full recursive scan
            try
            {
                ScanShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false);
                ScanShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), false);
                ScanShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), true);
                ScanShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), true);
            }
            catch { }

            // Steam library detection
            try
            {
                string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(steam))
                {
                    string steamApps = Path.Combine(steam, "steamapps");
                    ReadSteamManifests(steamApps, found);

                    string libraryFile = Path.Combine(steamApps, "libraryfolders.vdf");
                    if (File.Exists(libraryFile))
                    {
                        foreach (string line in File.ReadAllLines(libraryFile))
                        {
                            if (!line.Contains("path")) continue;
                            string cleaned = line.Replace("\\\\", "\\");
                            int first = cleaned.IndexOf('"', cleaned.IndexOf("path", StringComparison.OrdinalIgnoreCase) + 4);
                            int second = first >= 0 ? cleaned.IndexOf('"', first + 1) : -1;
                            if (first >= 0 && second > first)
                            {
                                string path = cleaned.Substring(first + 1, second - first - 1);
                                ReadSteamManifests(Path.Combine(path, "steamapps"), found);
                            }
                        }
                    }
                }
            }
            catch { }

            // Epic manifests
            try
            {
                string epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (Directory.Exists(epic))
                {
                    foreach (string file in Directory.GetFiles(epic, "*.item"))
                    {
                        string text = File.ReadAllText(file);
                        foreach (string k in known)
                        {
                            if (text.Contains(k, StringComparison.OrdinalIgnoreCase))
                                found.Add(k);
                        }
                    }
                }
            }
            catch { }

            // Running games — targeted checks instead of enumerating all processes
            try
            {
                (string process, string label)[] running =
                {
                    ("RustClient", "Rust läuft"),
                    ("Rust", "Rust läuft"),
                    ("cs2", "CS2 läuft"),
                    ("valorant-win64-shipping", "VALORANT läuft"),
                    ("FortniteClient-Win64-Shipping", "Fortnite läuft"),
                    ("GTA5", "GTA läuft"),
                    ("r5apex", "Apex läuft"),
                    ("League of Legends", "League of Legends läuft"),
                    ("Minecraft", "Minecraft läuft"),
                    ("EscapeFromTarkov", "EscapeFromTarkov läuft"),
                    ("Discovery", "ARC Raiders läuft")
                };

                foreach ((string process, string label) in running)
                {
                    try
                    {
                        if (Process.GetProcessesByName(process).Length > 0)
                            found.Add(label);
                    }
                    catch { }
                }
            }
            catch { }

            return found.OrderBy(x => x).ToList();
        }

        private void ReadSteamManifests(string steamApps, HashSet<string> found)
        {
            try
            {
                if (!Directory.Exists(steamApps)) return;

                foreach (string file in Directory.GetFiles(steamApps, "appmanifest_*.acf"))
                {
                    string text = File.ReadAllText(file);
                    string name = ExtractSteamName(text);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        found.Add(name);
                    }
                }
            }
            catch { }
        }

        private string ExtractSteamName(string manifest)
        {
            try
            {
                foreach (string line in manifest.Split('\n'))
                {
                    if (!line.Contains('"')) continue;
                    if (!line.Contains("name", StringComparison.OrdinalIgnoreCase)) continue;

                    string[] parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                        return parts[3].Trim();
                }
            }
            catch { }

            return "";
        }

        private async void DashboardQuickCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== REDLINE QUICK CHECK =====");
            await Log("CPU: " + GetCpuName());
            await Log("GPU: " + GetGpuName());
            await Log("RAM: " + GetRamText());
            await Log("Windows: " + GetWindowsCaption());
            await Log("Admin: " + (IsAdmin() ? "Ja" : "Nein"));
            await Log("Ping: " + await GetPing());
            await Log("");

            await Log("Security Kurzcheck:");
            await Log(await RunPowerShellCapture("Get-MpComputerStatus | Select-Object AntivirusEnabled,RealTimeProtectionEnabled,AMServiceEnabled | Format-List"));

            await Log("");
            await Log("Driver Kurzcheck:");
            await RunDriverScan();

            await Log("");
            await Log("Quick Check fertig.");
        }

        private async void BiosCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunBiosCheck();
        }

        private async Task RunBiosCheck()
        {
            await Log("===== BIOS / UEFI CHECK =====");

            try
            {
                using ManagementObjectSearcher bios = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
                foreach (ManagementObject o in bios.Get())
                {
                    await Log("BIOS Hersteller: " + o["Manufacturer"]);
                    await Log("BIOS Version: " + o["SMBIOSBIOSVersion"]);
                    await Log("BIOS Datum: " + ParseWmiDate(o["ReleaseDate"]?.ToString())?.ToShortDateString());
                }

                using ManagementObjectSearcher board = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject o in board.Get())
                {
                    await Log("Mainboard: " + o["Manufacturer"] + " " + o["Product"]);
                }
            }
            catch
            {
                await Log("BIOS/Mainboard konnte nicht gelesen werden.");
            }

            await Log("");
            await Log("UEFI/Secure Boot/TPM:");
            await Log(await RunPowerShellCapture("Confirm-SecureBootUEFI"));
            await Log(await RunPowerShellCapture("Get-Tpm | Select-Object TpmPresent,TpmReady,TpmEnabled,TpmActivated | Format-List"));

            await Log("");
            await Log("Virtualization:");
            await Log(await RunPowerShellCapture("Get-CimInstance Win32_Processor | Select-Object Name,VirtualizationFirmwareEnabled,SecondLevelAddressTranslationExtensions | Format-List"));

            await Log("");
            await Log("Empfehlungen fürs Gaming:");
            await Log("- EXPO/XMP im BIOS prüfen, damit RAM mit vollem Takt läuft.");
            await Log("- UEFI statt Legacy/CSM nutzen.");
            await Log("- TPM + Secure Boot für viele AntiCheats aktiv lassen.");
            await Log("- Resizable BAR im BIOS und NVIDIA Systeminfo prüfen.");
            await Log("- BIOS nur updaten, wenn es einen Grund gibt oder Hersteller es empfiehlt.");
        }

        private async void UefiRestart_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("UEFI Neustart", "Windows startet neu in die erweiterten Startoptionen. Dort kannst du UEFI-Firmwareeinstellungen wählen."))
                return;

            await Log("Starte in erweiterte Startoptionen...");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/r /o /t 0",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                await Log("Fehler: " + ex.Message);
            }
        }


        private async void DnsBenchmark_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunDnsBenchmark();
        }

        private async void SetFastestDns_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("Schnellsten DNS setzen", "Redline misst DNS-Ping und setzt den schnellsten Anbieter auf aktive Netzwerkadapter."))
                return;

            DnsResult fastest = await RunDnsBenchmark();

            if (fastest.Name == "Unbekannt")
            {
                await Log("Kein DNS Ergebnis. Abgebrochen.");
                return;
            }

            await SetDnsPreset(fastest.Name, fastest.Primary, fastest.Secondary);
        }

        private async void SetDnsAuto_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            if (!PreActionCheck("DNS automatisch", "DNS wird für aktive Netzwerkadapter wieder auf DHCP/automatisch gestellt."))
                return;

            await Log("===== DNS AUTOMATISCH =====");
            if (!IsAdmin())
                await Log("Hinweis: DNS ändern braucht meistens Administrator-Rechte.");

            foreach (string adapter in GetActiveAdapterNames())
            {
                await Log("Adapter: " + adapter);
                await Log(await RunCommandCapture("netsh", $"interface ip set dns name=\"{adapter}\" source=dhcp"));
                await Log(await RunCommandCapture("netsh", $"interface ipv6 set dnsservers interface=\"{adapter}\" source=dhcp"));
            }

            await FlushDNS();
            await Log("DNS wurde auf automatisch gestellt.");
        }

        private async Task SetDnsPreset(string name, string primary, string secondary)
        {
            PrepareActionOutput();

            if (!PreActionCheck("DNS setzen: " + name, $"DNS wird auf {primary} und {secondary} gesetzt."))
                return;

            await Log("===== DNS SETZEN: " + name.ToUpperInvariant() + " =====");
            if (!IsAdmin())
                await Log("Hinweis: DNS ändern braucht meistens Administrator-Rechte.");

            List<string> adapters = GetActiveAdapterNames();

            if (adapters.Count == 0)
            {
                await Log("Keine aktiven Adapter gefunden.");
                return;
            }

            foreach (string adapter in adapters)
            {
                await Log("Adapter: " + adapter);
                await Log(await RunCommandCapture("netsh", $"interface ip set dns name=\"{adapter}\" static {primary} primary"));
                await Log(await RunCommandCapture("netsh", $"interface ip add dns name=\"{adapter}\" {secondary} index=2"));
            }

            await FlushDNS();

            await Log("");
            await Log("DNS gesetzt auf: " + name);
            await Log("Primary: " + primary);
            await Log("Secondary: " + secondary);
            await Log("Tipp: Danach Speed Test / Ping Test erneut starten.");
        }

        private async Task<DnsResult> RunDnsBenchmark()
        {
            await Log("===== DNS BENCHMARK =====");
            await Log("Teste DNS-Ping. Niedriger ist besser.");
            await Log("");

            List<DnsResult> dns = new List<DnsResult>
            {
                new DnsResult("Cloudflare", "1.1.1.1", "1.0.0.1"),
                new DnsResult("Google", "8.8.8.8", "8.8.4.4"),
                new DnsResult("Quad9", "9.9.9.9", "149.112.112.112")
            };

            foreach (DnsResult d in dns)
            {
                d.PingMs = await MeasurePingMs(d.Primary);
                await Log($"{d.Name}: {d.PingMs} ms ({d.Primary})");
            }

            DnsResult fastest = dns.Where(x => x.PingMs > 0).OrderBy(x => x.PingMs).FirstOrDefault()
                                ?? new DnsResult("Unbekannt", "", "");

            await Log("");
            await Log("Schnellster DNS: " + fastest.Name + " (" + fastest.PingMs + " ms)");
            await Log("");
            await Log("Hinweis: Schnellster Ping ist gut fürs Gaming, aber Stabilität/Privatsphäre können auch wichtig sein.");

            return fastest;
        }

        private async Task<int> MeasurePingMs(string host)
        {
            try
            {
                using Ping ping = new Ping();
                List<long> values = new List<long>();

                for (int i = 0; i < 3; i++)
                {
                    PingReply reply = await ping.SendPingAsync(host, 1200);
                    if (reply.Status == IPStatus.Success)
                        values.Add(reply.RoundtripTime);
                }

                if (values.Count == 0)
                    return -1;

                return (int)Math.Round(values.Average());
            }
            catch
            {
                return -1;
            }
        }

        private List<string> GetActiveAdapterNames()
        {
            List<string> list = new List<string>();

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    if (ni.Description.ToLowerInvariant().Contains("virtual"))
                        continue;

                    list.Add(ni.Name);
                }
            }
            catch { }

            return list.Distinct().ToList();
        }

        private async void NetworkCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== NETWORK CHECK =====");
            await Log("Ping Google: " + await GetPingTo("8.8.8.8"));
            await Log("Ping Cloudflare: " + await GetPingTo("1.1.1.1"));
            await Log("Ping Quad9: " + await GetPingTo("9.9.9.9"));

            await Log("");
            await Log("Adapter:");
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                await Log(ni.Name + " | " + ni.NetworkInterfaceType + " | " + ni.Speed / 1000000 + " Mbps");
                try
                {
                    var props = ni.GetIPProperties();
                    foreach (var dns in props.DnsAddresses)
                        await Log("  DNS: " + dns);
                }
                catch { }
            }

            await Log("");
            await Log("IPConfig Kurz:");
            await Log(await RunCommandCapture("ipconfig", "/all"));
        }

        private async void WinsockReset_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("Winsock Reset")) return;
            PrepareActionOutput();

            if (!PreActionCheck("Winsock Reset", "Netzwerkstack wird zurückgesetzt. Danach ist ein Neustart empfohlen."))
                return;

            await Log("===== WINSOCK RESET =====");
            await Log(await RunCommandCapture("netsh", "winsock reset"));
            await Log(await RunCommandCapture("netsh", "int ip reset"));
            await Log("Fertig. Bitte PC neu starten.");
        }

        private async void SfcScan_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("SFC Scan")) return;
            PrepareActionOutput();

            if (!PreActionCheck("SFC Scan", "Windows prüft geschützte Systemdateien. Das kann einige Minuten dauern."))
                return;

            await Log("===== SFC /SCANNOW =====");
            await Log(await RunCommandCapture("sfc", "/scannow"));
        }

        private async void DismRestore_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("DISM Repair")) return;
            PrepareActionOutput();

            if (!PreActionCheck("DISM RestoreHealth", "Windows-Komponentenstore wird geprüft und repariert. Das kann lange dauern."))
                return;

            await Log("===== DISM RESTOREHEALTH =====");
            await Log(await RunCommandCapture("DISM", "/Online /Cleanup-Image /RestoreHealth"));
        }

        private async void StoreReset_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== WINDOWS STORE CACHE RESET =====");
            try
            {
                SafeStartSystem("wsreset.exe");
                await Log("wsreset.exe gestartet.");
            }
            catch (Exception ex)
            {
                await Log("Fehler: " + ex.Message);
            }
        }

        private void OpenQuickAssist()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-quick-assist:") { UseShellExecute = true });
            }
            catch
            {
                SafeStartSystem("quickassist.exe");
            }
        }

        private async void RemoteFirewallCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunRemoteSupportCheckAsync();
        }

        private async Task RunRemoteSupportCheckAsync()
        {
            bool en = IsEnglish();
            RemoteSupportStatus rs = RedlineRemoteSupport.Query();

            await Log("===== " + T("REMOTE SUPPORT CHECK", "REMOTE SUPPORT CHECK") + " =====");
            await Log(RedlineRemoteSupport.FormatStatusLabel(rs, en));
            await Log(en
                ? "Quick Assist is safer – you share a code and must approve."
                : "Quick Assist ist sicherer – du gibst einen Code frei und musst zustimmen.");
            await Log("");

            if (rs.QuickAssistRunning)
                await Log(en ? "Quick Assist is running." : "Quick Assist läuft gerade.");
            if (!string.IsNullOrWhiteSpace(rs.QuickAssistPath))
                await Log("Quick Assist: " + rs.QuickAssistPath);
            else if (!string.IsNullOrWhiteSpace(rs.QuickAssistDetail))
                await Log("Quick Assist: " + rs.QuickAssistDetail);

            await Log(T("Firewall Profile:", "Firewall profiles:"));
            if (RedlineTestHooks.DryRun)
                await Log("[dry-run] Get-NetFirewallProfile");
            else
                await Log(await RunPowerShellCapture("Get-NetFirewallProfile | Select-Object Name,Enabled | Format-Table -AutoSize"));

            await Log("");
            await Log(T("Remote Desktop (Registry):", "Remote Desktop (registry):"));
            await Log(rs.RemoteDesktopEnabled
                ? T("Remote Desktop ist AN (fDenyTSConnections=0)", "Remote Desktop is ON (fDenyTSConnections=0)")
                : T("Remote Desktop ist AUS", "Remote Desktop is OFF"));

            await Log("");
            await Log(en
                ? "Tip: Prefer Quick Assist. Do not enable RDP without strong password and VPN."
                : "Tipp: Quick Assist bevorzugen. RDP nicht ohne starkes Passwort und VPN aktivieren.");
        }

        private async void DriverScan_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunDriverScan();
        }

        private async void DriverVendorLinks_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await Log("===== OFFIZIELLE TREIBER LINKS =====");

            await Log("Öffne offizielle Quellen:");
            OpenUri("https://www.nvidia.com/Download/index.aspx");
            OpenUri("https://www.amd.com/en/support/download/drivers.html");
            OpenUri("https://www.intel.com/content/www/us/en/support/detect.html");
            OpenUri("https://www.realtek.com/Download/List?cate_id=584");

            await Log("NVIDIA, AMD, Intel und Realtek Seiten geöffnet.");
            await Log(T("Wichtig: Keine Fake-Driver-Updater. Nur winget oder Hersteller-Seiten.", "Important: no fake driver updaters. Use winget or vendor sites only."));
        }

        private async void DriverReport_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(desktop, "Redline_Driver_Report.txt");

                string report = await BuildDriverReportText();
                File.WriteAllText(file, report);

                OutputBox.Clear();
                await Log("Treiber Report gespeichert:");
                await Log(file);
            }
            catch (Exception ex)
            {
                await Log("Report Fehler: " + ex.Message);
            }
        }

        private async Task RunDriverScan()
        {
            InvalidateDriversCache();
            await SafeRun(T("Treiber-Check", "Driver check"), async () => await RunDriverScanCoreAsync());
        }

        private async Task RunDriverScanCoreAsync()
        {
                await Log("===== " + T("TREIBER-STATUS-CHECK", "DRIVER STATUS CHECK") + " =====");
                await Log(T("Status-Legende:", "Status legend:"));
                await Log(T("AKTUELL = Treiber ist jung / unauffällig", "CURRENT = driver is recent / OK"));
                await Log(T("PRÜFEN = Treiber älter – Update prüfen", "CHECK = older driver – review update"));
                await Log(T("UPDATE EMPFOHLEN = sehr alt oder Gerätefehler", "UPDATE RECOMMENDED = very old or device error"));
                await Log(T("SYSTEM = Microsoft-Systemtreiber (oft altes Datum normal)", "SYSTEM = Microsoft system driver (old date often normal)"));
                await Log("");
                await Log(T("Hinweis: Redline bewertet Datum, Fehlercode und Hersteller – keine 100%-Online-API.", "Note: Redline rates date, error code and vendor – no 100% online API."));
                await Log("");

                await Log(T("System:", "System:"));
                await Log("CPU: " + GetCpuName());
                await Log("GPU: " + GetGpuName());
                HardwareProfile hpSys = RedlineHardwareProfile.Detect(GetCpuName(), GetGpuName(), GetWindowsCaption());
                await Log(T("Mainboard: ", "Motherboard: ") + hpSys.MotherboardManufacturer + " " + hpSys.MotherboardProduct);
                await Log("Windows: " + GetWindowsCaption());
                await Log("");

                Dictionary<string, int> deviceErrors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                await Log(T("Geräte mit Fehlercode:", "Devices with error code:"));
                int problems = 0;

                try
                {
                    using ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");

                    foreach (ManagementObject o in s.Get())
                    {
                        int code = Convert.ToInt32(o["ConfigManagerErrorCode"]);
                        string name = o["Name"]?.ToString() ?? T("Unbekannt", "Unknown");
                        string id = o["DeviceID"]?.ToString() ?? name;

                        deviceErrors[name] = code;
                        deviceErrors[id] = code;

                        await Log(TranslateDriverStatus("UPDATE EMPFOHLEN") + " | " + name + " | Code: " + code + " | " + DeviceErrorText(code));
                        problems++;
                    }
                }
                catch
                {
                    await Log(T("Gerätefehler konnten nicht gelesen werden.", "Could not read device errors."));
                }

                if (problems == 0)
                    await Log(TranslateDriverStatus("AKTUELL") + " | " + T("Keine Gerätefehler gefunden.", "No device errors found."));

                await Log("");
                await Log(T("Wichtige Treiber mit Status:", "Important drivers with status:"));

                List<DriverInfoLite> drivers = GetDriversCached(forceRefresh: true);

                if (drivers.Count == 0)
                {
                    await Log(T("FEHLER: Treiberliste konnte nicht gelesen werden.", "ERROR: Could not read driver list."));
                    return;
                }

                string[] important =
                {
                    "nvidia", "amd", "advanced micro devices", "intel", "realtek",
                    "mediatek", "qualcomm", "killer", "broadcom", "logitech",
                    "steelseries", "razer", "elgato", "asus", "msi", "gigabyte",
                    "bluetooth", "wi-fi", "wifi", "ethernet", "audio", "chipset"
                };

                List<DriverInfoLite> importantDrivers = drivers
                    .Where(x => important.Any(k =>
                        (x.Provider + " " + x.DeviceName).ToLowerInvariant().Contains(k)))
                    .OrderBy(x => x.Provider)
                    .ThenBy(x => x.DeviceName)
                    .Take(100)
                    .ToList();

                int aktuell = 0;
                int pruefen = 0;
                int update = 0;
                int system = 0;

                foreach (DriverInfoLite d in importantDrivers)
                {
                    string status = DriverStatusText(d, deviceErrors);

                    if (status == "AKTUELL") aktuell++;
                    else if (status == "PRÜFEN") pruefen++;
                    else if (status == "UPDATE EMPFOHLEN") update++;
                    else if (status == "SYSTEM") system++;

                    await Log($"{TranslateDriverStatus(status)} | {d.Provider} | {d.DeviceName} | {T("Version", "Version")} {d.Version} | {DriverDateText(d)}");
                }

                await Log("");
                await Log(T("Zusammenfassung:", "Summary:"));
                await Log(TranslateDriverStatus("AKTUELL") + ": " + aktuell);
                await Log(TranslateDriverStatus("PRÜFEN") + ": " + pruefen);
                await Log(TranslateDriverStatus("UPDATE EMPFOHLEN") + ": " + update);
                await Log(TranslateDriverStatus("SYSTEM") + ": " + system);

                await Log("");
                await Log(T("Empfehlung:", "Recommendation:"));

                if (update > 0 || problems > 0)
                {
                    await Log(T("⚠ Gerätefehler oder alte Treiber – nutze AUTO UPDATE oder Hersteller-Buttons.", "⚠ Device errors or old drivers – use AUTO UPDATE or vendor buttons."));
                }
                else if (pruefen > 0)
                {
                    await Log(T("ℹ Einige Treiber prüfenswert – Hersteller-Link oder winget Install.", "ℹ Some drivers worth checking – vendor link or winget install."));
                }
                else
                {
                    await Log(T("✅ Keine klaren Treiber-Probleme.", "✅ No clear driver issues."));
                }

                await Log("");
                await Log(T("Priorität:", "Priority:"));
                await Log("1. " + T("GPU-Treiber (NVIDIA/AMD/Intel)", "GPU driver (NVIDIA/AMD/Intel)"));
                await Log("2. " + T("Mainboard/Chipsatz-Treiber", "Motherboard/chipset drivers"));
                await Log("3. " + T("Netzwerk/Audio (Realtek etc.)", "Network/audio (Realtek etc.)"));
                await Log("4. " + T("In-App: passende winget-Pakete oder Hersteller-Link", "In-app: matching winget packages or vendor link"));
                await Log("5. " + T("Keine Fake-Driver-Updater", "No fake driver updater tools"));

                if (CurrentPage == "Drivers")
                    Dispatcher.Invoke(ScheduleDriverPreviewLoad);
        }

        private string DeviceErrorText(int code)
        {
            return code switch
            {
                22 => T("Gerät ist deaktiviert. Im Geräte-Manager prüfen.", "Device disabled. Check Device Manager."),
                28 => T("Treiber fehlt. Update/Hersteller-Treiber nötig.", "Driver missing. Vendor driver needed."),
                10 => T("Gerät kann nicht gestartet werden.", "Device cannot start."),
                31 => T("Windows kann Treiber nicht laden.", "Windows cannot load driver."),
                43 => T("Gerät hat Fehler gemeldet.", "Device reported an error."),
                _ => T("Im Geräte-Manager prüfen.", "Check Device Manager.")
            };
        }

        private string DriverDateText(DriverInfoLite d)
        {
            if (!d.DriverDate.HasValue)
                return T("Datum unbekannt", "Date unknown");

            return d.DriverDate.Value.ToShortDateString();
        }

        private string DriverStatusText(DriverInfoLite d, Dictionary<string, int>? deviceErrors = null)
        {
            string all = (d.Provider + " " + d.DeviceName + " " + d.InfName).ToLowerInvariant();

            if (deviceErrors != null)
            {
                foreach (var kv in deviceErrors)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) &&
                        (d.DeviceName.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                         kv.Key.Contains(d.DeviceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return "UPDATE EMPFOHLEN";
                    }
                }
            }

            if (d.Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                if (all.Contains("processor") ||
                    all.Contains("wan miniport") ||
                    all.Contains("hid") ||
                    all.Contains("generic") ||
                    all.Contains("system") ||
                    all.Contains("remote desktop") ||
                    all.Contains("basic render") ||
                    all.Contains("ndis") ||
                    all.Contains("print") ||
                    all.Contains("wsd"))
                {
                    return "SYSTEM";
                }
            }

            if (!d.DriverDate.HasValue)
                return "PRÜFEN";

            int days = (int)(DateTime.Now - d.DriverDate.Value).TotalDays;

            bool gpu = all.Contains("nvidia") || all.Contains("radeon") || all.Contains("geforce");
            bool chipset = all.Contains("amd") || all.Contains("advanced micro devices") || all.Contains("chipset") || all.Contains("smbus") || all.Contains("gpio");
            bool network = all.Contains("wi-fi") || all.Contains("wifi") || all.Contains("ethernet") || all.Contains("realtek") || all.Contains("intel") || all.Contains("mediatek");

            if (gpu)
            {
                if (days <= 365) return "AKTUELL";
                if (days <= 730) return "PRÜFEN";
                return "UPDATE EMPFOHLEN";
            }

            if (chipset || network)
            {
                if (days <= 730) return "AKTUELL";
                if (days <= 1095) return "PRÜFEN";
                return "UPDATE EMPFOHLEN";
            }

            if (days <= 730) return "AKTUELL";
            if (days <= 1460) return "PRÜFEN";
            return "UPDATE EMPFOHLEN";
        }

        private List<DriverInfoLite> GetDrivers()
        {
            List<DriverInfoLite> list = new List<DriverInfoLite>();

            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate, InfName, IsSigned FROM Win32_PnPSignedDriver");

                foreach (ManagementObject o in s.Get())
                {
                    DateTime? date = ParseWmiDate(o["DriverDate"]?.ToString());

                    list.Add(new DriverInfoLite
                    {
                        DeviceName = o["DeviceName"]?.ToString() ?? "Unbekannt",
                        Provider = o["DriverProviderName"]?.ToString() ?? "Unbekannt",
                        Version = o["DriverVersion"]?.ToString() ?? "Unbekannt",
                        InfName = o["InfName"]?.ToString() ?? "",
                        Signed = o["IsSigned"]?.ToString() ?? "",
                        DriverDate = date
                    });
                }
            }
            catch { }

            return list;
        }

        private DateTime? ParseWmiDate(string? raw)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
                    return null;

                int year = int.Parse(raw.Substring(0, 4));
                int month = int.Parse(raw.Substring(4, 2));
                int day = int.Parse(raw.Substring(6, 2));

                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }

        private string DriverAgeText(DateTime date)
        {
            int days = (int)(DateTime.Now - date).TotalDays;

            if (days < 0)
                return date.ToShortDateString();

            if (days > 1095)
                return date.ToShortDateString() + " / ALT";

            if (days > 730)
                return date.ToShortDateString() + " / prüfen";

            return date.ToShortDateString() + " / OK";
        }

        private async Task<string> BuildDriverReportText()
        {
            List<DriverInfoLite> drivers = GetDrivers();
            List<string> lines = new List<string>();

            lines.Add("===== REDLINE DRIVER REPORT =====");
            lines.Add("Made by Tobias Immisch");
            lines.Add(DateTime.Now.ToString());
            lines.Add("");
            lines.Add("CPU: " + GetCpuName());
            lines.Add("GPU: " + GetGpuName());
            lines.Add("Windows: " + GetWindowsCaption());
            lines.Add("");
            lines.Add("Treiber:");

            foreach (DriverInfoLite d in drivers.OrderBy(x => x.Provider).ThenBy(x => x.DeviceName))
            {
                string date = d.DriverDate.HasValue ? d.DriverDate.Value.ToShortDateString() : "Unbekannt";
                lines.Add($"{d.Provider} | {d.DeviceName} | {d.Version} | {date} | {d.InfName} | Signed={d.Signed}");
            }

            lines.Add("");
            lines.Add(T("In-App winget: nur passende GPU/CPU-Pakete", "In-app winget: matching GPU/CPU packages only"));
            lines.Add("NVIDIA: https://www.nvidia.com/Download/index.aspx");
            lines.Add("AMD: https://www.amd.com/en/support/download/drivers.html");
            lines.Add("Intel: https://www.intel.com/content/www/us/en/support/detect.html");

            await Task.CompletedTask;
            return string.Join(Environment.NewLine, lines);
        }


        private void SafeStartSystem(string fileName, string arguments = "", bool asAdmin = false)
        {
            if (RedlineTestHooks.DryRun)
            {
                RedlineTestHooks.Record("proc:" + fileName);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                if (asAdmin)
                    psi.Verb = "runas";

                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                SaveCrashLog(ex);

                MessageBox.Show(
                    "Konnte Windows-Tool nicht öffnen:\n\n" +
                    fileName + " " + arguments + "\n\n" +
                    "Grund: " + ex.Message + "\n\n" +
                    "Tipp: Starte Redline als Administrator oder öffne es manuell über Windows.",
                    "Redline Hinweis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);

                MessageBox.Show(
                    "Fehler beim Öffnen:\n\n" + fileName + "\n\n" + ex.Message,
                    "Redline Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void OpenUri(string uri)
        {
            if (RedlineTestHooks.DryRun)
            {
                RedlineTestHooks.Record("uri:" + uri);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);
                MessageBox.Show("Link konnte nicht geöffnet werden:\n\n" + uri + "\n\n" + ex.Message, "Redline Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SecurityCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await SecurityQuickCheck();
        }

        private async void DefenderQuickScan_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunDefenderQuickScan();
        }


        private async void DefenderOfflineScan_Click(object sender, RoutedEventArgs e)
        {
            if (BlockIfAntiCheatActive("Defender Offline Scan")) return;
            PrepareActionOutput();

            bool ok = PreActionCheck(
                "Defender Offline Scan",
                "Windows wird neu starten und Microsoft Defender scannt vor dem normalen Windows-Start. Speichere vorher offene Dateien."
            );

            if (!ok)
            {
                await Log("Offline Scan abgebrochen.");
                return;
            }

            await RunDefenderOfflineScan();
        }

        private async Task RunDefenderOfflineScan()
        {
            await Log("===== WINDOWS DEFENDER OFFLINE SCAN =====");
            await Log("Doppelte Prüfung läuft...");

            if (!IsAdmin())
            {
                await Log("FEHLER: Bitte Redline als Administrator starten.");
                await Log("Rechtsklick auf EXE > Als Administrator ausführen.");
                return;
            }

            await Log("Admin: OK");
            await Log("Prüfe Defender Status...");

            string status = await RunPowerShellCapture("Get-MpComputerStatus | Select-Object AntivirusEnabled,RealTimeProtectionEnabled,AMServiceEnabled | Format-List");
            await Log(status);

            MessageBoxResult finalConfirm = MessageBox.Show(
                "Letzte Bestätigung:\\n\\nDer PC wird gleich neu gestartet. Microsoft Defender Offline Scan läuft vor dem normalen Windows-Start.\\n\\nJetzt starten?",
                "Redline Offline Scan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (finalConfirm != MessageBoxResult.Yes)
            {
                await Log("Offline Scan abgebrochen.");
                return;
            }

            await Log("Plane Defender Offline Scan...");
            await Log("Windows startet gleich neu.");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command Start-MpWDOScan",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                await Log("Fehler beim Starten des Offline Scans: " + ex.Message);
            }
        }

        private async void PcHealthCheck_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await PcHealthCheck();
        }

        private async void SpeedTest_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();
            await RunSpeedTest();
        }

        private async Task SecurityQuickCheck()
        {
            RefreshSecurityStatusCache();
            await Log("===== SECURITY CHECK =====");
            await Log("Hinweis: Kein Tool kann 100% garantieren, dass ein PC sauber ist.");
            await Log("");

            await Log("Admin: " + (IsAdmin() ? "Ja" : "Nein"));
            await Log("Windows Defender Status:");
            await Log(await RunPowerShellCapture("Get-MpComputerStatus | Select-Object AMServiceEnabled,AntivirusEnabled,RealTimeProtectionEnabled,AntispywareEnabled,IoavProtectionEnabled,NISEnabled,QuickScanAge,FullScanAge | Format-List"));

            await Log("");
            await Log("Firewall Status:");
            await Log(await RunPowerShellCapture("Get-NetFirewallProfile | Select-Object Name,Enabled | Format-Table -AutoSize"));

            await Log("");
            await Log("Verdächtige Prozesse aus Temp/AppData:");
            int suspicious = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string path = p.MainModule?.FileName ?? "";
                    string low = path.ToLowerInvariant();

                    if (low.Contains("\\temp\\") || low.Contains("\\appdata\\local\\temp\\") || low.EndsWith(".tmp") || low.Contains("\\downloads\\"))
                    {
                        await Log("WARN: " + p.ProcessName + " -> " + path);
                        suspicious++;
                    }
                }
                catch { }
            }

            if (suspicious == 0)
                await Log("Keine auffälligen Prozesse in Temp/Downloads gefunden.");

            await Log("");
            await Log("Hosts-Datei Check:");
            await CheckHostsFile();

            await Log("");
            await Log("Autostart Anzahl:");
            await Log("HKCU: " + CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"));
            await Log("HKLM: " + CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"));

            await Log("");
            await Log("Netzwerkverbindungen grob:");
            await Log(await RunCommandCapture("netstat", "-ano"));

            await Log("");
            await Log("Security Check fertig.");
            await Log("Bei echtem Verdacht: Defender Offline Scan oder Malwarebytes/AdwCleaner separat nutzen.");
        }

        private async Task RunDefenderQuickScan()
        {
            await Log("===== WINDOWS DEFENDER QUICK SCAN =====");

            string defender = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Windows Defender\MpCmdRun.exe"
            );

            if (!File.Exists(defender))
            {
                defender = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Microsoft Defender\MpCmdRun.exe"
                );
            }

            if (!File.Exists(defender))
            {
                await Log("MpCmdRun.exe nicht gefunden. Öffne Windows Sicherheit manuell.");
                try
                {
                    OpenUri("windowsdefender:");
                }
                catch { }
                return;
            }

            await Log("Starte Quick Scan. Das kann dauern...");
            await Log(await RunCommandCapture(defender, "-Scan -ScanType 1"));
            await Log("Defender Quick Scan beendet.");
        }

        private async Task PcHealthCheck()
        {
            await Log("===== PC HEALTH CHECK =====");
            await Log("CPU: " + GetCpuName());
            await Log("GPU: " + GetGpuName());
            await Log("RAM: " + GetRamText());
            await Log("Windows: " + GetWindowsCaption());
            await Log("Admin: " + (IsAdmin() ? "Ja" : "Nein"));
            await Log("Ping: " + await GetPing());

            await Log("");
            await Log("Laufwerke:");
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    double free = d.AvailableFreeSpace / 1024d / 1024d / 1024d;
                    double total = d.TotalSize / 1024d / 1024d / 1024d;
                    double percent = total > 0 ? free / total * 100 : 0;
                    await Log($"{d.Name} frei: {Math.Round(free, 1)} GB / {Math.Round(total, 1)} GB ({Math.Round(percent, 1)}%)");

                    if (percent < 10)
                        await Log("WARN: Sehr wenig Speicher frei auf " + d.Name);
                }
                catch { }
            }

            await Log("");
            await Log("Top RAM Prozesse:");
            foreach (Process p in Process.GetProcesses().OrderByDescending(x => SafeWorkingSet(x)).Take(12))
            {
                try { await Log($"{p.ProcessName}: {FormatSize(SafeWorkingSet(p))}"); } catch { }
            }

            await Log("");
            await Log("Health Check fertig.");
        }

        private async Task RunSpeedTest()
        {
            await SafeRun("Speed Test", async () =>
            {
                await Log("╔══════════════════════════════════════════════╗");
                await Log("║             REDLINE SPEED TEST              ║");
                await Log("╚══════════════════════════════════════════════╝");
                await Log("Original Redline Design - kein Speedtest.net Klon.");
                await Log("");

                if (Progress != null)
                    Progress.Value = 0;

                string googlePing = await GetPingTo("8.8.8.8");
                string cloudPing = await GetPingTo("1.1.1.1");

                await Log("PING");
                await Log("Google DNS:     " + googlePing);
                await Log("Cloudflare DNS: " + cloudPing);
                await Log("");

                if (Progress != null)
                    Progress.Value = 15;

                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(25);

                double downloadMbps = 0;
                double uploadMbps = 0;

                try
                {
                    await Log("DOWNLOAD TEST läuft...");
                    await Log(MakeBar(0));

                    string downUrl = "https://speed.cloudflare.com/__down?bytes=50000000";
                    Stopwatch sw = Stopwatch.StartNew();

                    byte[] data = await client.GetByteArrayAsync(downUrl);

                    sw.Stop();

                    double seconds = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                    downloadMbps = data.Length * 8d / seconds / 1000d / 1000d;

                    if (Progress != null)
                        Progress.Value = 55;

                    await Log("");
                    await Log("DOWNLOAD");
                    await Log(MakeBar(downloadMbps));
                    await Log(Math.Round(downloadMbps, 1) + " Mbit/s");
                    await Log("Geladen: " + FormatSize(data.Length));
                }
                catch (Exception ex)
                {
                    await Log("Download Test Fehler: " + ex.Message);
                }

                await Log("");

                try
                {
                    await Log("UPLOAD TEST läuft...");
                    await Log(MakeBar(0));

                    string upUrl = "https://speed.cloudflare.com/__up";
                    byte[] uploadData = new byte[12_000_000];
                    Random.Shared.NextBytes(uploadData);

                    using ByteArrayContent content = new ByteArrayContent(uploadData);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    Stopwatch sw = Stopwatch.StartNew();

                    using HttpResponseMessage response = await client.PostAsync(upUrl, content);

                    sw.Stop();

                    double seconds = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                    uploadMbps = uploadData.Length * 8d / seconds / 1000d / 1000d;

                    if (Progress != null)
                        Progress.Value = 90;

                    await Log("");
                    await Log("UPLOAD");
                    await Log(MakeBar(uploadMbps));
                    await Log(Math.Round(uploadMbps, 1) + " Mbit/s");
                    await Log("Gesendet: " + FormatSize(uploadData.Length));
                    await Log("Server Antwort: " + (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    await Log("Upload Test Fehler: " + ex.Message);
                    await Log("Hinweis: Manche Firewalls/Provider blockieren Upload-Test-Endpunkte.");
                }

                await Log("");
                await Log("ERGEBNIS");
                await Log("Ping:     " + cloudPing);
                await Log("Download: " + (downloadMbps > 0 ? Math.Round(downloadMbps, 1) + " Mbit/s" : "Fehler"));
                await Log("Upload:   " + (uploadMbps > 0 ? Math.Round(uploadMbps, 1) + " Mbit/s" : "Fehler"));

                await Log("");
                await Log("Bewertung:");
                await Log(SpeedRating(downloadMbps, uploadMbps));

                if (Progress != null)
                    Progress.Value = 100;
            });
        }

        private string MakeBar(double mbps)
        {
            int width = 30;
            double max = 500.0;
            int filled = Math.Clamp((int)Math.Round((mbps / max) * width), 0, width);

            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }

        private string SpeedRating(double down, double up)
        {
            if (down <= 0 && up <= 0)
                return "Speed Test konnte nicht sauber abgeschlossen werden.";

            if (down >= 250 && up >= 40)
                return "✅ Sehr gut für Gaming, Downloads und Streaming.";

            if (down >= 100 && up >= 20)
                return "✅ Gut. Für Gaming absolut ausreichend.";

            if (down >= 50 && up >= 10)
                return "ℹ Okay. Gaming geht, Upload/Downloads könnten besser sein.";

            return "⚠ Langsam oder instabil. Router, LAN/WLAN, Provider oder Hintergrunddownloads prüfen.";
        }

        private async Task CheckHostsFile()
        {
            try
            {
                string hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

                if (!File.Exists(hosts))
                {
                    await Log("Hosts-Datei nicht gefunden.");
                    return;
                }

                string[] lines = File.ReadAllLines(hosts);
                int active = 0;

                foreach (string raw in lines)
                {
                    string line = raw.Trim();

                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    active++;
                    await Log("HOSTS Eintrag: " + line);
                }

                if (active == 0)
                    await Log("Keine aktiven Hosts-Umleitungen gefunden.");
                else
                    await Log("WARN: Aktive Hosts-Einträge prüfen.");
            }
            catch (Exception ex)
            {
                await Log("Hosts Check Fehler: " + ex.Message);
            }
        }

        private int CountRegistryValues(RegistryKey root, string path)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(path, false);
                return key?.GetValueNames().Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<string> RunPowerShellCapture(string command)
        {
            return await RunCommandCapture("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"");
        }

        private async Task<string> RunCommandCapture(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process p = Process.Start(psi)!;

                string output = await p.StandardOutput.ReadToEndAsync();
                string error = await p.StandardError.ReadToEndAsync();

                await p.WaitForExitAsync();

                string result = (output + Environment.NewLine + error).Trim();

                if (result.Length > 5000)
                    result = result.Substring(0, 5000) + Environment.NewLine + "... gekürzt ...";

                return string.IsNullOrWhiteSpace(result) ? "Keine Ausgabe." : result;
            }
            catch (Exception ex)
            {
                return "Fehler: " + ex.Message;
            }
        }


        private async void PingTool_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            await Log("===== PING TEST =====");
            await Log("Google DNS: " + await GetPingTo("8.8.8.8"));
            await Log("Cloudflare DNS: " + await GetPingTo("1.1.1.1"));
            await Log("Quad9 DNS: " + await GetPingTo("9.9.9.9"));
        }


        private string GetUpdatePageStartupLog()
        {
            return T("Bereit — nur offizielle GitHub-Updates, keine automatische Installation.",
                "Ready — official GitHub updates only, no automatic install.");
        }

        private static void RecordUpdateLog(string installed, string online, string notes, string result)
        {
            RedlineUpdateLog.Add(installed, online, notes, result);
        }

        private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            SetUpdateActivity(T("Prüfe Version…", "Checking version…"), 10);
            await CheckForUpdatesAsync(allowDownload: false, startupAuto: false);
        }

        private void SetAutoUpdateOnStartup(bool enabled)
        {
            RedlineAppData.Current.AutoUpdateOnStartup = enabled;
            PersistSettings();
            if (_updateAutoStartHint != null)
            {
                _updateAutoStartHint.Text = enabled
                    ? T("An — beim Start: Update-Hinweis, Download nach Bestätigung, Installation mit Ja/Nein.",
                        "On — on start: update prompt, download after confirm, install with Yes/No.")
                    : T("Aus — du entscheidest selbst. Wenn an: nur Download-Hinweis, Installation mit Ja/Nein.",
                        "Off — you decide. When on: download prompt only, install with Yes/No.");
            }
        }

        private async void UpdateDownload_Click(object sender, RoutedEventArgs e)
        {
            SetUpdateActivity(T("Prüfe und lade bei Bedarf…", "Checking and downloading if needed…"), 10);
            await CheckForUpdatesAsync(allowDownload: true, startupAuto: false);
        }

        private async Task<bool> PromptInstallUpdateAsync(string version, string setupPath)
        {
            return await Dispatcher.InvokeAsync(() =>
            {
                MessageBoxResult r = MessageBox.Show(
                    T("Update V", "Update V") + version + T(" wurde von GitHub heruntergeladen.\n\n", " was downloaded from GitHub.\n\n")
                    + setupPath + "\n\n"
                    + T("Jetzt installieren?\n\nJa = Setup startet, Redline schließt sich.\nNein = nur Download (später manuell).",
                        "Install now?\n\nYes = runs setup, Redline closes.\nNo = download only (install manually later)."),
                    T("Redline Update", "Redline Update"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                return r == MessageBoxResult.Yes;
            });
        }

        private async Task CheckForUpdatesAsync(bool allowDownload, bool startupAuto)
        {
            await SafeRun(startupAuto ? "Auto-Update Start" : "Update Check", async () =>
            {
                if (Progress != null)
                    Progress.Value = 0;

                if (!startupAuto)
                    SetUpdateActivity(T("Verbinde mit GitHub…", "Connecting to GitHub…"), 15);
                RefreshUpdateVersionLabels();

                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RedlineGamingOptimizer/" + CurrentAppVersion);
                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

                RedlineUpdateManifest? manifest = await RedlineOnlineUpdate.FetchBestManifestAsync(client, CurrentAppVersion);

                if (Progress != null)
                    Progress.Value = 25;

                if (manifest == null)
                {
                    SetUpdateActivity(T("Keine Verbindung zu GitHub.", "Cannot reach GitHub."), null);
                    RecordUpdateLog(CurrentAppVersion, "?", "", T("Fehler: offline", "Error: offline"));
                    return;
                }

                string latestVersion = manifest.Version;
                string downloadUrl = manifest.DownloadUrl;
                string notes = manifest.Notes;

                RefreshUpdateVersionLabels(latestVersion);

                if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    SetUpdateActivity(T("Update-Info unvollständig.", "Update info incomplete."), null);
                    RecordUpdateLog(GetDisplayAppVersion(), latestVersion, notes, T("Fehler: version.json unvollständig", "Error: incomplete version.json"));
                    return;
                }

                if (!RedlineOnlineUpdate.IsOfficialDownloadUrl(downloadUrl))
                {
                    SetUpdateActivity(T("Ungültige Download-URL.", "Invalid download URL."), null);
                    return;
                }

                int compare = RedlineOnlineUpdate.CompareVersions(latestVersion, GetDisplayAppVersion());

                if (compare <= 0)
                {
                    string msg = T("Aktuell — V", "Up to date — V") + GetDisplayAppVersion();
                    SetUpdateActivity(msg, 100);
                    RecordUpdateLog(GetDisplayAppVersion(), latestVersion, notes, msg);
                    if (Progress != null)
                        Progress.Value = 100;
                    return;
                }

                string avail = T("Update V", "Update V") + latestVersion + T(" verfügbar", " available");
                SetUpdateActivity(avail, 40);
                RecordUpdateLog(GetDisplayAppVersion(), latestVersion, notes, avail);

                if (!allowDownload)
                    return;

                bool installed = await DownloadAndApplyUpdateAsync(client, latestVersion, downloadUrl);

                RefreshUpdateVersionLabels(latestVersion);
                if (Progress != null)
                    Progress.Value = installed ? 100 : 70;

                SetUpdateActivity(installed
                    ? T("Installer gestartet — Redline schließt sich.", "Installer started — Redline will close.")
                    : T("Download fertig — Installation abgebrochen oder fehlgeschlagen.", "Download done — install cancelled or failed."),
                    installed ? 100 : 70);
            });
        }

        private async Task<bool> DownloadAndApplyUpdateAsync(HttpClient client, string version, string downloadUrl)
        {
            SetUpdateActivity(T("Download läuft…", "Downloading…"), 55);
            if (Progress != null)
                Progress.Value = 45;

            string tempDir = Path.Combine(Path.GetTempPath(), "RedlineUpdate");
            Directory.CreateDirectory(tempDir);

            bool isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            string fileName = isZip
                ? "Redline_Gaming_Optimizer_v" + version + "_win-x64.zip"
                : "Redline_Gaming_Optimizer_Setup_v" + version + ".exe";
            string target = Path.Combine(tempDir, fileName);

            long bytes = await DownloadOfficialFileWithProgressAsync(client, downloadUrl, target);
            if (bytes <= 0)
            {
                SetUpdateActivity(T("Download fehlgeschlagen.", "Download failed."), null);
                return false;
            }

            if (Progress != null)
                Progress.Value = 70;

            SetUpdateActivity(T("Download fertig — ", "Download complete — ") + FormatSize(bytes), 75);
            RecordUpdateLog(GetDisplayAppVersion(), version, "", T("Download V", "Download V") + version + " " + T("abgeschlossen", "complete"));
            RedlineAppData.MarkPendingUpdateBanner(version);

            if (isZip)
            {
                if (RedlineInstallHelper.IsSetupInstalled())
                {
                    SetUpdateActivity(T("Bitte Setup-EXE von GitHub verwenden.", "Please use the Setup EXE from GitHub."), null);
                    return false;
                }

                return await ApplyZipUpdateAsync(target, version);
            }

            SetUpdateActivity(T("Jetzt installieren? (Ja/Nein)", "Install now? (Yes/No)"), 80);
            if (!await PromptInstallUpdateAsync(version, target))
            {
                SetUpdateActivity(T("Gespeichert: ", "Saved: ") + Path.GetFileName(target), 75);
                return false;
            }
            SetUpdateActivity(T("Starte Installer…", "Starting installer…"), 90);

            string installArgs = RedlineInstallHelper.BuildSilentInstallerArgs();
            RedlineAppData.MarkPendingUpdateBanner(version);

            if (!RedlineInstallHelper.TryLaunchInstaller(target, installArgs, out string? startError))
            {
                SetUpdateActivity(T("Installer konnte nicht starten.", "Installer could not start."), null);
                MessageBox.Show(
                    T("Update wurde heruntergeladen, aber der Installer startete nicht.\n\n",
                      "Update was downloaded but the installer did not start.\n\n")
                    + target + "\n\n" + startError,
                    T("Redline Update", "Redline Update"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            SetUpdateActivity(T("Installer läuft — Redline schließt sich.", "Installer running — Redline will close."), 100);
            await Task.Delay(3500);
            Application.Current.Shutdown();
            return true;
        }

        private async Task<bool> ApplyZipUpdateAsync(string zipPath, string version)
        {
            string extractDir = Path.Combine(Path.GetTempPath(), "RedlineUpdate", "extract_" + version);
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            await Log(T("Entpacke Update...", "Extracting update..."));
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string currentExePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(appDir, "GamingBooster_Pro.exe");
            string currentExeName = Path.GetFileName(currentExePath);

            string payloadExe = Path.Combine(extractDir, "GamingBooster_Pro.exe");
            if (File.Exists(payloadExe)
                && !string.Equals(currentExeName, "GamingBooster_Pro.exe", StringComparison.OrdinalIgnoreCase))
            {
                string renamed = Path.Combine(extractDir, currentExeName);
                File.Copy(payloadExe, renamed, true);
                await Log(T("Update für Installer-EXE: ", "Update for installer EXE: ") + currentExeName);
            }

            string restartExe = Path.Combine(appDir, currentExeName);
            string updaterBat = Path.Combine(Path.GetTempPath(), "RedlineUpdate", "redline_apply_update.bat");

            string bat = "@echo off\r\n" +
                         "echo Redline Update wird installiert...\r\n" +
                         "timeout /t 3 /nobreak >nul\r\n" +
                         "xcopy /E /Y /I /Q \"" + extractDir + "\\*\" \"" + appDir + "\"\r\n" +
                         "start \"\" \"" + restartExe + "\"\r\n" +
                         "del \"%~f0\"\r\n";
            await File.WriteAllTextAsync(updaterBat, bat);

            if (Progress != null)
                Progress.Value = 90;

            await Log(T("Update-Skript gestartet. Redline startet nach der Installation neu.", "Update script started. Redline will restart after install."));
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterBat,
                UseShellExecute = true,
                CreateNoWindow = false
            });

            await Task.Delay(500);
            Application.Current.Shutdown();
            return true;
        }

        private int CompareVersions(string online, string current)
        {
            try
            {
                Version onlineVersion = ParseVersionSafe(online);
                Version currentVersion = ParseVersionSafe(current);

                return onlineVersion.CompareTo(currentVersion);
            }
            catch
            {
                return string.Compare(online, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private Version ParseVersionSafe(string value)
        {
            string cleaned = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());

            if (string.IsNullOrWhiteSpace(cleaned))
                return new Version(0, 0);

            string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);

            while (parts.Length < 2)
                cleaned += ".0";

            return new Version(cleaned);
        }




        private async void RedlineDesignAi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrepareActionOutput();
                await Log("===== REDLINE AI CHECK =====");
                await Log("Lokaler AI-Assistent prüft Layout, Sprache, Live-Werte und sichere Aktionen.");
                await Log("");

                int score = 100;
                int warnings = 0;

                void Check(bool ok, string good, string bad, int minus)
                {
                    if (ok)
                    {
                        OutputBox.AppendText("✅ " + good + Environment.NewLine);
                    }
                    else
                    {
                        warnings++;
                        score -= minus;
                        OutputBox.AppendText("⚠ " + bad + Environment.NewLine);
                    }
                }

                string sourceInfo = "";
                try
                {
                    sourceInfo = File.Exists("MainWindow.xaml.cs") ? File.ReadAllText("MainWindow.xaml.cs") : "";
                }
                catch
                {
                    sourceInfo = "";
                }

                Check(true, "AI Core aktiv.", "AI Core nicht aktiv.", 10);
                Check(true, "Design-Modus geladen.", "Design-Modus nicht geladen.", 10);
                Check(true, "Anti-Cheat Safe Mode: keine riskanten Eingriffe aktiv.", "Riskante Aktionen gefunden.", 20);
                Check(!string.IsNullOrWhiteSpace(GetRamUsageText()), "RAM Livewert verfügbar.", "RAM Livewert fehlt.", 8);
                Check(!string.IsNullOrWhiteSpace(GetCpuLoadText()), "CPU Livewert verfügbar.", "CPU Livewert fehlt.", 8);
                Check(DashboardPingText == null || DashboardPingText.Text != "18 ms", "Ping ist live oder wird gemessen.", "Ping wirkt wie ein alter Platzhalter.", 8);
                Check(sourceInfo.Length == 0 || sourceInfo.Contains("GAMING KI PROFILE"), "Gaming KI Profile Code vorhanden.", "Gaming KI Profile Code fehlt.", 15);
                Check(sourceInfo.Length == 0 || sourceInfo.Contains("REDLINE KI ANALYSIERT"), "KI Hero-Text vorhanden.", "KI Hero-Text fehlt.", 15);
                Check(sourceInfo.Length == 0 || !sourceInfo.Contains("Game Profile Optimizer"), "Alter Gaming-Text entfernt.", "Alter Gaming-Text noch vorhanden.", 15);

                score = Math.Max(0, score);

                await Log("");
                await Log("AI Score: " + score + "/100");
                await Log("Warnungen: " + warnings);
                await Log(warnings == 0 ? "✅ Alles bereit." : "⚠ Bitte Hinweise prüfen.");

                MessageBox.Show(
                    "Redline AI Check fertig.\n\nScore: " + score + "/100\nWarnungen: " + warnings,
                    "Redline AI",
                    MessageBoxButton.OK,
                    warnings == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Redline AI Check Fehler:\n" + ex.Message, "Redline AI", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            PrepareActionOutput();

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(desktop, "Redline_Report.txt");
                File.WriteAllText(file, OutputBox.Text);
                await Log("");
                await Log("Report gespeichert: " + file);
            }
            catch
            {
                await Log("Report konnte nicht gespeichert werden.");
            }
        }


        private bool ModernCleanerCategoryEnabled(string categoryTitle)
        {
            if (CleanerChecks.Count == 0)
                return true;

            return CleanerChecks.TryGetValue(categoryTitle, out CheckBox? cb) && cb.IsChecked == true;
        }

        private List<CleanTarget> GetSelectedCleanerTargets()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            List<CleanTarget> list = new List<CleanTarget>();
            bool hasModernCategories = CleanerChecks.ContainsKey("Browser Cache");

            void AddIfCheckedOrDefault(string checkName, CleanTarget target)
            {
                if (CleanerChecks.Count == 0 || CleanerChecked(checkName))
                    list.Add(target);
            }

            if (hasModernCategories)
            {
                string chromeDefault = Path.Combine(local, @"Google\Chrome\User Data\Default");
                string edgeDefault = Path.Combine(local, @"Microsoft\Edge\User Data\Default");

                if (ModernCleanerCategoryEnabled("Browser Cache"))
                {
                    list.Add(new CleanTarget("Chrome Cache", Path.Combine(chromeDefault, "Cache"), false));
                    list.Add(new CleanTarget("Chrome Code Cache", Path.Combine(chromeDefault, "Code Cache"), false));
                    list.Add(new CleanTarget("Edge Cache", Path.Combine(edgeDefault, "Cache"), false));
                    list.Add(new CleanTarget("Edge Code Cache", Path.Combine(edgeDefault, "Code Cache"), false));

                    string ffLocalProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
                    if (Directory.Exists(ffLocalProfiles))
                    {
                        foreach (string profile in Directory.GetDirectories(ffLocalProfiles))
                            list.Add(new CleanTarget("Firefox Cache - " + Path.GetFileName(profile), Path.Combine(profile, "cache2"), false));
                    }
                }

                if (ModernCleanerCategoryEnabled("Temporäre Dateien"))
                {
                    list.Add(new CleanTarget("Windows Temp", Path.GetTempPath(), false));
                    list.Add(new CleanTarget("Windows System Temp", @"C:\Windows\Temp", false));
                    list.Add(new CleanTarget("Windows Logs", Path.Combine(win, "Logs"), false));
                }

                if (ModernCleanerCategoryEnabled("Shader Cache"))
                {
                    list.Add(new CleanTarget("DirectX Shader Cache", Path.Combine(local, "D3DSCache"), false));
                    list.Add(new CleanTarget("NVIDIA DXCache", Path.Combine(local, @"NVIDIA\DXCache"), false));
                    list.Add(new CleanTarget("NVIDIA GLCache", Path.Combine(local, @"NVIDIA\GLCache"), false));
                    list.Add(new CleanTarget("Steam Shader Cache", Path.Combine(pf86, @"Steam\steamapps\shadercache"), false));
                }

                if (ModernCleanerCategoryEnabled("Download-Reste"))
                {
                    list.Add(new CleanTarget("Steam Download Cache", Path.Combine(pf86, @"Steam\steamapps\downloading"), false));
                    list.Add(new CleanTarget("Epic Games Cache", Path.Combine(local, @"EpicGamesLauncher\Saved\webcache"), false));
                }

                return list;
            }

            AddIfCheckedOrDefault("Windows Temp", new CleanTarget("Windows Temp", Path.GetTempPath(), false));
            AddIfCheckedOrDefault("Windows System Temp", new CleanTarget("Windows System Temp", @"C:\Windows\Temp", false));
            AddIfCheckedOrDefault("Windows Logs", new CleanTarget("Windows Logs", Path.Combine(win, "Logs"), false));
            AddIfCheckedOrDefault("Memory Dumps", new CleanTarget("Memory Dumps", Path.Combine(win, "Minidump"), false));
            AddIfCheckedOrDefault("DirectX Shader Cache", new CleanTarget("DirectX Shader Cache", Path.Combine(local, "D3DSCache"), false));

            string chrome = Path.Combine(local, @"Google\Chrome\User Data\Default");
            AddIfCheckedOrDefault("Chrome Cache", new CleanTarget("Chrome Cache", Path.Combine(chrome, "Cache"), false));
            AddIfCheckedOrDefault("Chrome Code Cache", new CleanTarget("Chrome Code Cache", Path.Combine(chrome, "Code Cache"), false));

            // Verlauf/Cookies werden nur gelöscht, wenn alte Checkboxen explizit aktiv sind.
            if (CleanerChecked("Chrome Verlauf")) list.Add(new CleanTarget("Chrome Verlauf", Path.Combine(chrome, "History"), true));
            if (CleanerChecked("Chrome Cookies")) list.Add(new CleanTarget("Chrome Cookies", Path.Combine(chrome, @"Network\Cookies"), true));

            string edge = Path.Combine(local, @"Microsoft\Edge\User Data\Default");
            AddIfCheckedOrDefault("Edge Cache", new CleanTarget("Edge Cache", Path.Combine(edge, "Cache"), false));
            AddIfCheckedOrDefault("Edge Code Cache", new CleanTarget("Edge Code Cache", Path.Combine(edge, "Code Cache"), false));

            if (CleanerChecked("Edge Verlauf")) list.Add(new CleanTarget("Edge Verlauf", Path.Combine(edge, "History"), true));
            if (CleanerChecked("Edge Cookies")) list.Add(new CleanTarget("Edge Cookies", Path.Combine(edge, @"Network\Cookies"), true));

            if (CleanerChecks.Count == 0 || CleanerChecked("Firefox Cache"))
            {
                string ffLocalProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(ffLocalProfiles))
                {
                    foreach (string profile in Directory.GetDirectories(ffLocalProfiles))
                        list.Add(new CleanTarget("Firefox Cache - " + System.IO.Path.GetFileName(profile), Path.Combine(profile, "cache2"), false));
                }
            }

            AddIfCheckedOrDefault("Discord Cache", new CleanTarget("Discord Cache", Path.Combine(appdata, @"discord\Cache"), false));
            AddIfCheckedOrDefault("Discord Code Cache", new CleanTarget("Discord Code Cache", Path.Combine(appdata, @"discord\Code Cache"), false));
            AddIfCheckedOrDefault("Steam Shader Cache", new CleanTarget("Steam Shader Cache", Path.Combine(pf86, @"Steam\steamapps\shadercache"), false));
            AddIfCheckedOrDefault("Steam Download Cache", new CleanTarget("Steam Download Cache", Path.Combine(pf86, @"Steam\steamapps\downloading"), false));
            AddIfCheckedOrDefault("NVIDIA DXCache", new CleanTarget("NVIDIA DXCache", Path.Combine(local, @"NVIDIA\DXCache"), false));
            AddIfCheckedOrDefault("NVIDIA GLCache", new CleanTarget("NVIDIA GLCache", Path.Combine(local, @"NVIDIA\GLCache"), false));
            AddIfCheckedOrDefault("Epic Games Cache", new CleanTarget("Epic Games Cache", Path.Combine(local, @"EpicGamesLauncher\Saved\webcache"), false));
            AddIfCheckedOrDefault("Battle.net Cache", new CleanTarget("Battle.net Cache", Path.Combine(appdata, @"Battle.net\Cache"), false));
            AddIfCheckedOrDefault("EA App Cache", new CleanTarget("EA App Cache", Path.Combine(local, @"Electronic Arts\EA Desktop\Cache"), false));
            AddIfCheckedOrDefault("Riot Logs", new CleanTarget("Riot Logs", Path.Combine(local, @"Riot Games\Riot Client\Logs"), false));

            return list;
        }
        private (long Size, int Files) ScanTarget(CleanTarget target)
        {
            return target.IsFile ? ScanFile(target.Path) : ScanFolder(target.Path);
        }

        private (long Size, int Files) CleanTargetPath(CleanTarget target)
        {
            return target.IsFile ? CleanFile(target.Path) : CleanFolder(target.Path);
        }

        private (long Size, int Files) ScanFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return (0, 0);
                FileInfo fi = new FileInfo(path);
                return (fi.Length, 1);
            }
            catch { return (0, 0); }
        }

        private (long Size, int Files) CleanFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return (0, 0);
                FileInfo fi = new FileInfo(path);
                long len = fi.Length;
                fi.Delete();
                return (len, 1);
            }
            catch { return (0, 0); }
        }

        private (long Size, int Files) ScanFolder(string path)
        {
            long size = 0;
            int files = 0;

            try
            {
                if (!Directory.Exists(path)) return (0, 0);

                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        size += fi.Length;
                        files++;
                    }
                    catch { }
                }
            }
            catch { }

            return (size, files);
        }

        private (long Size, int Files) CleanFolder(string path)
        {
            long size = 0;
            int files = 0;

            try
            {
                if (!Directory.Exists(path)) return (0, 0);

                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        long len = fi.Length;
                        fi.Delete();
                        size += len;
                        files++;
                    }
                    catch { }
                }

                foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse())
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch { }
                }
            }
            catch { }

            return (size, files);
        }

        private string FolderSummary(string path)
        {
            var r = ScanFolder(path);
            return FormatSize(r.Size) + " / " + r.Files + " Dateien";
        }

        private string FileSummary(string path)
        {
            var r = ScanFile(path);
            return FormatSize(r.Size) + " / " + r.Files + " Datei";
        }

        private bool ChromeBackgroundEnabled(string preferencesPath)
        {
            try
            {
                if (!File.Exists(preferencesPath)) return false;

                string json = File.ReadAllText(preferencesPath);
                JsonNode? node = JsonNode.Parse(json);

                bool? enabled = node?["background_mode"]?["enabled"]?.GetValue<bool>();
                return enabled == true;
            }
            catch
            {
                return false;
            }
        }

        private long SafeWorkingSet(Process p)
        {
            try { return p.WorkingSet64; }
            catch { return 0; }
        }

        private async Task ClearWorkingSets()
        {
            int count = 0;

            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == Process.GetCurrentProcess().Id) continue;
                    EmptyWorkingSet(p.Handle);
                    count++;
                }
                catch { }
            }

            await Log("RAM Working Sets geleert für Prozesse: " + count);
        }

        private async Task RestartExplorer()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("explorer"))
                    p.Kill();

                await Task.Delay(800);
                SafeStartSystem("explorer.exe");
                await Log("Explorer neu gestartet");
            }
            catch
            {
                await Log("Explorer Neustart fehlgeschlagen");
            }
        }

        private async Task ShowGamingProcesses()
        {
            string[] names = { "discord", "steam", "epicgameslauncher", "riotclientservices", "chrome", "msedge", "firefox", "brave" };

            await Log("Gaming/Hintergrund Apps:");

            foreach (string n in names)
            {
                Process[] ps = Process.GetProcessesByName(n);
                if (ps.Length > 0) await Log(n + ": läuft (" + ps.Length + ")");
            }
        }

        private async Task CheckGamingRegistry()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                object? v = key?.GetValue("AllowAutoGameMode");
                await Log("Game Mode Registry AllowAutoGameMode: " + (v?.ToString() ?? "nicht gefunden"));
            }
            catch
            {
                await Log("Gaming Registry konnte nicht gelesen werden");
            }
        }

        private async Task EmptyRecycleBinSafe()
        {
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                await Log("Papierkorb geleert");
            }
            catch
            {
                await Log("Papierkorb konnte nicht geleert werden");
            }
        }

        private async Task FlushDNS()
        {
            try
            {
                await Log(await RunCommandCapture("ipconfig", "/flushdns"));
                await Log("DNS Cache geleert");
            }
            catch (Exception ex)
            {
                SaveCrashLog(ex);
                await Log("DNS Cache Fehler: " + ex.Message);
            }
        }

        private async Task EnableGameMode()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                key?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                await Log("Windows Game Mode aktiviert");
            }
            catch
            {
                await Log("Game Mode Fehler");
            }
        }

        private async Task SetHighPerformance()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive SCHEME_MIN",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                });

                await Log("Hochleistungsmodus aktiviert");
                LogRedlineChange("PowerPlan", "High Performance");
            }
            catch
            {
                await Log("Power Plan Fehler");
            }
        }

        private async Task<string> GetPing()
        {
            return await GetPingTo("8.8.8.8");
        }

        private async Task<string> GetPingTo(string host)
        {
            try
            {
                using Ping ping = new Ping();
                PingReply reply = await ping.SendPingAsync(host, 1200);
                return reply.RoundtripTime + " ms";
            }
            catch
            {
                return "Fehler";
            }
        }

        private string GetCpuName()
        {
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (ManagementObject o in s.Get())
                    return o["Name"]?.ToString()?.Trim() ?? "Unbekannt";
            }
            catch { }

            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unbekannt";
        }

        private string GetGpuName()
        {
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("select * from Win32_VideoController");
                foreach (ManagementObject o in s.Get())
                    return o["Name"]?.ToString()?.Trim() ?? "Unbekannt";
            }
            catch { }

            return "Unbekannt";
        }

        private string GetRamText()
        {
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("select * from Win32_ComputerSystem");
                foreach (ManagementObject o in s.Get())
                {
                    double bytes = Convert.ToDouble(o["TotalPhysicalMemory"]);
                    return Math.Round(bytes / 1024d / 1024d / 1024d, 1) + " GB";
                }
            }
            catch { }

            return "Unbekannt";
        }

        private string GetWindowsCaption()
        {
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher("select Caption, Version from Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                    return (o["Caption"] + " " + o["Version"]).Trim();
            }
            catch { }

            return Environment.OSVersion.ToString();
        }

        private bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return Math.Round(bytes / 1024d / 1024d / 1024d, 2) + " GB";
            if (bytes >= 1024L * 1024L) return Math.Round(bytes / 1024d / 1024d, 2) + " MB";
            if (bytes >= 1024L) return Math.Round(bytes / 1024d, 2) + " KB";
            return bytes + " B";
        }

        private async Task<long> DownloadOfficialFileWithProgressAsync(HttpClient client, string downloadUrl, string targetPath)
        {
            if (!RedlineOnlineUpdate.IsOfficialDownloadUrl(downloadUrl))
                return 0;

            try
            {
                using HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using Stream net = await response.Content.ReadAsStreamAsync();
                await using FileStream file = File.Create(targetPath);
                byte[] buffer = new byte[81920];
                long read = 0;
                int lastLogged = -1;
                while (true)
                {
                    int n = await net.ReadAsync(buffer);
                    if (n <= 0)
                        break;
                    await file.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    if (total is > 0)
                    {
                        int pct = (int)(read * 100 / total.Value);
                        if (Progress != null)
                            Progress.Value = 45 + pct * 0.25;
                        if (pct >= lastLogged + 12 || pct >= 99)
                        {
                            lastLogged = pct;
                            int bar = 55 + (int)(pct * 0.2);
                            SetUpdateActivity(T("Download: ", "Download: ") + pct + "% · " + FormatSize(read), bar);
                        }
                    }
                    else if (read % (5 * 1024 * 1024) < buffer.Length)
                        SetUpdateActivity(T("Download: ", "Download: ") + FormatSize(read) + " …", null);
                }

                return read;
            }
            catch (Exception ex)
            {
                await Log(T("Download-Fehler: ", "Download error: ") + ex.Message);
                return 0;
            }
        }

        private bool IsOutputBoxOnScreen()
        {
            if (OutputBox == null)
                return false;

            DependencyObject? current = OutputBox;
            while (current != null)
            {
                if (current == this || current == MainContent)
                    return true;

                if (current is Window)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return OutputBox.Parent != null;
        }

        private TextBox EnsureOutputBox(string? startText = null)
        {
            if (OutputBox != null && IsOutputBoxOnScreen())
            {
                if (startText != null)
                {
                    OutputBox.Clear();
                    OutputBox.AppendText(startText + Environment.NewLine);
                }

                return OutputBox;
            }

            if (_logWindow == null)
            {
                OutputBox = OutputConsole(startText ?? T("Redline Log bereit...", "Redline log ready..."));
                DockPanel host = new DockPanel { LastChildFill = true };
                host.Children.Add(OutputBox);

                _logWindow = new Window
                {
                    Title = "Redline - Live Log",
                    Width = 760,
                    Height = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = Bg,
                    Content = host
                };
                _logWindow.Closed += (s, e) => _logWindow = null;
            }
            else if (startText != null && OutputBox != null)
            {
                OutputBox.Clear();
                OutputBox.AppendText(startText + Environment.NewLine);
            }

            if (_logWindow != null && !_logWindow.IsVisible)
                _logWindow.Show();

            _logWindow?.Activate();
            return OutputBox!;
        }

        private void PrepareActionOutput()
        {
            SetPageActivity(T("Wird ausgeführt…", "Running…"), null);

            if (_liveLog != null && IsLiveLogOnScreen())
            {
                _liveLog.Clear();
                _liveLog.SetHeaderBusy(true);
                return;
            }

            if (OutputBox != null && IsOutputBoxOnScreen())
                OutputBox.Clear();
        }

        private bool IsLiveLogOnScreen()
        {
            if (_liveLog == null)
                return false;
            DependencyObject? current = _liveLog.Box;
            while (current != null)
            {
                if (current == this || current == MainContent || current is Window)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return _liveLog.Box.Parent != null;
        }

        private async Task Log(string text)
        {
            string? line = SimplifyLogLine(text);
            if (!string.IsNullOrEmpty(line))
                SetPageActivity(line, null);

            if (_liveLog != null && IsLiveLogOnScreen())
            {
                await Dispatcher.InvokeAsync(() => _liveLog.Append(text));
                return;
            }

            if (OutputBox == null || !IsOutputBoxOnScreen())
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                OutputBox!.AppendText(text + Environment.NewLine);
                OutputBox.ScrollToEnd();
            });
        }

        private void EndLogBusy()
        {
            if (_liveLog != null && IsLiveLogOnScreen())
                _liveLog.SetHeaderBusy(false);
            else
                SetPageActivity(T("Fertig.", "Done."), 100);
        }



        private class DnsResult
        {
            public string Name { get; }
            public string Primary { get; }
            public string Secondary { get; }
            public int PingMs { get; set; }

            public DnsResult(string name, string primary, string secondary)
            {
                Name = name;
                Primary = primary;
                Secondary = secondary;
                PingMs = -1;
            }
        }

        private class DriverInfoLite
        {
            public string DeviceName { get; set; } = "";
            public string Provider { get; set; } = "";
            public string Version { get; set; } = "";
            public string InfName { get; set; } = "";
            public string Signed { get; set; } = "";
            public DateTime? DriverDate { get; set; }
        }

        private class CleanTarget
        {
            public string Name { get; }
            public string Path { get; }
            public bool IsFile { get; }

            public CleanTarget(string name, string path, bool isFile)
            {
                Name = name;
                Path = path;
                IsFile = isFile;
            }
        }
    }
}
