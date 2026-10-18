using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class VirusFx
    {
        private const float GraceSeconds = 30f;
        private const float IntervalSeconds = 20f;
        private static readonly Random Rnd = new Random();
        private static float nextAt = -1f;

        internal static void Attach()
        {
            Os.Handle(VirusCore.AppId, "virus.claim", Claim);
        }

        internal static void Tick()
        {
            if (!OsView.Visible)
            {
                nextAt = -1f;
                return;
            }
            float now = UnityEngine.Time.unscaledTime;
            if (nextAt < 0f)
            {
                nextAt = now + GraceSeconds;
                VirusCore.Changed(true);
            }
            if (now < nextAt) return;
            nextAt = now + IntervalSeconds;
            if (VirusCore.Enabled && OsBridge.Writable() && OsBridge.WorldReady()) Pulse();
        }

        private static void Pulse()
        {
            VirusPulse pulse = VirusRules.Pulse(VirusCore.Active, VirusCore.CheapLapsed, Rnd);
            if (pulse.Popups == 0 && !pulse.Error && !pulse.Nag) return;
            OsView.Deliver(new JObject(
                new JProperty("v", 1),
                new JProperty("type", "event"),
                new JProperty("app", VirusCore.AppId),
                new JProperty("name", "pulse"),
                new JProperty("popups", pulse.Popups),
                new JProperty("error", pulse.Error),
                new JProperty("nag", pulse.Nag)));
        }

        private static string Claim(string args)
        {
            JObject a = OsBridge.Args(args);
            string kind = OsBridge.Text(a, "kind");
            if (kind != "winner" && kind != "moca") throw new OsBridgeException("bad_args", "kind must be winner or moca");
            OsBridge.CheckWritable();
            VirusDecision decision = VirusCore.Infect(VirusVector.PopupClaim, "a pop-up");
            return JsonConvert.SerializeObject(new { outcome = decision.Outcome.ToString().ToLowerInvariant() });
        }
    }
}
