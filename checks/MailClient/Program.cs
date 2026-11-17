using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Mail client check failed"); }
    private static void Main()
    {
        List<JObject> pages = new List<JObject>(); int total = 0;
        Check(MailPages.Append(pages, JObject.Parse("{\"items\":[{\"id\":1},{\"id\":2}],\"total\":3}"), ref total) && pages.Count == 2 && total == 3);
        Check(!MailPages.Append(pages, JObject.Parse("{\"items\":[1],\"total\":3}"), ref total) && pages.Count == 2 && total == 3);
        Check(!MailPages.Append(pages, null, ref total) && pages.Count == 2 && total == 3);
        Check(!MailPages.Append(pages, JObject.Parse("{\"items\":[],\"total\":\"bad\"}"), ref total) && pages.Count == 2 && total == 3);
        Check(MailPages.Append(pages, JObject.Parse("{\"items\":[{\"id\":3}],\"total\":3}"), ref total) && pages.Count == 3 && total == 3);
        Check(MailPages.Append(pages, JObject.Parse("{\"items\":[],\"total\":8}"), ref total) && total == pages.Count);
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
        MailDraft revisionCheck = new MailDraft(); long initialRevision = revisionCheck.Revision;
        revisionCheck.Subject = "subject"; Check(revisionCheck.Revision != initialRevision);
        long sameRevision = revisionCheck.Revision; revisionCheck.Subject = "subject"; Check(revisionCheck.Revision == sameRevision);
        revisionCheck.SetReply(7, "reply"); Check(revisionCheck.Revision != sameRevision);
        CloseConfirmation mailApproval = new CloseConfirmation();
        Check(!mailApproval.Allow(revisionCheck.Dirty, revisionCheck.Revision));
        Check(mailApproval.Allow(revisionCheck.Dirty, revisionCheck.Revision));
        revisionCheck.SetReply(7, "edited");
        Check(!mailApproval.Allow(revisionCheck.Dirty, revisionCheck.Revision));
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
