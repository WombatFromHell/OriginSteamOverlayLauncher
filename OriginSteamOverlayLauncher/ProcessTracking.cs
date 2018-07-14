using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OriginSteamOverlayLauncher
{
    public class ProcessTracking
    {
        // borrowed from: https://code.msdn.microsoft.com/windowsapps/C-Getting-the-Windows-da1bd524
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetClassName(IntPtr hwnd, StringBuilder lpClassName, long nMaxCount);

        private static string GetCaptionOfWindow(IntPtr hwnd)
        {
            string caption = "";
            StringBuilder windowText = null;

            try
            {
                int max_length = GetWindowTextLength(hwnd);
                windowText = new StringBuilder("", max_length + 5);
                GetWindowText(hwnd, windowText, max_length + 2);

                if (!String.IsNullOrEmpty(windowText.ToString()) && !String.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
            catch (Exception ex)
            {
                Program.Logger("EXCEPTION", ex.Message);
            }
            finally
            {
                windowText = null;
            }

            return caption;
        }

        private static string GetClassNameOfWindow(IntPtr hwnd)
        {
            string className = "";
            StringBuilder classText = null;

            try
            {
                int cls_max_length = 1000;
                classText = new StringBuilder("", cls_max_length + 5);
                GetClassName(hwnd, classText, cls_max_length + 2);

                if (!String.IsNullOrEmpty(classText.ToString()) && !String.IsNullOrWhiteSpace(classText.ToString()))
                    className = classText.ToString();
            }
            catch (Exception ex)
            {
                Program.Logger("EXCEPTION", ex.Message);
            }
            finally
            {
                classText = null;
            }

            return className;
        }
        //

        public static int DetectWindowType(Process procHnd)
        {// case testing for window class and title matching
            var processType = -1;
            var _hWnd = procHnd.MainWindowHandle != null ? procHnd.MainWindowHandle : (IntPtr)null;

            if (_hWnd != null)
            {// since we've got a window handle let's pass on what we find
                var _titleComp = "";
                var _classComp = "";
                var _targetTitle = GetCaptionOfWindow(_hWnd);
                var _targetClass = GetClassNameOfWindow(_hWnd);

                //
                // test for QT5 based launchers
                //

                // Origin Launcher [Type 2]
                _titleComp = "Origin";
                _classComp = "Qt5QWindowIcon";

                // class must match but title may differ
                if (Program.OrdinalContains(_titleComp, _targetTitle)
                    && Program.StringEquals(_classComp, _targetClass))
                    return processType = 2;

                // Blizzard Battle.net Launcher [Type 1]
                _titleComp = "Blizzard Battle.net";

                if (Program.OrdinalContains(_titleComp, _targetTitle)
                    && Program.StringEquals(_classComp, _targetClass))
                    return processType = 1;

                //
                // catch-all for everything else [Type 0]
                //

                // just check if we've got a window class and title
                if (_targetTitle.Length > 0 && _targetClass.Length > 0)
                    return processType = 0;
            }

            return processType;
        }

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

        public static Process GetProcessTreeHandle(Settings setHnd, String procName, ref int processType)
        {// actively attempt to rebind process by PID via ValidateProcTree()
            int _result = 0;
            Process _retProc = null;
            int sanity_counter = 0;
            processType = -1;

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
                    processType = DetectWindowType(_retProc); // pass our process type out by ref
                    Program.Logger("OSOL", String.Format("Bound to a valid process at PID: {0} [{1}] in {2} seconds", _result, String.Format("{0}.exe", procName), sanity_counter));
                    break;
                }
            }

            return _retProc; // returns null if nothing matched or we timed out, otherwise reports a validated Process handle
        }

        public static void LaunchProcess(Process proc)
        {// abstract Process.Start() for exception handling purposes...
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                Program.Logger("FATAL EXCEPTION", ex.Message);
                Environment.Exit(0);
            }
        }
        
        public void ProcessLauncher(Settings setHnd, IniFile iniHnd)
        {// pass our Settings and IniFile contexts into this workhorse routine
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
            String launcherMode = setHnd.LauncherMode;
            Process launcherProc = new Process();
            Process gameProc = new Process();
            TrayIconUtil trayUtil = new TrayIconUtil();
            // save detected process types for both our launcher and game
            int launcherType = -1;
            int gameType = -1;

            // save our monitoring path for later
            String monitorPath = Settings.ValidatePath(setHnd.MonitorPath) ? setHnd.MonitorPath : String.Empty;
            String monitorName = Path.GetFileNameWithoutExtension(monitorPath);
            String _launchType = (monitorPath.Length > 0 ? "monitor" : "game");
            // save PIDs that we find
            int launcherPID = 0;
            int gamePID = 0;


            /*
             * Launcher Detection:
             * 
             * 1) If LauncherPath not set skip to Step 9
             * 2) If SkipLauncher is set skip to Step 9
             * 3) Check if LauncherPath is actively running - relaunch via Steam->OSOL
             * 4) Execute pre-launcher delegate
             * 5) Execute launcher (using ShellExecute) - use LauncherURI if set (not ShellExecute)
             * 6) Hand off to GetProcessTreeHandle() for detection
             *    a) GetProcessTreeHandle() loops on timeout until valid process is detected
             *    b) ValidateProcessTree() attempts to validate Process and PID returns
             *    c) Returns Process with correct PID and Process handle
             * 7) Validate detection return (pass back launcher type case number)
             * 8) Perform post-launcher behaviors
             * 9) Continue to game
             */

            #region LauncherDetection
            // only use validated launcher if CommandlineProxy is not enabled, Launcher is not forced, and we have no DetectedCommandline
            if (!setHnd.SkipLauncher & Settings.ValidatePath(setHnd.LauncherPath) & (setHnd.ForceLauncher || !setHnd.CommandlineProxy || setHnd.DetectedCommandline.Length == 0))
            {
                // check for running instance of launcher (relaunch if required)
                if (Program.IsRunning(launcherName) && setHnd.ReLaunch)
                {// if the launcher is running before the game kill it so we can run it through Steam
                    Program.Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                    Program.KillProcTreeByName(launcherName);
                    Thread.Sleep(setHnd.ProxyTimeout * 1000); // pause a moment for the launcher to close
                }

                // ask a non-async delegate to run a process before the launcher
                Program.ExecuteExternalElevated(setHnd, setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

                if (Program.StringEquals(launcherMode, "URI") && !String.IsNullOrEmpty(setHnd.LauncherURI))
                {// use URI launching as a mutually exclusive alternative to "Normal" launch mode (calls launcher->game)
                    gameProc.StartInfo.UseShellExecute = true;
                    gameProc.StartInfo.FileName = setHnd.LauncherURI;
                    gameProc.StartInfo.Arguments = setHnd.GameArgs;
                    Program.Logger("OSOL", String.Format("Launching URI: {0} {1}", setHnd.LauncherURI, setHnd.GameArgs));

                    LaunchProcess(gameProc);
                }
                else
                {
                    launcherProc.StartInfo.UseShellExecute = true;
                    launcherProc.StartInfo.FileName = setHnd.LauncherPath;
                    launcherProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.LauncherPath).ToString();
                    launcherProc.StartInfo.Arguments = setHnd.LauncherArgs;
                    Program.Logger("OSOL", String.Format("Attempting to start the launcher: {0}", setHnd.LauncherPath));

                    LaunchProcess(launcherProc);
                }

                // loop until we have a valid process handle
                // pass a ref out of GetProcessTreeHandle() with our detected process type
                launcherProc = GetProcessTreeHandle(setHnd, launcherName, ref launcherType);
                launcherPID = launcherProc != null ? launcherProc.Id : 0;

                if (launcherPID > 0 && launcherType > -1)
                {
                    // do some waiting based on user tuneables to avoid BPM weirdness
                    Program.Logger("OSOL", String.Format("Waiting {0}s for launcher process to load...", setHnd.PreGameLauncherWaitTime));
                    Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000);
                    Program.BringToFront(launcherProc.MainWindowHandle);

                    // if the user requests it minimize our launcher after detecting it
                    if (setHnd.MinimizeLauncher)
                        Program.MinimizeWindow(launcherProc.MainWindowHandle);
                }
            }
            #endregion

            /*
             * Game Process Detection:
             * 
             * 1) Only launch GamePath if we're in "Normal" launch mode (pre-validated)
             * 2) Execute GamePath (or use Launcher if we have an exclusive case)
             * 3) Hand off to GetProcessTreeHandle() for detection
             *    a) GetProcessTreeHandle() loops on timeout until valid process is detected
             *    b) ValidateProcessTree() attempts to validate Process and PID returns
             *    c) Returns Process with correct PID and Process handle
             * 4) If we're using CommandlineProxy attempt to detect the target process cmdline
             *    a) If we've got a cmdline relaunch GamePath with it
             * 5) Validate game process detection return
             * 6) Do post-game-detection steps
             * 7) Spin for Game process until it exits
             */

            #region GameDetection
            if (Program.StringEquals(launcherMode, "Normal"))
            {// we're not in URI or LauncherOnly modes - this is the default
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.GamePath;
                gameProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.GamePath).ToString();

                if (launcherType == 1)
                {// we've detected Battle.net Launcher so let's ask the launcher politely to start the game
                    gameProc.StartInfo.FileName = setHnd.LauncherPath; // use the launcher - look for the game below in GetProcessTreeHandle()
                    gameProc.StartInfo.Arguments = setHnd.LauncherArgs; // these contain the game launch command

                    Program.Logger("OSOL", String.Format("Detected Battle.net launcher, calling game via: {0} {1}", setHnd.LauncherPath, setHnd.LauncherArgs));

                    LaunchProcess(gameProc);
                }
                else if (setHnd.CommandlineProxy && setHnd.DetectedCommandline.Length > 0)
                {// avoid executing GamePath if we need to grab arguments from a child of the launcher
                    // use the saved commandline from DetectedCommandline with GameArgs
                    gameProc.StartInfo.Arguments = setHnd.DetectedCommandline + " " + setHnd.GameArgs;
                    Program.Logger("OSOL", String.Format("Launching game with DetectedCommandline arguments, cmd: {0} {1} {2}", setHnd.GamePath, setHnd.DetectedCommandline, setHnd.GameArgs));

                    LaunchProcess(gameProc);
                }
                else if (!setHnd.CommandlineProxy)
                {// just launch the game since we've fallen through all the exclusive cases
                    Program.Logger("OSOL", String.Format("Launching game, cmd: {0} {1}", setHnd.GamePath, setHnd.GameArgs));
                    gameProc.StartInfo.Arguments = setHnd.GameArgs;

                    LaunchProcess(gameProc);

                    if (setHnd.SkipLauncher && Settings.ValidatePath(setHnd.LauncherPath))
                    {// we still need LauncherPath tracking in Normal mode even though we didn't launch it ourselves (if defined)
                        Program.Logger("OSOL", String.Format("Acquiring launcher handle because we didn't launch it ourselves..."));
                        launcherProc = GetProcessTreeHandle(setHnd, launcherName, ref launcherType);
                        launcherPID = launcherProc != null ? launcherProc.Id : 0;

                        if (launcherPID > 0 && launcherType > -1)
                        {
                            // do some waiting based on user tuneables to avoid BPM weirdness
                            Program.Logger("OSOL", String.Format("Waiting {0}s for launcher process to load...", setHnd.PreGameLauncherWaitTime));
                            Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000);
                            Program.BringToFront(launcherProc.MainWindowHandle);

                            // if the user requests it minimize our launcher after detecting it
                            if (setHnd.MinimizeLauncher)
                                Program.MinimizeWindow(launcherProc.MainWindowHandle);
                        }
                    }
                }
            }

            // wait for the executable defined in GamePath (up to the ProcessAcquisitionTimeout) or use our MonitorPath if the user requests it
            gameProc = monitorPath.Length > 0 ? 
                GetProcessTreeHandle(setHnd, monitorName, ref gameType) : GetProcessTreeHandle(setHnd, gameName, ref gameType);

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

                    LaunchProcess(gameProc);
                    Thread.Sleep(setHnd.ProxyTimeout * 1000);

                    // rebind to relaunched process
                    gameProc = monitorPath.Length > 0 ? GetProcessTreeHandle(setHnd, monitorName, ref gameType) : GetProcessTreeHandle(setHnd, gameName, ref gameType);
                    gamePID = gameProc != null ? gameProc.Id : 0;

                    // save our newest active commandline for later
                    Program.StoreCommandline(setHnd, iniHnd, _cmdLine);
                    Program.Logger("OSOL", String.Format("Process arguments saved to INI: {0}", _cmdLine));
                }
            }
            #endregion

            #region WaitForGame
            if (gamePID > 0 && gameType > -1)
            {
                // run our post-game launch commands after a configurable sleep
                Thread.Sleep((setHnd.PostGameCommandWaitTime - 1) * 1000);

                if (setHnd.GameProcessAffinity > 0)
                {// use our specified CPU affinity bitmask
                    gameProc.ProcessorAffinity = (IntPtr)setHnd.GameProcessAffinity;
                    Program.Logger("OSOL", String.Format("Setting game process CPU affinity to: {0}", BitmaskExtensions.AffinityToCoreString(setHnd.GameProcessAffinity)));
                }
                if (!Program.StringEquals(_procPrio, "Normal"))
                {// we have a custom process priority so let's use it
                    gameProc.PriorityClass = setHnd.GameProcessPriority;
                    Program.Logger("OSOL", String.Format("Setting game process priority to: {0}", setHnd.GameProcessPriority.ToString()));
                }

                if (setHnd.TerminateOSOLUponLaunch)
                {// since we've done all that's been asked we can quit out if requested
                    Program.Logger("OSOL", "User requested self-termination after game launch, exiting now...");
                    Environment.Exit(0);
                }
                else
                {
                    while (Program.IsRunningPID(gamePID))
                    {// spin while game is running
                        Thread.Sleep(1000);
                    }
                }

                Program.Logger("OSOL", String.Format("The {0} exited, moving on to clean up...", _launchType));
            }
            else
                Program.Logger("WARNING", String.Format("Could not find a {0} process by name: {1}", _launchType, Program.StringEquals("monitor", _launchType) ? gameName : monitorName));
            #endregion

            /*
             * Post-Game Cleanup
             */
            #region PostGame
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
            #endregion
        }
    }
}