using System;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Bet identity check failed"); }
    private static void Refused(Action action, string code)
    {
        try { action(); throw new Exception("expected rejection"); }
        catch (OsBridgeException e) { Check(e.Code == code); }
    }
    private static void Main()
    {
        BetBridge.CheckExpectedOdds(new JObject(), 211);
        BetBridge.CheckExpectedOdds(JObject.Parse("{\"expectedOdds\":211}"), 211);
        Refused(() => BetBridge.CheckExpectedOdds(JObject.Parse("{\"expectedOdds\":211}"), 212), "denied");
        Refused(() => BetBridge.CheckExpectedOdds(JObject.Parse("{\"expectedOdds\":100}"), 211), "bad_args");
        Refused(() => BetBridge.CheckExpectedOdds(JObject.Parse("{\"expectedOdds\":\"211\"}"), 211), "bad_args");
        BetSave state = new BetSave(); state.Bets.Add(new BetRec { Cid = "one", Print = "fb-1-1-1|home|10" });
        JObject args = JObject.Parse("{\"cid\":\"one\",\"fixture\":\"fb-1-1-1\",\"pick\":\"home\",\"stake\":10}");
        Check((bool)JObject.Parse(BetBridge.ResultFor(state, args.ToString()))["found"]);
        args["cid"] = "missing"; Check(!(bool)JObject.Parse(BetBridge.ResultFor(state, args.ToString()))["found"]); args["cid"] = "one";
        args["stake"] = 11; Refused(() => BetBridge.ResultFor(state, args.ToString()), "bad_args"); args["stake"] = 10;
        args["fixture"] = "ih-1-1-1"; args["pick"] = "draw"; Refused(() => BetBridge.ResultFor(state, args.ToString()), "bad_args");
        args["fixture"] = "invalid"; Refused(() => BetBridge.ResultFor(state, args.ToString()), "bad_args");
        args["cid"] = new string('x', 65); Refused(() => BetBridge.ResultFor(state, args.ToString()), "bad_args");
        Check(state.Bets.Count == 1 && state.Bets[0].Print == "fb-1-1-1|home|10");
        Console.WriteLine("Bet identity checks passed");
    }
}
