using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class VirusCore
    {
        internal const string AppId = "virusfx";
        private const string StoreId = "nomad.obenseuer.os.virus";
        private const string TurvaToast = "TurvaPro 2000: your PC is protected!";
        private static readonly Random Rnd = new Random();
        private static VirusSave state = new VirusSave();
        private static bool cleanPending = true;

        internal static bool Enabled { get; private set; }

        internal static bool MoneyTheft { get; private set; }

        internal static List<string> Active
        {
            get
            {
                return state.Active;
            }
        }

        internal static bool CheapLapsed
        {
            get
            {
                return state.CheapOwned && state.CheapLapsed;
            }
        }

        internal static void Attach(bool enabled, bool moneyTheft)
        {
            Enabled = enabled;
            MoneyTheft = moneyTheft;
            SaveData.Register<VirusSave>(StoreId, () => state, loaded => { state = loaded ?? new VirusSave(); cleanPending = true; OsView.Refresh(); });
            Events.SceneLoaded += scene => { if (scene == "Main Menu") state = new VirusSave(); };
            Events.HourPassed += hour => Tamper();
            Os.Handle(AppId, "virus.state", StateNow);
        }

        internal static VirusSave State
        {
            get
            {
                return state;
            }
        }

        internal static VirusDecision Infect(VirusVector vector, string source)
        {
            return Roll(vector, source, (protect, rnd) => VirusRules.Decide(state, Enabled, vector, protect, rnd));
        }

        internal static VirusDecision Browse(bool shady, string source)
        {
            VirusVector vector = shady ? VirusVector.ShadyPageView : VirusVector.OtherPageView;
            return Roll(vector, source, (protect, rnd) => VirusRules.Browse(state, Enabled, shady, WorldClock.Now.Day, protect, rnd));
        }

        private static VirusDecision Roll(VirusVector vector, string source, Func<VirusProtect, Random, VirusDecision> decide)
        {
            if (!OsBridge.Writable() || !OsBridge.WorldReady()) return new VirusDecision(VirusOutcome.Clean, null);
            if (state.Seed == 0) state.Seed = new Random().Next(1, int.MaxValue);
            Random rnd = new Random(state.Seed);
            VirusProtect protect = VirusRules.ProtectionOf(state, vector);
            VirusDecision decision = decide(protect, rnd);
            state.Seed = rnd.Next(1, int.MaxValue);
            if (decision.Outcome == VirusOutcome.Infected)
            {
                state.Active.Add(decision.Id);
                Changed(false);
                if (protect == VirusProtect.Cheap) Toast(TurvaToast);
            }
            else if (VirusRules.CountRealBlock(state, decision, protect)) Toast("PC Master Guard blocked " + VirusRules.Name(decision.Id) + " from " + source);
            return decision;
        }

        internal static void CleanAll(bool restoreFiles)
        {
            if (!OsBridge.Writable() || !OsBridge.WorldReady()) return;
            if (restoreFiles) VirusFiles.Restore(new Disk(), state);
            if (VirusAvRules.ClearInfections(state)) Changed(false);
        }

        internal static void Tick()
        {
            if (!cleanPending || Enabled || !OsBridge.Writable() || !OsBridge.WorldReady()) return;
            cleanPending = false;
            CleanAll(true);
        }

        internal static string StateJson(VirusSave save)
        {
            VirusEffects fx = Enabled ? VirusRules.Effects(save.Active, save.CheapOwned && save.CheapLapsed) : new VirusEffects();
            return JsonConvert.SerializeObject(new
            {
                enabled = Enabled,
                fx = new { popups = fx.Popups, lag = fx.Lag, wall = fx.Wall, shuffle = fx.Shuffle, errors = fx.Errors, nag = fx.Nag },
                protect = new { real = save.RealOwned, cheap = CheapState(save) },
            });
        }

        private static string StateNow(string args)
        {
            return StateJson(state);
        }

        private static string CheapState(VirusSave save)
        {
            if (!save.CheapOwned) return "none";
            return save.CheapLapsed ? "lapsed" : "active";
        }

        private static void Tamper()
        {
            if (!Enabled || !OsView.Visible || !OsBridge.Writable() || !OsBridge.WorldReady()) return;
            VirusFiles.Hour(new Disk(), state, WorldClock.Now.Day, Rnd);
        }

        internal static void Changed(bool reset)
        {
            OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "event"), new JProperty("app", AppId), new JProperty("name", "state"), new JProperty("reset", reset)));
        }

        internal static void Toast(string title)
        {
            OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "toast"), new JProperty("title", title)));
        }

        internal sealed class Disk : IVirusDisk
        {
            public string Read(string path)
            {
                return OsFiles.Read(path);
            }

            public bool Create(string path, string text)
            {
                return OsFiles.Read(path) == null && OsFiles.Write(path, text);
            }

            public bool Move(string from, string to)
            {
                return OsFiles.Move(from, to);
            }

            public bool Discard(string path)
            {
                return OsFiles.Discard(path);
            }

            public string[] List(string dir)
            {
                return OsFiles.List(dir);
            }

            public string[] ListAll(string dir)
            {
                return OsFiles.ListAll(dir);
            }
        }
    }
}
