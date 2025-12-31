using System;
using System.Windows;

namespace KernelDash
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                string message = $"Fehler: {args.ExceptionObject}";
                MessageBox.Show(message, "Kritischer Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                string message = $"UI-Fehler: {args.Exception}";
                MessageBox.Show(message, "UI-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}

