using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class JobsList
    {
        internal static List<JObject> Select(JArray jobs, string skill, string hardness, string sort)
        {
            List<JObject> result = new List<JObject>();
            foreach (JObject job in jobs)
            {
                string value = (string)job["skill"] ?? "none";
                if (skill.Length > 0 && skill != value) continue;
                if (hardness.Length > 0 && hardness != job["hardness"].ToString()) continue;
                result.Add(job);
            }
            result.Sort(delegate(JObject a, JObject b)
            {
                int name = string.Compare((string)a["name"], (string)b["name"], StringComparison.CurrentCultureIgnoreCase);
                int oc = ((decimal)a["oc"]).CompareTo((decimal)b["oc"]), rm = ((decimal)a["rm"]).CompareTo((decimal)b["rm"]);
                if (sort == "name") return name != 0 ? name : oc != 0 ? -oc : -rm;
                int order = sort == "pay_up" ? 1 : -1;
                return oc != 0 ? order * oc : rm != 0 ? order * rm : name;
            });
            return result;
        }
    }
}
