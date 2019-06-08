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
        {// check for parent or descendent
            var match = GetFirstDescendentByName(exeName);
            if (match != null && !match.HasExited)
                return true;
            return false;
        }

        public static Process GetProcessByName(string exeName)
        {
            List<Process> _procs = GetProcessesByName(exeName);
            if (_procs != null && _procs.Count > 0)
            {
                for (int i = 0; i < _procs.Count; i++)
                {
                    if (IsValidProcess(_procs[i]))
                        return _procs[i];
                }
            }
            return null;
        }

        public static Process GetFirstDescendentByName(string exeName)
        {
            var parent = GetProcessByName(exeName);
            // search by parent PID rather than process name
            // children could be named anything but only have valid PPIDs
            // NOTE: PPIDs may include processes not currently running!
            var children = GetChildProcesses(parent);
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != null && IsValidProcess(children[i]))
                    return children[i]; // return the first valid match
            }
            return parent; // no valid children found
        }

        public static bool IsValidProcess(Process proc)
        {
            if (proc != null && !proc.HasExited && proc.Handle != IntPtr.Zero &&
                proc.MainModule.ModuleName.Contains(".exe"))
            {
                var _hwnd = WindowUtils.HwndFromProc(proc);
                if (WindowUtils.DetectWindowType(_hwnd) > -1 || WindowUtils.WindowHasDetails(_hwnd) || proc.Id > 0)
                    return true;
            }
            return false;
        }

        public static int GetPIDByName(string exeName)
        {
            return GetProcessByName(exeName)?.Id ?? 0;
        }

        public static int GetDescendentPIDByName(string exeName)
        {
            return GetFirstDescendentByName(exeName)?.Id ?? 0;
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

        public static List<Process> GetChildProcesses(Process proc)
        {
            var output = new List<Process>();
            try
            {
                lock (wmiLock)
                {
                    if (proc != null && !proc.HasExited && proc.Id > 0)
                    {
                        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_Process WHERE ParentProcessId={proc?.Id}"))
                        using (ManagementObjectCollection processes = searcher.Get())
                        {
                            foreach (var process in processes)
                            {
                                var curPID = Convert.ToInt32(process.Properties["ProcessID"].Value);
                                var curProcess = Process.GetProcessById(curPID);
                                if (curProcess != null && IsValidProcess(curProcess))
                                    output.Add(curProcess);
                            }
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
