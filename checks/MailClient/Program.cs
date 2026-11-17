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
        original.SetReply(7, "inline reply");
        Check(MailDrafts.ForSession(1).Body == "kept" && MailDrafts.ForSession(1).Reply(7) == "inline reply");
        original.Clear(); Check(original.Dirty && !original.ComposeDirty && original.Reply(7) == "inline reply");
        original.SetReply(7, ""); Check(!original.Dirty);
        MailDraft reply = new MailDraft(); reply.SetReply(42, "hello");
        MailClient send = new MailClient(request =>
        {
            Check((string)request["method"] == "mail.send" && (string)request["args"]["to"] == "a@stalburg.ts");
            Check((string)request["args"]["subject"] == "Re: Hello" && (string)request["args"]["body"] == "hello");
            return JObject.Parse("{\"ok\":true,\"result\":{}}");
        }, null);
        Check(send.SendReply(reply, 42, "a@stalburg.ts", "Hello") && !reply.Dirty);
        reply.SetReply(42, "again");
        try { failed.SendReply(reply, 42, "a@stalburg.ts", "Re: Hello"); throw new Exception("error ignored"); }
        catch (InvalidOperationException) { Check(reply.Reply(42) == "again"); }
        MailDraft next = MailDrafts.ForSession(2);
        Check(!next.Dirty && next.Reply(7) == "" && !object.ReferenceEquals(original, next));
        next.Subject = "new save"; next.Clear(); Check(!next.Dirty && next.To == "");
        Console.WriteLine("Mail client checks passed");
    }
}
