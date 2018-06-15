using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OriginSteamOverlayLauncher
{
    class ProcessTracking
    {
        public static int ValidateProcTree(Process[] procTree, int timeout)
        {
            var procChildren = procTree.Count();
            Thread.Sleep(timeout * 1000); // let process stabilize before gathering data

            if (procChildren == 1 && !procTree[0].HasExited 
                && procTree[0].MainWindowHandle != IntPtr.Zero && procTree[0].MainWindowTitle.Length > 0)
            {
                return procTree[0].Id; // just return the PID of the parent
            }
            else if (procChildren > 1)
            {// our parent is likely a caller or proxy
                for (int i = 0; i < procChildren; i++)
                {// iterate through each process in the tree and determine which process we should bind to
                    var proc = procTree[i];

                    if (proc.Id > 0 && !proc.HasExited)
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero && !proc.SafeHandle.IsInvalid && proc.MainWindowTitle.Length > 0)
                        {// probably a real process (launcher or game) because it has a real hwnd and title
                            return proc.Id;
                        }
                        else if (!procTree[0].HasExited && procTree[0].MainWindowHandle == IntPtr.Zero && procTree[0].MainWindowTitle.Length > 0)
                        {// child returns an invalid hwnd, return the parent PID instead
                            return procTree[0].Id;
                        }
                    }
                }
            }

            return 0;
        }

        private static Process GetProcessTreeHandle(Settings setHnd, String procName)
        {// actively attempt to rebind process by PID via ValidateProcTree()
            int _result = 0;
            Process _retProc = null;
            int sanity_counter = 0;

            Program.Logger("OSOL", String.Format("Searching for valid process by name: {0}", procName));
            while (sanity_counter < setHnd.ProcessAcquisitionTimeout)
            {// loop every ProxyTimeout (via ValidateProcTree()) until we get a validated PID by procName
                var _procTree = Program.GetProcessTreeByName(procName);
                // grab our first matching validated window PID
                _result = ValidateProcTree(_procTree, setHnd.ProxyTimeout);
                // update our counter for logging purposes
                sanity_counter = sanity_counter + setHnd.ProxyTimeout;

                // first check if we should bail early due to timeout
                if (sanity_counter >= setHnd.ProcessAcquisitionTimeout)
                {
                    Program.Logger("WARNING", String.Format("Could not bind to a valid process after waiting {0} seconds!", setHnd.ProcessAcquisitionTimeout));
                    break;
                }

#if DEBUG
                if (_procTree.Count() != 0)
                {
                    StringBuilder _procOut = new StringBuilder();
                    _procOut.Append("Trying to bind to detected processes at PIDs: ");

                    foreach (Process proc in _procTree)
                    {
                        _procOut.Append(proc.Id + " ");
                    }
                    Program.Logger("DEBUG", _procOut.ToString());
                }
#endif

                if (_result > 0)
                {// rebind our process handle using our validated PID
                    _retProc = Program.RebindProcessByID(_result);
                    Program.Logger("OSOL", String.Format("Bound to a valid process at PID: {0} [{1}] in {2} seconds", _result, String.Format("{0}.exe", procName), sanity_counter));
                    break;
                }
            }

            return _retProc; // returns null if nothing matched or we timed out, otherwise reports a validated Process handle
        }

        public void ProcessLauncher(Settings setHnd, IniFile iniHnd)
        {// pass our Settings and IniFile contexts into this workhorse routine
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
            String launcherMode = setHnd.LauncherMode;
            Process launcherProc = new Process();
            Process gameProc = new Process();
            TrayIconUtil trayUtil = new TrayIconUtil();

            // save our monitoring path for later
            String monitorPath = Settings.ValidatePath(setHnd.MonitorPath) ? setHnd.MonitorPath : String.Empty;
            String monitorName = Path.GetFileNameWithoutExtension(monitorPath);
            String _launchType = (monitorPath.Length > 0 ? "monitor" : "game");
            // save PIDs that we find
            int launcherPID = 0;
            int gamePID = 0;

            /*
             * Launcher Detection
             */

            // only use validated launcher if CommandlineProxy is not enabled, Launcher is not forced, and we have no DetectedCommandline
            if (Settings.ValidatePath(setHnd.LauncherPath) & (setHnd.ForceLauncher || !setHnd.CommandlineProxy || setHnd.DetectedCommandline.Length == 0))
            {
                // obey the user and avoid killing and relaunching the target launcher
                if (Program.IsRunning(launcherName) && setHnd.ReLaunch)
                {// if the launcher is running before the game kill it so we can run it through Steam
                    Program.Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                    Program.KillProcTreeByName(launcherName);
                    Thread.Sleep(setHnd.ProxyTimeout * 1000); // pause a moment for the launcher to close
                }

                // ask a non-async delegate to run a process before the launcher
                Program.ExecuteExternalElevated(setHnd, setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

                launcherProc.StartInfo.UseShellExecute = true;
                launcherProc.StartInfo.FileName = setHnd.LauncherPath;
                launcherProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.LauncherPath).ToString();
                launcherProc.StartInfo.Arguments = setHnd.LauncherArgs;

                Program.Logger("OSOL", String.Format("Attempting to start the launcher: {0}", setHnd.LauncherPath));
                launcherProc.Start();

                // loop until we have a valid process handle
                launcherProc = GetProcessTreeHandle(setHnd, launcherName);
                launcherPID = launcherProc != null ? launcherProc.Id : 0;
             
                if (setHnd.ForceLauncher || !setHnd.CommandlineProxy || setHnd.DetectedCommandline.Length == 0)
                if (launcherPID > 0)
                {
                    // do some waiting based on user tuneables to avoid BPM weirdness
                    Program.Logger("OSOL", String.Format("Waiting {0} seconds for potential launcher slowness...", setHnd.PreGameLauncherWaitTime));
                    Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000);

                    // force the launcher window to activate before the game to avoid BPM hooking issues
                    Program.BringToFront(launcherProc.MainWindowHandle);
                    // if the user requests it minimize our launcher after detecting it
                    if (setHnd.MinimizeLauncher)
                        Program.MinimizeWindow(launcherProc.MainWindowHandle);
                }
            }

            /*
             * Game Process Detection
             */

            if (Program.StringEquals(launcherMode, "Normal"))
            {// only run game ourselves if the user asks
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.GamePath;
                gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();

                // avoid executing GamePath if we need to grab arguments from a child of the launcher
                if (setHnd.CommandlineProxy && setHnd.DetectedCommandline.Length > 0)
                {// use the saved commandline from DetectedCommandline with GameArgs
                    gameProc.StartInfo.Arguments = setHnd.DetectedCommandline + " " + setHnd.GameArgs;
                    Program.Logger("OSOL", String.Format("Launching game with DetectedCommandline arguments, cmd: {0} {1} {2}", setHnd.GamePath, setHnd.DetectedCommandline, setHnd.GameArgs));
                    gameProc.Start();
                }
                else if (!setHnd.CommandlineProxy)
                {// just use the specified GameArgs since CommandlineProxy is disabled
                    Program.Logger("OSOL", String.Format("Launching game, cmd: {0} {1}", setHnd.GamePath, setHnd.GameArgs));
                    gameProc.StartInfo.Arguments = setHnd.GameArgs;
                    gameProc.Start();
                }
                    
            }
            else if (Program.StringEquals(launcherMode, "URI"))
            {
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.LauncherURI;
                
                try
                {// we can't control what will happen so try to catch exceptions
                    Program.Logger("OSOL", String.Format("Launching URI: {0}", setHnd.LauncherURI));
                    gameProc.Start();
                }
                catch (Exception ex)
                {// catch any exceptions and dump to log
                    Program.Logger("WARNING", String.Format("Failed to launch URI [{0}] double check your launcher installation: {1}", setHnd.LauncherURI, ex.ToString()));
                }
            }
            
            // wait for the GamePath executable up to the ProcessAcquisitionTimeout and use our MonitorPath if the user requests it
            gameProc = monitorPath.Length > 0 ? GetProcessTreeHandle(setHnd, monitorName) : GetProcessTreeHandle(setHnd, gameName);
            gamePID = gameProc != null ? gameProc.Id : 0;
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

                var _cmdLine = Program.GetCommandLineToString(gameProc, setHnd.GamePath);
                var _storedCmdline = setHnd.DetectedCommandline;
                Program.Logger("OSOL", String.Format("Detected arguments in [{0}]: {1}", gameProc.MainModule.ModuleName, _cmdLine));

                if (!Program.CompareCommandlines(_storedCmdline, _cmdLine)
                    && !Program.StringEquals(setHnd.GameArgs, _cmdLine))
                {// only proxy arguments if our target arguments differ
                    gameProc.Kill();
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    gameProc.StartInfo.UseShellExecute = true;
                    gameProc.StartInfo.FileName = setHnd.GamePath;
                    gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();
                    gameProc.StartInfo.Arguments = setHnd.GameArgs + " " + _cmdLine;
                    Program.Logger("OSOL", String.Format("Relaunching with proxied commandline, cmd: {0} {1} {2}", setHnd.GamePath, _cmdLine, setHnd.GameArgs));

                    gameProc.Start();
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    // rebind to relaunched process
                    gameProc = monitorPath.Length > 0 ? GetProcessTreeHandle(setHnd, monitorName) : GetProcessTreeHandle(setHnd, gameName);
                    gamePID = gameProc != null ? gameProc.Id : 0;

                    // save our newest active commandline for later
                    Program.StoreCommandline(setHnd, iniHnd, _cmdLine);
                    Program.Logger("OSOL", String.Format("Process arguments saved to INI: {0}", _cmdLine));
                }
            }

            if (gamePID > 0)
            {
                // run our post-game launch commands after a configurable sleep
                Thread.Sleep((setHnd.PostGameCommandWaitTime - 1) * 1000);

                if (setHnd.GameProcessAffinity > 0)
                {// use our specified CPU affinity bitmask
                    gameProc.ProcessorAffinity = (IntPtr)setHnd.GameProcessAffinity;
                    Program.Logger("OSOL", String.Format("Setting game process CPU affinity to: {0}", BitmaskExtensions.AffinityToCoreString(setHnd.GameProcessAffinity)));
                }
                if (!String.IsNullOrEmpty(_procPrio) && !Program.StringEquals(_procPrio, "Normal"))
                {// we have a custom process priority so let's use it
                    gameProc.PriorityClass = setHnd.GameProcessPriority;
                    Program.Logger("OSOL", String.Format("Setting game process priority to: {0}", setHnd.GameProcessPriority.ToString()));
                }

                while (Program.IsRunningPID(gamePID))
                {// spin while game is running
                    Thread.Sleep(1000);
                }

                Program.Logger("OSOL", String.Format("The {0} exited, moving on to clean up...", _launchType));
            }
            else
                Program.Logger("WARNING", String.Format("Could not find a {0} process by name: {1}", _launchType, Program.StringEquals("monitor", _launchType) ? gameName : monitorName));

            /*
             * Post-Game Cleanup
             */
            if (launcherPID > 0 && launcherProc != null && !launcherProc.HasExited && !setHnd.DoNotClose)
            {// found the launcher left after the game exited
                Thread.Sleep(1000);

                // resend the message to minimize our launcher
                if (setHnd.MinimizeLauncher)
                    Program.MinimizeWindow(launcherProc.MainWindowHandle);

                // let Origin sync with the cloud
                Thread.Sleep((setHnd.PostGameWaitTime - 1) * 1000);

                Program.Logger("OSOL", String.Format("Found launcher still running, cleaning up...", _launchType));

                // finally, kill our launcher proctree
                Program.KillProcTreeByName(launcherName);
            }

            // ask a non-async delegate to run a process after the game and launcher exit
            Program.ExecuteExternalElevated(setHnd, setHnd.PostGameExec, setHnd.PostGameExecArgs);

            // make sure we sleep a bit to ensure the external process and launcher terminate properly
            Thread.Sleep(setHnd.ProxyTimeout * 1000);
            // clean up system tray if process related icons are leftover
            trayUtil.RefreshTrayArea();
        }
    }
}