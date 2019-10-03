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
        private string GameName { get; set; }
        private string MonitorPath { get; set; }
        private string MonitorName { get; set; }
        private bool LauncherPathValid { get; set; }
        private bool LauncherURIMode { get; set; }
        private bool ExitRequested { get; set; }

        private SettingsData SetHnd { get; } = Program.CurSettings.Data;
        private TrayIconUtil TrayUtil { get; }

        public LaunchLogic()
        {
            LauncherName = Path.GetFileNameWithoutExtension(SetHnd.Paths.LauncherPath);
            GameName = Path.GetFileNameWithoutExtension(SetHnd.Paths.GamePath);
            MonitorPath = SettingsData.ValidatePath(SetHnd.Paths.MonitorPath) ?
                SetHnd.Paths.MonitorPath : "";
            MonitorName = Path.GetFileNameWithoutExtension(MonitorPath);
            LauncherPathValid = SettingsData.ValidatePath(SetHnd.Paths.LauncherPath);
            LauncherURIMode = SettingsData.ValidateURI(SetHnd.Paths.LauncherURI);
            TrayUtil = new TrayIconUtil();
        }

        public async Task ProcessLauncher()
        {
            // check for running instance of launcher (relaunch if required)
            if (SetHnd.Options.ReLaunch && LauncherPathValid && ProcessWrapper.IsRunningByName(LauncherName))
            {// if the launcher is running before the game kill it so we can run it through Steam
                ProcessUtils.Logger("OSOL", $"Found previous instance of launcher [{LauncherName}.exe], relaunching...");
                ProcessWrapper.KillProcTreeByName(LauncherName);
            }

            // run PreLaunchExecPath/Args before the launcher
            PreLauncherPL = new ProcessLauncher(
                SetHnd.Paths.PreLaunchExecPath,
                SetHnd.Paths.PreLaunchExecArgs,
                elevate: SetHnd.Options.ElevateExternals
            );
            await PreLauncherPL.Launch();

            if (LauncherPathValid && !SetHnd.Options.SkipLauncher)
            {

                LauncherPL = new ProcessLauncher(
                    SetHnd.Paths.LauncherPath,
                    SetHnd.Paths.LauncherArgs,
                    avoidProcName: GameName
                );
                await LauncherPL.Launch(); // launching the launcher is optional
                LauncherMonitor = new ProcessMonitor(
                    LauncherPL,
                    SetHnd.Options.ProcessAcquisitionTimeout,
                    SetHnd.Options.InterProcessAcquisitionTimeout
                );
                LauncherMonitor.ProcessHardExit += OnLauncherExited;
            }

            // signal for manual game launch
            if (SetHnd.Options.SkipLauncher || LauncherMonitor == null)
                OnLauncherAcquired(this, null);
            else if (LauncherMonitor != null)
                LauncherMonitor.ProcessAcquired += OnLauncherAcquired;

            // wait for all running threads to exit
            while (!ExitRequested ||
                LauncherMonitor != null && LauncherMonitor.IsRunning() ||
                GameMonitor != null && GameMonitor.IsRunning() ||
                PreLauncherPL != null && PreLauncherPL.ProcWrapper.IsRunning() ||
                PostGamePL != null && PostGamePL.ProcWrapper.IsRunning())
                await Task.Delay(1000);
        }

        #region Event Delegates
        private async void OnLauncherAcquired(object sender, ProcessEventArgs e)
        {
            int _type = -1;
            bool _running = false;
            int _aPID = 0;
            if (LauncherPL != null && LauncherMonitor != null)
            {// collect launcher information for collision avoidance
                _type = LauncherPL?.ProcWrapper?.ProcessType ?? -1;
                _running = LauncherMonitor.IsRunning();
                _aPID = e?.AvoidPID ?? 0;
            }

            if (LauncherPL != null && LauncherPL.ProcWrapper.IsRunning())
            {
                // MinimizeWindow after acquisition to prevent issues with ProcessType() fetch
                if (SetHnd.Options.MinimizeLauncher)
                    WindowUtils.MinimizeWindow(LauncherPL.ProcWrapper.Hwnd);

                // pause to let the launcher process stabilize after being hooked
                if (!SetHnd.Options.SkipLauncher)
                {
                    ProcessUtils.Logger("OSOL",
                        $"Launcher detected (type {_type}), preparing to launch game in {SetHnd.Options.PreGameLauncherWaitTime}s...");
                    await Task.Delay(SetHnd.Options.PreGameLauncherWaitTime * 1000);
                }
            }

            if (SetHnd.Options.SkipLauncher)
            {// ignore AutoGameLaunch option explicitly here
                if (LauncherURIMode)  // URI mode
                    GamePL = new ProcessLauncher(
                        SetHnd.Paths.LauncherURI, "",
                        avoidProcName: LauncherName,
                        delayTime: SetHnd.Options.PreGameWaitTime,
                        monitorName: GameName
                    );
                else  // normal SkipLauncher behavior
                    GamePL = new ProcessLauncher(
                        SetHnd.Paths.GamePath,
                        SetHnd.Paths.GameArgs,
                        avoidProcName: LauncherName,
                        delayTime: SetHnd.Options.PreGameWaitTime,
                        monitorName: MonitorName
                    );
                await GamePL.Launch();
            }
            else
            {
                if (_running && LauncherURIMode) // URIs
                    GamePL = new ProcessLauncher(
                        SetHnd.Paths.LauncherURI, "",
                        avoidProcName: LauncherName,
                        delayTime: SetHnd.Options.PreGameWaitTime,
                        avoidPID: _aPID,
                        monitorName: GameName
                    );
                else if (_running && _type == 1) // Battle.net (relaunch LauncherArgs)
                {
                    // Battle.net v1
                    GamePL = new ProcessLauncher(
                        SetHnd.Paths.LauncherPath,
                        SetHnd.Paths.LauncherArgs,
                        avoidProcName: LauncherName,
                        delayTime: SetHnd.Options.PreGameWaitTime,
                        avoidPID: _aPID,
                        monitorName: GameName
                    );

                    // Battle.net v2 doesn't launch via LauncherArgs so send Enter to the launcher
                    if (ProcessUtils.OrdinalContains("productcode=", SetHnd.Paths.LauncherArgs))
                        WindowUtils.SendEnterToForeground(LauncherPL.ProcWrapper.Hwnd);
                }
                else if (LauncherPathValid && _running || SetHnd.Options.AutoGameLaunch) // normal behavior
                {
                    GamePL = new ProcessLauncher(
                        SetHnd.Paths.GamePath,
                        SetHnd.Paths.GameArgs,
                        avoidProcName: LauncherName,
                        delayTime: SetHnd.Options.PreGameWaitTime,
                        avoidPID: _aPID,
                        monitorName: MonitorName
                    );
                }
                if (GamePL != null && (LauncherPathValid && _running || SetHnd.Options.AutoGameLaunch))
                    await GamePL?.Launch(); // only launch if safe to do so
                else if (LauncherPathValid && LauncherMonitor.IsRunning())
                    ProcessUtils.Logger("OSOL", "AutoGameLaunch is false, waiting for user to launch game before timing out...");
            }

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
                GamePL.ProcWrapper.Proc.ProcessorAffinity = (IntPtr)SetHnd.Options.GameProcessAffinity;
                ProcessUtils.Logger("OSOL",
                    $"Setting game process CPU affinity to: {BitmaskExtensions.AffinityToCoreString(SetHnd.Options.GameProcessAffinity)}");
            }

            if (SetHnd.Options.GameProcessPriority.ToString() != "Normal")
            {
                GamePL.ProcWrapper.Proc.PriorityClass = SetHnd.Options.GameProcessPriority;
                ProcessUtils.Logger("OSOL",
                    $"Setting game process priority to: {SetHnd.Options.GameProcessPriority.ToString()}");
            }
        }

        private async void OnGameExited(object sender, ProcessEventArgs e)
        {
            if (SetHnd.Options.MinimizeLauncher && LauncherPL.ProcWrapper.IsRunning())
                WindowUtils.MinimizeWindow(LauncherPL.ProcWrapper.Hwnd);

            // run PostGameExecPath/Args after the game exits
            PostGamePL = new ProcessLauncher(
                SetHnd.Paths.PostGameExecPath,
                SetHnd.Paths.PostGameExecArgs,
                elevate: SetHnd.Options.ElevateExternals,
                delayTime: SetHnd.Options.PostGameWaitTime
            );
            await PostGamePL.Launch();

            if (SetHnd.Options.PostGameWaitTime > 0)
                ProcessUtils.Logger("OSOL", $"Game exited, moving on to clean up after {SetHnd.Options.PostGameWaitTime}s...");
            else
                ProcessUtils.Logger("OSOL", $"Game exited, cleaning up...");
            await Task.Delay(SetHnd.Options.PostGameWaitTime * 1000);

            if (SetHnd.Options.CloseLauncher && ProcessWrapper.IsRunningByName(LauncherName))
            {
                ProcessUtils.Logger("OSOL", $"Found launcher still running, killing it...");
                ProcessWrapper.KillProcTreeByName(LauncherName);
            }
            OnClosing();
        }

        private void OnLauncherExited(object sender, ProcessEventArgs e)
        {// edge case if launcher times out (or closes) before game launches
            ProcessUtils.Logger("OSOL",
                $"Launcher could not be acquired within {ProcessUtils.ElapsedToString(e.Elapsed)}, cleaning up...");
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
            // forcefully kill externals on OSOL exit if requested
            if (SetHnd.Options.ForceKillExternals && PreLauncherPL != null && PreLauncherPL.ProcWrapper.IsRunning())
                ProcessWrapper.KillProcTreeByName(PreLauncherPL.ProcWrapper.ProcessName);
            if (SetHnd.Options.ForceKillExternals && PostGamePL != null && PostGamePL.ProcWrapper.IsRunning())
                ProcessWrapper.KillProcTreeByName(PostGamePL.ProcWrapper.ProcessName);
            // clean up system tray
            TrayUtil.RefreshTrayArea();
            // exit gracefully
            ExitRequested = true;
        }
    }
}