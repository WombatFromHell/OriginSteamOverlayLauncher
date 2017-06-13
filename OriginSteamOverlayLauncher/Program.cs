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

    class ProcessSpinner
    {// just a utility wrapper class
        public Process SpunProcess { get; set; }
        public int Id { get { return SpunProcess.Id; } }
        public String MainWindowTitle { get { return SpunProcess.MainWindowTitle; } }
        public IntPtr MainWindowHandle { get { return SpunProcess.MainWindowHandle; } }

        public Process SpinProcess(String exePath, String exeArgs)
        {
            SpunProcess = new Process();
            SpunProcess.StartInfo.FileName = exePath;
            SpunProcess.StartInfo.Arguments = exeArgs;
            SpunProcess.StartInfo.UseShellExecute = true;
            SpunProcess.Start();

            return SpunProcess;
        }

        public Process StopProcess()
        {
            SpunProcess.Kill();
            return SpunProcess;
        }
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
            }
            else
                ProcessLauncher(curSet);
        }

        private static bool IsRunning(String name) { return Process.GetProcessesByName(name).Any(); }

        private static bool IsRunningPID(Int64 pid) { return Process.GetProcesses().Any(x => x.Id == pid); }

        private static int GetRunningPIDByName(String procName) { return Process.GetProcessesByName(procName).FirstOrDefault().Id; }

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

            /*
             * Launcher Detection
             */
            if (IsRunning(launcherName))
            {// if the launcher is running before the game kill it so we can run it through Steam
                Logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                KillProcTreeByName(launcherName);
                Thread.Sleep(3000); // pause a moment for the launcher to close
            }

            ProcessSpinner launcherProc = new ProcessSpinner();
            Logger("OSOL", "Attempting to start the launcher, cmd: " + setHnd.LauncherPath);
            launcherProc.SpinProcess(setHnd.LauncherPath, String.Empty);
            Thread.Sleep(7000); // let the launcher load so steam can hook into it


            /*
             * Game Post-Proxy Detection
             */
            ProcessSpinner gameProc = new ProcessSpinner();
            Logger("OSOL", "Launching game, cmd: " + setHnd.GamePath + " " + setHnd.GameArgs);
            gameProc.SpinProcess(setHnd.GamePath, setHnd.GameArgs);
            Thread.Sleep(10000); // let the game process load
            int gamePID = GetRunningPIDByName(gameName);

            while (IsRunning(gameName) && gamePID > 0)
            {
                Thread.Sleep(1000); // sleep while game is running
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
