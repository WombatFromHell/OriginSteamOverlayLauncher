using System;
using System.IO;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class LaunchLogic
    {
        private ProcessLauncher PreLauncherPL { get; set; }
        private ProcessLauncher LauncherPL { get; set; }
        private ProcessLauncher GamePL { get; set; }
        private ProcessLauncher PostGamePL { get; set; }
        private ProcessMonitor LauncherMonitor { get; set; }
        private ProcessMonitor GameMonitor { get; set; }

        private string LauncherName { get; set; }
        private string MonitorPath { get; set; }
        private bool LauncherPathValid { get; set; }
        private bool LauncherURIMode { get; set; }
        private bool ExitRequested { get; set; }

        private SettingsData SetHnd { get; } = Program.CurSettings.Data;
        private TrayIconUtil TrayUtil { get; }

        public LaunchLogic()
        {
            LauncherName = Path.GetFileNameWithoutExtension(SetHnd.Paths.LauncherPath);
            MonitorPath = SettingsData.ValidatePath(SetHnd.Paths.MonitorPath) ?
                SetHnd.Paths.MonitorPath : "";
            LauncherPathValid = SettingsData.ValidatePath(SetHnd.Paths.LauncherPath);
            LauncherURIMode = SettingsData.ValidateURI(SetHnd.Paths.LauncherURI);
            TrayUtil = new TrayIconUtil();
        }

        public async Task ProcessLauncher()
        {
            // check for running instance of launcher (relaunch if required)
            if (SetHnd.Options.ReLaunch && LauncherPathValid && ProcessUtils.IsAnyRunningByName(LauncherName))
            {// if the launcher is running before the game kill it so we can run it through Steam
                ProcessUtils.Logger("OSOL", $"Found previous instance of launcher [{LauncherName}.exe], relaunching...");
                ProcessUtils.KillProcTreeByName(LauncherName);
            }

            // run PreLaunchExecPath/Args before the launcher
            PreLauncherPL = new ProcessLauncher(
                SetHnd.Paths.PreLaunchExecPath,
                SetHnd.Paths.PreLaunchExecArgs,
                SetHnd.Options.ElevateExternals
            );
            await PreLauncherPL.Launch();

            if (!SetHnd.Options.SkipLauncher && SetHnd.Options.ReLaunch && LauncherPathValid)
            {// launcher is optional
                LauncherPL = new ProcessLauncher(
                    SetHnd.Paths.LauncherPath,
                    SetHnd.Paths.LauncherArgs
                );
                await LauncherPL.Launch();
                if (LauncherPL.TargetProcess != null)
                {
                    LauncherMonitor = new ProcessMonitor(
                        LauncherPL,
                        SetHnd.Options.ProcessAcquisitionTimeout,
                        SetHnd.Options.InterProcessAcquisitionTimeout
                    );
                    LauncherMonitor.ProcessAcquired += OnLauncherAcquired;
                    LauncherMonitor.ProcessHardExit += OnLauncherExited;
                }
            }
            else
                OnLauncherAcquired(this, null); // continuation

            // wait for all running threads to exit
            while (!ExitRequested ||
                PreLauncherPL != null && PreLauncherPL.IsRunning ||
                PostGamePL != null && PostGamePL.IsRunning ||
                LauncherMonitor != null && LauncherMonitor.IsRunning ||
                GameMonitor != null && GameMonitor.IsRunning)
                await Task.Delay(1000);
        }

        #region Event Delegates
        private async void OnLauncherAcquired(object sender, ProcessEventArgs e)
        {
            if (SetHnd.Options.ReLaunch && LauncherPathValid)
            {// pause to let the launcher process stabilize after being hooked
                ProcessUtils.Logger("OSOL",
                    $"Launcher detected, preparing to launch game in {SetHnd.Options.PreGameLauncherWaitTime}s...");
                await Task.Delay(SetHnd.Options.PreGameLauncherWaitTime * 1000);
            }

            if (LauncherURIMode || LauncherPL.IsRunning && LauncherPL.ProcessType == 4)  // URIs/EGL
                GamePL = new ProcessLauncher(
                    SetHnd.Paths.LauncherURI, "",
                    SetHnd.Options.PreGameWaitTime
                );
            else if (LauncherPL.IsRunning && LauncherPL.ProcessType == 1)  // Battle.net Launcher
                GamePL = new ProcessLauncher(
                    SetHnd.Paths.LauncherPath,
                    SetHnd.Paths.LauncherArgs,
                    SetHnd.Options.PreGameWaitTime
                );
            else  // normal behavior
            {
                GamePL = new ProcessLauncher(
                    MonitorPath.Length > 0 ? MonitorPath : SetHnd.Paths.GamePath,
                    SetHnd.Paths.GameArgs,
                    SetHnd.Options.PreGameWaitTime
                );
            }
            if (SetHnd.Options.AutoGameLaunch)
                await GamePL?.Launch();
            else
                ProcessUtils.Logger("OSOL", "AutoGameLaunch is false, waiting for user to launch game before timing out...");

            // watch for the MonitorPath rather than GamePath (if applicable)
            GameMonitor = new ProcessMonitor(
                GamePL,
                SetHnd.Options.ProcessAcquisitionTimeout,
                SetHnd.Options.InterProcessAcquisitionTimeout
            );
            GameMonitor.ProcessAcquired += OnGameAcquired;
            GameMonitor.ProcessHardExit += OnGameExited;
        }

        private void OnGameAcquired(object sender, ProcessEventArgs e)
        {
            if (SetHnd.Options.GameProcessAffinity > 0)
            {
                GamePL.TargetProcess.ProcessorAffinity = (IntPtr)SetHnd.Options.GameProcessAffinity;
                ProcessUtils.Logger("OSOL",
                    $"Setting game process CPU affinity to: {BitmaskExtensions.AffinityToCoreString(SetHnd.Options.GameProcessAffinity)}");
            }

            if (SetHnd.Options.GameProcessPriority.ToString() != "Normal")
            {
                GamePL.TargetProcess.PriorityClass = SetHnd.Options.GameProcessPriority;
                ProcessUtils.Logger("OSOL",
                    $"Setting game process priority to: {SetHnd.Options.GameProcessPriority.ToString()}");
            }
        }

        private async void OnGameExited(object sender, ProcessEventArgs e)
        {
            if (SetHnd.Options.MinimizeLauncher && LauncherPL.IsRunning)
                WindowUtils.MinimizeWindow(LauncherPL.HWnd);

            // run PostGameExecPath/Args after the game exits
            PostGamePL = new ProcessLauncher(
                SetHnd.Paths.PostGameExecPath,
                SetHnd.Paths.PostGameExecArgs,
                SetHnd.Options.PostGameWaitTime,
                SetHnd.Options.ElevateExternals
            );
            await PostGamePL.Launch();

            if (SetHnd.Options.PostGameWaitTime > 0)
                ProcessUtils.Logger("OSOL", $"Game exited, moving on to clean up after {SetHnd.Options.PostGameWaitTime}s...");
            else
                ProcessUtils.Logger("OSOL", $"Game exited, cleaning up...");
            await Task.Delay(SetHnd.Options.PostGameWaitTime * 1000);

            if (SetHnd.Options.CloseLauncher && LauncherPL.IsRunning)
            {
                ProcessUtils.Logger("OSOL", $"Found launcher still running, killing it...");
                ProcessUtils.KillProcTreeByName(LauncherName);
            }

            OnClosing();
        }

        private void OnLauncherExited(object sender, ProcessEventArgs e)
        {// edge case if launcher times out (or closes) before game launches
            ProcessUtils.Logger("OSOL", "Launcher was never acquired, cleaning up...");
            OnClosing();
        }
        #endregion

        private void OnClosing()
        {
            // request monitors to exit gracefully
            if (LauncherMonitor != null)
                LauncherMonitor.Stop();
            if (GameMonitor != null)
                GameMonitor.Stop();
            // clean up system tray
            TrayUtil.RefreshTrayArea();
            // exit gracefully
            ExitRequested = true;
        }
    }
}