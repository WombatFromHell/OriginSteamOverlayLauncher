using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using System.Collections.Generic;

namespace OriginSteamOverlayLauncher
{
    public class INIRecord
    {
        public string Section { get; set; } = "";
        public string KeyName { get; set; } = "";
        public string Value { get; set; } = "";
        public int Index { get; set; } = -1;
        public int SectionIndex { get; set; } = -1;
    }

    public class Settings
    {
        public SettingsData Data { get; private set; }

        public Settings(bool testable = false)
        {
            Data = new SettingsData();
            if (!testable && !CheckINI())
            {
                ProcessUtils.Logger("WARNING", "Config file partially invalid or doesn't exist, re-stubbing...");
                if (!MigrateLegacyConfig())
                {// rebuild from scratch if migrate fails
                    CreateINIFromInstance();
                    PathChooser();
                }
            }
            else if (!testable)
                ParseToInstance();
        }

        #region INI implementation methods
        /// <summary>
        /// Serialize our current Settings instance to a StringBuilder object
        /// </summary>
        /// <returns>A StringBuilder object that contains the serialized data</returns>
        public List<string> InstanceToList()
        {
            List<string> output = new List<string>();
            var sections = Data.GetType().GetProperties().Select(p => p.Name).ToArray();
            foreach (var sectionKey in sections)
            {// recreate each section from our safe internal defaults
                var members = Data.GetType().GetProperty(sectionKey).GetValue(Data, null);
                output.Add($"[{sectionKey}]");
                foreach (var member in members.GetType().GetProperties())
                {
                    var prop = member.GetValue(members);
                    if (member.Name == "GameProcessAffinity")
                        output.Add($"{member.Name}={BitmaskExtensions.AffinityToCoreString((long)prop)}");
                    else
                        output.Add($"{member.Name}={prop.ToString()}");
                }
            }
            return output;
        }

        /// <summary>
        /// Deserialize the current config file into our Settings instance
        /// </summary>
        public List<string> ParseToInstance(bool testable = false, List<string> sourceConfig = null)
        {
            List<string> input = new List<string>();
            SettingsData _local = testable ? new SettingsData() : Data;
            if (!testable && sourceConfig != null && sourceConfig.Count > 0)
                input = sourceConfig;
            else if (!testable)
                input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            else
                input = InstanceToList();

            string lastSection = "";
            object lastSectionObj;
            for (int i = 0; i < input.Count; i++)
            {// iterate through the .ini by parsing lines
                string[] parsedSection = input[i].Split(new char[] { '[', ']' }, 3);
                lastSection = parsedSection.Length > 1 ? parsedSection[1] : lastSection;
                if (!testable)
                    lastSectionObj = Data.GetType().GetProperty(lastSection).GetValue(Data, null);
                else
                    lastSectionObj = _local.GetType().GetProperty(lastSection).GetValue(_local, null);
                string[] item = input[i].Split(new char[] { '=' }, 2);

                if (item.Length == 2)
                {// make sure to validate all mutable inputs parsed from the config file
                    if (item[0] == "GameProcessAffinity" &&
                        SettingsData.ValidateProcessAffinity(item[1], out long _affinity))
                        lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, _affinity);
                    else if (item[0] == "GameProcessPriority" &&
                        SettingsData.ValidateProcessPriority(item[1], out ProcessPriorityClass _priority))
                        lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, _priority);
                    else if (item[0] == "ReleaseVersion") { /* IMMUTABLE */ }
                    else
                    {// parse our key/val pair into our instance (respecting data types)
                        if (SettingsData.ValidateInt(item[1], out int _valInt))
                            lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, _valInt);
                        else if (SettingsData.ValidateBool(item[1], out bool _valBool))
                            lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, _valBool);
                        else if (ProcessUtils.OrdinalContains("Path", item[0]) &&
                            SettingsData.ValidatePath(item[1], out string validated))
                            lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, validated);
                        else if (SettingsData.ValidateURI(item[1]) || !string.IsNullOrWhiteSpace(item[1]))
                            lastSectionObj.GetType().GetProperty(item[0]).SetValue(lastSectionObj, item[1]);
                    }
                }
            }
            return input;
        }

        private void CreateINIFromInstance()
        {// convert data struct to list of keyval pairs
            List<string> serialized = InstanceToList();
            if (File.Exists(Program.ConfigFile)) {
                List<string> prevConfig = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();

            }
            File.WriteAllText(Program.ConfigFile, ""); // overwrite ini
            File.WriteAllLines(Program.ConfigFile, serialized, Encoding.UTF8);
        }

        private bool CheckINI()
        {// return false if INI doesn't match our accessor list
            if (CompareKeysToProps() && CheckVersion() &&
                SettingsData.ValidatePath(ReadKey("GamePath", "Paths").Value ?? "", out string _sanitized) &&
                File.Exists(_sanitized))
                return true;
            return false;
        }

        public bool CheckVersion(bool testable = false, string testableVer = "")
        {
            if (!testable && KeyExists("ReleaseVersion"))
                return ProcessUtils.OrdinalEquals(ReadKey("ReleaseVersion", "Info").Value, Program.AsmProdVer);
            else if (testable && KeyExists("ReleaseVersion"))
                return ProcessUtils.OrdinalEquals(testableVer, Program.AsmProdVer);
            return false;
        }

        public bool CompareKeysToProps(bool testable = false, Settings instance = null)
        {// validate if existing keynames are equal to our data structure
            var sections = !testable ?
                Data.GetType().GetProperties().ToArray() :
                instance?.GetType().GetProperties().ToArray();
            if (SettingsData.ValidatePath(Program.ConfigFile) || testable)
                return ParsedComparison();
            return false;

            bool ParsedComparison()
            {
                var _local = InstanceToList();
                foreach (var props in sections)
                {
                    var subProps = !testable ?
                        Data.GetType().GetProperty(props.Name).GetValue(Data, null) :
                        instance?.GetType().GetProperty(props.Name).GetValue(instance, null);
                    var members = subProps.GetType().GetProperties();
                    foreach (var p in members)
                    {
                        if (testable && _local.FirstOrDefault(i => i.Contains(p.Name)) == string.Empty)
                            return false;
                        else if (!testable && !KeyExists(p.Name))
                            return false;
                    }
                }
                return true;
            }
        }

        public bool KeyExists(string keyName, bool testable = false)
        {// section agnostic key check (return first match)
            var element = ReadKey(keyName, "", testable);
            if (element != null && element.Index > -1)
                return true;
            return false;
        }

        public INIRecord ReadKey(string keyName, string sectionName = null, bool testable = false)
        {
            INIRecord output = new INIRecord();
            var input = !testable ?
                File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList() :
                InstanceToList();
            string lastSection = "";

            for (int i = 0; i < input.Count; i++)
            {// iterate through the .ini by parsing lines
                string[] parsedSection = input[i].Split(new char[] { '[', ']' }, 3);
                lastSection = parsedSection.Length > 1 ? parsedSection[1] : lastSection;
                output.SectionIndex = string.IsNullOrWhiteSpace(lastSection) ? i : -1; // mark our section index
                string[] item = input[i].Split(new char[] { '=' }, 2);
                bool keyMatch = ProcessUtils.FuzzyEquals(item[0], keyName) && item.Length == 2;
                if (ProcessUtils.FuzzyEquals(lastSection, sectionName) && keyMatch || keyMatch)
                {// return first matched section + key or first key (section optional)
                    output.Section = lastSection;
                    output.KeyName = item[0];
                    output.Value = item[1];
                    output.Index = i;
                    return output; // return on the first match
                }
            }
            return output;
        }

        public bool WriteKey(string keyName, string value, string sectionName, bool testable = false)
        {
            if (string.IsNullOrWhiteSpace(value) && KeyExists(keyName))
                return false; // do not overwrite data with nothing!

            List<string> input = new List<string>();
            if (!testable)
                input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            else
                input = InstanceToList();

            var matchedKey = ReadKey(keyName, sectionName, testable);
            if (matchedKey.Index > -1)
            {// overwrite the existing key
                InsertKey(keyName, value, matchedKey.Index, testable);
                return true;
            }
            else
            {
                string lastSection = "";
                for (int i = 0; i < input.Count; i++)
                {
                    string[] parsedSection = input[i].Split(new char[] { '[', ']' }, 3);
                    lastSection = parsedSection.Length > 1 ? parsedSection[1] : lastSection;
                    if (lastSection == sectionName)
                    {// insert key after the matching section
                        InsertKey(keyName, value, i + 1, testable);
                        return true;
                    }
                }
                // no matching section?
                input.Add($"{keyName}={value}");
                if (!testable)
                    File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return false;
            }
        }

        public bool InsertKey(string keyName, string value, int index, bool testable = false)
        {// insert a key at a given index
            List<string> input = new List<string>();
            if (!testable)
                input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            else
                input = InstanceToList();

            if (input.Count > 0 && index < input.Count)
            {
                input.Insert(index, $"{keyName}={value}");
                if (!testable)
                    File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public bool RemoveKey(string keyName, string sectionName, bool testable = false)
        {// remove the first match of the key in the config file
            List<string> input = new List<string>();
            if (!testable)
                input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            else
                input = InstanceToList();

            var matchedKey = ReadKey(keyName, sectionName);
            if (input.Count > 0 && input[matchedKey.Index].Contains(keyName))
            {
                input.RemoveAt(matchedKey.Index);
                if (!testable)
                    File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public bool RemoveKeys(string keyName, bool testable = false)
        {// remove all matches of the key in the config file
            List<string> input = new List<string>();
            if (!testable)
                input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            else
                input = InstanceToList();

            var output = input.ToList();
            for (int i = 0; i < output.Count; i++)
            {
                if (ProcessUtils.OrdinalContains(keyName, output[i]))
                    output.RemoveAt(i);
            }
            if (output.Count != input.Count)
            {
                if (!testable)
                    File.WriteAllLines(Program.ConfigFile, output, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public void ReplaceKey(string keyName, string value, string sectionName, bool testable = false)
        {// replaces n matches of fuzzy key with single key in a specified section
            RemoveKeys(keyName, testable);
            WriteKey(keyName, value, sectionName, testable);
        }
        #endregion

        private bool MigrateLegacyConfig()
        {
            if (File.Exists(Program.ConfigFile))
            {
                var configLines = File.ReadAllLines(Program.ConfigFile).ToList();
                // use a dictionary for easy conversion of key names
                var convKeys = new Dictionary<string, string>();
                convKeys.Add("LauncherPath", "LauncherPath");
                convKeys.Add("LauncherArgs", "LauncherArgs");
                convKeys.Add("LauncherURI", "LauncherURI");
                convKeys.Add("GamePath", "GamePath");
                convKeys.Add("GameArgs", "GameArgs");
                convKeys.Add("MonitorPath", "MonitorPath");
                convKeys.Add("PreLaunchExec", "PreLaunchExecPath");
                convKeys.Add("PreLaunchExecArgs", "PreLaunchExecArgs");
                convKeys.Add("PostGameExec", "PostGameExecPath");
                convKeys.Add("PostGameExecArgs", "PostGameExecArgs");
                // build a fresh config
                var freshConfig = InstanceToList();

                foreach (var item in convKeys)
                {// migrate each old variable to the new config variable in memory
                    var lineMatch = configLines.FindIndex(l => l.StartsWith(item.Key));
                    var dataMatch = lineMatch > -1 ?
                        configLines[lineMatch].Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries) :
                        new string[] { };
                    var configMatchIdx = freshConfig.FindIndex(l => l.StartsWith(item.Value));
                    if (dataMatch.Length == 2 && configMatchIdx > -1)
                    {
                        if (!string.IsNullOrWhiteSpace(dataMatch[1]))
                            freshConfig[configMatchIdx] = $"{item.Value}={dataMatch[1]}";
                    }
                }
                ParseToInstance(sourceConfig: freshConfig);
                ProcessUtils.Logger("OSOL", "Old config file path data migrated to new version...");
                File.WriteAllLines(Program.ConfigFile, freshConfig);
                // use PathChooser() to validate our parsed Path variables and alert the user
                PathChooser();
                return true;
            }
            return false;
        }

        private void PathChooser()
        {
            /*
             * Ask for the Game path
             */

            if (!SettingsData.ValidatePath(Data.Paths.GamePath))
            {// only re-ask for path if current one is invalid
                OpenFileDialog file = new OpenFileDialog()
                {
                    Title = "Choose the path of your game executable",
                    Filter = "EXE Files|*.exe|All Files|*.*",
                    InitialDirectory = Path.GetDirectoryName(Program.GetCodeBase)
                };

                if (file.ShowDialog() == DialogResult.OK
                    && SettingsData.ValidatePath(file.FileName))
                {
                    Data.Paths.GamePath = file.FileName;
                    ReplaceKey("GamePath", Data.Paths.GamePath, "Paths");
                }// don't do anything if we cancel out
            }

            /*
             * Ask for the Launcher path
             */
            if (!SettingsData.ValidatePath(Data.Paths.LauncherPath))
            {
                OpenFileDialog file = new OpenFileDialog()
                {
                    Title = "Choose the path of your launcher executable",
                    Filter = "EXE Files|*.exe|All Files|*.*",
                    InitialDirectory = Path.GetDirectoryName(Program.GetCodeBase)
                };

                if (file.ShowDialog() == DialogResult.OK
                    && SettingsData.ValidatePath(file.FileName))
                {
                    Data.Paths.LauncherPath = file.FileName;
                    ReplaceKey("LauncherPath", Data.Paths.LauncherPath, "Paths");
                }
            }

            if (!SettingsData.ValidatePath(Data.Paths.GamePath))
            {// sanity check in case of cancelling both path inputs
                ProcessUtils.Logger("FATAL", "A valid GamePath is required to function!");
                Process.GetCurrentProcess().Kill(); // bail!
            }

            ProcessUtils.MessageBox(IntPtr.Zero, "INI updated, OSOL should be restarted for normal behavior, exiting...", "Notice", (int)0x00001000L);
            Process.GetCurrentProcess().Kill();
        }
    }
}
