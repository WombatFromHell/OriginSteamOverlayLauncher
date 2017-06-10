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
            String launcherpath = string.Empty;
            String gamepath = string.Empty;
            String gameargs = string.Empty;

            // INI Support , courtesy of: https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
            IniFile myINI = new IniFile("OriginSteamOverlayLauncher.ini");

            if (!myINI.KeyExists("LauncherPath", "Paths"))
            {
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
                    myINI.Write("GamePath", string.Empty, "Paths");
                    myINI.Write("GameArgs", string.Empty, "Paths");
                }
            }
            else
            {
                launcherpath = myINI.Read("LauncherPath", "Paths");
                gamepath = myINI.Read("GamePath", "Paths");
                gameargs = myINI.Read("GameArgs", "Paths");
            }

            if (launcherpath.Length <= 0)
                throw new System.IO.FileNotFoundException("No Launcher path set, check the INI file!");
            if (gamepath.Length <= 0)
                throw new System.IO.FileNotFoundException("No Game path set, check the INI file!");
            
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherpath)).Any())
            {// if the launcher is running before the game, kill it so we can run it through Steam
                KillProcByName(Path.GetFileNameWithoutExtension(launcherpath));
                Thread.Sleep(2000); // pause a moment for the launcher to close
            }

            // start the launcher
            launcherproc.StartInfo.FileName = launcherpath;
            launcherproc.StartInfo.UseShellExecute = true;
            launcherproc.Start();
            
            Thread.Sleep(5000); // let the launcher load so steam can hook into it
            
            // start the game itself
            gameproc.StartInfo.FileName = gamepath;
            gameproc.StartInfo.UseShellExecute = true;

            if (gameargs.Length > 0)
                gameproc.StartInfo.Arguments = gameargs;

            gameproc.Start();
            Thread.Sleep(5000); // let the game process load

            // check once if game EXE is running, if not, try running it again
            if (!Process.GetProcessesByName(Path.GetFileNameWithoutExtension(gamepath)).Any())
            {
                gameproc.Start();
                Thread.Sleep(10000); // wait a little longer this time to be safe
            }

            while (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(gamepath)).Any())
            {
                Thread.Sleep(1000); // sleep while game is running
            }

            // game has exited, double check that the launcher is still running
            if (Process.GetProcesses().Any(x => x.Id == launcherproc.Id))
            {
                Thread.Sleep(5000); // let Origin sync with the cloud
                KillProcByName(Path.GetFileNameWithoutExtension(launcherpath)); // kill the launcher now that the game is closed
            }
        }

        static private void KillProcByName(string procName)
        {
            Process[] foundProcs = Process.GetProcessesByName(procName);
            foreach (Process proc in foundProcs)
            {
                proc.Kill();
            }
        }
    }
}
