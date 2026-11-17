using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class OsBridgeException : Exception { internal readonly string Code; internal OsBridgeException(string code, string message) : base(message) { Code = code; } }
    internal static class OsBridge
    {
        internal static JObject Args(string args) { try { return JObject.Parse(args); } catch (Exception) { throw new OsBridgeException("bad_args", "args must be a JSON object"); } }
        internal static string Text(JObject o, string key) { JToken t = o[key]; return t != null && t.Type == JTokenType.String ? (string)t : null; }
        internal static int Span(JObject a, string key, int max, int fallback)
        {
            JToken t = a[key]; if (t == null) return fallback; long value;
            if (t.Type != JTokenType.Integer || !long.TryParse(t.ToString(Formatting.None), out value) || value < 0 || value > max) throw new OsBridgeException("bad_args", key + " is out of range");
            return (int)value;
        }
    }
    internal sealed class BetSave { internal int Seed, StartDay, Season; internal readonly List<BetRec> Bets = new List<BetRec>(); internal readonly List<FixtureRec> Fixtures = new List<FixtureRec>(); }
    internal sealed class BetRec { internal string Cid, Print; }
    internal sealed class FixtureRec { internal string Id; internal int Sport, Season, Round, Match, Home, Away, Kickoff, Odds1, OddsX, Odds2; }
}
