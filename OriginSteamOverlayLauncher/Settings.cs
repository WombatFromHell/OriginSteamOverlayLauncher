using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public String MonitorPath { get; set; }

        // options
        public String LauncherMode { get; set; }
        public String PreLaunchExec { get; set; }
        public String PreLaunchExecArgs { get; set; }
        public String PostGameExec { get; set; }
        public String PostGameExecArgs { get; set; }
        public String DetectedCommandline { get; set; }

        // bools for OSOL behavior
        public Boolean ReLaunch { get; set; }
        public Boolean DoNotClose { get; set; }
        public Boolean MinimizeLauncher { get; set; }
        public Boolean CommandlineProxy { get; set; }

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
                iniHnd.Write("MonitorPath", String.Empty, "Paths");
                iniHnd.Write("DetectedCommandline", String.Empty, "Paths");

                // options
                iniHnd.Write("PreLaunchExec", String.Empty, "Options");
                iniHnd.Write("PreLaunchExecArgs", String.Empty, "Options");
                iniHnd.Write("PostGameExec", String.Empty, "Options");
                iniHnd.Write("PostGameExecArgs", String.Empty, "Options");

                // integer options (sensible defaults)
                iniHnd.Write("PreGameOverlayWaitTime", "5", "Options"); //5s
                iniHnd.Write("PreGameLauncherWaitTime", "12", "Options"); //12s
                iniHnd.Write("PostGameWaitTime", "7", "Options"); //7s
                iniHnd.Write("ProxyTimeout", "3", "Options"); //3s
                iniHnd.Write("ProcessAcquisitionTimeout", "300", "Options"); //5mins

                // options as parsed strings
                //Kill and relaunch detected launcher PID before game
                iniHnd.Write("ReLaunch", "True", "Options");
                // Do not kill detected launcher PID after game exits
                iniHnd.Write("DoNotClose", "False", "Options");
                // Do not minimize launcher on process detection
                iniHnd.Write("MinimizeLauncher", "False", "Options");
                // Do not copy the commandline from a previous instance of the game/monitor executable
                iniHnd.Write("CommandlineProxy", "False", "Options");

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
                    && iniHnd.KeyExists("MonitorPath") && iniHnd.KeyExists("GameArgs")
                    && iniHnd.KeyExists("LauncherMode") && iniHnd.KeyExists("PreLaunchExec") && iniHnd.KeyExists("PreLaunchExecArgs")
                    && iniHnd.KeyExists("PostGameExec") && iniHnd.KeyExists("PostGameExecArgs") && iniHnd.KeyExists("DetectedCommandline")
                    && iniHnd.KeyExists("PreGameOverlayWaitTime") && iniHnd.KeyExists("PreGameLauncherWaitTime")
                    && iniHnd.KeyExists("PostGameWaitTime") && iniHnd.KeyExists("ProcessAcquisitionTimeout")
                    && iniHnd.KeyExists("ProxyTimeout") && iniHnd.KeyExists("ReLaunch")
                    && iniHnd.KeyExists("DoNotClose") && iniHnd.KeyExists("MinimizeLauncher") && iniHnd.KeyExists("CommandlineProxy"))
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
                Program.Logger("WARNING", String.Format("Path validator failed on: [{0}], because: {1}", path, e.ToString()));
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
            setHnd.LauncherURI = ValidateString(iniHnd, String.Empty, "LauncherURI", "LauncherURI", "Paths");

            setHnd.GamePath = ValidateString(iniHnd, String.Empty, "GamePath", "GamePath", "Paths");
            setHnd.GameArgs = ValidateString(iniHnd, String.Empty, "GameArgs", "GameArgs", "Paths");
            setHnd.MonitorPath = ValidateString(iniHnd, String.Empty, "MonitorPath", "MonitorPath", "Paths");

            // support for saving a commandline grabbed via CommandlineProxy
            setHnd.DetectedCommandline = ValidateString(iniHnd, String.Empty, setHnd.DetectedCommandline, "DetectedCommandline", "Paths");

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
            setHnd.ProxyTimeout = ValidateInt(iniHnd, 3, setHnd.ProxyTimeout, "ProxyTimeout", "Options"); // 3s default wait time
            setHnd.PreGameOverlayWaitTime = ValidateInt(iniHnd, 5, setHnd.PreGameOverlayWaitTime, "PreGameOverlayWaitTime", "Options"); // 5s default wait time
            setHnd.PreGameLauncherWaitTime = ValidateInt(iniHnd, 12, setHnd.PreGameLauncherWaitTime, "PreGameLauncherWaitTime", "Options"); // 12s default wait time
            setHnd.ProcessAcquisitionTimeout = ValidateInt(iniHnd, 300, setHnd.ProcessAcquisitionTimeout, "ProcessAcquisitionTimeout", "Options"); // 5mins default wait time
            setHnd.PostGameWaitTime = ValidateInt(iniHnd, 7, setHnd.PostGameWaitTime, "PostGameWaitTime", "Options"); // 7s default wait time

            // parse strings into bools
            // Default to closing the previously detected launcher PID
            setHnd.ReLaunch = ValidateBool(iniHnd, true, setHnd.ReLaunch, "ReLaunch", "Options");
            // Default to closing the detected launcher PID when a game exits
            setHnd.DoNotClose = ValidateBool(iniHnd, false, setHnd.DoNotClose, "DoNotClose", "Options");
            // Default to leaving the launcher window alone after detecting it
            setHnd.MinimizeLauncher = ValidateBool(iniHnd, false, setHnd.MinimizeLauncher, "MinimizeLauncher", "Options");
            // Default to not proxying the commandline from a running instance of the game/monitor executable
            setHnd.CommandlineProxy = ValidateBool(iniHnd, false, setHnd.CommandlineProxy, "CommandlineProxy", "Options");

            if (ValidatePath(setHnd.LauncherPath) && ValidatePath(setHnd.GamePath))
                return true; // only continue if both required paths work

            return false;
        }
        #endregion
    }
}
