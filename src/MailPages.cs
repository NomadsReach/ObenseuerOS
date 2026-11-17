using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class MailPages
    {
        internal static bool Append(List<JObject> rows, JObject page, ref int total)
        {
            JArray items = page == null ? null : page["items"] as JArray;
            JToken value = page == null ? null : page["total"];
            if (items == null || value == null || value.Type != JTokenType.Integer) return false;
            long count;
            try { count = (long)value; }
            catch (System.OverflowException) { return false; }
            if (count < 0 || count > int.MaxValue) return false;
            foreach (JToken item in items) if (!(item is JObject)) return false;
            foreach (JObject item in items) rows.Add(item);
            total = items.Count == 0 ? rows.Count : (int)count;
            return true;
        }
    }
}
