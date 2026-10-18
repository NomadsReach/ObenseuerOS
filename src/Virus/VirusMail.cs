using System;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class SpamTemplate
    {
        internal readonly string From;
        internal readonly string Subject;
        internal readonly string Body;
        internal readonly string File;

        internal SpamTemplate(string from, string subject, string body, string file)
        {
            From = from;
            Subject = subject;
            Body = body;
            File = file;
        }
    }

    internal static class VirusMail
    {
        private static readonly Random Rnd = new Random();
        private static readonly SpamTemplate[] Templates =
        {
            new SpamTemplate("Monetary Bank of Stalburg <security@monetary-bank.ts>", "Your account has been locked", "Dear customer,\n\nYour account was locked after several failed sign-in attempts. Open the attached unlock_form.exe to restore access. Unlock tools are also at www.kolhola-warez.ts.\n\nMonetary Bank of Stalburg, Security team", "unlock_form.exe"),
            new SpamTemplate("NaSpoBet bonus <free-bets@naspo-bet.ts>", "Your FREE bet is waiting", "Congratulations, you have a free bet. Open bonus.exe to claim it. More free bets at www.free-rm.ts.\n\nNaSpoBet", "bonus.exe"),
            new SpamTemplate("Stalburg Web Lucky Visitor <winner@stalburg-web.ts>", "You are our lucky visitor!", "You are our 1 000 000th visitor and you have won a prize. Open prize_claim.exe, or claim at www.voittaja.ts before the offer ends today.", "prize_claim.exe"),
            new SpamTemplate("PC Mastr Support <support@pcmastr.ts>", "Your PC is infected", "We found 14 threats on your computer. Run fix_tool.exe now to remove them. Full protection at www.kolhola-warez.ts.\n\nPC Mastr Support", "fix_tool.exe"),
            new SpamTemplate("Parcel Service <notice@parcel-post.ts>", "Your parcel could not be delivered", "Your parcel could not be delivered. Print the label from label.pdf.exe and bring it to the depot. Track your parcel at www.free-rm.ts.", "label.pdf.exe"),
        };

        internal static void Attach()
        {
            Events.HourPassed += hour => Tick();
        }

        private static void Tick()
        {
            if (!VirusCore.Enabled || !OsBridge.Writable() || !OsBridge.WorldReady() || !OsMail.HasAccount()) return;
            WorldTime now = WorldClock.Now;
            if (!VirusRules.SpamDue(VirusCore.State, now.Day, now.Hour, OsMail.ItemCount(), Rnd)) return;
            SpamTemplate spam = Templates[Rnd.Next(Templates.Length)];
            bool attach = Rnd.Next(100) < VirusRules.SpamAttachPercent;
            OsMail.SendSpam(spam.From, spam.Subject, spam.Body, attach ? spam.File : "");
        }
    }
}
