using OriginSteamOverlayLauncher;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OriginSteamOverlayLauncher
{
    class Settings
    {// externalize our config variables for encapsulation
        public String LauncherPath { get; set; }
        public String GamePath { get; set; }
        public String GameArgs { get; set; }
    }

    class Program
    {
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
            
            if (!iniHnd.KeyExists("LauncherPath", "Paths"))
            {// just a quick check to see if the file is relatively sane
                return false;
            }
            
            if (iniHnd.Read("LauncherPath", "Paths") != String.Empty)
                setHnd.LauncherPath = iniHnd.Read("LauncherPath", "Paths");
            if (iniHnd.Read("LauncherPath", "Paths") != String.Empty)
                setHnd.GamePath = iniHnd.Read("GamePath", "Paths");
            if (iniHnd.Read("GameArgs", "Paths") != String.Empty)
                setHnd.GameArgs = iniHnd.Read("GameArgs", "Paths");

            if (File.Exists(setHnd.LauncherPath)
                && File.Exists(setHnd.GamePath))
                return true; // should be able to use it

            return false;
        }

        private static void PathChooser(Settings setHnd, IniFile iniHnd)
        {
            /*
             * Ask for the Game path
             */
            OpenFileDialog file = new OpenFileDialog()
            {
                Title = "Choose the path of your game executable",
                Filter = "EXE Files|*.exe|All Files|*.*",
                InitialDirectory = System.Reflection.Assembly.GetExecutingAssembly().Location
            };

            if (file.ShowDialog() == DialogResult.OK
                && File.Exists(file.FileName))
            {
                setHnd.GamePath = file.FileName;
                iniHnd.Write("GamePath", setHnd.GamePath, "Paths");
                iniHnd.Write("GameArgs", String.Empty, "Paths");
            }


            /*
             * Ask for the Launcher path
             */
            file = new OpenFileDialog()
            {
                Title = "Choose the path of the launcher executable (Origin)",
                Filter = "EXE Files|*.exe|All Files|*.*",
                InitialDirectory = System.Reflection.Assembly.GetExecutingAssembly().Location
            };
            if (file.ShowDialog() == DialogResult.OK
                && File.Exists(file.FileName))
            {
                setHnd.LauncherPath = file.FileName;
                iniHnd.Write("LauncherPath", setHnd.LauncherPath, "Paths");
            }
        }

        private static void ProcessLauncher(Settings setHnd)
        {
            String launcherName = Path.GetFileNameWithoutExtension(setHnd.LauncherPath);
            String gameName = Path.GetFileNameWithoutExtension(setHnd.GamePath);
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

            launcherProc.StartInfo.UseShellExecute = true;
            launcherProc.StartInfo.FileName = setHnd.LauncherPath;
            Logger("OSOL", "Attempting to start the launcher, cmd: " + setHnd.LauncherPath);
            launcherProc.Start();

            int sanity_counter = 0;
            while (launcherProc.MainWindowTitle.Length == 0
                && sanity_counter <= 120)
            {// wait up to 2 mins. for the launcher process
                if (sanity_counter == 120)
                {
                    Logger("FATAL", "Could not detect the launcher process after waiting 2 mins, exiting!");
                    return;
                }

                launcherProc.Refresh();
                sanity_counter++;
                Thread.Sleep(1000);
            }
            Logger("OSOL", "Detected the launcher process window at PID [" + launcherProc.Id + "] in " + sanity_counter + " sec.");

            /*
             * Game Post-Proxy Detection
             */
            gameProc.StartInfo.UseShellExecute = true;
            gameProc.StartInfo.FileName = setHnd.GamePath;
            gameProc.StartInfo.Arguments = setHnd.GameArgs;
            Logger("OSOL", "Launching game, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);
            gameProc.Start();
            Thread.Sleep(3000); // wait for the proxy to close
            
            sanity_counter = 0;
            int foundPID = GetRunningPIDByName(gameName);
            while (foundPID == 0 && gameProc.Id != foundPID 
                && sanity_counter <= 300)
            {// actively attempt to reacquire process, wait up to 5 mins
                if (sanity_counter == 300)
                {
                    Logger("FATAL", "Timed out while looking for game process, exiting! Internet connection or launcher issue?");
                    return;
                }

                foundPID = GetRunningPIDByName(gameName);
                // only rebind process if we found something
                if (foundPID != 0)
                    gameProc = RebindProcessByID(foundPID);

                sanity_counter++;
                Thread.Sleep(1000);
            }

            if (gameProc.Id != 0)
                Logger("OSOL", "Detected the game process at PID [" + gameProc.Id + "] in " + sanity_counter + " sec.");
            else
            {
                Logger("FATAL", "Lost track of the game process somehow, this shouldn't happen! Internet connection or launcher issue?");
                return;
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
                Thread.Sleep(5000); // let Origin sync with the cloud
                Logger("OSOL", "Game exited, killing launcher instance and cleaning up...");
                KillProcTreeByName(launcherName);
            }
        }
    }
}
