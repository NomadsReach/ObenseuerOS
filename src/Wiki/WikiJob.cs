using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class WikiJob
    {
        internal string Id;
        internal string Key;
        internal bool Done;
        internal string Result;
        internal string Code;
        internal string Message;
        internal int Steps;
        internal int Total;
    }

    internal static class WikiJobs
    {
        private const int MaxJobs = 32;
        internal const string DeniedText = "The Stalburg Wiki is off. Allow it in the site to fetch pages.";
        private const string BusyText = "The wiki is busy. Try again in a moment.";
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly List<WikiJob> All = new List<WikiJob>();
        private static int seq;

        internal static WikiJob Start(string key)
        {
            seq++;
            WikiJob job = new WikiJob { Id = seq.ToString(CultureInfo.InvariantCulture), Key = key };
            All.Add(job);
            if (All.Count > MaxJobs) All.RemoveAll(j => j.Done);
            return job;
        }

        internal static WikiJob Open(string key)
        {
            return All.Find(j => !j.Done && j.Key == key);
        }

        internal static WikiJob Find(string id)
        {
            return id == null ? null : All.Find(j => j.Id == id);
        }

        internal static void Fail(WikiJob job, string code, string message)
        {
            job.Code = code;
            job.Message = message;
            job.Done = true;
        }

        internal static void FailKind(WikiJob job, string kind)
        {
            if (kind == WikiResponse.Network) Fail(job, "offline", "The wiki could not be reached, and no saved copy exists.");
            else if (kind == WikiResponse.Timeout) Fail(job, "timeout", "The wiki took too long to answer.");
            else if (kind == WikiResponse.NotFound) Fail(job, "not_found", "The wiki has no such page or file.");
            else if (kind == WikiResponse.RateLimited || kind == WikiResponse.Challenge || kind == WikiResponse.Maxlag) Fail(job, "rate_limited", PauseText());
            else if (kind == WikiResponse.Dropped) Fail(job, "denied", DeniedText);
            else if (kind == WikiResponse.Blocked) Fail(job, "blocked", "That request is not allowed on the wiki.");
            else if (kind == WikiResponse.TooLarge) Fail(job, "too_large", "This page is too large to save.");
            else Fail(job, "bad_response", "The wiki sent a reply this mod cannot use.");
        }

        internal static void FailRefused(WikiJob job, string kind)
        {
            if (kind == WikiNet.Busy) Fail(job, "busy", BusyText);
            else if (kind == WikiResponse.RateLimited) Fail(job, "rate_limited", PauseText());
            else FailKind(job, kind);
        }

        internal static void Send(WikiJob job, string url, int cap, bool json, WikiNet.Lane lane, Action<WikiReply> then)
        {
            string refused = WikiNet.Send(url, cap, json, lane, reply => Guard(job, () => then(reply)));
            if (refused != null) FailRefused(job, refused);
        }

        internal static void Robots(WikiJob job, Action then)
        {
            if (WikiRobots.Fresh(WikiNet.Now()))
            {
                then();
                return;
            }
            Send(job, WikiTitle.Origin + WikiRobots.RobotsPath, WikiRobots.Cap, false, WikiNet.Lane.Front, reply => Loaded(job, reply, then));
        }

        private static void Loaded(WikiJob job, WikiReply reply, Action then)
        {
            if (reply.Kind == WikiResponse.Ok) WikiRobots.Set(Encoding.UTF8.GetString(reply.Body), WikiNet.Now());
            else if (reply.Kind == WikiResponse.NotFound) WikiRobots.Set("", WikiNet.Now());
            else
            {
                FailKind(job, reply.Kind);
                return;
            }
            then();
        }

        internal static void Guard(WikiJob job, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Log.Warn("wiki reply could not be read: " + e.Message);
                Fail(job, "bad_response", "The wiki sent a reply this mod cannot use.");
            }
        }

        internal static string ReadyJson(WikiPage page, bool stale)
        {
            return new JObject(new JProperty("state", "ready"), new JProperty("title", page.Title), new JProperty("display", page.Title.Replace('_', ' ')), new JProperty("file", page.File), new JProperty("revid", page.RevId), new JProperty("fetched", page.Fetched), new JProperty("stale", stale)).ToString(Formatting.None);
        }

        private static string PauseText()
        {
            return "The wiki asked for a pause. Try again in " + Wait(WikiNet.CooldownLeft) + ".";
        }

        private static string Wait(long seconds)
        {
            if (seconds >= 3600)
            {
                long hours = (seconds + 3599) / 3600;
                return hours + (hours == 1 ? " hour" : " hours");
            }
            long minutes = Math.Max(1L, (seconds + 59) / 60);
            return minutes + (minutes == 1 ? " minute" : " minutes");
        }
    }
}
