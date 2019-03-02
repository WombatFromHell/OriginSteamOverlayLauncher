using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class ProcessTracking
    {
        private static void ProcessMonitor(Settings setHnd, ProcessObj procObj)
        {// attempt to reacquire the target process by name within a timeout if it exits
            int globalElapsed = 0;
            internalSpinner(procObj);

            while (globalElapsed < setHnd.InterProcessAcquisitionTimeout * 1000)
            {// by default we wait up to 10s for a named process to respawn
                Stopwatch _sw = new Stopwatch();
                _sw.Start();
                ProcessObj _proc = new ProcessObj(procObj.ProcessName);
                _sw.Stop();
                globalElapsed += Convert.ToInt32(_sw.ElapsedMilliseconds);

                if (_proc.ProcessId > 0 && _proc.ProcessRef != null && !_proc.ProcessRef.HasExited)
                {
                    ProcessUtils.Logger("OSOL",
                        $"Process monitor found another matching process ({_proc.ProcessName}.exe [{_proc.ProcessId}])"
                    );
                    globalElapsed = 0;
                    internalSpinner(_proc);
                }
                else if (_proc.ProcessId == 0)
                {// we failed to get a running process so wait a tick
                    Thread.Sleep(1000);
                    globalElapsed += 1000;
                }
            }

            ProcessUtils.Logger("OSOL",
                $"Timed out waiting for monitored process {procObj.ProcessName}.exe after {setHnd.InterProcessAcquisitionTimeout}s"
            );

            void internalSpinner(ProcessObj process)
            {// spin while target process is running
                Stopwatch _isw = new Stopwatch();
                _isw.Start();
                while (!process.ProcessRef.HasExited)
                {
                    Thread.Sleep(1000);
                }
                _isw.Stop();
                ProcessUtils.Logger("OSOL",
                    $"Monitored process exited after {ConvertElapsedToString(_isw.ElapsedMilliseconds)} attempting to reaquire {process.ProcessName}.exe"
                );
            }
        }

        public static String ConvertElapsedToString(long stopwatchElapsed)
        {
            double tempSecs = Convert.ToDouble(stopwatchElapsed / 1000);
            double tempMins = Convert.ToDouble(tempSecs / 60);
            // return minutes or seconds (if applicable)
            return tempSecs > 60 ? $"{tempMins:0.##}m" : $"{tempSecs:0.##}s";
        }

        public void ProcessLauncher(Settings setHnd, IniFile iniHnd)
        {// pass our Settings and IniFile contexts into this workhorse routine
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
            String launcherMode = setHnd.LauncherMode;
            Process launcherProc = new Process(), gameProc = new Process();
            ProcessObj gameProcObj = null, launcherProcObj = null;
            TrayIconUtil trayUtil = new TrayIconUtil();

            // save our monitoring path for later
            String monitorPath = Settings.ValidatePath(setHnd.MonitorPath) ? setHnd.MonitorPath : String.Empty;
            String monitorName = Path.GetFileNameWithoutExtension(monitorPath);
            String _launchType = (monitorPath.Length > 0 ? "monitor" : "game");

            // TODO: IMPLEMENT ASYNC TASK SYSTEM FOR LAUNCHER AND GAME!


            /*
             * Launcher Detection:
             * 
             * 1) If LauncherPath not set skip to Step 8
             * 2) If SkipLauncher is set skip to Step 8
             * 3) Check if LauncherPath is actively running - relaunch via Steam->OSOL
             * 4) Execute pre-launcher delegate
             * 5) Execute launcher (using ShellExecute) - use LauncherURI if set (not ShellExecute)
             * 6) Hand off to GetProcessObj() for detection
             *    a) GetProcessObj() loops on timeout until valid process is detected
             *    b) ValidateProcessByName() attempts to validate Process and PID returns
             *    c) Returns ProcessObj with correct PID, process type, and Process handle
             * 7) Perform post-launcher behaviors
             * 8) Continue to game
             */

            #region LauncherDetection
            // only use validated launcher if CommandlineProxy is not enabled, Launcher is not forced, and we have no DetectedCommandline
            if (!setHnd.SkipLauncher & Settings.ValidatePath(setHnd.LauncherPath) & (setHnd.ForceLauncher || !setHnd.CommandlineProxy || setHnd.DetectedCommandline.Length == 0))
            {
                // check for running instance of launcher (relaunch if required)
                if (ProcessUtils.IsRunningByName(launcherName) && setHnd.ReLaunch)
                {// if the launcher is running before the game kill it so we can run it through Steam
                    ProcessUtils.Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                    ProcessUtils.KillProcTreeByName(launcherName);
                    Thread.Sleep(setHnd.ProxyTimeout * 1000); // pause a moment for the launcher to close
                }

                // ask a non-async delegate to run a process before the launcher
                ProcessUtils.ExecuteExternalElevated(setHnd, setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs, 0);

                if (ProcessUtils.StringEquals(launcherMode, "URI") && !String.IsNullOrEmpty(setHnd.LauncherURI))
                {// use URI launching as a mutually exclusive alternative to "Normal" launch mode (calls launcher->game)
                    gameProc.StartInfo.UseShellExecute = true;
                    gameProc.StartInfo.FileName = setHnd.LauncherURI;
                    gameProc.StartInfo.Arguments = setHnd.GameArgs;
                    ProcessUtils.Logger("OSOL", $"Launching URI: {setHnd.LauncherURI} {setHnd.GameArgs}");

                    ProcessUtils.LaunchProcess(gameProc);
                }
                else
                {
                    launcherProc.StartInfo.UseShellExecute = true;
                    launcherProc.StartInfo.FileName = setHnd.LauncherPath;
                    launcherProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.LauncherPath).ToString();
                    launcherProc.StartInfo.Arguments = setHnd.LauncherArgs;
                    ProcessUtils.Logger("OSOL", $"Attempting to start the launcher: {setHnd.LauncherPath}");

                    ProcessUtils.LaunchProcess(launcherProc);
                }

                launcherProcObj = ProcessObj.GetProcessObj(setHnd, launcherName);
                launcherProc = launcherProcObj.ProcessRef;

                if (launcherProcObj.ProcessId > 0)
                {
                    // do some waiting based on user tuneables to avoid BPM weirdness
                    ProcessUtils.Logger("OSOL", $"Waiting {setHnd.PreGameLauncherWaitTime}s for launcher process to load...");
                    Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000);

                    if (launcherProcObj.ProcessType > -1)
                    {// we can only send window messages if we have a window handle
                        WindowUtils.BringToFront(WindowUtils.HwndFromProc(launcherProc));
                        if (setHnd.MinimizeLauncher && launcherProc.MainWindowHandle != IntPtr.Zero)
                            WindowUtils.MinimizeWindow(WindowUtils.HwndFromProc(launcherProc));
                    }
                }
            }
            #endregion

            /*
             * Game Process Detection:
             * 
             * 1) Only launch GamePath if we're in "Normal" launch mode (pre-validated)
             * 2) Execute GamePath (or use Launcher if we have an exclusive case)
             * 3) Hand off to GetProcessObj() for detection
             *    a) GetProcessObj() loops on timeout until valid process is detected
             *    b) ValidateProcessByName() attempts to validate Process and PID returns
             *    c) Returns ProcessObj with correct PID, process type, and Process handle
             * 4) If we're using CommandlineProxy attempt to detect the target process cmdline
             *    a) If we've got a cmdline relaunch GamePath with it
             * 5) Do post-game-detection steps
             * 6) Hand off to MonitorProcess() for watching our launched game
             */

            #region GameDetection
            if (ProcessUtils.StringEquals(launcherMode, "Normal"))
            {// we're not in URI or LauncherOnly modes - this is the default
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.GamePath;
                gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();

                if (launcherProcObj.ProcessType == 1)
                {// we've detected Battle.net Launcher so let's ask the launcher politely to start the game
                    gameProc.StartInfo.FileName = setHnd.LauncherPath; // use the launcher - look for the game below in GetProcessTreeHandle()
                    gameProc.StartInfo.Arguments = setHnd.LauncherArgs; // these contain the game launch command

                    ProcessUtils.Logger("OSOL", $"Detected Battle.net launcher, calling game via: {setHnd.LauncherPath} {setHnd.LauncherArgs}");

                    ProcessUtils.LaunchProcess(gameProc);
                }
                else if (setHnd.CommandlineProxy && setHnd.DetectedCommandline.Length > 0)
                {// avoid executing GamePath if we need to grab arguments from a child of the launcher
                    // use the saved commandline from DetectedCommandline with GameArgs
                    gameProc.StartInfo.Arguments = setHnd.DetectedCommandline + " " + setHnd.GameArgs;
                    ProcessUtils.Logger("OSOL", $"Launching game with DetectedCommandline arguments, cmd: {setHnd.GamePath} {setHnd.DetectedCommandline} {setHnd.GameArgs}");

                    ProcessUtils.LaunchProcess(gameProc);
                }
                else if (!setHnd.CommandlineProxy)
                {// just launch the game since we've fallen through all the exclusive cases
                    ProcessUtils.Logger("OSOL", $"Launching game, cmd: {setHnd.GamePath} {setHnd.GameArgs}");
                    gameProc.StartInfo.Arguments = setHnd.GameArgs;

                    ProcessUtils.LaunchProcess(gameProc);

                    if (setHnd.SkipLauncher && Settings.ValidatePath(setHnd.LauncherPath))
                    {// we still need LauncherPath tracking in Normal mode even though we didn't launch it ourselves (if defined)
                        ProcessUtils.Logger("OSOL", "Acquiring launcher handle because we didn't launch it ourselves...");
                        launcherProcObj = ProcessObj.GetProcessObj(setHnd, launcherName);
                        launcherProc = launcherProcObj.ProcessRef;

                        if (launcherProcObj.ProcessId > 0)
                        {
                            // do some waiting based on user tuneables to avoid BPM weirdness
                            ProcessUtils.Logger("OSOL", $"Waiting {setHnd.PreGameLauncherWaitTime}s for launcher process to load...");
                            Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000);

                            if (launcherProcObj.ProcessType > -1)
                            {// we can only send window messages if we have a window handle
                                WindowUtils.BringToFront(WindowUtils.HwndFromProc(launcherProc));
                                if (setHnd.MinimizeLauncher)
                                    WindowUtils.MinimizeWindow(WindowUtils.HwndFromProc(launcherProc));
                            }
                        }
                    }
                }
            }

            // wait for the executable defined in GamePath (up to the ProcessAcquisitionTimeout) or use our MonitorPath if the user requests it
            gameProcObj = monitorPath.Length > 0 ? ProcessObj.GetProcessObj(setHnd, monitorName) : ProcessObj.GetProcessObj(setHnd, gameName);
            gameProc = gameProcObj.ProcessRef;
            string _procPrio = setHnd.GameProcessPriority.ToString();

            if (setHnd.CommandlineProxy && setHnd.DetectedCommandline.Length == 0)
            {
                /*
                 * Our logic here is a bit confusing:
                 *  1) If CommandlineProxy is enabled and we have no proxied arguments then grab them from the bound process
                 *  2) Once we have arguments kill the existing bound process
                 *  3) Launch a new process based on the GamePath with our freshly proxied arguments
                 *  4) Save these proxied arguments to the INI under DetectedCommandline
                 */

                var _cmdLine = ProcessUtils.GetCommandLineToString(gameProc, setHnd.GamePath);
                var _storedCmdline = setHnd.DetectedCommandline;
                ProcessUtils.Logger("OSOL", $"Detected arguments in [{gameProc.MainModule.ModuleName}]: {_cmdLine}");

                if (!ProcessUtils.CompareCommandlines(_storedCmdline, _cmdLine)
                    && !ProcessUtils.StringEquals(setHnd.GameArgs, _cmdLine))
                {// only proxy arguments if our target arguments differ
                    gameProc.Kill();
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    gameProc.StartInfo.UseShellExecute = true;
                    gameProc.StartInfo.FileName = setHnd.GamePath;
                    gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();
                    gameProc.StartInfo.Arguments = setHnd.GameArgs + " " + _cmdLine;
                    ProcessUtils.Logger("OSOL", $"Relaunching with proxied commandline, cmd: {setHnd.GamePath} {_cmdLine} {setHnd.GameArgs}");

                    ProcessUtils.LaunchProcess(gameProc);
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    // rebind to relaunched process
                    gameProcObj = monitorPath.Length > 0 ?
                        ProcessObj.GetProcessObj(setHnd, monitorName) : ProcessObj.GetProcessObj(setHnd, gameName);
                    gameProc = gameProcObj.ProcessRef;

                    // save our newest active commandline for later
                    ProcessUtils.StoreCommandline(setHnd, iniHnd, _cmdLine);
                    ProcessUtils.Logger("OSOL", $"Process arguments saved to INI: {_cmdLine}");
                }
            }
            #endregion

            #region WaitForGame
            if (gameProcObj != null && gameProcObj.ProcessId > 0 && gameProcObj.ProcessType > -1)
            {
                // run our post-game launch commands after a configurable sleep
                Thread.Sleep((setHnd.PostGameCommandWaitTime - 1) * 1000);

                if (setHnd.GameProcessAffinity > 0)
                {// use our specified CPU affinity bitmask
                    gameProc.ProcessorAffinity = (IntPtr)setHnd.GameProcessAffinity;
                    ProcessUtils.Logger("OSOL",
                        $"Setting game process CPU affinity to: {BitmaskExtensions.AffinityToCoreString(setHnd.GameProcessAffinity)}"
                    );
                }
                if (!ProcessUtils.StringEquals(_procPrio, "Normal"))
                {// we have a custom process priority so let's use it
                    gameProc.PriorityClass = setHnd.GameProcessPriority;
                    ProcessUtils.Logger("OSOL", $"Setting game process priority to: {setHnd.GameProcessPriority.ToString()}");
                }

                if (setHnd.TerminateOSOLUponLaunch)
                {// since we've done all that's been asked we can quit out if requested
                    ProcessUtils.Logger("OSOL", "User requested self-termination after game launch, exiting now...");
                    Environment.Exit(0);
                }
                else
                {
                    // watch for our game process and return when appropriate
                    ProcessMonitor(setHnd, gameProcObj);
                    ProcessUtils.Logger("OSOL",
                        $"Process exited ({gameProcObj.ProcessName}.exe [{gameProcObj.ProcessId}]) moving on to clean up after {setHnd.PostGameWaitTime}s..."
                    );
                }
            }
            else
            {
                string _procName = ProcessUtils.StringEquals("monitor", _launchType) ? gameProcObj.ProcessName : monitorName;
                ProcessUtils.Logger("WARNING", $"Could not find a {_launchType} process by name, exiting: {_procName}");
            }
            #endregion

            /*
             * Post-Game Cleanup
             */
            #region PostGame
            if (launcherProcObj.ProcessId > 0 && !launcherProcObj.ProcessRef.HasExited && !setHnd.DoNotClose)
            {
                Thread.Sleep(1000); // let the launcher settle after exiting the game

                // resend the message to minimize our launcher
                if (setHnd.MinimizeLauncher && launcherProc.MainWindowHandle != IntPtr.Zero)
                    WindowUtils.MinimizeWindow(WindowUtils.HwndFromProc(launcherProc));

                // let Origin sync with the cloud
                Thread.Sleep((setHnd.PostGameWaitTime - 1) * 1000);

                ProcessUtils.Logger("OSOL", "Found launcher still running, cleaning up...");

                // finally, kill our launcher proctree
                ProcessUtils.KillProcTreeByName(launcherName);
            }

            // ask a non-async delegate to run a process after the game and launcher exit
            ProcessUtils.ExecuteExternalElevated(setHnd, setHnd.PostGameExec, setHnd.PostGameExecArgs, setHnd.PostGameCommandWaitTime);

            // make sure we sleep a bit to ensure the external process and launcher terminate properly
            Thread.Sleep(setHnd.ProxyTimeout * 1000);
            // clean up system tray if process related icons are leftover
            trayUtil.RefreshTrayArea();
            #endregion
        }
    }
}