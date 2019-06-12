using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        public bool IsValid
        {
            get
            {
                this.GetIsRunning();
                return IsValidProcess(Proc);
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

        /// <summary>
        /// Wrapper for Process objects that collects additional runtime information
        /// </summary>
        /// <param name="srcProc">Optional Process object to instantiate with</param>
        public ProcessWrapper(Process srcProc = null)
        {// avoid returning null refs
            Proc = srcProc != null ? srcProc : new Process();
            ProcessName = Path.GetFileNameWithoutExtension(Proc.StartInfo.FileName);
        }

        private bool GetIsChild()
        {// compare current PPID against assembly PID to determine parentage
            int _ppid = ParentProcessUtils.GetParentPID(Proc.Handle);
            return _ppid > 0 && _ppid != Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Provides an enumerator of all processes associated with this Process
        /// </summary>
        public List<Process> GetProcesses()
        {
            var output = new List<Process>();
            try
            {
                var result = Process.GetProcesses(Environment.MachineName);
                for (int i = 0; i < result.Length; i++)
                    // avoid Win32Exception by checking ProcessName first
                    if (result[i] != null && result[i].ProcessName.Contains(this.ProcessName) && !result[i].HasExited)
                        output.Add(result[i]);
            }
            catch (Win32Exception)
            {// eat "Access is denied"
                return output;
            }
            return output;
        }

        public bool GetIsRunning()
        {// also doubles as a refresh method
            var _procs = GetProcesses();
            for (int i = 0; i < _procs.Count; i++)
            {
                if (!_procs[i].HasExited && IsValidProcess(_procs[i]))
                {// bail on the first valid match we find
                    Proc = _procs[i];
                    return true;
                }
            }
            return _procs.Count > 0 && PID != 0;
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
            if (procHandle != null && !procHandle.HasExited && procHandle.MainWindowHandle != IntPtr.Zero)
                return procHandle.MainWindowHandle;
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
