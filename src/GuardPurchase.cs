using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class GuardPurchase
    {
        internal static bool Accepted(JToken receipt)
        {
            return receipt is JObject && receipt["owned"] != null && receipt["owned"].Type == JTokenType.Boolean && (bool)receipt["owned"];
        }
    }
}
