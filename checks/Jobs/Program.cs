using System;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Jobs check failed"); }
    private static void Main()
    {
        JArray jobs = JArray.Parse("[{\"name\":\"A\",\"oc\":2,\"rm\":0,\"skill\":null,\"hardness\":1},{\"name\":\"B\",\"oc\":5,\"rm\":0,\"skill\":\"Repair\",\"hardness\":2},{\"name\":\"C\",\"oc\":5,\"rm\":1,\"skill\":\"Repair\",\"hardness\":1}]");
        Check((string)JobsList.Select(jobs, "", "", "pay")[0]["name"] == "C");
        Check((string)JobsList.Select(jobs, "Repair", "", "pay_up")[0]["name"] == "B");
        Check(JobsList.Select(jobs, "none", "1", "name").Count == 1);
        Check(JobsList.Select(jobs, "Repair", "3", "name").Count == 0);
        ScratchProgress progress = new ScratchProgress();
        for (int i = 0; i < 14; i++) Check(progress.Mark(i));
        Check(!progress.Mark(1) && !progress.Finished);
        Check(progress.Mark(14) && progress.Finished);
        JObject reply = JObject.Parse("{\"card\":{\"id\":3,\"kind\":\"oc\",\"revealed\":true,\"prize\":5,\"currency\":\"OC\"},\"balance\":10,\"rm\":0.5}");
        Check(ScratchReply.Valid(reply, 3, "oc") && !ScratchReply.Valid(reply, 4, "oc") && !ScratchReply.Valid(reply, 3, "spade"));
        reply["card"]["currency"] = "RM"; Check(!ScratchReply.Valid(reply, 3, "oc")); reply["card"]["currency"] = "OC";
        reply["card"]["revealed"] = false; Check(!ScratchReply.Valid(reply, 3, "oc"));
        Console.WriteLine("Jobs checks passed");
    }
}
