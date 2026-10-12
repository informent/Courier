using System.Windows;
using System.IO;
namespace Courier;
public partial class App : System.Windows.Application
{
    public App()
    {
        Startup += (_, e) =>
        {
            if (ReceiptCommand.IsCommand(e.Args))
            {
                try { Shutdown(ReceiptCommand.Run(e.Args)); }
                catch (Exception) { Shutdown(2); }
                return;
            }
            Diagnostics.Record("startup"); new MainWindow().Show();
        };
        DispatcherUnhandledException += (_, e) =>
        {
            Diagnostics.Record("fatal-ui", e.Exception);
            System.Windows.MessageBox.Show("Courier encountered an unexpected error. A diagnostic was attempted in Downloads\\GITHUB\\Courier\\Logs. Restart the app before trying again.", "Courier error");
            e.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Diagnostics.Record("fatal-runtime", e.ExceptionObject as Exception);
    }
}
public static class Diagnostics
{
    public static void Record(string operation, Exception? exception = null)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "GITHUB", "Courier", "Logs");
            Directory.CreateDirectory(folder);
            // Never log exception messages, paths, file names, pairing codes or file contents.
            var line = $"{DateTimeOffset.UtcNow:O} {operation} {exception?.GetType().Name ?? "OK"} {exception?.HResult.ToString("X8") ?? ""}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(folder, $"Courier-{DateTime.UtcNow:yyyyMMdd}-{Environment.ProcessId}.log"), line);
        }
        catch { /* Diagnostics must never prevent startup or recovery. */ }
    }
}
