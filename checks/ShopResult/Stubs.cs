using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class OsBridgeException : Exception { internal OsBridgeException(string code, string message) : base(message) { } }
    internal static class OsBridge
    {
        internal static JObject Args(string args) { try { return JObject.Parse(args); } catch (Exception) { throw new OsBridgeException("bad_args", "args must be a JSON object"); } }
        internal static string Text(JObject o, string key) { JToken t = o[key]; return t != null && t.Type == JTokenType.String ? (string)t : null; }
    }
    internal sealed class ShopLine { internal string Key; internal int Qty; }
    internal sealed class OrderRec { internal string Cid, Print; }
    internal sealed class ShopSave { internal readonly List<OrderRec> Orders = new List<OrderRec>(); }
}
