using OriginSteamOverlayLauncher;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ConsoleApp1
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Process launcherproc = new Process();
            Process gameproc = new Process();
            String launcherpath = String.Empty;
            String gamepath = String.Empty;
            String gameargs = String.Empty;

            
            // INI Support , courtesy of: https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
            IniFile myINI = new IniFile("OriginSteamOverlayLauncher.ini");
            // overwrite/create log upon startup
            File.WriteAllText("OriginSteamOverlayLauncher_Log.txt", String.Empty);


            if (!myINI.KeyExists("LauncherPath", "Paths"))
            {
                logger("OSOL", "Couldn't find the config file, creating it and asking user for input...");

                // we don't have paths set in the ini, let's ask the user
                OpenFileDialog file = new OpenFileDialog();
                file.Title = "Choose the path of Launcher .exe";
                file.Filter = "EXE Files|*.exe|All Files|*.*";
                file.InitialDirectory = System.Reflection.Assembly.GetExecutingAssembly().Location;

                if (file.ShowDialog() == DialogResult.OK)
                {
                    launcherpath = file.FileName;
                    myINI.Write("LauncherPath", launcherpath, "Paths");
                }
                else
                    myINI.Write("LauncherPath", launcherpath, "Paths");


                file = new OpenFileDialog(); // re-init
                file.Title = "Choose the path of Game .exe";
                file.Filter = "EXE Files|*.exe|All Files|*.*";
                file.InitialDirectory = System.Reflection.Assembly.GetExecutingAssembly().Location;

                if (file.ShowDialog() == DialogResult.OK)
                {
                    gamepath = file.FileName;
                    myINI.Write("GamePath", gamepath, "Paths");
                    myINI.Write("GameArgs", gameargs, "Paths");
                }
                else
                {
                    myINI.Write("GamePath", String.Empty, "Paths");
                    myINI.Write("GameArgs", String.Empty, "Paths");
                }
            }
            else
            {
                launcherpath = myINI.Read("LauncherPath", "Paths");
                gamepath = myINI.Read("GamePath", "Paths");
                gameargs = myINI.Read("GameArgs", "Paths");
            }

            if (launcherpath.Length <= 0)
            {
                logger("FATAL", "No Launcher path set, check the INI file!");
                return;
            }
            if (gamepath.Length <= 0)
            {
                logger("FATAL", "No Game path set, check the INI file!");
                return;
            }
            
            if (IsRunning(Path.GetFileNameWithoutExtension(launcherpath)))
            {// if the launcher is running before the game, kill it so we can run it through Steam
                logger("OSOL", "Found previous instance of launcher by name, killing and relaunching...");
                KillProcTreeByName(Path.GetFileNameWithoutExtension(launcherpath));
                Thread.Sleep(2000); // pause a moment for the launcher to close
            }


            // start the launcher
            launcherproc.StartInfo.FileName = launcherpath;
            launcherproc.StartInfo.UseShellExecute = true;
            logger("OSOL", "Attempting to start the launcher, cmd: " + launcherpath);
            launcherproc.Start();
            
            Thread.Sleep(5000); // let the launcher load so steam can hook into it
            
            // start the game itself
            gameproc.StartInfo.FileName = gamepath;
            gameproc.StartInfo.UseShellExecute = true;

            if (gameargs.Length > 0)
                gameproc.StartInfo.Arguments = gameargs;

            logger("OSOL", "Launching game, cmd: " + gamepath + " " + gameargs);
            gameproc.Start();
            Thread.Sleep(5000); // let the game process load

            // check once if game EXE is running, if not, try running it again
            if (!IsRunning(Path.GetFileNameWithoutExtension(gamepath)))
            {
                logger("OSOL", "Unable to find game process, trying to relaunch...");
                gameproc.Start();
                Thread.Sleep(10000); // wait a little longer this time to be safe
            }

            while (IsRunning(Path.GetFileNameWithoutExtension(gamepath)))
            {
                Thread.Sleep(1000); // sleep while game is running
            }


            // game has exited, double check that the launcher is still running
            if (IsRunningPID(launcherproc.Id))
            {
                Thread.Sleep(5000); // let Origin sync with the cloud
                logger("OSOL", "Game exited, killing launcher instance and cleaning up...");
                KillProcTreeByName(Path.GetFileNameWithoutExtension(launcherpath)); // kill the launcher now that the game is closed
            }
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

        private static void logger(String cause, String message)
        {
            using (StreamWriter stream = File.AppendText("OriginSteamOverlayLauncher_Log.txt"))
            {
                stream.Write("[{0}] [{1}] {2}\r\n", DateTime.Now.ToUniversalTime(), cause, message);
            }
        }
    }
}
