using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal interface IVirusDisk
    {
        string Read(string path);
        bool Create(string path, string text);
        bool Move(string from, string to);
        bool Discard(string path);
        string[] List(string dir);
        string[] ListAll(string dir);
    }

    internal sealed class VirusRestored
    {
        internal int Put;
        internal int Junk;
    }

    internal static class VirusFiles
    {
        internal const string KindRename = "rename";
        internal const string KindEncrypt = "encrypt";
        internal const string KindHide = "hide";
        internal const string JunkText = "Sekaisin was here. Your PC is fine. Get the full cleaner at www.kolhola-warez.ts\n";
        internal const string StubText = "YOUR FILES ARE LOCKED BY LUKKO.LOCKER\n\nTo get them back, send 500 OC to the address below and wait for the key.\nDo not open or rename the locked files, they will be lost.\n\nkey: 7fQ2-Zx9K-pLm0-Wv3R-8eHc\n\n#@!% ~~ b4Z+ Qq9/ x0Fk Tr71 ##hv ==Lm ^^wz 0p8N\n";
        private const int WormPercent = 50;
        private const int RenamesPerDay = 3;
        private const int RenamesTotal = 10;
        private const int JunkMax = 5;
        private const int EncryptMax = 8;
        private const int HideMax = 4;
        private const int MaxRestoredNames = 99;

        internal static void Hour(IVirusDisk disk, VirusSave save, int day, Random rnd)
        {
            if (save.Active.Contains(VirusRules.Sekaisin) && rnd.Next(100) < WormPercent) Worm(disk, save, day, rnd);
            if (save.Active.Contains(VirusRules.Lukko) && save.LockDay != day) Locker(disk, save, day, rnd);
        }

        internal static void DropJunk(IVirusDisk disk, VirusSave save)
        {
            if (save.Junk.Count >= JunkMax) return;
            save.JunkSeq++;
            string path = VirusRules.JunkPath(save.JunkSeq);
            if (!disk.Create(path, JunkText)) return;
            save.Junk.Add(new VirusJunk { Path = path, Text = JunkText });
        }

        internal static bool Scramble(IVirusDisk disk, VirusSave save, string file)
        {
            string stub = VirusRules.StubPath(file);
            if (stub == null) return false;
            string vault = VirusRules.VaultPath(save.VaultSeq + 1);
            if (!disk.Move(file, vault)) return false;
            if (!disk.Create(stub, StubText))
            {
                disk.Move(vault, file);
                return false;
            }
            save.VaultSeq++;
            save.Moves.Add(new VirusMove { Kind = KindEncrypt, Original = file, Now = vault });
            save.Junk.Add(new VirusJunk { Path = stub, Text = StubText });
            return true;
        }

        internal static bool Stash(IVirusDisk disk, VirusSave save, string file)
        {
            string vault = VirusRules.VaultPath(save.VaultSeq + 1);
            if (!disk.Move(file, vault)) return false;
            save.VaultSeq++;
            save.Moves.Add(new VirusMove { Kind = KindHide, Original = file, Now = vault });
            return true;
        }

        internal static VirusRestored Restore(IVirusDisk disk, VirusSave save)
        {
            VirusRestored done = new VirusRestored();
            for (int i = save.Junk.Count - 1; i >= 0; i--)
            {
                VirusJunk junk = save.Junk[i];
                if (disk.Read(junk.Path) != junk.Text) save.Junk.RemoveAt(i);
                else if (disk.Discard(junk.Path))
                {
                    done.Junk++;
                    save.Junk.RemoveAt(i);
                }
            }
            for (int i = save.Moves.Count - 1; i >= 0; i--)
            {
                VirusMove move = save.Moves[i];
                if (disk.Read(move.Now) == null) save.Moves.RemoveAt(i);
                else if (PutBack(disk, move))
                {
                    done.Put++;
                    save.Moves.RemoveAt(i);
                }
            }
            Recover(disk, save);
            return done;
        }

        private static void Recover(IVirusDisk disk, VirusSave save)
        {
            foreach (string vault in disk.ListAll(VirusRules.Vault))
            {
                string target = Tracked(save, vault) ? null : FreeRecovered(disk);
                if (target != null) disk.Move(vault, target);
            }
        }

        private static string FreeRecovered(IVirusDisk disk)
        {
            for (int n = 1; n <= MaxRestoredNames; n++)
            {
                string candidate = VirusRules.RecoveredPath(n);
                if (disk.Read(candidate) == null) return candidate;
            }
            return null;
        }

        private static void Worm(IVirusDisk disk, VirusSave save, int day, Random rnd)
        {
            if (rnd.Next(2) == 0)
            {
                DropJunk(disk, save);
                return;
            }
            if (save.RenameDay != day)
            {
                save.RenameDay = day;
                save.RenamesToday = 0;
            }
            if (save.RenamesToday >= RenamesPerDay || Count(save, KindRename) >= RenamesTotal) return;
            string file = Pick(disk, save, rnd);
            string target = file == null ? null : VirusRules.RenamePath(file);
            if (target == null || !disk.Move(file, target)) return;
            save.Moves.Add(new VirusMove { Kind = KindRename, Original = file, Now = target });
            save.RenamesToday++;
        }

        private static void Locker(IVirusDisk disk, VirusSave save, int day, Random rnd)
        {
            string file = Pick(disk, save, rnd);
            if (file == null) return;
            bool scramble = rnd.Next(2) == 0;
            if (Count(save, scramble ? KindEncrypt : KindHide) >= (scramble ? EncryptMax : HideMax)) return;
            bool done = scramble ? Scramble(disk, save, file) : Stash(disk, save, file);
            if (done) save.LockDay = day;
        }

        private static bool PutBack(IVirusDisk disk, VirusMove move)
        {
            string target = FreeName(disk, move.Original);
            return target != null && disk.Move(move.Now, target);
        }

        private static string FreeName(IVirusDisk disk, string original)
        {
            if (disk.Read(original) == null) return original;
            for (int n = 1; n <= MaxRestoredNames; n++)
            {
                string candidate = VirusRules.RestoredPath(original, n);
                if (candidate != null && disk.Read(candidate) == null) return candidate;
            }
            return null;
        }

        private static string Pick(IVirusDisk disk, VirusSave save, Random rnd)
        {
            List<string> files = new List<string>();
            foreach (string path in disk.List(VirusRules.Docs))
            {
                if (!Tracked(save, path)) files.Add(path);
            }
            return files.Count == 0 ? null : files[rnd.Next(files.Count)];
        }

        private static bool Tracked(VirusSave save, string path)
        {
            foreach (VirusMove move in save.Moves)
            {
                if (move.Now == path) return true;
            }
            foreach (VirusJunk junk in save.Junk)
            {
                if (junk.Path == path) return true;
            }
            return false;
        }

        private static int Count(VirusSave save, string kind)
        {
            int n = 0;
            foreach (VirusMove move in save.Moves)
            {
                if (move.Kind == kind) n++;
            }
            return n;
        }
    }
}
