using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RagnaController
{
    public partial class App : Application
    {
        private const int RequiredMajor = 8;
        private const int RequiredMinor = 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;

            if (!IsRuntimeSufficient(out string found))
            {
                var result = MessageBox.Show(
                    $"RagnaController requires .NET {RequiredMajor}.{RequiredMinor} or newer.\n\n" +
                    $"Found version: {found}\n\n" +
                    $"Required: .NET Windows Desktop Runtime {RequiredMajor}+\n\n" +
                    $"Open Microsoft download page now?",
                    "Missing Requirement – .NET Runtime",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = "https://aka.ms/dotnet-8-windowsdesktop-x64", UseShellExecute = true });

                Shutdown(1);
                return;
            }

            base.OnStartup(e);
            ShowSplashThenMain();
        }

        private static async void ShowSplashThenMain()
        {
            try
            {
                // ── Splash anzeigen ──────────────────────────────────────────
                var splash = new SplashWindow();
                splash.Show();

                // Wait until FadeIn (700ms) + Windows compositor delay (~500ms) = 1200ms
                // Only then play startup voice — splash is guaranteed visible on screen
                _ = Task.Delay(1200).ContinueWith(_ => 
                    Application.Current.Dispatcher.Invoke(() => splash.PlayVoice()),
                    System.Threading.Tasks.TaskContinuationOptions.None);

                // ── 5 Sekunden Splash anzeigen ───────────────────────────────
                // (Animationen laufen komplett durch: Logo-Pop ~0.7s, alle Partikel, Progress ~3.8s)
                await Task.Delay(5000);

                // ── MainWindow vorbereiten: unsichtbar, aber bereits geladen ─
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Opacity = 0;
                main.Show();

                // Kurze Pause damit MainWindow seinen Render-Pass abschließt
                // (verhindert "weißen Blitz" beim ersten Erscheinen)
                await Task.Delay(80);

                // ── Crossfade: Splash aus, MainWindow ein ────────────────────
                splash.FadeAndClose(durationMs: 600);
                FadeInWindow(main, durationMs: 600);
            }
            catch (Exception ex)
            {
                WriteCrashLog("ShowSplashThenMain", ex);
                MessageBox.Show(
                    $"Startup error:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "RagnaController – Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(1);
            }
        }

        /// <summary>Blendet ein Window von Opacity 0 auf 1 ein.</summary>
        private static void FadeInWindow(Window window, int durationMs)
        {
            var anim = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(durationMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior   = FillBehavior.Stop   // Animation danach entfernen
            };
            // Nach Animation: Opacity als lokalen Wert auf 1 setzen
            // Ohne dies: FillBehavior.Stop würde auf den lokalen Wert (0) zurückfallen
            anim.Completed += (_, _) =>
            {
                window.BeginAnimation(Window.OpacityProperty, null); // Animation entfernen
                window.Opacity = 1;                                   // Lokalen Wert sichern
            };
            window.BeginAnimation(Window.OpacityProperty, anim);
        }

        // ── Exception Handlers ───────────────────────────────────────────────

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog("UI Thread", e.Exception);
            MessageBox.Show(
                $"Unhandled UI error:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "RagnaController – Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Current.Shutdown(1);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                WriteCrashLog("AppDomain", ex);
        }

        private static void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog("Task", e.Exception);
            e.SetObserved();
        }

        private static void WriteCrashLog(string context, Exception ex)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string path    = Path.Combine(desktop, "RagnaController_crash.txt");
                string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH in {context}\r\n" +
                                 $"Type:    {ex.GetType().FullName}\r\n" +
                                 $"Message: {ex.Message}\r\n" +
                                 $"Stack:\r\n{ex.StackTrace}\r\n";
                if (ex.InnerException != null)
                    content += $"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}\r\n";
                File.AppendAllText(path, content + "\r\n---\r\n");
            }
            catch { }
        }

        private static bool IsRuntimeSufficient(out string foundVersion)
        {
            var ver = Environment.Version;
            foundVersion = $".NET {ver.Major}.{ver.Minor}.{ver.Build}";
            if (ver.Major > RequiredMajor) return true;
            if (ver.Major == RequiredMajor && ver.Minor >= RequiredMinor) return true;
            return false;
        }
    }
}
