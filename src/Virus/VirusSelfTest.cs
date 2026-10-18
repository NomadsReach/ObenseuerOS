using System.Collections.Generic;
using System.Text;

namespace ObenseuerOS
{
    internal static class VirusSelfTest
    {
        internal static string Run()
        {
            foreach (KeyValuePair<string, bool> check in VirusRuleChecks.Run())
            {
                if (!check.Value) return check.Key;
            }
            if (Encoding.UTF8.GetByteCount(VirusCore.StateJson(WorstCase())) > OsBridge.MaxResultBytes) return "virus.state over 12 KB";
            return Encoding.UTF8.GetByteCount(OsMail.ReadJson(WorstMail())) > OsBridge.MaxResultBytes ? "mail.read with an attachment over 12 KB" : null;
        }

        private static MailRec WorstMail()
        {
            return new MailRec
            {
                Id = 1,
                Folder = "inbox",
                From = new string('f', OsMail.MaxFrom),
                To = new string('t', OsMail.MaxFrom),
                Subject = new string('s', OsMail.MaxSubject),
                Body = new string('€', OsMail.MaxBody),
                When = "2026-10-13 12:00",
                Attach = "label.pdf.exe",
            };
        }

        private static VirusSave WorstCase()
        {
            VirusSave save = new VirusSave();
            foreach (KeyValuePair<string, string> item in VirusRules.Catalogue) save.Active.Add(item.Key);
            save.RealOwned = true;
            save.CheapOwned = true;
            save.CheapLapsed = true;
            return save;
        }
    }
}
