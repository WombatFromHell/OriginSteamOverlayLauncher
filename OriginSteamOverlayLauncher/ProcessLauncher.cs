using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessLauncher
    {
        public ProcessWrapper ProcWrapper { get; private set; }

        private string ExecPath { get; set; }
        private string ExecArgs { get; set; }
        private int Delay { get; set; }
        private bool Elevated { get; set; }
        public int LaunchPID { get; private set; }

        public ProcessLauncher(string procPath, string procArgs, int delayTime, bool elevate)
        {
            ProcWrapper = new ProcessWrapper();
            ExecPath = procPath;
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
        /// Returns the running Process if launching was successful
        /// </summary>
        /// <returns></returns>
        public async Task<Process> Launch()
        {
            if (!SettingsData.ValidateURI(ExecPath) && SettingsData.ValidatePath(ExecPath) ||
                SettingsData.ValidateURI(ExecPath))
            {
                Process _procObj = new Process();
                _procObj.StartInfo.UseShellExecute = true;
                _procObj.StartInfo.FileName = ExecPath;
                _procObj.StartInfo.Arguments = ExecArgs;
                if (!SettingsData.ValidateURI(ExecPath))
                    _procObj.StartInfo.WorkingDirectory = Directory.GetParent(ExecPath).ToString();
                if (Elevated)
                    _procObj.StartInfo.Verb = "runas";

                if (Delay > 0)
                {
                    ProcessUtils.Logger("LAUNCHER", $"Launching process after {Delay}s: {ExecPath} {ExecArgs}");
                    await Task.Delay(Delay * 1000);
                }
                else
                    ProcessUtils.Logger("LAUNCHER", $"Launching process: {ExecPath} {ExecArgs}");

                // bind our process wrapper
                ProcWrapper = new ProcessWrapper(_procObj);
                ProcWrapper.Proc.Start();
                await Task.Delay(10); // spin up
                if (ProcWrapper.IsRunning)
                    LaunchPID = ProcWrapper.PID;
            }
            return ProcWrapper.Proc;
        }
    }
}
