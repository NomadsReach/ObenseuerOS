using Newtonsoft.Json.Linq;
namespace ObenseuerOS
{
    internal static class TurvaReceipt
    {
        internal static bool ScanAccepted(JToken receipt)
        {
            if (!(receipt is JObject) || !(receipt["found"] is JArray) || receipt["cookies"] == null || receipt["cookies"].Type != JTokenType.Integer) return false;
            foreach (JToken item in (JArray)receipt["found"])
                if (!(item is JObject) || item["name"] == null || item["name"].Type != JTokenType.String || item["result"] == null || item["result"].Type != JTokenType.String) return false;
            return true;
        }
        internal static bool Accepted(JToken receipt)
        {
            return receipt is JObject && receipt["owned"] != null && receipt["owned"].Type == JTokenType.Boolean && (bool)receipt["owned"] && receipt["status"] != null && receipt["status"].Type == JTokenType.String && (string)receipt["status"] == "active";
        }
    }
}
