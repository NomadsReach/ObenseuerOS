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
        Console.WriteLine("Jobs checks passed");
    }
}
