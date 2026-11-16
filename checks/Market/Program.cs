using System;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Market check failed"); }
    private static void Main()
    {
        MarketChartPoint[] empty = MarketChart.Points(new double[0]); Check(empty.Length == 0);
        MarketChartPoint[] single = MarketChart.Points(new[] { 4d }); Check(single[0].X == 1f && single[0].Y == 0.5f);
        MarketChartPoint[] points = MarketChart.Points(new[] { 2d, 4d, 3d });
        Check(points[0].X == 0f && points[0].Y == 1f && points[1].X == 0.5f && points[1].Y == 0f && points[2].Y == 0.5f);
        int calls = 0;
        TransactionClient client = new TransactionClient("exchange", request =>
        {
            calls++; Check((string)request["app"] == "exchange");
            return JObject.Parse("{\"ok\":true,\"result\":{\"ready\":true}}");
        }, null);
        Check((bool)client.Read("exchange.status", null)["ready"] && calls == 1);
        bool found = false; bool malformed = false; string sentId = null;
        TransactionClient uncertain = new TransactionClient("exchange", request =>
        {
            if ((string)request["method"] != "exchange.result") { sentId = (string)request["args"]["cid"]; return null; }
            Check((string)request["args"]["cid"] == sentId);
            return new JObject(new JProperty("ok", true), new JProperty("result", malformed ? new JObject() : new JObject(new JProperty("found", found))));
        }, null);
        JObject order = new JObject(new JProperty("ticker", "SIP"), new JProperty("shares", 1), new JProperty("margin", false));
        JObject lookup = (JObject)order.DeepClone(); lookup["action"] = "buy";
        try { uncertain.Write("exchange.buy", order); throw new Exception("expected uncertain result"); } catch (InvalidOperationException) { }
        Check(!uncertain.Resolve("exchange.buy", order, "exchange.result", lookup) && uncertain.HasPending);
        malformed = true;
        try { uncertain.Resolve("exchange.buy", order, "exchange.result", lookup); throw new Exception("expected invalid result"); } catch (InvalidOperationException) { }
        Check(uncertain.HasPending); malformed = false;
        found = true;
        Check(uncertain.Resolve("exchange.buy", order, "exchange.result", lookup) && !uncertain.HasPending);
        try { uncertain.Write("exchange.buy", order); throw new Exception("expected uncertain result"); } catch (InvalidOperationException) { }
        Check(!uncertain.Forget("exchange.sell", order) && uncertain.HasPending);
        Check(uncertain.Forget("exchange.buy", order) && !uncertain.HasPending);
        Console.WriteLine("Market checks passed");
    }
}
