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
        TransactionClient client = new TransactionClient("bank", request =>
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
        TransactionClient shared = TransactionClients.ForSession("bank", 7, request => null, null);
        Check(object.ReferenceEquals(shared, TransactionClients.ForSession("bank", 7, request => null, null)));
        Check(!object.ReferenceEquals(shared, TransactionClients.ForSession("exchange", 7, request => null, null)));
        Check(!object.ReferenceEquals(shared, TransactionClients.ForSession("bank", 8, request => null, null)));
        try { shared.Write("bank.payInvoice", args); } catch (InvalidOperationException) { }
        long acknowledgedVersion = shared.PendingVersion;
        try { shared.Write("bank.payInvoice", other); } catch (InvalidOperationException) { }
        JObject thirdRequest = new JObject(new JProperty("bank", "SBB"), new JProperty("id", 3));
        Check(acknowledgedVersion != shared.PendingVersion && !shared.MayStart("bank.payInvoice", thirdRequest, acknowledgedVersion == shared.PendingVersion));
        Check(GuardPurchase.Accepted(JObject.Parse("{\"owned\":true}")));
        Check(!GuardPurchase.Accepted(JObject.Parse("{\"owned\":false}")) && !GuardPurchase.Accepted(JObject.Parse("{\"owned\":\"true\"}")) && !GuardPurchase.Accepted(new JObject()) && !GuardPurchase.Accepted(null));
        Check(TurvaReceipt.Accepted(JObject.Parse("{\"owned\":true,\"status\":\"active\"}")));
        Check(!TurvaReceipt.Accepted(JObject.Parse("{\"owned\":true,\"status\":\"lapsed\"}")) && !TurvaReceipt.Accepted(new JObject()) && !TurvaReceipt.Accepted(null));
        Check(TurvaReceipt.ScanAccepted(JObject.Parse("{\"cookies\":3,\"found\":[{\"name\":\"worm\",\"result\":\"missed\"}]}")));
        Check(!TurvaReceipt.ScanAccepted(JObject.Parse("{\"cookies\":3,\"found\":[{}]}")) && !TurvaReceipt.ScanAccepted(JObject.Parse("{\"cookies\":\"3\",\"found\":[]}")) && !TurvaReceipt.ScanAccepted(null));
        Check(ShadyReply.Valid(JObject.Parse("{\"outcome\":\"blocked\",\"file\":\"a.exe\"}"), "a.exe"));
        Check(ShadyReply.Valid(JObject.Parse("{\"outcome\":\"infected\"}"), null) && !ShadyReply.Valid(new JObject(), null) && !ShadyReply.Valid(JObject.Parse("{\"outcome\":\"blocked\"}"), "a.exe") && !ShadyReply.Valid(JObject.Parse("{\"outcome\":\"oops\"}"), null));
        Check(VirusPopupRules.Room(3) && !VirusPopupRules.Room(4));
        Check(VirusPopupRules.Dodge(1, 0.34) && !VirusPopupRules.Dodge(2, 0.0) && !VirusPopupRules.Dodge(0, 0.35));
        Check(VirusPopupRules.Spawn(2, false, 0.14) && !VirusPopupRules.Spawn(3, false, 0.0) && !VirusPopupRules.Spawn(0, true, 0.0));
        int claimCalls = 0;
        TransactionClient claim = TransactionClients.ForSession("virusfx", 101, request => { claimCalls++; return null; }, null);
        JObject claimArgs = new JObject(new JProperty("kind", "winner"));
        try { claim.Write("virus.claim", claimArgs); } catch (InvalidOperationException) { }
        Check(claim.HasPending && claim.PendingFor("virus.claim", claimArgs));
        Check(object.ReferenceEquals(claim, TransactionClients.ForSession("virusfx", 101, request => null, null)) && claimCalls == 1);
        Check(!claim.Forget("virus.claim", new JObject(new JProperty("kind", "moca"))) && claim.HasPending);
        Check(claim.Forget("virus.claim", claimArgs) && !claim.HasPending);
        bool wall, shuffle;
        Check(VirusDesktopState.TryRead(JObject.Parse("{\"fx\":{\"wall\":true,\"shuffle\":false}}"), out wall, out shuffle) && wall && !shuffle);
        Check(VirusDesktopState.TryRead(JObject.Parse("{\"fx\":{\"wall\":false,\"shuffle\":true}}"), out wall, out shuffle) && !wall && shuffle);
        Check(!VirusDesktopState.TryRead(JObject.Parse("{\"fx\":{\"wall\":true,\"shuffle\":1}}"), out wall, out shuffle) && !wall && !shuffle);
        decimal debit, fee, sent;
        Check(BankQuote.TryCreate("OC", 100m, 2m, out debit, out fee, out sent) && debit == 100m && fee == 2m && sent == 98m);
        Check(BankQuote.TryCreate("RM", 12.3m, 2m, out debit, out fee, out sent) && fee == 0.3m && sent == 12m);
        Check(!BankQuote.TryCreate("OC", 1m, 2m, out debit, out fee, out sent));
        Check(!BankQuote.TryCreate("OC", 16000001m, 2m, out debit, out fee, out sent));
        Console.WriteLine("Bank client checks passed");
    }
}
