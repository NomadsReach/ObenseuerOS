using System;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Bank bridge check failed"); }
    private static void Main()
    {
        OsBank.Attach(2f);
        Check(OsBank.SelfCheck() == null);
        for (int id = 35; id >= 1; id--)
            Invoices.Values.Add(new InvoiceInfo { Id = id, Debtor = "Debtor " + id, Amount = id, Currency = MoneyCurrency.OC, DaysRemaining = 3 });
        Func<string, string> list = Os.Handlers["bank.invoices"];
        JObject legacy = JObject.Parse(list("{}"));
        Check((int)legacy["total"] == 35 && ((JArray)legacy["items"]).Count == 30);
        Check((int)legacy["items"][0]["id"] == 35 && (int)legacy["items"][29]["id"] == 6 && legacy["nextAfterId"] == null);
        JObject first = JObject.Parse(list("{\"paged\":true,\"afterId\":0}"));
        Check((int)first["total"] == 35 && ((JArray)first["items"]).Count == 30);
        Check((int)first["items"][0]["id"] == 1 && (int)first["items"][29]["id"] == 30 && (int)first["nextAfterId"] == 30);
        Invoices.Values.RemoveAll(item => item.Id == 2);
        Invoices.Values.Add(new InvoiceInfo { Id = 36, Debtor = "New", Currency = MoneyCurrency.RM });
        JObject second = JObject.Parse(list("{\"paged\":true,\"afterId\":30}"));
        Check((int)second["total"] == 35 && ((JArray)second["items"]).Count == 6);
        Check((int)second["items"][0]["id"] == 31 && (int)second["items"][5]["id"] == 36 && (int)second["nextAfterId"] == 0);
        JObject empty = JObject.Parse(list("{\"paged\":true,\"afterId\":36}"));
        Check(((JArray)empty["items"]).Count == 0 && (int)empty["nextAfterId"] == 0);
        try { list("{\"paged\":true,\"afterId\":-1}"); throw new Exception("negative cursor accepted"); }
        catch (OsBridgeException) { }
        Banking.Open = false;
        try { list("{\"paged\":true}"); throw new Exception("locked bank accepted"); }
        catch (OsBridgeException) { }
        Console.WriteLine("Bank bridge checks passed");
    }
}
