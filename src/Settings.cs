using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace EqualizerProfileSwitcher
{
    [DataContract]
    public sealed class Settings
    {
        [DataMember] public string ConfigPath { get; set; }
        [DataMember] public string ProfilesDirectory { get; set; }
        [DataMember] public bool AlwaysOnTop { get; set; }
        [DataMember] public bool MinimizeAfterApply { get; set; }
        public static string Home { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EqualizerProfileSwitcher"); } }
        private static string SettingsPath { get { return Path.Combine(Home, "settings.json"); } }

        public static Settings Load()
        {
            Directory.CreateDirectory(Home);
            if (File.Exists(SettingsPath))
            {
                using (var stream = File.OpenRead(SettingsPath))
                    return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
            }
            string profiles = Path.Combine(Home, "Profiles");
            Directory.CreateDirectory(profiles);
            string starter = Path.Combine(profiles, "No additional processing.txt");
            if (!File.Exists(starter)) File.WriteAllText(starter, "# Adds no processing. Existing config.txt directives remain active.\r\n");
            string candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config", "config.txt");
            var settings = new Settings { ConfigPath = File.Exists(candidate) ? candidate : "", ProfilesDirectory = profiles, AlwaysOnTop = true };
            settings.Save();
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(Home);
            string temp = SettingsPath + ".tmp";
            using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, this);
            if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null);
            else File.Move(temp, SettingsPath);
        }
    }
}
