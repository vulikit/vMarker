using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Tomlyn.Model;
using Tomlyn;

namespace vMarker
{
    public static class vMarkerConfig
    {
        public static Cfg Config { get; set; } = new Cfg();
        public static string ConfigPath { get; set; }

        static vMarkerConfig()
        {
            try
            {
                string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "vMarker";
                ConfigPath = Path.Combine(Server.GameDirectory,
                    "csgo",
                    "addons",
                    "counterstrikesharp",
                    "configs",
                    "plugins",
                    assemblyName,
                    "config.toml"
                );
            }
            catch (Exception ex)
            {
                ConfigPath = string.Empty;
            }
        }

        public static void LoadConfig()
        {
            try
            {
                string configDir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                ConfigLoad(ConfigPath);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static void ConfigLoad(string configPath)
        {
            try
            {
                string configText = File.ReadAllText(configPath);
                TomlTable model = Toml.ToModel(configText);

                Config = new Cfg
                {
                    Settings = LoadSettings(model["Settings"] as TomlTable)
                };
            }
            catch (TomlException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static string[] GetStringArray(object tomlArray)
        {
            if (tomlArray is TomlArray array)
            {
                var result = array.OfType<object>()
                                 .Select(item => item?.ToString() ?? "")
                                 .Where(s => !string.IsNullOrEmpty(s))
                                 .ToArray();
                return result;
            }
            else if (tomlArray is string[] stringArray)
            {
                var result = stringArray.Where(s => !string.IsNullOrEmpty(s)).ToArray();
                return result;
            }
            return [];
        }

        private static xSettings LoadSettings(TomlTable settingsTable)
        {
            var settings = new xSettings();

            if (settingsTable == null)
            {
                return settings;
            }

            try
            {
                settings.TimeToCleanUp = float.Parse(settingsTable["TimeToCleanUp"].ToString()!);

                object MarkerPermissions = settingsTable.ContainsKey("MarkerPermissions") ? settingsTable["MarkerPermissions"] : settings.MarkerPermissions;
                settings.MarkerPermissions = GetStringArray(MarkerPermissions);

                settings.UseMarkerType = StringExtensions.ReplaceColorTags(settingsTable.GetValueOrDefault("UseMarkerType", settings.UseMarkerType));

                object MarkerCommands = settingsTable.ContainsKey("MarkerCommands") ? settingsTable["MarkerCommands"] : settings.MarkerCommands;
                settings.MarkerCommands = GetStringArray(MarkerCommands);
            }
            catch (Exception ex)
            {
                settings = new xSettings();
            }

            return settings;
        }

        private static T GetValueOrDefault<T>(this TomlTable table, string key, T defaultValue)
        {
            if (table.TryGetValue(key, out var value))
            {
                if (typeof(T) == typeof(string[]) && value is TomlArray tomlArray)
                {
                    return (T)(object)tomlArray.OfType<object>()
                                               .Select(item => item?.ToString() ?? "")
                                               .Where(s => !string.IsNullOrEmpty(s))
                                               .ToArray();
                }
                return value is T typedValue ? typedValue : defaultValue;
            }
            return defaultValue;
        }
    }

    public class Cfg
    {
        public xSettings Settings { get; set; } = new xSettings();
    }

    public class xSettings
    {
        public string UseMarkerType { get; set; } = "ping";
        public float TimeToCleanUp { get; set; } = 30f;
        public string[] MarkerPermissions { get; set; } = new[] { "@css/root", "@jailbreak/warden" };
        public string[] MarkerCommands { get; set; } = new[] { "css_vmarker", "vmarker" };
    }
}
