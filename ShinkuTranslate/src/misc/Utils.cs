﻿using System;
using System.Linq;
using System.Text;

using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Configuration;
using ShinkuTranslate.forms;
using ShinkuTranslate.settings;
using System.Management;
using System.Drawing;
using System.Net;

namespace ShinkuTranslate.misc {
    static class Utils {
        public static string getRootPath() {
            return Application.StartupPath;
        }

        public static Uri getUriForBrowser(string page) {
            return new Uri("file://" + Path.GetFullPath(Path.Combine(getRootPath(), "www", page)));
        }

        public static string toJson(object data)
        {
            return JsonSerializer.Serialize(data);
        }

        public static int startProcess(string exeName) {
            if (isJapaneseLocale()) {
                return startProcessNormal(exeName);
            } else {
                NonJapaneseLocaleWatDo watDo;
                if (Settings.app.nonJpLocaleAsk) {
                    watDo = NonJapaneseLocaleForm.show();
                } else {
                    watDo = Settings.app.nonJpLocale;
                }
                switch (watDo) {
                    case NonJapaneseLocaleWatDo.USE_LOCALE_EMULATOR:
                        return startProcessLE(exeName);
                    case NonJapaneseLocaleWatDo.USE_APPLOCALE:
                        return startProcessAppLocale(exeName);
                    case NonJapaneseLocaleWatDo.RUN_ANYWAY:
                        return startProcessNormal(exeName);
                    case NonJapaneseLocaleWatDo.ABORT:
                        return 0;
                    default:
                        throw new MyException("NonJapaneseLocaleWatDo");
                }
            }
        }

        private static int startProcessAppLocale(string exeName) {
            if (!isAppLocaleInstalled()) {
                Settings.app.nonJpLocaleAsk = true;
                return startProcess(exeName);
            }
            ProcessStartInfo pi = new ProcessStartInfo();
            string agthPath = Path.Combine(getRootPath(), @"tools\agth\agth.exe");
            pi.FileName = agthPath;
            pi.Arguments = "/L /NH \"" + exeName + "\"";
            pi.UseShellExecute = false;
            pi.WorkingDirectory = Path.GetDirectoryName(exeName);
            Process res = Process.Start(pi);
            res.WaitForInputIdle(5000);
            return getChildPid(res.Id);
        }

        public static int startProcessLE(string exeName) {
            if (!isWindowsVistaOrLater()) {
                Settings.app.nonJpLocaleAsk = true;
                return startProcess(exeName);
            }
            ProcessStartInfo pi = new ProcessStartInfo();
            string lePath = Path.Combine(getRootPath(), @"tools\le\LEProc.exe");
            pi.FileName = lePath;
            pi.Arguments = "-runas \"a4bb3b58-1243-4cc1-b72b-49f549a8b448\" \"" + exeName + "\"";
            pi.UseShellExecute = false;
            pi.WorkingDirectory = Path.GetDirectoryName(lePath);
            Process res = Process.Start(pi);
            res.WaitForInputIdle(5000);
            return getChildPid(res.Id);
        }

        private static int getChildPid(int pid) {
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select ProcessID from Win32_Process where ParentProcessID=" + pid);
            ManagementObject mo = mos.Get().OfType<ManagementObject>().FirstOrDefault();
            if (mo == null) {
                return 0;
            } else {
                return Convert.ToInt32(mo["ProcessID"]);
            }
        }

        public static int startProcessNormal(string exeName) {
            ProcessStartInfo pi = new ProcessStartInfo();
            pi.FileName = exeName;
            pi.UseShellExecute = true;
            pi.WorkingDirectory = Path.GetDirectoryName(exeName);
            Process res = Process.Start(pi);
            res.WaitForInputIdle(5000);
            return res.Id;
        }

        private static string appPath;
        internal static string getAppDataPath() {
            if (appPath == null) {
                appPath = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
            }
            return appPath;
        }

        internal static bool confirm(string p) {
            return MessageBox.Show(p, "Shinku Translate", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        internal static void error(string p) {
            MessageBox.Show(p, "Shinku Translate", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        internal static bool isJapaneseLocale() {
            return Winapi.GetSystemDefaultLCID() == 1041;
        }

        internal static bool isAppLocaleInstalled() {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"AppPatch\AppLoc.exe"));
        }

        internal static bool isWindowsVistaOrLater() {
            return Environment.OSVersion.Version.Major >= 6;
        }

        internal static void info(string p) {
            MessageBox.Show(p, "Shinku Translate", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        internal static void setWindowNoActivate(IntPtr hwnd, bool isNoActivate = true) {
            int oldStyle = Winapi.GetWindowLong(hwnd, Winapi.GWL_EXSTYLE);
            if (isNoActivate) {
                Winapi.SetWindowLong(hwnd, Winapi.GWL_EXSTYLE, oldStyle | Winapi.WS_EX_NOACTIVATE);
            } else {
                Winapi.SetWindowLong(hwnd, Winapi.GWL_EXSTYLE, oldStyle & ~Winapi.WS_EX_NOACTIVATE);
            }
        }

        internal static bool isFullscreen() {
            Winapi.RECT appBounds;
            Rectangle screenBounds;
            IntPtr hWnd;

            //get the dimensions of the active window
            hWnd = Winapi.GetForegroundWindow();
            IntPtr desktopHandle = Winapi.GetDesktopWindow();
            IntPtr shellHandle = Winapi.GetShellWindow();
            if (hWnd != null && !hWnd.Equals(IntPtr.Zero)) {
                //Check we haven't picked up the desktop or the shell
                if (!(hWnd.Equals(desktopHandle) || hWnd.Equals(shellHandle))) {
                    Winapi.GetWindowRect(hWnd, out appBounds);
                    //determine if window is fullscreen
                    screenBounds = Screen.FromHandle(hWnd).Bounds;
                    if ((appBounds.Bottom - appBounds.Top) == screenBounds.Height && (appBounds.Right - appBounds.Left) == screenBounds.Width) {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static string httpRequest(string url, bool useShiftJis, string method, string query) {
            WebRequest req;
            if (method.ToUpper() == "GET") {
                req = WebRequest.Create(url);
                req.Proxy = null;
                (req as HttpWebRequest).UserAgent = "Mozilla/5.0 (compatible; Windows NT 6.1; WOW64)";
            } else {
                req = WebRequest.Create(url);
                req.Method = method.ToUpper();
                req.ContentType = "application/x-www-form-urlencoded";
                req.Proxy = null;
                (req as HttpWebRequest).UserAgent = "Mozilla/5.0 (compatible; Windows NT 6.1; WOW64)";
                var content = Encoding.UTF8.GetBytes(query);
                req.ContentLength = content.Length;
                Stream os = req.GetRequestStream();
                os.Write(content, 0, content.Length);
                os.Close();
            }
            WebResponse resp = req.GetResponse();
            StreamReader sr = new StreamReader(resp.GetResponseStream(), useShiftJis ? Encoding.GetEncoding(932) : Encoding.UTF8);
            var res = sr.ReadToEnd().Trim();
            return res;
        }

    }
}
