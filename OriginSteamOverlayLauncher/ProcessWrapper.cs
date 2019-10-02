using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;

namespace OriginSteamOverlayLauncher
{
    public class ProcessWrapper
    {
        public Process Proc { get; set; }
        public string ProcessName { get; private set; }
        public int PID { get; private set; }
        public int ProcessType { get; private set; }

        public IntPtr Hwnd { get => WindowUtils.GetHWND(Proc); }
        public int ParentPID { get => NativeProcessUtils.GetParentPID(Proc?.Handle ?? IntPtr.Zero); }

        private int AvoidPID { get; set; }
        private string AvoidProcName { get; set; }
        private string MonitorName { get; set; }
        private string _ProcName { get; set; }

        /// <summary>
        /// Wrapper for Process objects that collects additional runtime information
        /// </summary>
        /// <param name="srcProc">Optional Process object to instantiate with</param>
        public ProcessWrapper(Process srcProc = null, int avoidPID = 0, string altName = "", string avoidProcName = "")
        {// avoid returning null refs
            Proc = srcProc != null ? srcProc : new Process();
            ProcessName = Path.GetFileNameWithoutExtension(Proc.StartInfo.FileName);
            MonitorName = altName;
            AvoidPID = avoidPID;
            AvoidProcName = avoidProcName;
            _ProcName = !string.IsNullOrWhiteSpace(MonitorName) ? MonitorName : ProcessName;
        }

        public List<Tuple<int, int, Process>> GetChildProcesses(int PID = 0)
        {// a List of Tuples in the form of: { PPID, PID, Process, ProcessType }
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
                        var _pidIsRunning = NativeProcessUtils.IsProcessAlive(curPID);
                        // avoid ArgumentException with IsProcessAlive()
                        if (_pidIsRunning)
                        {
                            var curProcess = Process.GetProcessById(curPID);
                            int _ppid = NativeProcessUtils.GetParentPID(curProcess.Handle);
                            // use AvoidPID to avoid selecting an already filtered process
                            if (curProcess != null && !curProcess.HasExited)
                                output.Add(new Tuple<int, int, Process>(_ppid, curPID, curProcess));
                        }
                    }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                }
            }
            return output;
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
                        var _pidIsRunning = NativeProcessUtils.IsProcessAlive(curPID);
                        if (_pidIsRunning)
                        {
                            var curProcess = Process.GetProcessById(curPID);
                            if (curProcess != null && !curProcess.HasExited)
                                output.Add(curProcess);
                        }
                    }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                }
            }
            return output;
        }

        private List<Tuple<int, int, Process>> EnumerateDescendents()
        {// abstraction of GetChildProcesses() for grandchild enumeration
            var output = new List<Tuple<int, int, Process>>();
            // retrieve all children of this assembly
            var procs = GetChildProcesses();
            output.AddRange(procs);
            foreach (var item in procs)
            {// if children have children process those (avoidance by PID)
                var _cProcs = GetChildProcesses(item.Item2);
                output.AddRange(_cProcs);
                foreach (var gcitem in _cProcs)
                {// if our grandchildren have children process those as well
                    var _gcProcs = GetChildProcesses(gcitem.Item2);
                    if (_gcProcs.Count > 0)
                        output.AddRange(_gcProcs);
                }
            }
            return output;
        }

        public bool IsRunning()
        {
            try
            {
                // don't rely on Process() class' HasExited when cleaning up
                if (Proc == null || !NativeProcessUtils.IsProcessAlive(PID))
                {
                    var enumResults = EnumerateDescendents();
                    // enumerate children and grandchildren of our assembly
                    for (int i = enumResults.Count-1; i >= 0; i--)
                    {
                        var item = enumResults[i];
                        var curProc = item.Item3;
                        var curType = WindowUtils.DetectWindowType(curProc);
                        var curName = NativeProcessUtils.GetProcessModuleName(item.Item2);
#if DEBUG
                        Debug.WriteLine($"enumResults item: [PPID={item.Item1},"+
                            $"PID={item.Item2},Proc={curName}," + 
                            $"Type={curType},Avoid={AvoidPID},AvoidName={AvoidProcName}]");
#endif
                        if (ValidateWMIProc(curProc) && curProc.Id != AvoidPID)
                        {
                            UpdateRefs(curProc, curType);
                            return true;
                        }
                    }
                    // no match yet, so try resolving by name
                    var byNameResults = GetProcessesByName(_ProcName);
                    for (int i = byNameResults.Count - 1; i >= 0; i--)
                    {// enumerate in reverse order (youngest to oldest)
                        var item = byNameResults[i];
                        var curProc = item;
                        var curType = WindowUtils.DetectWindowType(curProc);
                        var curName = NativeProcessUtils.GetProcessModuleName(curProc.Id);
#if DEBUG
                        Debug.WriteLine($"byNameResults item: [Proc={curName}," +
                            $"Avoid={AvoidPID},AvoidName={AvoidProcName}]");
#endif
                        if (ValidateWMIProc(curProc) && curProc.Id != AvoidPID)
                        {// update our target if it's changed
                            UpdateRefs(curProc, curType);
                            return true;
                        }
                    }
                    return false; // both methods failed!
                }
                else
                    return true; // return cached result
            }
            catch (Exception) {
                // rough check until the next iteration
                return Proc == null || PID > 0 && !NativeProcessUtils.IsProcessAlive(PID);
            }
        }

        private void UpdateRefs(Process proc, int windowType)
        {// only update if proc data has changed
            string moduleName = NativeProcessUtils.GetProcessModuleName(proc.Id);
#if DEBUG
            Debug.WriteLine($"Monitor@{_ProcName} selected: [Proc={moduleName}@{proc.Id},Type={windowType}," +
                $"Avoid={AvoidPID},AvoidName={AvoidProcName}]");
#endif
            if (PID != proc.Id)
            {
                Proc = proc;
                PID = proc.Id;
                ProcessType = windowType;
                ProcessName = moduleName;
            }
        }

        public static bool IsRunningByName(string name)
        {
            return Process.GetProcessesByName(name).Length > 0;
        }

        public static bool IsValidProcess(Process proc)
        {// rough approximation of a working Launcher/Game window
            try
            {
                return proc != null && !proc.HasExited && proc.Handle != IntPtr.Zero && proc.Id > 0;
            }
            catch (Exception) { return false; }
        }

        private bool ValidateWMIProc(Process procItem)
        {
            try
            {
                string moduleName = NativeProcessUtils.GetProcessModuleName(procItem.Id);
                bool isValid = IsValidProcess(procItem);
                bool avoidMatches = ProcessUtils.OrdinalEquals(AvoidProcName, moduleName);
                bool nameMatches = ProcessUtils.OrdinalEquals(MonitorName, moduleName) ||
                    ProcessUtils.OrdinalEquals(ProcessName, moduleName);
                bool hasDetails = WindowUtils.DetectWindowType(procItem) > -1;
                return (isValid && !avoidMatches && nameMatches && hasDetails) ||
                    (isValid && !avoidMatches && nameMatches) ||
                    (isValid && !avoidMatches && hasDetails);
            }
            catch { return false; }
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
    /// A utility class to determine a process parent and check if a process is alive
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeProcessUtils
    {
        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        #region IMPORTS
        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref NativeProcessUtils processInformation,
            int processInformationLength,
            out int returnLength
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags access, bool inheritHandle, int procId);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
        [DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
        #endregion

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
            NativeProcessUtils pbi = new NativeProcessUtils();
            int returnLength;
            int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
            if (status != 0)
                return -1;
            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }

        public static bool IsProcessAlive(int processId)
        {// https://stackoverflow.com/a/54727764
            IntPtr h = OpenProcess(ProcessAccessFlags.QueryInformation, true, processId);
            if (h == IntPtr.Zero)
                return false;

            uint code = 0;
            bool b = GetExitCodeProcess(h, out code);
            CloseHandle(h);
            if (b)
                b = (code == 259) /* STILL_ACTIVE  */;
            return b;
        }

        public static string GetProcessPath(int pid)
        {
            IntPtr handle = OpenProcess(
                ProcessAccessFlags.QueryInformation |
                ProcessAccessFlags.VirtualMemoryRead,
                false, pid);
            if (handle == null)
                return "";
            var output = new StringBuilder(2048);

            StringBuilder strbld = new StringBuilder(2048);
            // get only the main module's path
            GetModuleFileNameEx(handle, IntPtr.Zero, strbld, (uint)(strbld.Capacity));
            CloseHandle(handle);
            return strbld.ToString();
        }

        public static string GetProcessModuleName(int pid)
        {
            var result = GetProcessPath(pid);
            return Path.GetFileNameWithoutExtension(result);
        }
    }
}
