using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace OriginSteamOverlayLauncher
{
    public class ProcessUtils
    {
        private static readonly object writerLock = new object();

        #region Imports
        // for custom modal support
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string msg, string caption, int type);
        #endregion

        public static bool CliArgExists(string[] args, string matchArg)
        {// credit to: https://stackoverflow.com/a/30569947
            string _argType1 = $"/{matchArg.ToLower()}";
            string _argType2 = $"-{matchArg.ToLower()}";

            // we accept -arg and /arg formats
            var singleFound = args.Where(
                    w => OrdinalEquals(w.ToLower(), _argType1)
                    || OrdinalEquals(w.ToLower(), _argType2)
                ).FirstOrDefault();

            if (singleFound != null)
            {
                return true;
            }

            return false;
        }

        public static void Logger(string cause, string message)
        {
            lock (writerLock)
            {
                string _msg = $"[{DateTime.Now.ToLocalTime()}] [{cause}] {message}";
                byte[] _encMsg = Encoding.Unicode.GetBytes(_msg + "\r\n");
                using (FileStream stream = File.Open(Program.LogFile, FileMode.Open))
                {
                    stream.Seek(0, SeekOrigin.End);
                    stream.WriteAsync(_encMsg, 0, _encMsg.Length).Wait();
                }
#if DEBUG
                Debug.WriteLine(_msg);
#endif
            }
        }

        public static bool OrdinalContains(string match, string container)
        {// if container string contains match string, via valid index, then true
            return !string.IsNullOrWhiteSpace(match) &&
                !string.IsNullOrWhiteSpace(container) &&
                container.IndexOf(match, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        public static bool OrdinalEquals(string input, string comparator)
        {// support function for checking string equality using Ordinal comparison
            return string.Equals(input, comparator, StringComparison.OrdinalIgnoreCase);
        }

        public static bool FuzzyEquals(string input, string comparator)
        {// case insensitive equality
            return input != null && comparator != null &&
                input.Length == comparator.Length &&
                OrdinalContains(input, comparator);
        }

        public static string ElapsedToString(double stopwatchElapsed)
        {
            double tempSecs = Convert.ToDouble(stopwatchElapsed * 1.0f / 1000.0f);
            double tempMins = Convert.ToDouble(tempSecs / 60.0f);
            // return minutes or seconds (if applicable)
            return tempSecs > 60 ? $"{tempMins:0.##}m" : $"{tempSecs:0.##}s";
        }

        public static int GetAssemblyPID()
        {
            var assemblyProcess = Process.GetCurrentProcess();
            return assemblyProcess.Id;
        }
    }
}
