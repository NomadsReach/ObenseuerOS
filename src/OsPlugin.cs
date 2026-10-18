using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using ObenseuerCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenseuerOS
{
    [BepInPlugin(Os.ModId, "ObenseuerOS", "0.2.0")]
#if BEPINEX5
    [BepInDependency("obenseuer.moddingframework", "0.10.0")]
    [BepInDependency("obenseuer.moddingframework.webui", "0.10.0")]
#else
    [BepInDependency("obenseuer.moddingframework", ">=0.10.0")]
    [BepInDependency("obenseuer.moddingframework.webui", ">=0.10.0")]
#endif
    public class OsPlugin : BaseUnityPlugin
    {
        private const string DeskTitle = "Viewports 8";
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static OsPlugin instance;
        private static readonly FieldInfo CharacterField = typeof(SaveController).GetField("CharacterName", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo SaveField = typeof(SaveController).GetField("SaveName", BindingFlags.Static | BindingFlags.NonPublic);
        private static string playingSave;

        private void Awake()
        {
            instance = this;
            try
            {
                OsLaptopPrompt.Attach(
                    Config.Bind("Laptop", "ExtraNames", "", "Extra GameObject names to use as laptops, comma separated.").Value,
                    Config.Bind("Debug", "LogLookTarget", false, "Log each object the look ray hits: path, layer, usable layer, colliders and interactables.").Value);
            }
            catch (System.Exception e)
            {
                Log.Error("laptop prompt off: " + e);
            }
            if (Config.Bind("Debug", "SelfCheck", false, "Run the file path and limit self-check at start and log the result.").Value)
            {
                string error = OsFiles.SelfCheck() ?? OsBridge.SelfCheck() ?? ShopSelfTest.Run() ?? OsBank.SelfCheck() ?? OsMail.SelfCheck() ?? LottoSelfTest.Run() ?? BetSelfTest.Run() ?? StalExBridge.SelfCheck() ?? MarketMath.SelfCheck() ?? ChatSelfTest.Run() ?? NewsSelfTest.Run() ?? OsScreenFinder.SelfCheck() ?? VirusSelfTest.Run() ?? WikiSelfTest.Failure();
                Log.Info(error == null ? "self-check ok" : "self-check failed: " + error);
            }
            Setup("files", () => OsFiles.Attach());
            Setup("shop gates", () => ShopGates.Attach());
            Setup("shop orders", () => ShopOrders.Attach());
            Setup("bet book", () => BetBook.Attach());
            Setup("mail", () => OsMail.Attach());
            Setup("bank", () => OsBank.Attach(Config.Bind("Bank", "WithdrawFeePercent", 2f, "Fee taken from a cash order by post, as a percent of the amount (at least one unit).").Value));
            Setup("market", () => MarketHost.Attach(Config.Bind("Market", "Currency", MarketHost.LegacyCurrency() ?? "RM", new ConfigDescription("Money the market trades in. Changing it closes open positions at the next load and pays them back in the old currency.", new AcceptableValueList<string>("RM", "OC"))).Value));
            Setup("stal-ex", () => StalExBridge.Attach());
            Setup("wiki", () => WikiBridge.Attach(
                Config.Bind("Wiki", "Enabled", true, "Allow the Stalburg Wiki to contact stalburg.net. Off keeps the wiki offline even after the player allows it.").Value,
                Config.Bind("Wiki", "CacheMaxMB", 128, "Most space the saved wiki copy may use, in megabytes. The oldest pages are removed first.").Value,
                Config.Bind("Wiki", "MinIntervalMs", 1000, "Least time between two requests to stalburg.net, in milliseconds. Values below 500 are raised to 500.").Value));
            Setup("viruses", () => VirusCore.Attach(
                Config.Bind("Viruses", "Enabled", true, "Computer viruses, pop-ups and infections. Off stops new infections and cleans existing ones, restoring files; a bought antivirus keeps working.").Value,
                Config.Bind("Viruses", "MoneyTheft", true, "Let viruses take small amounts of OC from the wallet and RM from a bank account, within daily and weekly caps.").Value));
            Setup("virus effects", () => VirusFx.Attach());
            Setup("virus sites", () => VirusSites.Attach());
            Setup("antivirus", () => VirusAv.Attach());
            Setup("virus mail", () => VirusMail.Attach());
            Setup("virus money", () => VirusMoney.Attach());
            Setup("lotto", () => LottoGame.Attach());
            Setup("chat", () => ChatGame.Attach());
            Setup("news", () => NewsBridge.Attach());
            Setup("built-in apps", () => Os.RegisterBuiltIns());
            Setup("bets", () => BetBridge.Register());
            Events.SceneLoaded += scene =>
            {
                OsLaptop.Close(true);
                OsView.Dismiss();
            };
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (mode == LoadSceneMode.Single) OsView.Reset(scene.name == "Main Menu");
            };
            SaveController.PlayerWillLoadGame += OnSaveLoad;
            ComputerApps.ScreenChanged += OsView.OnScreenChanged;
            Cash.MoneyChanged += OsView.OnMoneyChanged;
            if (!ComputerApps.Register(Os.ModId, Os.AppId, DeskTitle, null, host => OsView.Show())) Log.Warn("OS not added to the computer");
        }

        public static bool OpenLaptop()
        {
            return OsView.Show();
        }

        public static bool OpenLaptop(GameObject laptop)
        {
            return OsLaptop.Open(instance, laptop) || OsView.Show();
        }

        private void Update()
        {
            Tick("view", OsView.Tick);
            Tick("laptop", OsLaptop.Tick);
            Tick("bank", OsBank.Tick);
            Tick("mail", OsMail.Tick);
            Tick("chat", ChatGame.Tick);
            Tick("wiki", WikiBridge.Tick);
            Tick("virus effects", VirusFx.Tick);
            Tick("viruses", VirusCore.Tick);
        }

        private void OnDisable()
        {
            if (!OsLaptop.Active) return;
            OsLaptop.Close(true);
            OsView.Dismiss();
        }

        private static void OnSaveLoad()
        {
            if (SaveController.Loading) return;
            string save = CharacterField.GetValue(null) + "/" + SaveField.GetValue(null);
            if (save == playingSave) return;
            playingSave = save;
            OsView.Reset(true);
        }

        private static void Setup(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Os.Crash("OS setup " + name, e);
            }
        }

        private static void Tick(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Os.Crash("OS tick " + name, e);
            }
        }
    }
}
