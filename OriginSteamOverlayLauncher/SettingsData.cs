using System;
using System.Diagnostics;
using System.IO;

namespace OriginSteamOverlayLauncher
{
    public class SettingsData
    {
        public PathSettings Paths { get; }
        public OptionsSettings Options { get; }
        public InfoSettings Info { get; }
        public SettingsData()
        {
            Paths = new PathSettings();
            Options = new OptionsSettings();
            Info = new InfoSettings();
        }

        public class PathSettings
        {
            public string LauncherPath { get; set; } = "";
            public string LauncherArgs { get; set; } = "";
            public string LauncherURI { get; set; } = "";
            public string GamePath { get; set; } = "";
            public string GameArgs { get; set; } = "";
            public string MonitorPath { get; set; } = "";
            public string PreLaunchExecPath { get; set; } = "";
            public string PreLaunchExecArgs { get; set; } = "";
            public string PostGameExecPath { get; set; } = "";
            public string PostGameExecArgs { get; set; } = "";
        }

        public class OptionsSettings
        {
            public bool ReLaunch { get; set; } = true;
            public bool SkipLauncher { get; set; }
            public bool CloseLauncher { get; set; } = true;
            public bool AutoGameLaunch { get; set; } = true;
            public bool MinimizeLauncher { get; set; }
            public bool ElevateExternals { get; set; }

            public int PreGameLauncherWaitTime { get; set; } = 15;
            public int PreGameWaitTime { get; set; } = 0;
            public int PostGameWaitTime { get; set; } = 0;
            public int ProcessAcquisitionTimeout { get; set; } = 120;
            public int InterProcessAcquisitionTimeout { get; set; } = 15;

            public long GameProcessAffinity { get; set; }
            public ProcessPriorityClass GameProcessPriority { get; set; } = ProcessPriorityClass.Normal;
        }

        public class InfoSettings
        {
            public string ReleaseVersion { get; } = Program.AsmProdVer;
        }

        #region Validation Helpers
        public static bool ValidatePath(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                return true;
            return false;
        }

        public static bool ValidatePath(string value, out string sanitized)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string _sanitized = string.Concat(value.Split(Path.GetInvalidPathChars()));
                string _sanitizedDir = Path.GetDirectoryName(_sanitized);
                string _sanitizedFile = string.Concat(Path.GetFileName(_sanitized).Split(Path.GetInvalidFileNameChars()));
                sanitized = $"{_sanitizedDir}\\{_sanitizedFile}";
                return File.Exists(_sanitized);
            }
            sanitized = "";
            return false;
        }

        public static bool ValidateURI(string path)
        {// take a string and check if it's similar to a URI
            if (path.Length > 0 && !string.IsNullOrWhiteSpace(path) &&
                ProcessUtils.OrdinalContains(@"://", path))
                return true;
            return false;
        }

        public static bool ValidateInt(object val, out int output)
        {
            if (typeof(int) == val.GetType())
            {
                output = (int)val >= 0 ? (int)val : 0;
                return true;
            }
            else if (int.TryParse((string)val, out int _output))
            {
                output = _output >= 0 ? _output : 0;
                return true;
            }
            output = 0;
            return false;
        }

        public static bool ValidateBool(object val, out bool output)
        {
            if (typeof(bool) == val.GetType())
            {
                output = (bool)val;
                return true;
            }
            else if (bool.TryParse((string)val, out bool _output))
            {
                output = _output;
                return true;
            }
            output = false;
            return false;
        }

        public static bool ValidateProcessPriority(string val, out ProcessPriorityClass output)
        {
            output = ProcessPriorityClass.Normal; // default to Normal
            try
            {// override our output with a parse if necessary
                if (ProcessUtils.FuzzyEquals("Idle", val))
                    output = ProcessPriorityClass.Idle;
                else if (ProcessUtils.FuzzyEquals("BelowNormal", val))
                    output = ProcessPriorityClass.BelowNormal;
                else if (ProcessUtils.FuzzyEquals("AboveNormal", val))
                    output = ProcessPriorityClass.AboveNormal;
                else if (ProcessUtils.FuzzyEquals("High", val))
                    output = ProcessPriorityClass.High;
                else if (ProcessUtils.FuzzyEquals("RealTime", val))
                    output = ProcessPriorityClass.RealTime;
                return true;
            }
            catch (InvalidCastException) { }
            return false;
        }

        public static bool ValidateProcessAffinity(object val, out long output)
        {
            if (typeof(long) == val.GetType())
            {
                output = (long)val >= 0 ? (long)val : 0;
                return true;
            }
            else if (typeof(string) == val.GetType() &&
                BitmaskExtensions.TryParseCoreString((string)val, out long _output1))
            {
                output = _output1 >= 0 ? _output1 : 0;
                return true;
            }
            else if (typeof(string) == val.GetType() &&
                BitmaskExtensions.TryParseAffinity((string)val, out long _output2))
            {
                output = _output2 >= 0 ? _output2 : 0;
                return true;
            }
            output = 0;
            return false;
        }
        #endregion
    }
}
