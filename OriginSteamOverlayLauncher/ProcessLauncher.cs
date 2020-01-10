using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessLauncher
    {
        public ProcessWrapper ProcWrapper { get; private set; }
        public string MonitorName { get; private set; }
        public int LaunchPID { get; private set; }

        private string ExecPath { get; set; }
        private string ExecArgs { get; set; }
        private string AvoidProcName { get; set; }
        private int AvoidPID { get; set; }
        private int Delay { get; set; }
        private bool Elevated { get; set; }

        public ProcessLauncher(string procPath, string procArgs, // required
            string avoidProcName = "", int delayTime = 0, bool elevate = false,
            int avoidPID = 0, string monitorName = "") 
        {
            ProcWrapper = new ProcessWrapper();
            ExecPath = procPath;
            ExecArgs = procArgs;
            AvoidProcName = avoidProcName;
            Delay = delayTime;
            Elevated = elevate;
            MonitorName = monitorName;
            AvoidPID = avoidPID;

            // construct and save our ProcessWrapper
            if (SettingsData.ValidatePath(ExecPath) ||
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
                // bind our process wrapper
                ProcWrapper = new ProcessWrapper(_procObj,
                    avoidPID: AvoidPID,
                    altName: MonitorName,
                    avoidProcName: AvoidProcName
                );
            }
        }

        /// <summary>
        /// Returns the running Process if launching was successful
        /// </summary>
        /// <returns></returns>
        public async Task<Process> Launch(bool NoLaunch = false)
        {
            if (ProcWrapper != null && !string.IsNullOrWhiteSpace(ProcWrapper.ProcessName))
            {
                string monitorStr = !string.IsNullOrWhiteSpace(MonitorName) ? $" @{MonitorName}.exe" : "";

                if (Delay > 0)
                {
                    ProcessUtils.Logger("LAUNCHER", $"Launching process after {Delay}s via: {ExecPath} {ExecArgs}" + monitorStr);
                    await Task.Delay(Delay * 1000);
                }
                else
                    ProcessUtils.Logger("LAUNCHER", $"Launching process via: {ExecPath} {ExecArgs}" + monitorStr);

                // optional: launch by default
                if (!NoLaunch)
                    ProcWrapper.Proc.Start();

                await Task.Delay(1000); // spin up
                if (ProcWrapper.IsRunning())
                    LaunchPID = ProcWrapper.Proc.Id;
                return ProcWrapper.Proc;
            }
            else
                return null;
        }
    }
}
