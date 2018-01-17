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
        private static Process GetProcessTreeHandle(Settings setHnd, String procName)
        {// actively attempt to rebind process by PID via ValidateProcTree()
            int _result = 0;
            Process _retProc = null;
            int sanity_counter = 0;

            while (sanity_counter < setHnd.ProcessAcquisitionTimeout)
            {// loop every ProxyTimeout (via ValidateProcTree()) until we get a validated PID by procName
                var _procTree = Program.GetProcessTreeByName(procName);
                // grab our first matching validated window PID
                _result = Program.ValidateProcTree(_procTree, setHnd.ProxyTimeout);
                // update our counter for logging purposes
                sanity_counter = sanity_counter + setHnd.ProxyTimeout;

                // first check if we should bail early due to timeout
                if (sanity_counter >= setHnd.ProcessAcquisitionTimeout)
                {
                    Program.Logger("WARNING", "Could not detect a valid process after waiting " + setHnd.ProcessAcquisitionTimeout + " seconds!");
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
                    Program.Logger("OSOL", "Detected a valid process at PID: " + _result + " in " + sanity_counter + " seconds");
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
            String _launchType = (monitorPath.Length > 0 ? " monitor " : " game ");
            // save PIDs that we find
            int launcherPID = 0;
            int gamePID = 0;

            /*
             * Launcher Detection
             */

            // obey the user and avoid killing and relaunching the target launcher
            if (Program.IsRunning(launcherName) && setHnd.ReLaunch)
            {// if the launcher is running before the game kill it so we can run it through Steam
                Program.Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                Program.KillProcTreeByName(launcherName);
                Thread.Sleep(setHnd.ProxyTimeout * 1000); // pause a moment for the launcher to close
            }

            if (Settings.ValidatePath(setHnd.LauncherPath))
            {
                // ask a non-async delegate to run a process before the launcher
                Program.ExecuteExternalElevated(setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

                launcherProc.StartInfo.UseShellExecute = true;
                launcherProc.StartInfo.FileName = setHnd.LauncherPath;
                launcherProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.LauncherPath).ToString();
                launcherProc.StartInfo.Arguments = setHnd.LauncherArgs;

                Program.Logger("OSOL", "Attempting to start the launcher: " + setHnd.LauncherPath);
                launcherProc.Start();

                // loop until we have a valid process handle
                launcherProc = GetProcessTreeHandle(setHnd, launcherName);
                launcherPID = launcherProc != null ? launcherProc.Id : 0;

                // force the launcher window to activate before the game to avoid BPM hooking issues
                Thread.Sleep(setHnd.PreGameOverlayWaitTime * 1000); // wait for the BPM overlay notification
                Program.BringToFront(launcherProc.MainWindowHandle);

                // if the user requests it minimize our launcher after detecting it
                if (setHnd.MinimizeLauncher)
                    Program.MinimizeWindow(launcherProc.MainWindowHandle);
            }// skip over the launcher if we're only launching a game path

            /*
             * Game Post-Proxy Detection
             */

            if (Settings.StringEquals(launcherMode, "Normal"))
            {// only run game ourselves if the user asks
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.GamePath;
                gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();
                gameProc.StartInfo.Arguments = setHnd.GameArgs;
                Program.Logger("OSOL", "Launching game, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);

                gameProc.Start();
            }
            else if (Settings.StringEquals(launcherMode, "URI"))
            {
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.LauncherURI;

                Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000); // wait to hook some sluggish launchers
                try
                {// we can't control what will happen so try to catch exceptions
                    Program.Logger("OSOL", "Launching URI: " + setHnd.LauncherURI);
                    gameProc.Start();
                }
                catch (Exception x)
                {// catch any exceptions and dump to log
                    Program.Logger("OSOL", "Failed to launch URI [" + setHnd.LauncherURI + "] double check your launcher installation");
                    Program.Logger("EXCEPTION", x.ToString());
                }
            }
            
            // use the monitor module name if the user requests it, otherwise default to detecting by the game module name
            gameProc = monitorPath.Length > 0 ? GetProcessTreeHandle(setHnd, monitorName) : GetProcessTreeHandle(setHnd, gameName);
            gamePID = gameProc != null ? gameProc.Id : 0;

            if (setHnd.CommandlineProxy)
            {// relaunch based on detected commandline if the user requests it
                var _cmdLine = Program.GetCommandLineToString(gameProc, setHnd.GamePath);
                var _storedCmdline = setHnd.DetectedCommandline;
                
                if (!Program.CompareCommandlines(_storedCmdline, _cmdLine)
                    && !Settings.StringEquals(setHnd.GameArgs, _cmdLine))
                {// only proxy arguments if our target arguments differ
                    gameProc.Kill();
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    gameProc.StartInfo.UseShellExecute = true;
                    gameProc.StartInfo.FileName = setHnd.GamePath;
                    gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();
                    gameProc.StartInfo.Arguments = setHnd.GameArgs + " " + _cmdLine;
                    Program.Logger("OSOL", "Relaunching with proxied commandline, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);
                    gameProc.Start();

                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    // rebind to relaunched process targetting the monitor or game process
                    gameProc = monitorPath.Length > 0 ? GetProcessTreeHandle(setHnd, monitorName) : GetProcessTreeHandle(setHnd, gameName);
                    gamePID = gameProc != null ? gameProc.Id : 0;

                    // save our newest active commandline for later
                    Program.StoreCommandline(setHnd, iniHnd, _cmdLine);
                    Program.Logger("OSOL", String.Format("Process arguments saved to INI: {0}", _cmdLine));
                }
            }

            while (Program.IsRunningPID(gamePID))
            {
                Thread.Sleep(1000);
            }

            Program.Logger("OSOL", "Game exited, cleaning up...");

            /*
                * Post-Game Cleanup
                */
            if (setHnd.LauncherPath != String.Empty && Program.IsRunningPID(launcherProc.Id) && !setHnd.DoNotClose)
            {// found the launcher left after the game exited
                Thread.Sleep(setHnd.PostGameWaitTime * 1000); // let Origin sync with the cloud
                Program.KillProcTreeByName(launcherName);
            }

            // ask a non-async delegate to run a process after the game and launcher exit
            Program.ExecuteExternalElevated(setHnd.PostGameExec, setHnd.PostGameExecArgs);

            // make sure we sleep a bit to ensure the external process and launcher terminate properly
            Thread.Sleep(setHnd.ProxyTimeout * 1000);
            // clean up system tray if process related icons are leftover
            trayUtil.RefreshTrayArea();
        }
    }
}