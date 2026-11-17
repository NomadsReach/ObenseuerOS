using Newtonsoft.Json.Linq;
namespace ObenseuerOS
{
    internal static class ShadyReply
    {
        internal static bool Valid(JObject reply, string expectedFile)
        {
            if (reply == null || reply["outcome"] == null || reply["outcome"].Type != JTokenType.String) return false;
            string outcome = (string)reply["outcome"];
            return (outcome == "clean" || outcome == "infected" || outcome == "blocked") && (expectedFile == null || (reply["file"] != null && reply["file"].Type == JTokenType.String && (string)reply["file"] == expectedFile));
        }
    }
}
