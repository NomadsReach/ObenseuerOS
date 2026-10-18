using System;
using System.Collections.Generic;
using System.IO;

namespace ObenseuerOS
{
    internal static class ShopVisits
    {
        internal static HashSet<string> Levels(string folder)
        {
            HashSet<string> levels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return levels;
            foreach (string path in Directory.GetFiles(folder, "*.tnmt")) levels.Add(Path.GetFileNameWithoutExtension(path));
            return levels;
        }
    }
}
