using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessLauncher
    {
        public Process TargetProcess { get; private set; }
        public string ProcessName { get; private set; }
        public int TargetPID { get { return TargetProcess?.Id ?? 0; } }

        private string ExecPath { get; set; }
        private string ExecArgs { get; set; }
        private int Delay { get; set; }
        private bool Elevated { get; set; }

        public ProcessLauncher(string procPath, string procArgs, int delayTime, bool elevate)
        {
            ExecPath = procPath;
            ProcessName = Path.GetFileNameWithoutExtension(ExecPath);
            ExecArgs = procArgs;
            Delay = delayTime;
            Elevated = elevate;
        }

        // expose some alternate constructor helpers
        public ProcessLauncher(string procPath, string procArgs, int delayTime) :
            this(procPath, procArgs, delayTime, false) {}

        public ProcessLauncher(string procPath, string procArgs, bool elevate) : this(procPath, procArgs, 0, elevate) {}

        public ProcessLauncher(string procPath, string procArgs) : this(procPath, procArgs, 0, false) {}


        /// <summary>
        /// Returns a Process if a running process of the same name can be found
        /// </summary>
        public void Refresh(string altProcName = "")
        {// refresh against a different executable name
            if (!string.IsNullOrEmpty(altProcName))
            {
                var result = ProcessUtils.GetFirstDescendentByName(altProcName);
                TargetProcess = result;
                ProcessName = result?.ProcessName.Length > 0 ? result.ProcessName : ProcessName;
            }
            else
                TargetProcess = ProcessUtils.GetFirstDescendentByName(ProcessName);
        }

        public bool IsRunning()
        {
            return ProcessUtils.IsAnyRunningByName(ProcessName);
        }

        public IntPtr GetHWnd()
        {
            return WindowUtils.HwndFromProc(TargetProcess);
        }

        public int GetProcessType()
        {
            return WindowUtils.GetWindowType(TargetProcess);
        }

        /// <summary>
        /// Returns the running Process if launching was successful
        /// </summary>
        /// <returns></returns>
        public async Task<Process> Launch()
        {
            if (!SettingsData.ValidateURI(ExecPath) && SettingsData.ValidatePath(ExecPath) ||
                SettingsData.ValidateURI(ExecPath))
            {
                Process newProc = new Process();
                newProc.StartInfo.UseShellExecute = true;
                newProc.StartInfo.FileName = ExecPath;
                newProc.StartInfo.Arguments = ExecArgs;
                if (!SettingsData.ValidateURI(ExecPath))
                    newProc.StartInfo.WorkingDirectory = Directory.GetParent(ExecPath).ToString();
                if (Elevated)
                    newProc.StartInfo.Verb = "runas";

                if (Delay > 0)
                {
                    ProcessUtils.Logger("LAUNCHER", $"Launching process after {Delay}s: {ExecPath} {ExecArgs}");
                    await Task.Delay(Delay * 1000);
                    newProc.Start();
                }
                else
                {
                    ProcessUtils.Logger("LAUNCHER", $"Launching process: {ExecPath} {ExecArgs}");
                    newProc.Start();
                }
                await Task.Delay(100); // spin up
                TargetProcess = newProc;
                return newProc;
            }
            return null;
        }
    }
}
