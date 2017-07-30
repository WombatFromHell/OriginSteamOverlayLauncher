using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OriginSteamOverlayLauncher
{
    class Settings
    {// externalize our config variables for encapsulation
        public String LauncherPath { get; set; }
        public String LauncherURI { get; set; }
        public String GamePath { get; set; }
        public String GameArgs { get; set; }

        // options
        public String LauncherMode { get; set; }
        public String PreLaunchExec { get; set; }
        public String PreLaunchExecArgs { get; set; }
        public String PostGameExec { get; set; }
        public String PostGameExecArgs { get; set; }

        public int PostGameWaitTime { get; set; }

        public String AssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
    }

    class Program
    {
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string msg, string caption, int type);

        // for BringToFront() support
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        public const int SW_SHOWDEFAULT = 10;
        public const int SW_SHOW = 5;

        [STAThread]
        static void Main(string[] args)
        {
            Settings curSet = new Settings();
            // path to our local config
            String iniFilePath = "OriginSteamOverlayLauncher.ini";
            IniFile iniFile = new IniFile(iniFilePath);
            // overwrite/create log upon startup
            File.WriteAllText("OriginSteamOverlayLauncher_Log.txt", String.Empty);
            

            if (!ValidateINI(curSet, iniFile, iniFilePath))
            {
                Logger("WARNING", "Config file invalid or doesn't exist, creating it...");
                PathChooser(curSet, iniFile);

                if (ValidateINI(curSet, iniFile, iniFilePath))
                {
                    ProcessLauncher(curSet);
                }
                else
                    Logger("FATAL", "INI file invalid or file paths are bad, exiting!");
            }
            else
                ProcessLauncher(curSet);
        }

        private static void BringToFront(IntPtr wHnd)
        {// force the window handle owner to restore and activate to focus
            ShowWindowAsync(wHnd, SW_SHOWDEFAULT);
            ShowWindowAsync(wHnd, SW_SHOW);
            SetForegroundWindow(wHnd);
        }

        private static bool ValidatePath(String path)
        {// run a sanity check to see if the input is a valid path
            try
            {
                if (File.Exists(path))
                    return true;
            }
            catch (Exception e)
            {
                Logger("WARNING", "Path validator failed on: [" + path + "], because: " + e.ToString());
                return false;
            }

            return false;
        }

        private static bool StringEquals(String input, String comparator)
        {// support function for checking string equality using Ordinal comparison
            if (input != String.Empty && String.Equals(input, comparator, StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
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

        private static void Logger(String cause, String message)
        {
            using (StreamWriter stream = File.AppendText("OriginSteamOverlayLauncher_Log.txt"))
            {
                stream.Write("[{0}] [{1}] {2}\r\n", DateTime.Now.ToUniversalTime(), cause, message);
            }
        }

        private static bool ValidateINI(Settings setHnd, IniFile iniHnd, String iniFilePath)
        {// INI Support, courtesy of: https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
            bool stubbedSetting = false; // flag the user later if necessary

            if (!iniHnd.KeyPopulated("LauncherPath", "Paths"))
            {// just a quick check to see if the file is relatively sane
                return false;
            }
            
            if (iniHnd.KeyPopulated("LauncherPath", "Paths"))
                setHnd.LauncherPath = iniHnd.Read("LauncherPath", "Paths");

            if (iniHnd.KeyPopulated("LauncherURI", "Paths"))
                setHnd.LauncherURI = iniHnd.Read("LauncherURI", "Paths");
            else
                iniHnd.Write("LauncherURI", String.Empty, "Paths");

            if (iniHnd.KeyPopulated("GamePath", "Paths"))
                setHnd.GamePath = iniHnd.Read("GamePath", "Paths");
            if (iniHnd.KeyPopulated("GamePath", "Paths"))
                setHnd.GameArgs = iniHnd.Read("GameArgs", "Paths");

            // check options
            if (iniHnd.KeyPopulated("LauncherMode", "Options")
                && StringEquals(iniHnd.Read("LauncherMode", "Options"), "Normal")
                || StringEquals(iniHnd.Read("LauncherMode", "Options"), "URI")
                || StringEquals(iniHnd.Read("LauncherMode", "Options"), "LauncherOnly"))
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
                setHnd.LauncherMode = iniHnd.Read("LauncherMode", "Options");
            }
            else
            {// autocorrect for the user
                iniHnd.Write("LauncherMode", "Normal", "Options");
                setHnd.LauncherMode = "Normal";
            }

            // pre-launcher/post-game script support
            if (iniHnd.KeyPopulated("PreLaunchExec", "Options"))
                setHnd.PreLaunchExec = iniHnd.Read("PreLaunchExec", "Options");
            else if (!iniHnd.KeyExists("PreLaunchExec"))
            {
                iniHnd.Write("PreLaunchExec", String.Empty, "Options");
                stubbedSetting = true;
            }

            if (iniHnd.KeyPopulated("PreLaunchExecArgs", "Options"))
                setHnd.PreLaunchExecArgs = iniHnd.Read("PreLaunchExecArgs", "Options");
            else if (!iniHnd.KeyExists("PreLaunchExecArgs"))
                iniHnd.Write("PreLaunchExecArgs", String.Empty, "Options");

            if (iniHnd.KeyPopulated("PostGameExec", "Options"))
                setHnd.PostGameExec = iniHnd.Read("PostGameExec", "Options");
            else if (!iniHnd.KeyExists("PostGameExec"))
            {
                iniHnd.Write("PostGameExec", String.Empty, "Options");
                stubbedSetting = true;
            }
            
            if (iniHnd.KeyPopulated("PostGameExecArgs", "Options"))
                setHnd.PostGameExecArgs = iniHnd.Read("PostGameExecArgs", "Options");
            else if (!iniHnd.KeyExists("PostGameExecArgs"))
                iniHnd.Write("PostGameExecArgs", String.Empty, "Options");

            if (iniHnd.KeyPopulated("PostGameWaitTime", "Options"))
            {// sanity check our string parse for int
                Int32.TryParse(iniHnd.Read("PostGameWaitTime", "Options"), out int _waitTime);
                if (_waitTime > 0)
                    setHnd.PostGameWaitTime = _waitTime;
                else
                {
                    setHnd.PostGameWaitTime = 7; // set to our default, 7s
                    iniHnd.Write("PostGameWaitTime", setHnd.PostGameWaitTime.ToString(), "Options");
                }
            }
            else if (!iniHnd.KeyExists("PostGameWaitTime"))
            {
                setHnd.PostGameWaitTime = 7; // sensible default
                iniHnd.Write("PostGameWaitTime", setHnd.PostGameWaitTime.ToString(), "Options");
                stubbedSetting = true;
            }

            if (stubbedSetting)
            {
                Logger("OSOL", "Created new settings stubs in the INI file, telling the user to restart...");
                MessageBox(IntPtr.Zero, "New settings have been added to the INI file since we last ran.\r\nOSOL should be restarted for normal behavior, exiting...", "Alert", (int)0x00001000L);
                Process.GetCurrentProcess().Kill(); // bail early
            }

            if (ValidatePath(setHnd.LauncherPath)
                && ValidatePath(setHnd.GamePath))
                return true; // should be able to use it

            return false;
        }

        private static void PathChooser(Settings setHnd, IniFile iniHnd)
        {
            /*
             * Ask for the Game path
             */

            bool iniExists = ValidatePath(iniHnd.Path);

            OpenFileDialog file = new OpenFileDialog();
            file.Title = "Choose the path of your game executable";
            file.Filter = "EXE Files|*.exe|All Files|*.*";
            file.InitialDirectory = Path.GetDirectoryName(setHnd.AssemblyPath);

            if (file.ShowDialog() == DialogResult.OK
                && ValidatePath(file.FileName))
            {
                setHnd.GamePath = file.FileName;
                iniHnd.Write("GamePath", setHnd.GamePath, "Paths");
                iniHnd.Write("GameArgs", String.Empty, "Paths");
            }


            /*
             * Ask for the Launcher path
             */
            file = new OpenFileDialog();
            file.Title = "Choose the path of the launcher executable";
            file.Filter = "EXE Files|*.exe|All Files|*.*";
            file.InitialDirectory = Path.GetDirectoryName(setHnd.AssemblyPath);

            if (file.ShowDialog() == DialogResult.OK
                && ValidatePath(file.FileName))
            {
                setHnd.LauncherPath = file.FileName;
                iniHnd.Write("LauncherPath", setHnd.LauncherPath, "Paths");
                iniHnd.Write("LauncherURI", String.Empty, "Paths");
                iniHnd.Write("LauncherMode", "Normal", "Options");
            }

            if (iniHnd.Read("LauncherPath", "Paths") == String.Empty
                || iniHnd.Read("GamePath", "Paths") == String.Empty)
            {// sanity check in case of cancelling the path inputs
                Logger("FATAL", "The user cancelled the path chooser, we need valid paths so we're bailing!");
                Process.GetCurrentProcess().Kill(); // bail!
            }

            // since we started fresh, we need to tell the user to restart
            if (!iniExists)
            {
                // add in optional settings stubs for the user when starting fresh
                iniHnd.Write("PreLaunchExec", String.Empty, "Options");
                iniHnd.Write("PreLaunchExecArgs", String.Empty, "Options");
                iniHnd.Write("PostGameExec", String.Empty, "Options");
                iniHnd.Write("PostGameExecArgs", String.Empty, "Options");
                iniHnd.Write("PostGameWaitTime", "7", "Options");

                Logger("OSOL", "Created the INI file from the path chooser, telling the user to restart...");
                MessageBox(IntPtr.Zero, "INI file didn't exist, so we're creating it.\r\nOSOL should be restarted for normal behavior, exiting...", "Alert", (int)0x00001000L);
                Process.GetCurrentProcess().Kill();
            }
        }

        private static void ExecuteExternalElevated(String filePath, String fileArgs)
        {// generic process delegate for executing pre-launcher/post-game
            try
            {
                Process execProc = new Process();

                // sanity check our future process path first
                if (ValidatePath(filePath))
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

        private static void ProcessLauncher(Settings setHnd)
        {
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
            String launcherMode = setHnd.LauncherMode;
            Process launcherProc = new Process();
            Process gameProc = new Process();

            /*
             * Launcher Detection
             */

            if (IsRunning(launcherName))
            {// if the launcher is running before the game kill it so we can run it through Steam
                Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                KillProcTreeByName(launcherName);
                Thread.Sleep(3000); // pause a moment for the launcher to close
            }

            // ask a non-async delegate to run a process before the launcher
            ExecuteExternalElevated(setHnd.PreLaunchExec, setHnd.PreLaunchExecArgs);

            launcherProc.StartInfo.UseShellExecute = true;
            launcherProc.StartInfo.FileName = setHnd.LauncherPath;
            Logger("OSOL", "Attempting to start the launcher, cmd: " + setHnd.LauncherPath);
            launcherProc.Start();

            int sanity_counter = 0;
            int launcherPID = 0;
            while (sanity_counter <= 120)
            {// wait up to 2 mins. for the launcher process
                if (sanity_counter == 120)
                {
                    Logger("FATAL", "Could not detect the launcher process after waiting 2 mins, exiting!");
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
                
                sanity_counter++;
                Thread.Sleep(1000);
            }

            if (launcherProc.MainWindowTitle.Length > 0)
            {
                Logger("OSOL", "Detected the launcher process window at PID [" + launcherProc.Id + "] in " + sanity_counter + " sec.");
            }
            else
            {
                Logger("FATAL", "Cannot find main window handle of launcher process at PID [" + launcherProc.Id + "], perhaps the wrong launcher exe?");
                return;
            }

            // force the launcher window to activate before the game to avoid BPM hooking issues
            Thread.Sleep(5000); // wait for the BPM overlay notification
            BringToFront(launcherProc.MainWindowHandle);

            /*
             * Game Post-Proxy Detection
             */

            if (StringEquals(launcherMode, "Normal"))
            {// only run game ourselves if the user asks
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.GamePath;
                gameProc.StartInfo.Arguments = setHnd.GameArgs;
                Logger("OSOL", "Launching game, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);
                gameProc.Start();
                Thread.Sleep(5000); // wait for the proxy to close
            }
            else if (StringEquals(launcherMode, "URI"))
            {
                gameProc.StartInfo.UseShellExecute = true;
                gameProc.StartInfo.FileName = setHnd.LauncherURI;
                Thread.Sleep(7000); // longer wait (~12s) for hooking some launchers
                try
                {// we can't control what will happen so try to catch exceptions
                    Logger("OSOL", "Launching URI: " + setHnd.LauncherURI);
                    gameProc.Start();
                }
                catch (Exception x)
                {// catch any exceptions and dump to log
                    Logger("OSOL", "Failed to launch URI [" + setHnd.LauncherURI + "] double check your launcher installation");
                    Logger("OSOL", "Exception dump follows: ");
                    Logger("EXCEPT", x.ToString());
                }
            }
            else
                Logger("OSOL", "Searching for the game process, waiting up to 5 minutes...");
            

            sanity_counter = 0;
            int gamePID = 0;
            while (sanity_counter <= 300)
            {// actively attempt to reacquire process, wait up to 5 mins
                if (sanity_counter == 300)
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

                sanity_counter++;
                Thread.Sleep(1000);
            }

            if (gameProc.Id != 0)
                Logger("OSOL", "Detected the game process at PID [" + gameProc.Id + "] in " + sanity_counter + " sec.");
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
            if (IsRunningPID(launcherProc.Id))
            {// found the launcher left after the game exited
                Thread.Sleep(setHnd.PostGameWaitTime * 1000); // let Origin sync with the cloud
                Logger("OSOL", "Game exited, killing launcher instance and cleaning up...");
                KillProcTreeByName(launcherName);

                // ask a non-async delegate to run a process after the game and launcher exit
                ExecuteExternalElevated(setHnd.PostGameExec, setHnd.PostGameExecArgs);
            }
        }
    }
}
