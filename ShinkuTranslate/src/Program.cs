using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShinkuTranslate.forms;
using ShinkuTranslate.translation.edict;
using ShinkuTranslate.misc;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace ShinkuTranslate {
    static class Program {

        private static System.Threading.Timer settingsSaveTimer;

        public static string[] arguments;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] cmd) {
            arguments = cmd;
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                Exception ex = (Exception)args.ExceptionObject;
                Logger.logException(ex);
            };
            Task.Factory.StartNew(() => {
                Edict.instance.initialize();
            });
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += new EventHandler((_1, _2) => {
                try {
                    if (settingsSaveTimer != null) {
                        settingsSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    settings.Settings.app.save();
                } catch (Exception e) {
                    Logger.logException(e);
                }
                
            });
            settingsSaveTimer = new System.Threading.Timer(new TimerCallback((_) => {
                try {
                    settings.Settings.app.save();
                } catch { }
            }), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            StartPythonScript();
            Application.Run(new MainForm() { WindowState = FormWindowState.Minimized });
        }
        static void StartPythonScript()
        {
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pythonInterpreterPath = Path.Combine(executableDirectory, @"sugoi\Python\python.exe");
            string pythonScriptPath = Path.Combine(executableDirectory, @"sugoi\pys\yojetServer.py -g");

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = pythonInterpreterPath;
            startInfo.Arguments = pythonScriptPath;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

            Process pythonProcess = new Process();
            pythonProcess.StartInfo = startInfo;
            pythonProcess.Start();
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                if (!pythonProcess.HasExited)
                {
                    pythonProcess.Kill();
                    pythonProcess.WaitForExit();
                }
            };
        }
    }
}