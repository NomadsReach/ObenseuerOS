using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static partial class BetBridge
    {
        private const int MaxCid = 64;
        internal static void CheckExpectedOdds(JObject args, int current)
        {
            if (args["expectedOdds"] == null) return;
            int expected = OsBridge.Span(args, "expectedOdds", int.MaxValue, 0);
            if (expected < BetModel.MinOddsX100) throw new OsBridgeException("bad_args", "expectedOdds is invalid");
            if (expected != current) throw new OsBridgeException("denied", "The odds changed. Review the new quote before placing this bet.");
        }
        internal static string ResultFor(BetSave state, string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = CidArg(a); int sport; string fixture = FixtureArg(a, out sport);
            string pick = PickArg(a, sport); int stake = StakeArg(a);
            return new JObject(new JProperty("found", BetBook.Saved(state, cid, fixture + "|" + pick + "|" + stake) != null)).ToString(Formatting.None);
        }
        private static string CidArg(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }
        private static string FixtureArg(JObject a, out int sport)
        {
            string id = OsBridge.Text(a, "fixture"); int season, round, match;
            if (!BetSchedule.TryParse(id, out sport, out season, out round, out match)) throw new OsBridgeException("bad_args", "fixture is not a match id");
            return id;
        }
        private static string PickArg(JObject a, int sport)
        {
            string pick = OsBridge.Text(a, "pick");
            if (!BetSchedule.PickValid(sport, pick)) throw new OsBridgeException("bad_args", "pick is not valid for this sport");
            return pick;
        }
        private static int StakeArg(JObject a)
        {
            JToken token = a["stake"];
            if (token == null || token.Type != JTokenType.Integer) throw new OsBridgeException("bad_args", "stake must be a whole number of OC");
            long value;
            if (!long.TryParse(token.ToString(Formatting.None), out value)) value = long.MaxValue;
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }
    }
    internal static partial class BetBook
    {
        internal const string ConflictText = "This bet was already placed with different contents.";
        internal static BetRec Saved(BetSave state, string cid, string print)
        {
            foreach (BetRec bet in state.Bets)
            {
                if (bet.Cid != cid) continue;
                if (bet.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return bet;
            }
            return null;
        }
    }
}
