using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace RagnaController
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogFatal(ex.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
            DispatcherUnhandledException += (s, ex) =>
            {
                LogFatal(ex.Exception?.ToString() ?? "Unknown dispatcher exception");
                ex.Handled = true;
            };

            base.OnStartup(e);
            StartWorkflow();
        }

        private async void StartWorkflow()
        {
            try
            {
                var splash = new SplashWindow();
                MainWindow = splash;
                splash.Show();

                await Task.Delay(1000);
                splash.PlayVoice();
                await Task.Delay(3500);

                var main = new MainWindow();
                MainWindow = main;
                main.Opacity = 0;
                main.Show();

                await Task.Delay(200);
                splash.FadeAndClose(600);

                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
                main.BeginAnimation(Window.OpacityProperty, anim);
            }
            catch (Exception ex)
            {
                LogFatal(ex.ToString());
                MessageBox.Show("Fatal startup error. See startup_error.txt", "RagnaController");
                Shutdown();
            }
        }

        private static void LogFatal(string msg)
        {
            try { File.WriteAllText("startup_error.txt", msg); } catch { }
        }
    }
}
