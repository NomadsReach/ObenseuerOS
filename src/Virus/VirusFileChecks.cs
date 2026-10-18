using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class FakeDisk : IVirusDisk
    {
        internal readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        internal bool Full;
        internal bool NoMove;

        public string Read(string path)
        {
            string text;
            return Files.TryGetValue(path, out text) ? text : null;
        }

        public bool Create(string path, string text)
        {
            if (Full || Files.ContainsKey(path) || !OsPaths.ValidPath(path)) return false;
            Files[path] = text;
            return true;
        }

        public bool Move(string from, string to)
        {
            if (NoMove || !Files.ContainsKey(from) || Files.ContainsKey(to) || !OsPaths.ValidPath(to)) return false;
            Files[to] = Files[from];
            Files.Remove(from);
            return true;
        }

        public bool Discard(string path)
        {
            return Files.Remove(path);
        }

        public string[] List(string dir)
        {
            List<string> names = new List<string>();
            foreach (string path in Files.Keys)
            {
                if (path.StartsWith(dir + "/", StringComparison.Ordinal) && path.IndexOf('/', dir.Length + 1) < 0) names.Add(path);
            }
            names.Sort(StringComparer.Ordinal.Compare);
            return names.ToArray();
        }

        public string[] ListAll(string dir)
        {
            return List(dir);
        }
    }

    internal static class VirusFileChecks
    {
        private const int Seed = 777;
        private const int Days = 40;
        private const int Count = 30;

        internal static List<KeyValuePair<string, bool>> Run()
        {
            List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
            checks.Add(Check("files: generated names pass OsPaths.ValidPath for rename, junk, stub, vault and restore", NamesOk()));
            checks.Add(Check("files: rename, junk, encrypt and hide over 40 days, then restore, gives back every file byte-identical, a taken name included", RoundTripOk()));
            checks.Add(Check("files: junk the player edited is kept on restore, unedited junk is removed, junk stops at 5", JunkOk()));
            checks.Add(Check("files: a disk full during an encrypt undoes the move, and a taken stub name does too", FullOk()));
            checks.Add(Check("files: a vault file with no record is moved to /docs/recovered_<n>.txt by restore", OrphanOk()));
            checks.Add(Check("files: a put-back that fails keeps its record and its vault file, and a later restore gives it back", PutBackFailOk()));
            checks.Add(Check("files: a taken 120-character name restores as /docs/restored_1.txt", LongNameOk()));
            return checks;
        }

        private static KeyValuePair<string, bool> Check(string name, bool ok)
        {
            return new KeyValuePair<string, bool>(name, ok);
        }

        private static bool NamesOk()
        {
            string[] files = { "/docs/notes.txt", "/docs/a.b.txt", "/docs/" + new string('a', 110) + ".txt", "/docs/Report (2).pdf", "/docs/a .txt", "/docs/x.t" };
            foreach (string file in files)
            {
                string[] names = { VirusRules.RenamePath(file), VirusRules.StubPath(file), VirusRules.RestoredPath(file, 1), VirusRules.RestoredPath(file, 2) };
                foreach (string name in names)
                {
                    if (name != null && !OsPaths.ValidPath(name)) return false;
                }
            }
            for (int seq = 1; seq <= 2000; seq++)
            {
                if (!OsPaths.ValidPath(VirusRules.JunkPath(seq)) || !OsPaths.ValidPath(VirusRules.VaultPath(seq))) return false;
            }
            return VirusRules.RenamePath("/docs/notes.txt") == "/docs/n0t3s_x.txt"
                && VirusRules.RenamePath("/docs/notes") == null
                && VirusRules.StubPath("/docs/notes.txt") == "/docs/notes.txt.lukko"
                && VirusRules.RestoredPath("/docs/notes.txt", 2) == "/docs/notes (restored 2).txt";
        }

        private static bool RoundTripOk()
        {
            FakeDisk disk = new FakeDisk();
            for (int i = 0; i < Count; i++)
            {
                disk.Files["/docs/report" + i + ".txt"] = "content of file " + i + " with some bytes";
            }
            disk.Files["/docs/notes.txt"] = "héllo, notes";
            Dictionary<string, string> originals = new Dictionary<string, string>(disk.Files);
            VirusSave save = new VirusSave();
            save.Active.Add(VirusRules.Sekaisin);
            save.Active.Add(VirusRules.Lukko);
            Random rnd = new Random(Seed);
            for (int day = 0; day < Days; day++)
            {
                for (int hour = 0; hour < 24; hour++) VirusFiles.Hour(disk, save, day, rnd);
            }
            if (!HasKind(save, VirusFiles.KindRename) || !HasKind(save, VirusFiles.KindEncrypt) || !HasKind(save, VirusFiles.KindHide)) return false;
            string taken = save.Moves[0].Original;
            if (!disk.Create(taken, "player wrote this")) return false;
            VirusFiles.Restore(disk, save);
            if (save.Moves.Count != 0 || save.Junk.Count != 0) return false;
            foreach (KeyValuePair<string, string> item in originals)
            {
                if (item.Key != taken && disk.Read(item.Key) != item.Value) return false;
            }
            string copy = VirusRules.RestoredPath(taken, 1);
            return disk.Read(taken) == "player wrote this"
                && copy != null && disk.Read(copy) == originals[taken]
                && disk.Files.Count == originals.Count + 1;
        }

        private static bool JunkOk()
        {
            FakeDisk disk = new FakeDisk();
            VirusSave save = new VirusSave();
            for (int i = 0; i < 8; i++) VirusFiles.DropJunk(disk, save);
            if (save.Junk.Count != 5 || disk.Files.Count != 5) return false;
            string edited = save.Junk[0].Path;
            disk.Files[edited] = "my own notes";
            VirusFiles.Restore(disk, save);
            return save.Junk.Count == 0 && disk.Read(edited) == "my own notes" && disk.Files.Count == 1;
        }

        private static bool FullOk()
        {
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/a.txt"] = "alpha";
            disk.Files["/docs/b.txt"] = "bravo";
            disk.Files["/docs/b.txt.lukko"] = "player";
            VirusSave save = new VirusSave();
            disk.Full = true;
            bool full = VirusFiles.Scramble(disk, save, "/docs/a.txt");
            disk.Full = false;
            bool taken = VirusFiles.Scramble(disk, save, "/docs/b.txt");
            return !full && !taken
                && disk.Read("/docs/a.txt") == "alpha"
                && disk.Read("/docs/b.txt") == "bravo"
                && disk.Read("/docs/b.txt.lukko") == "player"
                && disk.Files.Count == 3
                && save.Moves.Count == 0 && save.Junk.Count == 0 && save.VaultSeq == 0;
        }

        private static bool OrphanOk()
        {
            FakeDisk disk = new FakeDisk();
            disk.Files["/vault/v7.dat"] = "lost file";
            disk.Files["/docs/recovered_1.txt"] = "player wrote this";
            VirusFiles.Restore(disk, new VirusSave());
            return disk.Read("/vault/v7.dat") == null
                && disk.Read("/docs/recovered_1.txt") == "player wrote this"
                && disk.Read("/docs/recovered_2.txt") == "lost file";
        }

        private static bool PutBackFailOk()
        {
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/a.txt"] = "alpha";
            VirusSave save = new VirusSave();
            if (!VirusFiles.Stash(disk, save, "/docs/a.txt")) return false;
            disk.NoMove = true;
            VirusFiles.Restore(disk, save);
            bool kept = save.Moves.Count == 1 && disk.Read("/vault/v1.dat") == "alpha" && disk.Read("/docs/a.txt") == null;
            disk.NoMove = false;
            VirusFiles.Restore(disk, save);
            return kept && save.Moves.Count == 0 && disk.Read("/docs/a.txt") == "alpha" && disk.Files.Count == 1;
        }

        private static bool LongNameOk()
        {
            FakeDisk disk = new FakeDisk();
            string original = "/docs/" + new string('a', 110) + ".txt";
            disk.Files[original] = "long name";
            VirusSave save = new VirusSave();
            if (!VirusFiles.Stash(disk, save, original)) return false;
            disk.Files[original] = "player wrote this";
            VirusFiles.Restore(disk, save);
            return save.Moves.Count == 0 && disk.Read(original) == "player wrote this" && disk.Read("/docs/restored_1.txt") == "long name";
        }

        private static bool HasKind(VirusSave save, string kind)
        {
            foreach (VirusMove move in save.Moves)
            {
                if (move.Kind == kind) return true;
            }
            return false;
        }
    }
}
