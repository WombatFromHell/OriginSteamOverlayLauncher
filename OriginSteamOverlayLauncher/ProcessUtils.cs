using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace OriginSteamOverlayLauncher
{
    public class ProcessUtils
    {
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
                    w => ProcessUtils.StringEquals(w.ToLower(), _argType1)
                    || ProcessUtils.StringEquals(w.ToLower(), _argType2)
                ).FirstOrDefault();

            if (singleFound != null)
            {// ugly equality check here (compare using ordinality)
                return StringEquals(singleFound, _argType1)
                || StringEquals(singleFound, _argType2) ? true : false;
            }

            return false;
        }

        public static void Logger(String cause, String message)
        {
            string _msg = $"[{DateTime.Now.ToLocalTime()}] [{cause}] {message}";
            using (StreamWriter stream = File.AppendText(Program.appName + "_Log.txt"))
            {
                stream.Write($"{_msg}\r\n");
            }
#if DEBUG
            Debug.WriteLine(_msg);
#endif
        }

        public static bool IsRunningPID(int PID)
        {
            var _proc = Process.GetProcessById(PID);
            if (_proc != null && !_proc.HasExited)
                return true;
            return false;
        }

        public static bool IsRunningByName(String exeName)
        {
            var _procs = GetProcessesByName(exeName);
            if (_procs == null) return false;

            var _lastChild = GetLastProcessChild(_procs);
            if (_procs != null && _lastChild != null && !_lastChild.HasExited)
                return true;

            return false;
        }

        public static Process GetLastProcessChild(List<Process> procList)
        {
            if (procList != null)
                return procList.LastOrDefault();
            return null;
        }

        public static Process GetLastProcessChildByName(String exeName)
        {
            var _procs = GetProcessesByName(exeName);
            var _lastChild = GetLastProcessChild(_procs);
            if (_procs != null && _lastChild != null && !_lastChild.HasExited)
                return _lastChild;
            return null;
        }

        public static List<Process> GetProcessesByName(String exeName)
        {// returns a List() of Process refs from an executable name search via WMI
            var _query = new SelectQuery($"SELECT * FROM Win32_Process where Name LIKE '{exeName}.exe'");
            try
            {
                using (ManagementObjectSearcher search = new ManagementObjectSearcher(_query))
                {
                    var _procs = search.Get();
                    if (_procs.Count > 0)
                    {
                        List<Process> output = new List<Process>();
                        foreach (ManagementObject proc in _procs)
                        {
                            proc.Get();
                            var procProps = proc.Properties;
                            int _pid = Convert.ToInt32(procProps["ProcessID"].Value);
                            // add Process refs to our list that match this executable name
                            output.Add(Process.GetProcessById(_pid));
                        }
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ManagementException)
                    return null; // throw away "Not Found" exceptions
                else
                    Logger("FATAL EXCEPTION", ex.Message);
            }
            return null;
        }

        public static int GetParentProcessByPID(int PID)
        {
            int _pid = 0;
            using (ManagementObject parentProcess = new ManagementObject("win32_process.handle='" + PID.ToString() + "'"))
            {
                parentProcess.Get();
                _pid = Convert.ToInt32(parentProcess["ParentProcessId"]);
            }
            return _pid;
        }

        public static bool IsValidProcess(Process targetProc)
        {// rough process validation - must have an hwnd or handle
            if (targetProc == null || targetProc.Id == 0)
                return false; // sanity check

            if (!targetProc.HasExited &&
                WindowUtils.HwndFromProc(targetProc) != IntPtr.Zero &&
                targetProc.MainWindowTitle.Length > 0 ||
                targetProc.Handle != IntPtr.Zero)
                return true;

            return false;
        }

        public static void KillProcTreeByName(String procName)
        {
            Process[] foundProcs = Process.GetProcessesByName(procName);
            foreach (Process proc in foundProcs)
            {
                proc.Kill();
                proc.Dispose();
            }
        }

        public static String ConvertUnixToDosPath(String path)
        {
            string output = "";

            if (OrdinalContains(":/", path))
            {// look for a unix style full-path
                // strip escape chars from the beginning and end of path
                string _path = path.Replace("\\\"", "");
                // format to dos style
                output = _path.Replace("/", "\\");
            }

            // pass back unchanged if no work performed
            return !String.IsNullOrEmpty(output) ? output : path;
        }

        public static bool PathIsURI(String path)
        {// take a string and check if it's similar to a URI
            if (!String.IsNullOrEmpty(path) && !String.IsNullOrWhiteSpace(path)
                && ProcessUtils.OrdinalContains(@"://", path))
                return true;

            return false;
        }

        public static String GetCmdlineFromProcByName(String procName)
        {// try using Process() to get CommandLine from ...StartInfo.Arguments
            var _proc = GetLastProcessChildByName(procName);
            var _cmdLine = "";

            if (_proc != null)
                _cmdLine = _proc.StartInfo.Arguments.ToString();

            if (_cmdLine.Contains(@":/"))
                _cmdLine = ConvertUnixToDosPath(_cmdLine);

            return !String.IsNullOrEmpty(_cmdLine) ? _cmdLine : String.Empty;
        }

        public static string GetCommandLineToString(Process process, String startPath)
        { // credit to: https://stackoverflow.com/a/40501117
            String cmdLine = String.Empty;
            String _parsedPath = String.Empty;

            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
                {// use WMI to grab the CommandLine string by looking up the PID
                    var matchEnum = searcher.Get().GetEnumerator();

                    // include a space to clean up the output parsed args
                    if (startPath.Contains(" "))
                        _parsedPath = $"\"{startPath}\" ";
                    else
                        _parsedPath = $"{startPath} ";


                    if (matchEnum.MoveNext())
                    {// this will always return at most 1 result
                        string _cmdLine = matchEnum.Current["CommandLine"]?.ToString();

                        // unix-style path in target - we need to convert it
                        if (!String.IsNullOrEmpty(_cmdLine) && _cmdLine.Contains(@":/"))
                            cmdLine = ConvertUnixToDosPath(_cmdLine);
                        else
                            cmdLine = !String.IsNullOrEmpty(_cmdLine) ? _cmdLine : String.Empty;
                    }
                }

                if (cmdLine == null)
                {
                    // Not having found a command line implies 1 of 2 exceptions, which the
                    // WMI query masked:
                    // An "Access denied" exception due to lack of privileges.
                    // A "Cannot process request because the process (<pid>) has exited."
                    // exception due to the process having terminated.
                    // We provoke the same exception again simply by accessing process.MainModule.
                    var dummy = process.MainModule; // Provoke exception.
                }
            }
            // Catch and ignore "access denied" exceptions.
            catch (Win32Exception ex) when (ex.HResult == -2147467259) { }
            // Catch and ignore "Cannot process request because the process (<pid>) has
            // exited." exceptions.
            // These can happen if a process was initially included in 
            // Process.GetProcesses(), but has terminated before it can be
            // examined below.
            catch (InvalidOperationException ex) when (ex.HResult == -2146233079) { }

            // remove the full path from our parsed arguments
            return RemoveInPlace(cmdLine, _parsedPath);
        }

        public static void ExecuteExternalElevated(Settings setHnd, String filePath, String fileArgs, int standoffTimer)
        {// generic process delegate for executing pre-launcher/post-game
            try
            {
                Process execProc = new Process();

                // sanity check our future process path first
                if (Settings.ValidatePath(filePath))
                {
                    execProc.StartInfo.UseShellExecute = true;
                    execProc.StartInfo.FileName = filePath;
                    execProc.StartInfo.Arguments = fileArgs;

                    // ask the user for contextual UAC privs in case they need elevation
                    if (setHnd.ElevateExternals)
                        execProc.StartInfo.Verb = "runas";

                    if (standoffTimer > 0)
                    {
                        Thread.Sleep(standoffTimer * 1000);
                        Logger("OSOL", $"Attempting to run external process after {standoffTimer}s: {filePath} {fileArgs}");
                    }
                    else
                        Logger("OSOL", $"Attempting to run external process: {filePath} {fileArgs}");

                    execProc.Start();
                    execProc.WaitForExit(); // idle waiting for outside process to return
                    Logger("OSOL", "External process delegate returned, continuing...");
                }
                else if (filePath != null && filePath.Length > 0)
                {
                    Logger("WARNING", $"External process path is invalid: {filePath} {fileArgs}");
                }
            }
            catch (Exception e)
            {
                Logger("EXCEPTION", $"Process delegate failed on [{filePath} {fileArgs}], due to: {e.Message}");
            }
        }

        public static bool OrdinalContains(String match, String container)
        {// if container string contains match string, via valid index, then true
            if (container.IndexOf(match, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;

            return false;
        }

        public static bool StringEquals(String input, String comparator)
        {// support function for checking string equality using Ordinal comparison
            if (!String.IsNullOrEmpty(input) && String.Equals(input, comparator, StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
        }

        public static string RemoveInPlace(String input, String match)
        {// remove matched substring from input string
            if (OrdinalContains(match, input))
            {
                string _result = input.Replace(match, String.Empty);
                return _result;
            }

            return String.Empty;
        }

        public static void StoreCommandline(Settings setHnd, IniFile iniHnd, String cmdLine)
        {// save the passed commandline string to our ini for later
            if (cmdLine.Length > 0)
            {
                setHnd.DetectedCommandline = cmdLine;
                iniHnd.Write("DetectedCommandline", cmdLine, "Paths");
            }
        }

        public static bool CompareCommandlines(String storedCmdline, String comparatorCmdline)
        {// compared stored against active to prevent unnecessary relaunching
            if (storedCmdline.Length > 0 && comparatorCmdline.Length > 0 && StringEquals(comparatorCmdline, storedCmdline))
            {
                return true;
            }

            return false;
        }

        public static void LaunchProcess(Process proc)
        {// abstract Process.Start() for exception handling purposes...
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("FATAL EXCEPTION", ex.Message);
                Environment.Exit(0);
            }
        }
    }
}
