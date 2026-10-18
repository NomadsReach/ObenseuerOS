using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using ObenseuerOS;

namespace Example.Hello
{
    [BepInPlugin("example.hello", "Hello OS", "1.0.0")]
#if BEPINEX5
    [BepInDependency("obenseuer.moddingframework", "0.10.0")]
#else
    [BepInDependency("obenseuer.moddingframework", ">=0.10.0")]
#endif
    [BepInDependency("nomad.obenseuer.os", BepInDependency.DependencyFlags.SoftDependency)]
    public class HelloPlugin : BaseUnityPlugin
    {
        private static readonly CoreLog Log = CoreLog.For("example.hello");

        private void Awake()
        {
            if (Loader.Find("nomad.obenseuer.os") == null) return;
            try
            {
                HelloOs.Register(Path.GetDirectoryName(typeof(HelloPlugin).Assembly.Location));
            }
            catch (Exception e)
            {
                Log.Warn("OS features off: " + e.GetType().Name);
            }
        }
    }

    internal static class HelloOs
    {
        private const string ModId = "example.hello";
        private const string AppId = "hello";
        private const string SiteId = "hello-site";

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Register(string dir)
        {
            string web = Path.Combine(dir, "web");
            OsAppOptions app = new OsAppOptions();
            app.Title = "Hello";
            app.Folder = web;
            app.Icon = "icon.svg";
            app.Width = 420;
            app.Height = 300;
            if (!Os.RegisterApp(ModId, AppId, app)) return;
            Os.Handle(ModId, AppId, "greet", Greet);
            OsSiteOptions site = new OsSiteOptions();
            site.Host = "www.hello.ts";
            site.Title = "Hello";
            site.Description = "The example site of the Hello OS mod.";
            site.Keywords = new string[] { "hello", "example" };
            site.Folder = web;
            site.Page = "site.html";
            Os.RegisterSite(ModId, SiteId, site);
            Os.AddNewsSource(ModId, "Local", Story);
            Events.HourPassed += OnHour;
        }

        private static string Greet(string args)
        {
            string name = Os.UserName;
            return new JObject(new JProperty("text", "Hello, " + (name ?? "stranger") + ".")).ToString(Formatting.None);
        }

        private static void OnHour(int hour)
        {
            Os.Emit(ModId, AppId, "hour", new JObject(new JProperty("hour", hour)).ToString(Formatting.None));
        }

        private static OsNewsStory[] Story(int day)
        {
            OsNewsStory story = new OsNewsStory();
            story.Headline = "Hello OS example runs";
            story.Body = "The example mod wrote this story for day " + day + ".";
            return new OsNewsStory[] { story };
        }
    }
}
