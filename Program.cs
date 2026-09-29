using System;
using System.IO;
using System.Windows.Forms;

namespace RockeyPasswordTester
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Never vanish without a trace: route every unhandled error to a crash log + a message box
            // instead of the window silently disappearing.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportCrash("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash("background thread", e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { ReportCrash("task", e.Exception); e.SetObserved(); };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // "--resumed" is passed by an automatic recovery restart: the fresh (still-elevated) process
            // resets the dongles from a clean state and auto-continues the run from the logs.
            bool resumed = Array.IndexOf(args, "--resumed") >= 0;
            Application.Run(new MainForm(resumed));
        }

        private static void ReportCrash(string where, Exception? ex)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({where})\r\n{ex}\r\n\r\n";
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash-log.txt"), entry);
            }
            catch { }
            try
            {
                MessageBox.Show(
                    $"An unexpected error occurred on the {where}:\n\n{ex?.Message}\n\nThe full details were saved to crash-log.txt next to the app.\nThe app will keep running if it can.",
                    "Rockey Tester - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}