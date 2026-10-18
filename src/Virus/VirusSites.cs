using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class VirusSites
    {
        internal const string FreeRmApp = "freerm";
        internal const string WarezApp = "warez";
        internal const string VoittajaApp = "voittaja";
        private const int MaxHost = 253;
        private const string StubText = "This is a placeholder for a downloaded file. Nothing was installed.";

        internal static void Attach()
        {
            Os.Handle("browser", "virus.browse", Browse);
            Os.Handle(FreeRmApp, "virus.download", args => Download(args, VirusVector.FreeRmDownload));
            Os.Handle(WarezApp, "virus.download", args => Download(args, VirusVector.WarezDownload));
            Os.Handle(VoittajaApp, "virus.claim", Claim);
        }

        private static string Browse(string args)
        {
            string host = Host(OsBridge.Args(args));
            OsBridge.CheckWritable();
            return JsonConvert.SerializeObject(new { outcome = Outcome(VirusCore.Browse(VirusRules.IsShady(host), host)) });
        }

        private static string Download(string args, VirusVector vector)
        {
            string item = OsBridge.Text(OsBridge.Args(args), "item") ?? "";
            string file = VirusRules.DownloadFile(vector, item);
            if (file == null) throw new OsBridgeException("bad_args", "item is not a download on this site");
            OsBridge.CheckWritable();
            VirusDecision decision = VirusCore.Infect(vector, file);
            if (decision.Outcome != VirusOutcome.Blocked) Stub(file);
            return JsonConvert.SerializeObject(new { outcome = Outcome(decision), file = file });
        }

        private static void Stub(string file)
        {
            string path = "/downloads/" + file;
            if (OsFiles.Read(path) == null) OsFiles.Write(path, StubText);
        }

        private static string Claim(string args)
        {
            OsBridge.CheckWritable();
            return JsonConvert.SerializeObject(new { outcome = Outcome(VirusCore.Infect(VirusVector.VoittajaClaim, "VOITTAJA!")) });
        }

        private static string Host(JObject a)
        {
            string host = OsBridge.Text(a, "host");
            if (string.IsNullOrEmpty(host) || host.Length > MaxHost) throw new OsBridgeException("bad_args", "host must be a site address");
            return host.ToLowerInvariant();
        }

        private static string Outcome(VirusDecision decision)
        {
            return decision.Outcome.ToString().ToLowerInvariant();
        }
    }
}
