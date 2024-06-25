using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;

namespace OriginSteamOverlayLauncher
{
    public class SystemTrayIcon : IDisposable
    {
        private NotifyIcon trayIcon;

        public SystemTrayIcon()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = Icon.FromHandle(new Bitmap(Properties.Resources.OSOL_tray).GetHicon()),
                Visible = false
            };

            trayIcon.ContextMenu = new ContextMenu(new MenuItem[]
            {
            new MenuItem("Exit", Exit)
            });
        }

        public void Display()
        {
            trayIcon.Visible = true;
            trayIcon.Text = "OriginSteamOverlayLauncher";
        }

        private void Exit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        public void Dispose()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }
    }

    public class Program
    {
        // make our config file location local to our assembly
        public static string ConfigFile { get; } = new FileInfo($"{AppName}.ini").FullName.ToString();
        public static string LogFile { get; } = new FileInfo($"{AppName}_Log.txt").FullName.ToString();

        public static Settings CurSettings { get; private set; }
        public static string GetCodeBase { get => Assembly.GetExecutingAssembly().CodeBase; }
        public static string AppName { get => Path.GetFileNameWithoutExtension(GetCodeBase); }
        public static string AsmProdVer { get => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion.ToString(); }
        public static int AssemblyPID { get => Process.GetCurrentProcess().Id; }

        [STAThread]
        private static void Main(string[] args)
        {
            // get our current mutex id based off our AssemblyInfo.cs
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
            string mutexId = $"Global\\{{{appGuid}}}";

            // overwrite log file on startup
            File.WriteAllText(LogFile, "");
            ProcessUtils.Logger("NOTE", $"OSOL is running as: {AppName}");
            CurSettings = new Settings();

            /// begin real entry point
            Application.EnableVisualStyles(); // enable DPI awareness
            Application.SetCompatibleTextRenderingDefault(false);

            using (var systemTrayIcon = new SystemTrayIcon())
            {
                systemTrayIcon.Display();

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

                        if (ProcessUtils.CliArgExists(args, "help") || ProcessUtils.CliArgExists(args, "?"))
                        {
                            // display an INI settings overview if run with /help or /?
                            DisplayHelpDialog();
                        }
                        else
                        {
                            Task.Run(async () =>
                            {
                                await CoreLogic();
                            });
                        }

                        Application.Run();
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                        ProcessUtils.Logger("OSOL", "Exiting...");
                        Environment.Exit(0);
                    }
                }
            }
        }

        private static async Task CoreLogic() {
            try
            {
                LaunchLogic procTrack = new LaunchLogic();
                await procTrack.ProcessLauncher();
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    ProcessUtils.Logger("EXCEPTION", $"{ex.ToString()}: {ex.Message}");
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
