using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace OriginSteamOverlayLauncher
{
    public class ProcessWrapper
    {
        public Process Proc { get; set; }
        public string ProcessName { get; private set; }
        public IntPtr Hwnd
        {
            get
            {
                this.GetIsRunning();
                return GetHWND(Proc);
            }
        }
        public int PID
        {
            get
            {
                this.GetIsRunning();
                return Proc?.Id ?? 0;
            }
        }
        public int ParentPID
        {
            get
            {
                this.GetIsRunning();
                return ParentProcessUtils.GetParentPID(Proc.Handle);
            }
        }
        public int ProcessType
        {
            get
            {
                this.GetIsRunning();
                return WindowUtils.DetectWindowType(GetHWND(Proc));
            }
        }
        public bool IsChild { get { return GetIsChild(); } }
        public bool IsRunning { get { return GetIsRunning(); } }

        private int AvoidPID { get; set; }
        private string MonitorName { get; set; }

        /// <summary>
        /// Wrapper for Process objects that collects additional runtime information
        /// </summary>
        /// <param name="srcProc">Optional Process object to instantiate with</param>
        public ProcessWrapper(Process srcProc = null, int avoidPID = 0, string altName = "")
        {// avoid returning null refs
            Proc = srcProc != null ? srcProc : new Process();
            ProcessName = Path.GetFileNameWithoutExtension(Proc.StartInfo.FileName);
            MonitorName = !string.IsNullOrWhiteSpace(altName) ? altName : "";
            AvoidPID = avoidPID > 0 ? avoidPID : 0;
        }

        private bool GetIsChild()
        {// compare current PPID against assembly PID to determine parentage
            int _ppid = ParentProcessUtils.GetParentPID(Proc.Handle);
            return _ppid > 0 && _ppid != Program.AssemblyPID;
        }

        public List<Tuple<int, int, Process>> GetChildProcesses(int PID = 0)
        {
            var output = new List<Tuple<int, int, Process>>();
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ParentProcessId={(PID > 0 ? PID : Program.AssemblyPID)}"))
            using (ManagementObjectCollection processes = searcher.Get())
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var curPID = Convert.ToInt32(process.Properties["ProcessID"].Value);
                        var curProcess = Process.GetProcessById(curPID);
                        int _ppid = ParentProcessUtils.GetParentPID(curProcess.Handle);
                        // use AvoidPID to avoid selecting an already filtered process
                        if (ValidateProc(curProcess) && PID > 0 && curProcess.Id != AvoidPID)
                            output.Add(new Tuple<int, int, Process>(_ppid, curPID, curProcess));
                    }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                }
            }
            return output;
        }

        private bool ValidateProc(Process procItem)
        {// abstract process validator
            try
            {
                return IsValidProcess(procItem) &&
                    ProcessUtils.OrdinalContains(MonitorName, procItem.MainModule.ModuleName) ||
                    ProcessUtils.OrdinalContains(ProcessName, procItem.MainModule.ModuleName) ||
                    ProcessUtils.OrdinalContains(ProcessName, procItem.ProcessName) || procItem.Id > 0;
            }
            catch { return false; }
        }

        public List<Process> GetProcessesByName(string exeName)
        {
            var output = new List<Process>();
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process where Name LIKE '{exeName}.exe'"))
            using (ManagementObjectCollection processes = searcher.Get())
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var curPID = Convert.ToInt32(process.Properties["ProcessID"].Value);
                        var curProcess = Process.GetProcessById(curPID);
                        if (ValidateProc(curProcess))
                            output.Add(curProcess);
                    }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                }
            }
            return output;
        }

        private List<Tuple<int, int, Process>> EnumerateDescendents()
        {// abstraction of GetChildProcesses() for grandchild enumeration
            // a List of Tuples in the form of: { PPID, PID, Process }
            var output = new List<Tuple<int, int, Process>>();
            // retrieve all children of this assembly
            var procs = GetChildProcesses();
            output.AddRange(procs);
            foreach (var item in procs)
            {// if children have children process those (by PID)
                var _gcProcs = GetChildProcesses(item.Item2);
                if (_gcProcs.Count > 0)
                    output.AddRange(_gcProcs);
            }
            return output;
        }

        public bool GetIsRunning()
        {// also doubles as a refresh method
            var enumResults = EnumerateDescendents();
            if (enumResults.Count == 0)
            {// switch to searching by name if assembly child enumeration fails
                var byNameResults = GetProcessesByName(MonitorName.Length > 0 ? MonitorName : ProcessName);
                for (int j = 0; j < byNameResults.Count; j++)
                {
                    if (!byNameResults[j].HasExited)
                    {// update our target if it's changed
                        if (Proc != null && Proc.Id != byNameResults[j].Id)
                        {
                            Proc = byNameResults[j];
                            ProcessName = Path.GetFileNameWithoutExtension(byNameResults[j].MainModule.ModuleName);
                        }
                        return true; // bail early on success
                    }
                }
            }
            else
            {// enumerate children and grandchildren of our assembly
                for (int i = 0; i < enumResults.Count; i++)
                {
                    var curProc = enumResults[i].Item3;
                    if (!curProc.HasExited)
                    {// update our target if it's changed
                        if (Proc != null && Proc.Id != curProc.Id)
                        {
                            Proc = curProc;
                            ProcessName = Path.GetFileNameWithoutExtension(curProc.MainModule.ModuleName);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsRunningByName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                return Process.GetProcessesByName(name).Length > 0;
            return false;
        }

        public static bool IsValidProcess(Process proc)
        {
            if (proc != null && !proc.HasExited && proc.Handle != IntPtr.Zero &&
                proc.MainModule.ModuleName.Contains(".exe") &&
                WindowUtils.DetectWindowType(GetHWND(proc)) > -1)
                return true;
            return false;
        }

        public static IntPtr GetHWND(Process procHandle)
        {// just a helper to return an hWnd from a given Process (if it has a window handle)
            try
            {
                if (procHandle != null && !procHandle.HasExited &&
                    procHandle.MainWindowHandle != IntPtr.Zero)
                    return procHandle.MainWindowHandle;
            }
            catch (Exception) { }
            return IntPtr.Zero;
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
    }

    /// <summary>
    /// A utility class to determine a process parent.
    /// https://stackoverflow.com/a/3346055
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ParentProcessUtils
    {
        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ParentProcessUtils processInformation,
            int processInformationLength,
            out int returnLength
        );

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(int id)
        {
            return Process.GetProcessById(GetParentPID(Process.GetProcessById(id).Handle));
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class.</returns>
        public static int GetParentPID(IntPtr handle)
        {
            ParentProcessUtils pbi = new ParentProcessUtils();
            int returnLength;
            int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
            if (status != 0)
                return -1;
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
    }
}
