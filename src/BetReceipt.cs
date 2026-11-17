using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class BetReceipt
    {
        internal static bool SameOrder(JObject left, JObject right)
        {
            return left != null && right != null && left["fixture"] != null && left["pick"] != null && left["stake"] != null && JToken.DeepEquals(left["fixture"], right["fixture"]) && JToken.DeepEquals(left["pick"], right["pick"]) && JToken.DeepEquals(left["stake"], right["stake"]);
        }
        internal static bool Valid(JToken result, JObject request)
        {
            JObject bet = result is JObject ? result["bet"] as JObject : null;
            if (bet == null || bet["n"] == null || bet["n"].Type != JTokenType.Integer || (long)bet["n"] < 1) return false;
            if (!JToken.DeepEquals(bet["fixture"], request["fixture"]) || !JToken.DeepEquals(bet["pick"], request["pick"]) || !JToken.DeepEquals(bet["stake"], request["stake"]) || bet["stake"] == null || bet["stake"].Type != JTokenType.Integer || bet["odds"] == null || bet["odds"].Type != JTokenType.Integer || bet["ret"] == null || bet["ret"].Type != JTokenType.Integer) return false;
            long stake = (long)bet["stake"], odds = (long)bet["odds"], resultValue = (long)bet["ret"];
            return stake >= 10 && stake <= 5000 && odds >= 101 && odds <= int.MaxValue && resultValue == stake * odds / 100 && JToken.DeepEquals(bet["odds"], request["expectedOdds"]);
        }
    }
}
