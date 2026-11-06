using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Lanceur.Services;

namespace Lanceur;

public partial class App : Application
{
    private const string MutexName = "Lanceur.SingleInstance";
    private const string ShowEventName = "Lanceur.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Une seule instance : si l'application tourne déjà, on la ramène au premier plan.
        var user = Environment.UserName;
        _mutex = new Mutex(true, $"{MutexName}.{user}", out var isFirst);
        if (!isFirst)
        {
            try
            {
                using var ev = EventWaitHandle.OpenExisting($"{ShowEventName}.{user}");
                ev.Set();
            }
            catch { /* l'autre instance est en train de se fermer */ }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"{ShowEventName}.{user}");
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowAndActivate());
        }) { IsBackground = true, Name = "SingleInstanceListener" }.Start();

        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        var config = ConfigService.Load();
        ThemeService.Apply(config);

        var fromStartup = e.Args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        var window = new MainWindow(config);
        MainWindow = window;
        window.Start(startHidden: fromStartup && config.StartMinimized);

        if (ConfigService.LoadWarning is { } warning)
            Dialogs.Info(window, "Configuration réinitialisée", warning);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ConfigService.AppDataDir);
            File.AppendAllText(Path.Combine(ConfigService.AppDataDir, "erreurs.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch { }
        MessageBox.Show($"Une erreur inattendue est survenue :\n\n{e.Exception.Message}\n\n" +
                        $"Détails enregistrés dans {Path.Combine(ConfigService.AppDataDir, "erreurs.log")}",
                        "Lanceur", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
