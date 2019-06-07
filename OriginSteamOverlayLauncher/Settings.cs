using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;

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

        public Settings()
        {
            Data = new SettingsData();
            if (!CheckINI())
            {
                ProcessUtils.Logger("WARNING", "Config file partially invalid or doesn't exist, re-stubbing...");
                CreateINIFromInstance();
                PathChooser();
            }
            else
                ParseToInstance();
        }

        #region INI implementation methods
        /// <summary>
        /// Serialize our current Settings instance to a StringBuilder object
        /// </summary>
        /// <returns>A StringBuilder object that contains the serialized data</returns>
        public StringBuilder InstanceToStringBuilder()
        {
            var output = new StringBuilder();
            var sections = Data.GetType().GetProperties().Select(p => p.Name).ToArray();
            foreach (var sectionKey in sections)
            {// recreate each section from our safe internal defaults
                var members = Data.GetType().GetProperty(sectionKey).GetValue(Data, null);
                output.AppendLine($"[{sectionKey}]");
                foreach (var member in members.GetType().GetProperties())
                {
                    var prop = member.GetValue(members);
                    if (member.Name == "GameProcessAffinity")
                        output.AppendLine($"{member.Name}={BitmaskExtensions.AffinityToCoreString((long)prop)}");
                    else
                        output.AppendLine($"{member.Name}={prop.ToString()}");
                }
            }
            return output;
        }

        /// <summary>
        /// Deserialize the current config file into our Settings instance
        /// </summary>
        private void ParseToInstance()
        {
            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8);
            string lastSection = "";
            object lastSectionObj;
            for (int i = 0; i < input.Length; i++)
            {// iterate through the .ini by parsing lines
                string line = input[i];
                string[] parsedSection = line.Split(new char[] { '[', ']' }, 3);
                lastSection = parsedSection.Length > 1 ? parsedSection[1] : lastSection;
                lastSectionObj = Data.GetType().GetProperty(lastSection).GetValue(Data, null);
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
        }

        private void CreateINIFromInstance()
        {
            File.WriteAllText(Program.ConfigFile, ""); // overwrite ini
            // convert data struct to list of keyval pairs
            var serialized = InstanceToStringBuilder().ToString();
            File.WriteAllText(Program.ConfigFile, serialized);
        }

        private bool CheckINI()
        {// return false if INI doesn't match our accessor list
            SettingsData.ValidatePath(ReadKey("GamePath", "Paths").Value ?? "", out string _sanitized);
            if (CompareKeysToProps() && CheckVersion() && File.Exists(_sanitized))
                return true;
            return false;
        }

        private bool CheckVersion()
        {
            if (KeyExists("ReleaseVersion"))
                return ProcessUtils.OrdinalEquals(ReadKey("ReleaseVersion", "Info").Value, Program.AsmProdVer);
            return false;
        }

        public bool CompareKeysToProps()
        {// validate if existing keynames are equal to our data structure
            if (SettingsData.ValidatePath(Program.ConfigFile))
            {
                var sections = Data.GetType().GetProperties().ToArray();
                foreach (var props in sections)
                {
                    var subProps = Data.GetType().GetProperty(props.Name).GetValue(Data, null);
                    var members = subProps.GetType().GetProperties();
                    foreach (var p in members)
                    {
                        if (!KeyExists(p.Name))
                            return false;
                    }
                }
                return true;
            }
            return false;
        }

        public bool KeyExists(string keyName)
        {// section agnostic key check
            var element = ReadKey(keyName);
            if (element != null && element.Index > -1)
                return true;
            return false;
        }

        public INIRecord ReadKey(string keyName, string sectionName = null)
        {
            INIRecord output = new INIRecord();
            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            string lastSection = "";

            for (int i = 0; i < input.Count; i++)
            {// iterate through the .ini by parsing lines
                string line = input[i];
                string[] parsedSection = line.Split(new char[] { '[', ']' }, 3);
                lastSection = parsedSection.Length > 1 ? parsedSection[1] : lastSection;
                output.SectionIndex = string.IsNullOrWhiteSpace(lastSection) ? i : -1; // mark our section index
                string[] item = input[i].Split(new char[] { '=' }, 2);
                if (!string.IsNullOrWhiteSpace(lastSection) && ProcessUtils.StringFuzzyEquals(lastSection, sectionName) &&
                    ProcessUtils.StringFuzzyEquals(item[0], keyName) || item.Length == 2 &&
                    ProcessUtils.StringFuzzyEquals(item[0], keyName))
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

        public bool WriteKey(string keyName, string value, string sectionName)
        {
            if (string.IsNullOrWhiteSpace(value) && KeyExists(keyName))
                return false; // do not overwrite data with nothing!

            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            var matchedKey = ReadKey(keyName, sectionName);
            if (matchedKey.Index > -1)
            {// overwrite the existing key
                InsertKey(keyName, value, matchedKey.Index);
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
                        InsertKey(keyName, value, i + 1);
                        return true;
                    }
                }
                // no matching section?
                input.Add($"{keyName}={value}");
                File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return false;
            }
        }

        public bool InsertKey(string keyName, string value, int index)
        {// insert a key at a given index
            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            if (input.Count > 0 && index < input.Count)
            {
                input.Insert(index, $"{keyName}={value}");
                File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public bool RemoveKey(string keyName, string sectionName)
        {// remove the first match of the key in the config file
            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            var matchedKey = ReadKey(keyName, sectionName);
            if (input.Count > 0 && input[matchedKey.Index].Contains(keyName))
            {
                input.RemoveAt(matchedKey.Index);
                File.WriteAllLines(Program.ConfigFile, input, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public bool RemoveKeys(string keyName)
        {// remove all matches of the key in the config file
            var input = File.ReadAllLines(Program.ConfigFile, Encoding.UTF8).ToList();
            var output = input.ToList();
            for (int i = 0; i < output.Count; i++)
            {
                if (ProcessUtils.OrdinalContains(keyName, output[i]))
                    output.RemoveAt(i);
            }
            if (output.Count != input.Count)
            {
                File.WriteAllLines(Program.ConfigFile, output, Encoding.UTF8);
                return true;
            }
            return false;
        }

        public void ReplaceKey(string keyName, string value, string sectionName)
        {// replaces n matches of fuzzy key with single key in a specified section
            RemoveKeys(keyName);
            WriteKey(keyName, value, sectionName);
        }
        #endregion

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
