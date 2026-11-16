using System;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Bank check failed"); }
    private static void Main()
    {
        string first = null, second = null, third = null;
        int calls = 0; bool busy = false;
        BankClient client = new BankClient(request =>
        {
            calls++; string cid = (string)request["args"]["cid"];
            if (calls == 1) { first = cid; return null; }
            if (calls == 2) second = cid; else third = cid;
            return JObject.Parse("{\"ok\":true,\"result\":{\"id\":1}}");
        }, () => { if (busy) throw new InvalidOperationException("stale"); });
        JObject args = new JObject(new JProperty("bank", "SBB"), new JProperty("id", 1));
        try { client.Write("bank.payInvoice", args); throw new Exception("missing failure"); } catch (InvalidOperationException) { }
        Check(client.HasPending && client.PendingFor("bank.payInvoice", args));
        long pendingVersion = client.PendingVersion;
        JObject other = new JObject(new JProperty("bank", "SBB"), new JProperty("id", 2));
        Check(client.MayStart("bank.payInvoice", args, false) && !client.MayStart("bank.payInvoice", other, false));
        Check(client.MayStart("bank.payInvoice", other, true));
        Check((int)client.Write("bank.payInvoice", args)["id"] == 1 && first == second);
        Check(!client.HasPending && client.PendingVersion != pendingVersion);
        client.Write("bank.payInvoice", args); Check(third != first);
        busy = true;
        try { client.Write("bank.payInvoice", args); throw new Exception("guard ignored"); } catch (InvalidOperationException) { Check(calls == 3); }
        BankClient shared = BankClients.ForSession(7, request => null, null);
        Check(object.ReferenceEquals(shared, BankClients.ForSession(7, request => null, null)));
        Check(!object.ReferenceEquals(shared, BankClients.ForSession(8, request => null, null)));
        try { shared.Write("bank.payInvoice", args); } catch (InvalidOperationException) { }
        long acknowledgedVersion = shared.PendingVersion;
        try { shared.Write("bank.payInvoice", other); } catch (InvalidOperationException) { }
        JObject thirdRequest = new JObject(new JProperty("bank", "SBB"), new JProperty("id", 3));
        Check(acknowledgedVersion != shared.PendingVersion && !shared.MayStart("bank.payInvoice", thirdRequest, acknowledgedVersion == shared.PendingVersion));
        decimal debit, fee, sent;
        Check(BankQuote.TryCreate("OC", 100m, 2m, out debit, out fee, out sent) && debit == 100m && fee == 2m && sent == 98m);
        Check(BankQuote.TryCreate("RM", 12.3m, 2m, out debit, out fee, out sent) && fee == 0.3m && sent == 12m);
        Check(!BankQuote.TryCreate("OC", 1m, 2m, out debit, out fee, out sent));
        Check(!BankQuote.TryCreate("OC", 16000001m, 2m, out debit, out fee, out sent));
        Console.WriteLine("Bank client checks passed");
    }
}
