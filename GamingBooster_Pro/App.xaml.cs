using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GamingBooster_Pro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int code = RedlineSelfTest.RunAll();
                Environment.Exit(code);
                return;
            }

            MainWindow window = new MainWindow();
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);
            window.Show();
        }
    }
}
