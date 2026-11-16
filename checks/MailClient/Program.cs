using System;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Mail client check failed"); }
    private static void Main()
    {
        int calls = 0;
        bool busy = false;
        MailClient client = new MailClient(request =>
        {
            calls++;
            Check((string)request["app"] == "mail" && (string)request["method"] == "mail.read" && (int)request["args"]["id"] == 7);
            return JObject.Parse("{\"ok\":true,\"result\":{\"body\":\"saved\"}}");
        }, () => { if (busy) throw new InvalidOperationException("stale session"); });
        Check((string)client.Call("mail.read", new JObject(new JProperty("id", 7)))["body"] == "saved" && calls == 1);
        busy = true;
        try { client.Call("mail.read", new JObject(new JProperty("id", 7))); throw new Exception("guard ignored"); }
        catch (InvalidOperationException e) { Check(e.Message == "stale session" && calls == 1); }
        MailClient failed = new MailClient(request => JObject.Parse("{\"ok\":false,\"message\":\"mailbox full\"}"), null);
        try { failed.Call("mail.send", null); throw new Exception("error ignored"); }
        catch (InvalidOperationException e) { Check(e.Message == "mailbox full"); }
        MailDraft original = MailDrafts.ForSession(1);
        original.Subject = "unsent"; original.Body = "kept";
        Check(MailDrafts.ForSession(1).Body == "kept");
        MailDraft next = MailDrafts.ForSession(2);
        Check(!next.Dirty && !object.ReferenceEquals(original, next));
        next.Subject = "new save"; next.Clear(); Check(!next.Dirty && next.To == "");
        Console.WriteLine("Mail client checks passed");
    }
}
