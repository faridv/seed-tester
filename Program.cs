using System;
using System.Windows.Forms;

namespace RockeyPasswordTester
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // "--resumed" is passed by an automatic recovery restart: the fresh (still-elevated) process
            // resets the dongles from a clean state and auto-continues the run from the logs.
            bool resumed = Array.IndexOf(args, "--resumed") >= 0;
            Application.Run(new MainForm(resumed));
        }
    }
}