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

namespace OriginSteamOverlayLauncher
{
    class Program
    {
        #region Imports
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string msg, string caption, int type);

        // for BringToFront() support
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        public const int SW_SHOWDEFAULT = 10;
        public const int SW_SHOW = 5;
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
                        IniFile iniFile = new IniFile("OriginSteamOverlayLauncher.ini");
                        // overwrite/create log upon startup
                        File.WriteAllText("OriginSteamOverlayLauncher_Log.txt", String.Empty);

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
            using (StreamWriter stream = File.AppendText("OriginSteamOverlayLauncher_Log.txt"))
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

        private static bool IsRunning(String name) { return Process.GetProcessesByName(name).Any(); }

        private static bool IsRunningPID(Int64 pid) { return Process.GetProcesses().Any(x => x.Id == pid); }

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
                {// actively attempt to acquire launcher PID
                    if (l_sanity_counter == setHnd.ProcessAcquisitionTimeout)
                    {// bail if we hit our timeout
                        Logger("FATAL", "Could not detect the launcher process after waiting 5 mins, exiting!");
                        Process.GetCurrentProcess().Kill();
                    }

                    // only rebind process if we found something
                    if (GetRunningPIDByName(launcherName) != 0)
                    {
                        launcherPID = GetRunningPIDByName(launcherName);
                        launcherProc = RebindProcessByID(launcherPID);
                        if (launcherProc.MainWindowHandle != IntPtr.Zero
                            && launcherProc.MainWindowTitle.Length > 0)
                            break; // we probably found our real window
                    }

                    l_sanity_counter++;
                    Thread.Sleep(1000);
                }

                if (launcherProc.MainWindowTitle.Length > 0)
                {
                    Logger("OSOL", "Detected the launcher process window at PID [" + launcherProc.Id + "] in " + l_sanity_counter + " sec.");
                }
                else
                {
                    Logger("FATAL", "Cannot find main window handle of launcher process at PID [" + launcherProc.Id + "], perhaps the wrong launcher exe?");
                    return;
                }

                // force the launcher window to activate before the game to avoid BPM hooking issues
                Thread.Sleep(setHnd.PreGameOverlayWaitTime * 1000); // wait for the BPM overlay notification
                BringToFront(launcherProc.MainWindowHandle);
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
                    Logger("FATAL", "Timed out while looking for game process, exiting! Internet connection or launcher issue?");
                    Process.GetCurrentProcess().Kill();
                }

                if (GetRunningPIDByName(gameName) != 0)
                {// let's assume the game works similarly to our launcher (wrt proxies)
                    gamePID = GetRunningPIDByName(gameName);
                    gameProc = RebindProcessByID(gamePID);
                    if (gameProc.MainWindowHandle != IntPtr.Zero
                        && gameProc.MainWindowTitle.Length > 0)
                        break; // we probably found our real window
                }

                g_sanity_counter++;
                Thread.Sleep(1000);
            }

            if (gameProc.Id != 0)
                Logger("OSOL", "Detected the game process at PID [" + gameProc.Id + "] in " + g_sanity_counter + " sec.");
            else
            {
                Logger("FATAL", "Lost track of the game process somehow, this shouldn't happen! Internet connection or launcher issue?");
                Process.GetCurrentProcess().Kill();
            }

            while (IsRunning(gameName))
            {// sleep while game is running
                Thread.Sleep(1000);
            }

            /*
                * Post-Game Cleanup
                */
            if (setHnd.LauncherPath != String.Empty && IsRunningPID(launcherProc.Id) && !setHnd.DoNotClose)
            {// found the launcher left after the game exited
                Thread.Sleep(setHnd.PostGameWaitTime * 1000); // let Origin sync with the cloud
                Logger("OSOL", "Game exited, killing launcher instance and cleaning up...");
                KillProcTreeByName(launcherName);
            }
            else
            {// obey the user and don't kill the launcher if requested
                Logger("OSOL", "Game exited, cleaning up...");
            }

            // ask a non-async delegate to run a process after the game and launcher exit
            ExecuteExternalElevated(setHnd.PostGameExec, setHnd.PostGameExecArgs);
        }
    }
}
