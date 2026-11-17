using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Shop result check failed"); }
    private static bool Found(ShopSave state, JObject args) { return (bool)JObject.Parse(ShopBridge.ResultFor(state, args.ToString()))["found"]; }
    private static void Refused(ShopSave state, JObject args)
    {
        try { ShopBridge.ResultFor(state, args.ToString()); throw new Exception("Expected rejected shop request"); }
        catch (OsBridgeException) { }
    }
    private static void Main()
    {
        List<ShopLine> lines = new List<ShopLine> { new ShopLine { Key = "a", Qty = 3 }, new ShopLine { Key = "b", Qty = 2 } };
        string print = ShopOrders.Fingerprint(lines, "standard", "wallet", "parcel");
        ShopSave state = new ShopSave(); state.Orders.Add(new OrderRec { Cid = "order1", Print = print });
        JObject args = JObject.Parse("{\"cid\":\"order1\",\"lines\":[{\"id\":\"a\",\"qty\":3},{\"id\":\"b\",\"qty\":2}],\"tier\":\"standard\",\"pay\":\"wallet\",\"via\":\"parcel\"}");
        Check(Found(state, args));
        args["cid"] = "other"; Check(!Found(state, args)); args["cid"] = "order1";
        args["lines"] = JArray.Parse("[{\"id\":\"b\",\"qty\":2},{\"id\":\"a\",\"qty\":3}]"); Check(Found(state, args));
        args.Remove("via"); Check(Found(state, args));
        args["via"] = "mail"; Refused(state, args); args["via"] = "parcel";
        args["pay"] = "bank:other"; Refused(state, args); args["pay"] = "wallet";
        args["lines"][0]["qty"] = 0; Refused(state, args);
        args["lines"] = new JArray(); Refused(state, args);
        JArray oversized = new JArray(); for (int i = 0; i < 13; i++) oversized.Add(new JObject(new JProperty("id", "a"), new JProperty("qty", 1)));
        args["lines"] = oversized; Refused(state, args);
        args["cid"] = new string('x', 65); Refused(state, args);
        args["cid"] = ""; Refused(state, args);
        Check(state.Orders.Count == 1 && state.Orders[0].Print == print);
        Console.WriteLine("Shop purchase result checks passed");
    }
}
