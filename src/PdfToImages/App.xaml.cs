using System.Windows;
using System.Windows.Threading;

namespace PdfToImages;

/// <summary>
/// Application entry point. Also hosts a global exception handler so that
/// an unexpected error never crashes the process silently - reliability
/// is the top priority for this tool.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"אירעה שגיאה בלתי צפויה:\n\n{e.Exception.Message}\n\nהתוכנה תמשיך לפעול.",
            "שגיאה",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"אירעה שגיאה חמורה:\n\n{ex.Message}",
                "שגיאה קריטית",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
