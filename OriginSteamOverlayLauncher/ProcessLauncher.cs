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
        public IntPtr HWnd { get => WindowUtils.HwndFromProc(TargetProcess); }
        public int ProcessType { get => WindowUtils.DetectWindowType(HWnd); }

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
        /// <returns></returns>
        public void Refresh()
        {// GetProcessByName() runs sanity checks automatically
            TargetProcess = ProcessUtils.GetProcessByName(ProcessName);
        }

        public bool IsRunning()
        {
            return ProcessUtils.IsAnyRunningByName(ProcessName);
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
                if (!SettingsData.ValidateURI(ExecPath))
                {
                    newProc.StartInfo.Arguments = ExecArgs;
                    newProc.StartInfo.WorkingDirectory = Directory.GetParent(ExecPath).ToString();
                }
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
                await Task.Delay(1000); // wait for process to spin up
                if (ProcessUtils.IsValidProcess(newProc))
                {
                    TargetProcess = newProc;
                    return newProc;
                }
            }
            return null;
        }
    }
}
