using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20IdentityMigration
    {
        internal const string Id = "fs20vortexfamily";
        internal const string PreviousId = "br.japensou.nuclearoption.vortexctol";

        internal static void Apply(ConfigFile config, ManualLogSource logger)
        {
            try
            {
                var previous = Path.Combine(Paths.ConfigPath, PreviousId + ".cfg");
                if (!File.Exists(config.ConfigFilePath) && File.Exists(previous))
                {
                    var temporary = config.ConfigFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.Copy(previous, temporary, false);
                        File.Move(temporary, config.ConfigFilePath);
                        config.Reload();
                        logger.LogInfo("FS-20 settings migrated to " + Id + "; previous settings retained.");
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
            }
            catch (Exception error) { logger.LogWarning("FS-20 settings migration could not finish: " + error.Message); }

            // NOMM writes its own meta.json above the extracted package. Update only
            // ancestors of this DLL, never other installed mods or catalog entries.
            try
            {
                var root = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                while (directory != null && directory.FullName.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) break;
                    MigrateMetadata(Path.Combine(directory.FullName, "meta.json"), logger);
                    directory = directory.Parent;
                }
            }
            catch (Exception error) { logger.LogWarning("FS-20 NOMM identity migration could not finish: " + error.Message); }
        }

        [Serializable]
        private sealed class MetadataId { public string id = ""; }

        private static void MigrateMetadata(string path, ManualLogSource logger)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (JsonUtility.FromJson<MetadataId>(text)?.id != PreviousId) return;
            var pattern = new Regex("(\"id\"\\s*:\\s*\")" + Regex.Escape(PreviousId) + "(\")");
            if (pattern.Matches(text).Count != 1) return;
            var updated = pattern.Replace(text, match => match.Groups[1].Value + Id + match.Groups[2].Value, 1);
            if (JsonUtility.FromJson<MetadataId>(updated)?.id != Id) return;
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, updated, new UTF8Encoding(false));
                File.Replace(temporary, path, path + "." + Guid.NewGuid().ToString("N") + ".previous-id.bak");
                logger.LogInfo("FS-20 NOMM identity migrated to " + Id + "; previous metadata backed up. Refresh NOMM after closing the game.");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
