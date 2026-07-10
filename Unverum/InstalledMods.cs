using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Unverum
{
    /// <summary>
    /// Tracks which mods are already downloaded/installed (present in the Mods folder),
    /// regardless of whether they are enabled or not. Used to show the "Installed"
    /// tag in the Browse Mods section.
    /// </summary>
    public static class InstalledMods
    {
        private static readonly object _lock = new();
        // Homepage URLs read from mod.json files
        private static HashSet<string> _homepages = new(StringComparer.OrdinalIgnoreCase);
        // Sanitized folder names of installed mods
        private static HashSet<string> _folderNames = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Rescans the Mods folder of the current game and refreshes the caches.
        /// </summary>
        public static void Refresh()
        {
            var homepages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var game = Global.config?.CurrentGame;
                if (!String.IsNullOrEmpty(game))
                {
                    var modsFolder = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{game}";
                    if (Directory.Exists(modsFolder))
                    {
                        foreach (var dir in Directory.GetDirectories(modsFolder))
                        {
                            folderNames.Add(Path.GetFileName(dir));
                            var metadataPath = $@"{dir}{Global.s}mod.json";
                            if (File.Exists(metadataPath))
                            {
                                try
                                {
                                    var metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText(metadataPath));
                                    if (metadata?.homepage != null)
                                        homepages.Add(metadata.homepage.ToString().TrimEnd('/'));
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            lock (_lock)
            {
                _homepages = homepages;
                _folderNames = folderNames;
            }
        }

        /// <summary>
        /// Returns true if a mod with the given GameBanana link or title
        /// is already present in the Mods folder.
        /// </summary>
        public static bool IsInstalled(Uri link, string title)
        {
            lock (_lock)
            {
                if (link != null && _homepages.Contains(link.ToString().TrimEnd('/')))
                    return true;
                if (!String.IsNullOrEmpty(title))
                {
                    var sanitized = string.Concat(title.Split(Path.GetInvalidFileNameChars()));
                    if (_folderNames.Contains(sanitized))
                        return true;
                    // Also match duplicate folders like "Title (2)"
                    foreach (var folder in _folderNames)
                        if (folder.StartsWith(sanitized + " (", StringComparison.OrdinalIgnoreCase))
                            return true;
                }
                return false;
            }
        }
    }
}
