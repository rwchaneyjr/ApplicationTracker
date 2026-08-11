using JobApplicationTracker.Data;
using JobApplicationTracker.Forms;

namespace JobApplicationTracker;

/// <summary>
/// Application entry point. Creates the SQLite database on first launch
/// and starts the main Windows Forms UI.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

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
