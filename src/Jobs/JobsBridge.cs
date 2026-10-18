using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenseuerOS
{
    public class JobsSave
    {
        public List<string> Applied = new List<string>();
    }

    internal static class JobsBridge
    {
        private const string AppId = "jobs";
        private const string Sender = "Stalburg Web Jobs";
        internal const string StoreId = "nomad.obenseuer.os.jobs";
        private static JobsSave save = new JobsSave();

        internal static void Register()
        {
            SaveData.Register<JobsSave>(StoreId, () => save, loaded =>
            {
                save = loaded ?? new JobsSave();
                if (save.Applied == null) save.Applied = new List<string>();
            });
            Os.Handle(AppId, "jobs.list", JobList);
            Os.Handle(AppId, "jobs.apply", JobApply);
        }

        private static string JobList(string args)
        {
            List<object> rows = new List<object>();
            string place = PlaceOf();
            foreach (InteractableWaitWork job in Paid())
            {
                rows.Add(new { name = NameOf(job), oc = job.SalaryPerHour.OC, rm = job.SalaryPerHour.RM, skill = SkillOf(job), hardness = job.JobHardness, place = place, applied = save.Applied.Contains(KeyOf(job)) });
            }
            return JsonConvert.SerializeObject(new { jobs = rows });
        }

        private static string JobApply(string args)
        {
            JObject a = OsBridge.Args(args);
            string key = KeyOf(OsBridge.Text(a, "name"), OsBridge.Text(a, "place"), OsBridge.Span(a, "oc", int.MaxValue, -1), Rm(a));
            InteractableWaitWork job = Find(key);
            if (job == null) throw new OsBridgeException("not_found", "That job is not listed here any more. Reload the list.");
            if (save.Applied.Contains(key)) throw new OsBridgeException("denied", "You already applied for " + NameOf(job) + ". Your reply is in Stalburg Mail.");
            switch (OsMail.SendFrom(Os.ModId, Sender, "Your application for " + NameOf(job), Letter(job), 0))
            {
                case OsResult.Ok:
                    save.Applied.Add(key);
                    return "true";
                case OsResult.Busy:
                    throw new OsBridgeException("busy", "the game is saving or loading");
                case OsResult.Full:
                    throw new OsBridgeException("denied", "Your Stalburg Mail inbox is full. Delete some mail, then apply again.");
                default:
                    throw new OsBridgeException("failed", "the reply could not be sent");
            }
        }

        private static List<InteractableWaitWork> Paid()
        {
            List<InteractableWaitWork> list = new List<InteractableWaitWork>();
            foreach (InteractableWaitWork job in Object.FindObjectsByType<InteractableWaitWork>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (job.gameObject.scene == SceneManager.GetActiveScene() && (job.SalaryPerHour.OC > 0 || job.SalaryPerHour.RM > 0f)) list.Add(job);
            }
            list.Sort(Order);
            return list;
        }

        private static InteractableWaitWork Find(string key)
        {
            foreach (InteractableWaitWork job in Paid())
            {
                if (KeyOf(job) == key) return job;
            }
            return null;
        }

        private static float Rm(JObject a)
        {
            JToken t = a["rm"];
            if (t == null || (t.Type != JTokenType.Float && t.Type != JTokenType.Integer)) throw new OsBridgeException("bad_args", "rm must be a number");
            return (float)t;
        }

        private static int Order(InteractableWaitWork x, InteractableWaitWork y)
        {
            int c = string.CompareOrdinal(NameOf(x), NameOf(y));
            if (c != 0) return c;
            c = x.SalaryPerHour.OC.CompareTo(y.SalaryPerHour.OC);
            return c != 0 ? c : x.SalaryPerHour.RM.CompareTo(y.SalaryPerHour.RM);
        }

        private static string KeyOf(InteractableWaitWork job)
        {
            return KeyOf(NameOf(job), PlaceOf(), job.SalaryPerHour.OC, job.SalaryPerHour.RM);
        }

        private static string KeyOf(string name, string place, int oc, float rm)
        {
            return string.Join("|", new[] { name, place, oc.ToString(CultureInfo.InvariantCulture), rm.ToString("R", CultureInfo.InvariantCulture) });
        }

        private static string PlaceOf()
        {
            return ShopCatalog.AreaOf(SceneManager.GetActiveScene().name);
        }

        private static string NameOf(InteractableWaitWork job)
        {
            return string.IsNullOrEmpty(job.title) ? job.name : job.title;
        }

        private static string SkillOf(InteractableWaitWork job)
        {
            return job.Skill == PlayerStats.PlayerSkillType.None ? null : job.Skill.ToString();
        }

        private static string Letter(InteractableWaitWork job)
        {
            string pay = job.SalaryPerHour.OC + " OC" + (job.SalaryPerHour.RM > 0f ? " and " + job.SalaryPerHour.RM.ToString("#,0.00", CultureInfo.InvariantCulture).Replace(',', '\u202F').Replace('.', ',') + " RM" : "");
            return "Thank you for applying for " + NameOf(job) + ".\n\nGo to " + NameOf(job) + " in person and use it to start a shift. Pay is added while you work: " + pay + " per hour.";
        }
    }
}
