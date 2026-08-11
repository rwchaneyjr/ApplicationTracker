using System.Runtime.InteropServices;
using JobApplicationTracker.Data;
using JobApplicationTracker.Forms;

namespace JobApplicationTracker;

/// <summary>
/// Application entry point. Creates the SQLite database on first launch
/// and starts the main Windows Forms UI.
/// </summary>
internal static class Program
{
    // Stable Windows identity so taskbar / Start pinning stays on one icon.
    private const string AppUserModelId = "JobApplicationTracker.Desktop.1";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    [STAThread]
    private static void Main()
    {
        try
        {
            _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch
        {
            // Older Windows builds may not support this call; safe to ignore.
        }

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        try
        {
            DatabaseHelper.InitializeDatabase();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to initialize the local database.\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}
