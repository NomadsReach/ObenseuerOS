using Newtonsoft.Json.Linq;
namespace ObenseuerOS
{
    internal static class VirusDesktopState
    {
        internal static bool TryRead(JObject state, out bool wall, out bool shuffle)
        {
            wall = false; shuffle = false;
            JObject fx = state == null ? null : state["fx"] as JObject;
            if (fx == null || fx["wall"] == null || fx["wall"].Type != JTokenType.Boolean || fx["shuffle"] == null || fx["shuffle"].Type != JTokenType.Boolean) return false;
            wall = (bool)fx["wall"]; shuffle = (bool)fx["shuffle"]; return true;
        }
    }
}
