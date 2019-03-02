using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.ComponentModel;
using System.Management;
using System.Text;
using System.Collections.Generic;

namespace OriginSteamOverlayLauncher
{
    public class Program
    {
        public static string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        public static string appName = Path.GetFileNameWithoutExtension(codeBase);

        [STAThread]
        private static void Main(string[] args)
        {
            // get our current mutex id based off our AssemblyInfo.cs
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
            string mutexId = $"Global\\{{{appGuid}}}";

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
                        ProcessUtils.Logger("MUTEX", "Mutex is held by another instance, but seems abandoned!");
                        Environment.Exit(0);
                    }

                    /*
                     * Run our actual entry point here...
                     */
                    Application.EnableVisualStyles(); // enable DPI awareness
                    Application.SetCompatibleTextRenderingDefault(false);

                    if (ProcessUtils.CliArgExists(args, "help") || ProcessUtils.CliArgExists(args, "?"))
                    {// display an INI settings overview if run with /help or /?
                        DisplayHelpDialog();
                    }
                    else
                    {
                        ProcessTracking procTrack = new ProcessTracking();
                        Settings curSet = new Settings();
                        // path to our local config
                        IniFile iniFile = new IniFile(appName + ".ini");

                        // overwrite/create log upon startup
                        File.WriteAllText(appName + "_Log.txt", String.Empty);
                        ProcessUtils.Logger("NOTE", "OSOL is running as: " + appName);

                        if (Settings.CheckINI(iniFile)
                            && Settings.ValidateINI(curSet, iniFile, iniFile.Path))
                        {
                            procTrack.ProcessLauncher(curSet, iniFile); // normal functionality
                        }
                        else
                        {// ini doesn't match our comparison, recreate from stubs
                            ProcessUtils.Logger("WARNING", "Config file partially invalid or doesn't exist, re-stubbing...");
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

        public static void DisplayHelpDialog()
        {
            Form helpForm = new HelpForm();
            helpForm.ShowDialog();

            if (helpForm.DialogResult == DialogResult.OK || helpForm.DialogResult == DialogResult.Cancel)
            {
                Process.GetCurrentProcess().Kill(); // exit the assembly after the modal
            }
        }
    }
}
