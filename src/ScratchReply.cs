using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class ScratchReply
    {
        internal static bool Valid(JObject reply, int id, string kind)
        {
            JObject card = reply == null ? null : reply["card"] as JObject;
            if (card == null || card["id"] == null || card["id"].Type != JTokenType.Integer || (long)card["id"] != id || card["kind"] == null || card["kind"].Type != JTokenType.String || (string)card["kind"] != kind) return false;
            string currency = kind == "oc" ? "OC" : kind == "spade" ? "RM" : null;
            return currency != null && card["revealed"] != null && card["revealed"].Type == JTokenType.Boolean && (bool)card["revealed"] && card["prize"] != null && card["prize"].Type == JTokenType.Integer && card["currency"] != null && card["currency"].Type == JTokenType.String && (string)card["currency"] == currency && reply["balance"] != null && reply["balance"].Type == JTokenType.Integer && reply["rm"] != null && (reply["rm"].Type == JTokenType.Integer || reply["rm"].Type == JTokenType.Float);
        }
    }
}
