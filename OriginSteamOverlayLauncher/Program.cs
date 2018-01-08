using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;

namespace OriginSteamOverlayLauncher
{
    class Program
    {
        #region Imports
        // for custom modal support
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string msg, string caption, int type);

        // for BringToFront() support
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        public const int SW_SHOWDEFAULT = 10;
        public const int SW_MINIMIZE = 2;
        public const int SW_SHOW = 5;

        public static string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        public static string appName = Path.GetFileNameWithoutExtension(codeBase);
        #endregion

        [STAThread]
        static void Main(string[] args)
        {
            // get our current mutex id based off our AssemblyInfo.cs
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
            string mutexId = string.Format("Global\\{{{0}}}", appGuid);

            // simple global mutex, courtesy of: https://stackoverflow.com/a/1213517
            using (var mutex = new Mutex(false, mutexId))
            {
                try
                {
                    try
                    {
                        if (!mutex.WaitOne(TimeSpan.FromSeconds(1), false))
                            Environment.Exit(0);
                    }
                    catch (AbandonedMutexException)
                    {
                        Logger("MUTEX", "Mutex is held by another instance, but seems abandoned!");
                        Environment.Exit(0);
                    }

                    /*
                     * Run our actual entry point here...
                     */

                    if (CliArgExists(args, "help"))
                    {// display an INI settings overview if run with /help
                        DisplayHelpDialog();
                    }
                    else
                    {
                        Settings curSet = new Settings();
                        // path to our local config
                        IniFile iniFile = new IniFile(appName + ".ini");
                        // overwrite/create log upon startup
                        File.WriteAllText(appName + "_Log.txt", String.Empty);
                        Logger("NOTE", "OSOL is running as: " + appName);

                        if (Settings.CheckINI(iniFile)
                            && Settings.ValidateINI(curSet, iniFile, iniFile.Path))
                        {
                            ProcessLauncher(curSet); // normal functionality
                        }
                        else
                        {// ini doesn't match our comparison, recreate from stubs
                            Logger("WARNING", "Config file partially invalid or doesn't exist, re-stubbing...");
                            Settings.CreateINI(curSet, iniFile);
                            Settings.ValidateINI(curSet, iniFile, iniFile.Path);
                            Settings.PathChooser(curSet, iniFile);
                        }
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                    Environment.Exit(0);
                }
            }
        }

        #region ProcessHelpers
        private static bool CliArgExists(string[] args, string ArgName)
        {// courtesy of: https://stackoverflow.com/a/30569947
            var singleFound = args.Where(w => w.ToLower() == "/" + ArgName.ToLower()).FirstOrDefault();
            if (singleFound != null)
                return ArgName.Equals(ArgName.ToLower());
            else
                return false;
        }

        public static void Logger(String cause, String message)
        {
            using (StreamWriter stream = File.AppendText(appName + "_Log.txt"))
            {
                stream.Write("[{0}] [{1}] {2}\r\n", DateTime.Now.ToUniversalTime(), cause, message);
            }
        }
        
        private static void BringToFront(IntPtr wHnd)
        {// force the window handle owner to restore and activate to focus
            ShowWindowAsync(wHnd, SW_SHOWDEFAULT);
            ShowWindowAsync(wHnd, SW_SHOW);
            SetForegroundWindow(wHnd);
        }

        private static void MinimizeWindow(IntPtr wHnd)
        {// force the window handle to minimize
            ShowWindowAsync(wHnd, SW_MINIMIZE);
        }

        private static bool IsRunning(String name) { return Process.GetProcessesByName(name).Any(); }

        private static bool IsRunningPID(Int64 pid) { return Process.GetProcesses().Any(x => x.Id == pid); }

        private static int IsRunningValidPID(Process[] procTree)
        {// return the first PID with a non-null window handle
            foreach (Process proc in procTree)
            {
                if (proc.Id > 0 && !proc.HasExited && proc.MainWindowHandle != IntPtr.Zero)
                {
                    return proc.Id;
                }
            }

            return 0;
        }

        private static Process[] GetProcessTreeByName(String procName)
        {
            return Process.GetProcessesByName(procName);
        }

        private static int GetRunningPIDByName(String procName)
        {
            Process tmpProc = Process.GetProcessesByName(procName).FirstOrDefault();
            if (tmpProc != null)
                return tmpProc.Id;
            else
                return 0;
        }

        private static Process RebindProcessByID(int PID)
        {
            return Process.GetProcessById(PID);
        }

        private static void KillProcTreeByName(String procName)
        {
            Process[] foundProcs = Process.GetProcessesByName(procName);
            foreach (Process proc in foundProcs)
            {
                proc.Kill();
            }
        }

        private static void ExecuteExternalElevated(String filePath, String fileArgs)
        {// generic process delegate for executing pre-launcher/post-game
            try
            {
                Process execProc = new Process();

                // sanity check our future process path first
                if (Settings.ValidatePath(filePath))
                {
                    execProc.StartInfo.UseShellExecute = true;
                    execProc.StartInfo.FileName = filePath;
                    execProc.StartInfo.Arguments = fileArgs;
                    execProc.StartInfo.Verb = "runas"; // ask the user for contextual UAC privs in case they need elevation
                    Logger("OSOL", "Attempting to run external process: " + filePath + " " + fileArgs);
                    execProc.Start();
                    execProc.WaitForExit(); // idle waiting for outside process to return
                    Logger("OSOL", "External process delegate returned, continuing...");
                }
                else if (filePath != null && filePath.Length > 0)
                {
                    Logger("WARNING", "External process path is invalid: " + filePath + " " + fileArgs);
                }
            }
            catch (Exception e)
            {
                Logger("WARNING", "Process delegate failed on [" + filePath + " " + fileArgs + "], due to: " + e.ToString());
            }
        }

        private static void DisplayHelpDialog()
        {
            Form helpForm = new HelpForm();
            helpForm.ShowDialog();

            if (helpForm.DialogResult == DialogResult.OK || helpForm.DialogResult == DialogResult.Cancel)
            {
                Process.GetCurrentProcess().Kill(); // exit the assembly after the modal
            }
        }
        #endregion

        private static void ProcessLauncher(Settings setHnd)
        {
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
            String launcherMode = setHnd.LauncherMode;
            Process launcherProc = new Process();
            Process gameProc = new Process();

            // save our monitoring path for later
            String monitorPath = Settings.ValidatePath(setHnd.MonitorPath) ? setHnd.MonitorPath : String.Empty;
            String monitorName = Path.GetFileNameWithoutExtension(monitorPath);
            // internal counters to sync with timeout
            int l_sanity_counter = 0;
            int g_sanity_counter = 0;
            // track PIDs that we find
            int launcherPID = 0;
            int gamePID = 0;

            /*
             * Launcher Detection
             */
            
            // obey the user and avoid killing and relaunching the target launcher
            if (IsRunning(launcherName) && setHnd.ReLaunch)
            {// if the launcher is running before the game kill it so we can run it through Steam
                Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                KillProcTreeByName(launcherName);
                Thread.Sleep(3000); // pause a moment for the launcher to close
            }

            if (Settings.ValidatePath(setHnd.LauncherPath))
            {
                // ask a non-async delegate to run a process before the launcher
                ExecuteExternalElevated(setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

                launcherProc.StartInfo.UseShellExecute = true;
                launcherProc.StartInfo.FileName = setHnd.LauncherPath;
                launcherProc.StartInfo.WorkingDirectory = Directory.GetParent(setHnd.LauncherPath).ToString();
                launcherProc.StartInfo.Arguments = setHnd.LauncherArgs;

                Logger("OSOL", "Attempting to start the launcher: " + setHnd.LauncherPath);
                launcherProc.Start();

                while (l_sanity_counter <= setHnd.ProcessAcquisitionTimeout)
                {// actively attempt to acquire launcher PID by snapshotting processes every second
                    var _procTree = GetProcessTreeByName(launcherName);
                    var _validPID = IsRunningValidPID(_procTree);

                    if (l_sanity_counter == setHnd.ProcessAcquisitionTimeout)
                    {// bail if we hit our timeout
                        Logger("FATAL", "Could not detect the launcher process after waiting " + setHnd.ProcessAcquisitionTimeout + " seconds!");
                        Process.GetCurrentProcess().Kill();
                    }
                    
                    if (_validPID > 0)
                    {// use our hwnd checker to locate a valid PID
                        launcherPID = _validPID;
                        launcherProc = RebindProcessByID(launcherPID);
                        break;
                    }
                    else if (IsRunning(launcherName))
                    {// initial check for valid hwnd failed, fall back to searching by name
                        launcherPID = GetRunningPIDByName(launcherName);
                        launcherProc = RebindProcessByID(launcherPID);
                        Logger("OSOL", "Failed to find launcher PID by window handle, attempting to detect by name...");
                        break;
                    }

                    l_sanity_counter++;
                    Thread.Sleep(1000);
                }

                if (launcherPID > 0)
                    Logger("OSOL", "Detected the launcher window at PID: " + launcherPID);
                else
                {
                    Logger("WARNING", "Cannot find the PID of the launcher by either window handle or name!");
                }

                // force the launcher window to activate before the game to avoid BPM hooking issues
                Thread.Sleep(setHnd.PreGameOverlayWaitTime * 1000); // wait for the BPM overlay notification
                BringToFront(launcherProc.MainWindowHandle);

                // if the user requests it minimize our launcher after detecting it
                if (setHnd.MinimizeLauncher)
                    MinimizeWindow(launcherProc.MainWindowHandle);
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

                Logger("OSOL", "Launching game, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);
                gameProc.Start();
                Thread.Sleep(setHnd.ProxyTimeout); // wait for the proxy to close
            }
            else if (Settings.StringEquals(launcherMode, "URI"))
            {
                // make sure we run our pre-launcher event even in URI mode
                ExecuteExternalElevated(setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.LauncherURI;
                
                Thread.Sleep(setHnd.PreGameLauncherWaitTime * 1000); // wait to hook some sluggish launchers
                try
                {// we can't control what will happen so try to catch exceptions
                    Logger("OSOL", "Launching URI: " + setHnd.LauncherURI);
                    gameProc.Start();
                }
                catch (Exception x)
                {// catch any exceptions and dump to log
                    Logger("OSOL", "Failed to launch URI [" + setHnd.LauncherURI + "] double check your launcher installation");
                    Logger("EXCEPTION", x.ToString());
                }
            }
            else
                Logger("OSOL", "Searching for the game process...");

            while (g_sanity_counter <= setHnd.ProcessAcquisitionTimeout && setHnd.LauncherPath != String.Empty)
            {// actively attempt to reacquire process
                if (g_sanity_counter == setHnd.ProcessAcquisitionTimeout)
                {
                    Logger("FATAL", "Could not detect the game process after waiting " + setHnd.ProcessAcquisitionTimeout + " seconds, exiting!");
                    Process.GetCurrentProcess().Kill();
                }

                // use a monitor executable for tracking if the user requests it, otherwise use the game executable specified
                var _procTree = monitorPath.Length > 0 ? GetProcessTreeByName(monitorName) : GetProcessTreeByName(gameName);
                int _validPID = IsRunningValidPID(_procTree);

                if (_validPID > 0)
                {// assume our game works similar to our launcher, locate by hwnd first
                    gamePID = _validPID;
                    gameProc = RebindProcessByID(gamePID);
                    break;
                }
                else if (IsRunning(monitorName) || IsRunning(gameName))
                {// fall back to searching for running PID by name if hwnd fails
                    gamePID = monitorPath.Length > 0 ? GetRunningPIDByName(monitorName) : GetRunningPIDByName(gameName);
                    gameProc = RebindProcessByID(gamePID);
                    Logger("OSOL", "Failed to find the" + (monitorPath.Length > 0 ? " monitor " : " game ") + "PID by window handle, attempting to detect by name...");
                    break;
                }

                g_sanity_counter++;
                Thread.Sleep(1000);
            }

            if (gamePID > 0)
                Logger("OSOL", "Detected the" + (monitorPath.Length > 0 ? " monitor " : " game ") +" at PID: " + gameProc.Id);
            else
            {
                Logger("FATAL", "Cannot find the PID of the" + (monitorPath.Length > 0 ? " monitor " : " game ") + "by either window handle or name, exiting!");
                Process.GetCurrentProcess().Kill();
            }

            while (IsRunningPID(gamePID))
            {// sleep while the game/monitor is running
                Thread.Sleep(1000);
            }

            Logger("OSOL", "Game exited, cleaning up...");

            /*
                * Post-Game Cleanup
                */
            if (setHnd.LauncherPath != String.Empty && IsRunningPID(launcherProc.Id) && !setHnd.DoNotClose)
            {// found the launcher left after the game exited
                Thread.Sleep(setHnd.PostGameWaitTime * 1000); // let Origin sync with the cloud
                KillProcTreeByName(launcherName);
            }

            // ask a non-async delegate to run a process after the game and launcher exit
            ExecuteExternalElevated(setHnd.PostGameExec, setHnd.PostGameExecArgs);
        }
    }
}
