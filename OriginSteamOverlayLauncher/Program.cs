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
    class Settings
    {// externalize our config variables for encapsulation
        #region SettingStubs
        // strings for execution paths and arguments
        public String LauncherPath { get; set; }
        public String LauncherArgs { get; set; }
        public String LauncherURI { get; set; }
        public String GamePath { get; set; }
        public String GameArgs { get; set; }

        // options
        public String LauncherMode { get; set; }
        public String PreLaunchExec { get; set; }
        public String PreLaunchExecArgs { get; set; }
        public String PostGameExec { get; set; }
        public String PostGameExecArgs { get; set; }

        // bools for OSOL behavior
        public Boolean ReLaunch { get; set; }
        public Boolean DoNotClose { get; set; }

        // ints for exposing internal timings
        public int PreGameOverlayWaitTime { get; set; }
        public int PreGameLauncherWaitTime { get; set; }
        public int PostGameWaitTime { get; set; }
        public int ProxyTimeout { get; set; }
        public int ProcessAcquisitionTimeout { get; set; }
        #endregion
        
        #region Helpers
        public String AssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;

        public static bool StringEquals(String input, String comparator)
        {// support function for checking string equality using Ordinal comparison
            if (input != String.Empty && String.Equals(input, comparator, StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
        }

        public static void PathChooser(Settings setHnd, IniFile iniHnd)
        {
            /*
             * Ask for the Game path
             */

            if (!ValidatePath(setHnd.GamePath))
            {// only re-ask for path if current one is invalid
                OpenFileDialog file = new OpenFileDialog()
                {
                    Title = "Choose the path of your game executable",
                    Filter = "EXE Files|*.exe|All Files|*.*",
                    InitialDirectory = Path.GetDirectoryName(setHnd.AssemblyPath)
                };

                if (file.ShowDialog() == DialogResult.OK
                    && ValidatePath(file.FileName))
                {
                    setHnd.GamePath = file.FileName;
                    iniHnd.Write("GamePath", setHnd.GamePath, "Paths");
                    iniHnd.Write("GameArgs", String.Empty, "Paths");
                }// don't do anything if we cancel out
            }

            /*
             * Ask for the Launcher path
             */
            if (!ValidatePath(setHnd.LauncherPath))
            {
                OpenFileDialog file = new OpenFileDialog()
                {
                    Title = "Choose the path of your launcher executable",
                    Filter = "EXE Files|*.exe|All Files|*.*",
                    InitialDirectory = Path.GetDirectoryName(setHnd.AssemblyPath)
                };

                if (file.ShowDialog() == DialogResult.OK
                    && ValidatePath(file.FileName))
                {
                    setHnd.LauncherPath = file.FileName;
                    iniHnd.Write("LauncherPath", setHnd.LauncherPath, "Paths");
                    iniHnd.Write("LauncherArgs", String.Empty, "Paths");
                    iniHnd.Write("LauncherURI", String.Empty, "Paths");
                    iniHnd.Write("LauncherMode", "Normal", "Options");
                }
            }

            if (!ValidatePath(setHnd.LauncherPath) && !ValidatePath(setHnd.GamePath))
            {// sanity check in case of cancelling both path inputs
                Program.Logger("FATAL", "The user didn't select valid paths, bailing!");
                Process.GetCurrentProcess().Kill(); // bail!
            }

            Program.MessageBox(IntPtr.Zero, "INI updated, OSOL should be restarted for normal behavior, exiting...", "Notice", (int)0x00001000L);
            Process.GetCurrentProcess().Kill();
        }

        public static bool CreateINI(Settings setHnd, IniFile iniHnd)
        {// reusable initializer for recreating fresh INI
            if (!ValidatePath(iniHnd.Path))
            {// either our ini is invalid or doesn't exist
                File.WriteAllText(iniHnd.Path, String.Empty); // overwrite ini

                // paths
                iniHnd.Write("LauncherPath", String.Empty, "Paths");
                iniHnd.Write("LauncherArgs", String.Empty, "Paths");
                iniHnd.Write("LauncherURI", String.Empty, "Paths");
                iniHnd.Write("GamePath", String.Empty, "Paths");
                iniHnd.Write("GameArgs", String.Empty, "Paths");

                // options
                iniHnd.Write("PreLaunchExec", String.Empty, "Options");
                iniHnd.Write("PreLaunchExecArgs", String.Empty, "Options");
                iniHnd.Write("PostGameExec", String.Empty, "Options");
                iniHnd.Write("PostGameExecArgs", String.Empty, "Options");

                // integer options (sensible defaults)
                iniHnd.Write("PreGameOverlayWaitTime", "5", "Options"); //5s
                iniHnd.Write("PreGameLauncherWaitTime", "12", "Options"); //12s
                iniHnd.Write("PostGameWaitTime", "7", "Options"); //7s
                iniHnd.Write("ProxyTimeout", "5", "Options"); //5s
                iniHnd.Write("ProcessAcquisitionTimeout", "300", "Options"); //5mins

                // options as parsed strings
                //Kill and relaunch detected launcher PID before game
                iniHnd.Write("ReLaunch", "True", "Options");
                // Do not kill detected launcher PID after game exits
                iniHnd.Write("DoNotClose", "False", "Options");

                Program.Logger("OSOL", "Created the INI file from stubs after we couldn't find it...");
                return false;
            }
            else
                return true;
        }

        public static bool CheckINI(IniFile iniHnd)
        {// return false if ini doesn't match our accessor list
            if (ValidatePath(iniHnd.Path))
            {// skip this if our ini doesn't exist
                if (iniHnd.KeyExists("LauncherPath") && iniHnd.KeyExists("LauncherArgs")
                    && iniHnd.KeyExists("LauncherURI") && iniHnd.KeyExists("GamePath")
                    && iniHnd.KeyExists("GameArgs") && iniHnd.KeyExists("LauncherMode")
                    && iniHnd.KeyExists("PreLaunchExec") && iniHnd.KeyExists("PreLaunchExecArgs")
                    && iniHnd.KeyExists("PostGameExec") && iniHnd.KeyExists("PostGameExecArgs")
                    && iniHnd.KeyExists("PreGameOverlayWaitTime") && iniHnd.KeyExists("PreGameLauncherWaitTime")
                    && iniHnd.KeyExists("PostGameWaitTime") && iniHnd.KeyExists("ProcessAcquisitionTimeout")
                    && iniHnd.KeyExists("ProxyTimeout") && iniHnd.KeyExists("ReLaunch") && iniHnd.KeyExists("DoNotClose"))
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public static Boolean ValidatePath(String path)
        {// run a sanity check to see if the input is a valid path
            try
            {
                if (path != String.Empty && File.Exists(path))
                    return true;
            }
            catch (Exception e)
            {
                Program.Logger("WARNING", "Path validator failed on: [" + path + "], because: " + e.ToString());
                return false;
            }

            return false;
        }

        public static String ValidateString(IniFile iniHnd, String writeKey, String setKey, String subKey, String keyName)
        {// reusable key validator for ini strings
            /*
             * Takes:
             *     A ref to the IniFile: iniHnd
             *     A string to use as a default: writeKey
             *     A ref to the string to set in the INI: setKey
             *     A string key to modify in the INI: subKey
             *     A string master-key to modify in the INI: keyName
             */
            if (iniHnd.KeyPopulated(subKey, keyName))
            {// return empty if empty, or contents if valid
                setKey = iniHnd.ReadString(subKey, keyName);
                return setKey.Length > 0 ? setKey : String.Empty;
            }
            else if (!iniHnd.KeyExists(subKey))
            {// edge case where the contents change before program closes
                iniHnd.Write(subKey, writeKey, keyName);
                return String.Empty;
            }
            else
                return String.Empty;
        }

        public static Int32 ValidateInt(IniFile iniHnd, Int32 writeKey, Int32 setKey, String subKey, String keyName)
        {// reusable key validator for ini ints
            /*
             * Takes:
             *     A ref to the IniFile: iniHnd
             *     An int to use as the default: writeKey
             *     A ref to the int to set in the INI: setKey
             *     A string key to modify in the INI: subKey
             *     A string master-key to modify in the INI: keyName
             */
            if (iniHnd.KeyPopulated(subKey, keyName))
            {
                Int32.TryParse(iniHnd.ReadString(subKey, keyName), out int _output);
                return _output > 0 ? _output : -1; // must always be greater than 0s
            }
            else if (!iniHnd.KeyExists(subKey))
            {// edge case
                iniHnd.Write(subKey, writeKey.ToString(), keyName);
                return writeKey;
            }
            else
                return -1;
        }

        public static Boolean ValidateBool(IniFile iniHnd, Boolean writeKey, Boolean setKey, String subKey, String keyName)
        {// reusable key validator for ini bools
            /*
             * Takes:
             *     A ref to the IniFile: iniHnd
             *     A bool to use as a default: writeKey
             *     A ref to the bool to set in the INI: setKey
             *     A string key to modify in the INI: subKey
             *     A string master-key to modify in the INI: keyName
             */
            if (iniHnd.KeyPopulated(subKey, keyName))
            {// return empty if empty, or contents if valid
                return iniHnd.ReadBool(subKey, keyName);
            }
            else if (!iniHnd.KeyExists(subKey))
            {// edge case where the contents change before program closes
                iniHnd.Write(subKey, writeKey.ToString(), keyName);
                return false;
            }
            else
                return false;
        }

        public static bool ValidateINI(Settings setHnd, IniFile iniHnd, String iniFilePath)
        {// validate while reading from ini - filling in defaults where sensible
            setHnd.LauncherPath = ValidateString(iniHnd, String.Empty, "LauncherPath", "LauncherPath", "Paths");
            setHnd.LauncherArgs = ValidateString(iniHnd, String.Empty, "LauncherArgs", "LauncherArgs", "Paths");

            setHnd.GamePath = ValidateString(iniHnd, String.Empty, "GamePath", "GamePath", "Paths");

            // special case - check launchermode options
            if (iniHnd.KeyPopulated("LauncherMode", "Options")
                && Settings.StringEquals(iniHnd.ReadString("LauncherMode", "Options"), "Normal")
                || Settings.StringEquals(iniHnd.ReadString("LauncherMode", "Options"), "URI")
                || Settings.StringEquals(iniHnd.ReadString("LauncherMode", "Options"), "LauncherOnly"))
            {
                /*
                 * "LauncherMode" can have three options:
                 *     "Normal": launches Origin, launches the game (using the options provided by the user),
                 *         waits for the game to close, then closes Origin.
                 *     "URI": launches the user specified launcher, executes the user specified launcher URI,
                 *         waits for the user specified game to start, then closes the launcher when the game 
                 *         exits.
                 *     "LauncherOnly": launches Origin, waits for the game to be executed by the user, waits
                 *         for the game to close, then closes Origin.
                 *         
                 *     Note: 'LauncherOnly' is intended to provide extra compatibility when some games don't
                 *     work properly with the BPM overlay. This is to work around a Steam regression involving
                 *     hooking Origin titles launched through the Origin2 launcher.
                 */
                setHnd.LauncherMode = iniHnd.ReadString("LauncherMode", "Options");
            }
            else
            {// autocorrect for the user
                iniHnd.Write("LauncherMode", "Normal", "Options");
                setHnd.LauncherMode = "Normal";
            }
            
            // pre-launcher/post-game script support
            setHnd.PreLaunchExec = ValidateString(iniHnd, String.Empty, setHnd.PreLaunchExec, "PreLaunchExec", "Options");
            setHnd.PreLaunchExecArgs = ValidateString(iniHnd, String.Empty, setHnd.PreLaunchExecArgs, "PreLaunchExecArgs", "Options");
            setHnd.PostGameExec = ValidateString(iniHnd, String.Empty, setHnd.PostGameExec, "PostGameExec", "Options");
            setHnd.PostGameExecArgs = ValidateString(iniHnd, String.Empty, setHnd.PostGameExecArgs, "PostGameExecArgs", "Options");

            // treat ints differently
            setHnd.ProxyTimeout = ValidateInt(iniHnd, 5, setHnd.ProxyTimeout, "ProxyTimeout", "Options"); // 5s default wait time
            setHnd.PreGameOverlayWaitTime = ValidateInt(iniHnd, 5, setHnd.PreGameOverlayWaitTime, "PreGameOverlayWaitTime", "Options"); // 5s default wait time
            setHnd.PreGameLauncherWaitTime = ValidateInt(iniHnd, 12, setHnd.PreGameLauncherWaitTime, "PreGameLauncherWaitTime", "Options"); // 12s default wait time
            setHnd.ProcessAcquisitionTimeout = ValidateInt(iniHnd, 300, setHnd.ProcessAcquisitionTimeout, "ProcessAcquisitionTimeout", "Options"); // 5mins default wait time
            setHnd.PostGameWaitTime = ValidateInt(iniHnd, 7, setHnd.PostGameWaitTime, "PostGameWaitTime", "Options"); // 7s default wait time

            // parse strings into bools
            // Default to closing the previously detected launcher PID
            setHnd.ReLaunch = ValidateBool(iniHnd, true, setHnd.ReLaunch, "ReLaunch", "Options");
            // Default to closing the detected launcher PID when a game exits
            setHnd.DoNotClose = ValidateBool(iniHnd, false, setHnd.DoNotClose, "DoNotClose", "Options");

            if (ValidatePath(setHnd.LauncherPath) || ValidatePath(setHnd.GamePath))
                return true; // only flag to continue if either main path works

            return false;
        }
        #endregion
    }

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

                    if (cliArgExists(args, "help"))
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
        private static bool cliArgExists(string[] args, string ArgName)
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
                {
                    if (l_sanity_counter == setHnd.ProcessAcquisitionTimeout)
                    {
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
