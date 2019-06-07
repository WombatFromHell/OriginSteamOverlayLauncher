using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessUtils
    {
        private static readonly object writerLock = new object();
        private static readonly object wmiLock = new object();

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
            {// ugly equality check here (compare using ordinality)
                return OrdinalEquals(singleFound, _argType1)
                || OrdinalEquals(singleFound, _argType2) ? true : false;
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

        public static bool IsAnyRunningByName(string exeName)
        {
            var _procs = GetProcessesByName(exeName);
            if (_procs != null)
            {
                for (int i = 0; _procs.Count > 0 && i < _procs.Count; i++)
                {
                    if (WindowUtils.DetectWindowType(_procs[i].MainWindowHandle) > -1 || IsValidProcess(_procs[i]))
                        return true;
                }
            }
            return false;
        }

        public static Process GetProcessByName(string exeName)
        {
            List<Process> _procs = GetProcessesByName(exeName);
            if (_procs != null && _procs.Count > 0)
            {
                for (int i = 0; i < _procs.Count; i++)
                {
                    if (!_procs[i].HasExited)
                    {
                        int _type = WindowUtils.DetectWindowType(_procs[i].MainWindowHandle);
                        bool _valid = IsValidProcess(_procs[i]);
                        if (_type > -1 || _valid)
                            return _procs[i];
                    }
                }
            }
            return null;
        }

        public static bool IsValidProcess(Process proc)
        {
            if (proc != null && !proc.HasExited && proc.Handle != IntPtr.Zero)
            {
                var _hwnd = WindowUtils.HwndFromProc(proc);
                if (WindowUtils.WindowHasDetails(_hwnd) && WindowUtils.DetectWindowType(_hwnd) > -1 || proc.Id > 0)
                    return true;
            }
            return false;
        }

        public static int GetPIDByName(string exeName)
        {
            return GetProcessByName(exeName)?.Id ?? 0;
        }

        public static List<Process> GetProcessesByName(string exeName)
        {// returns a List() of Process refs from an executable name search via WMI
            List<Process> output = new List<Process>();
            var _query = new SelectQuery($"SELECT * FROM Win32_Process where Name LIKE '{exeName}.exe'");
            try
            {
                lock (wmiLock)
                {
                    using (ManagementObjectSearcher search = new ManagementObjectSearcher(_query))
                    using (var _procs = search.Get())
                    {
                        foreach (ManagementObject proc in _procs)
                        {
                            proc.Get();
                            var procProps = proc.Properties;
                            int _pid = Convert.ToInt32(procProps["ProcessID"].Value);
                            Process outputProc = Process.GetProcessById(_pid);
                            // add Process refs to our list that match this executable name
                            if (!outputProc.HasExited)
                                output.Add(outputProc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ManagementException)
                    return output; // throw away "Not Found" exceptions
                else if (ex is Win32Exception)
                    return output; // throw away "Access Denied"
                else
                    Logger("FATAL EXCEPTION", ex.Message);
            }
            return output;
        }

        public static void KillProcTreeByName(string procName)
        {
            Process[] foundProcs = Process.GetProcessesByName(procName);
            for (int i = 0; i < foundProcs.Length; i++)
            {
                foundProcs[i].Kill();
                foundProcs[i].Dispose();
            }
        }

        public static bool OrdinalContains(string match, string container)
        {// if container string contains match string, via valid index, then true
            if (container.IndexOf(match, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;
            return false;
        }

        public static bool OrdinalEquals(string input, string comparator)
        {// support function for checking string equality using Ordinal comparison
            if (string.Equals(input, comparator, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static bool StringFuzzyEquals(string input, string comparator)
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
    }
}
