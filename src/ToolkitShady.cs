using System;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitShady
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient free, warez, prize;
        private readonly Action checkSession;
        private readonly Func<string, bool> visit;
        private readonly ScrollView content;
        private readonly Label notice, title;
        private int site;
        private bool generated, terms, confirm;
        private static readonly string[,] Downloads = {
            { "stalex-insider", "Stal-Ex Insider 2.0", "12,8 MB", "3 519", "stalex_insider_2.0.exe" },
            { "naspo-autobet", "NaSpoBet autobet bot", "0,6 MB", "2 376", "naspobet_autobet.exe" },
            { "lotto-predictor", "LOTTO number predictor (100 % working)", "0,3 MB", "1 589", "lotto_predictor_100.exe" },
            { "minesweeper-gold", "Minesweeper GOLD crack", "0,4 MB", "1 204", "minesweeper_gold_crack.exe" },
            { "zigzag-bot", "Zigzag views bot 2001", "0,9 MB", "1 031", "zigzag_views_bot_2001.exe" },
            { "solitaire-keygen", "Solitaire Unlimited keygen", "0,1 MB", "887", "solitaire_keygen.exe" },
            { "postal-spy", "Postal Messenger spy 1.3", "2,2 MB", "741", "postal_messenger_spy_1.3.exe" },
            { "eyebook-finder", "EYEbook password finder", "1,1 MB", "652", "eyebook_finder.exe" },
            { "torchat-skin", "TorChat turnip skin pack", "4,7 MB", "418", "torchat_turnip_skin.exe" },
            { "stalmail-sender", "Stalburg Mail sender PRO", "1,8 MB", "298", "stalburg_mail_sender_pro.exe" }
        };
        private static readonly string[] Hosts = { "www.free-rm.ts", "www.kolhola-warez.ts", "www.voittaja.ts" };
        private static readonly string[] Names = { "FREE RM GENERATOR", "Kolhola Warez", "VOITTAJA!" };
        internal ToolkitShady(TransactionClient free, TransactionClient warez, TransactionClient prize, Action checkSession, Func<string, bool> visit)
        {
            this.free = free; this.warez = warez; this.prize = prize; this.checkSession = checkSession; this.visit = visit;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 150; Window.style.top = 32; Window.style.width = 1000; Window.style.height = 680;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.height = 36;
            title = Text(""); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None) Session(); }).Every(1000);
        }
        internal void Open(int index) { site = index; generated = false; terms = false; confirm = false; notice.text = ""; Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Render(); }
        private static Label Text(string text) { Label label = new Label(text); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = text; return button; }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { content.Clear(); notice.text = e.Message; return false; }
        }
        private void Render()
        {
            if (!Session()) return;
            content.Clear();
            TransactionClient current = site == 0 ? free : site == 1 ? warez : prize;
            if (current.HasPending)
            {
                notice.text = "The last action may have completed. Check your downloads and computer before repeating it.";
                content.Add(Button(confirm ? "Confirm: clear uncertain action" : "Clear uncertain action", delegate
                {
                    if (!confirm) { confirm = true; Render(); notice.text = "Repeating this action may cause its effects again. Select again to confirm."; return; }
                    if (site == 0) current.Forget("virus.download", new JObject(new JProperty("item", "generator")));
                    else if (site == 1) for (int i = 0; i < Downloads.GetLength(0); i++) current.Forget("virus.download", new JObject(new JProperty("item", Downloads[i, 0])));
                    else current.Forget("virus.claim", new JObject());
                    confirm = false; notice.text = ""; Render();
                }));
            }
            title.text = Names[site]; Label heading = Text(Names[site]); heading.style.fontSize = 28; content.Add(heading);
            if (site == 0)
            {
                content.Add(Text("*** FREE *** FREE *** NO VIRUS *** FREE ***\nGenerate 1 000 RM in seconds!!! 100 % working!!!"));
                if (!generated) content.Add(Button("GENERATE 1 000 RM", delegate { generated = true; Render(); }));
                else content.Add(Button("Download generator.exe", delegate { Act(free, "virus.download", "generator", "generator.exe"); }));
                content.Add(Text("WHAT OUR CUSTOMERS SAY!!!\nI generated 50 000 RM in one night!!! - Pertti, Koksburg\nGot a virus from the generator but it was only the FREE kind. 5 stars!!! - Mirjam, Tarfell"));
            }
            else if (site == 1)
            {
                content.Add(Text("Cracked games and tools. Fast servers!!!\nTOP 10 DOWNLOADS"));
                for (int i = 0; i < Downloads.GetLength(0); i++)
                {
                    string id = Downloads[i, 0], file = Downloads[i, 4];
                    content.Add(Button(Downloads[i, 1] + " — " + Downloads[i, 2] + " — " + Downloads[i, 3] + " downloads", delegate { Act(warez, "virus.download", id, file); }));
                }
                content.Add(Text("Every download is 100 % free!!! No registration needed!!!"));
            }
            else if (!terms)
            {
                content.Add(Text("CONGRATULATIONS!!! You are our 1 000 000th visitor and you have WON 5 000 RM!!!"));
                content.Add(Button("CLAIM PRIZE", delegate { Act(prize, "virus.claim", null, null); }));
                content.Add(Button("Prize rules and small print", delegate { terms = true; Render(); }));
            }
            else
            {
                content.Add(Text("The prize is paid in OC at the Money Exchange rate of the day, minus a 40 % handling fee and a 15 % fee for the Tax Office of Stalburg (not a real office).\nPressing NO THANKS also claims the prize. This is a feature.\nThe prize is not available in Ershfors, Koksburg or on Tuesdays.\nWinners must be 18 years or older and hold a mail account at any .ts address.\nVOITTAJA! is not affiliated with the Monetary Bank of Stalburg, NaSpoBet or any other bank or betting company.\nThis offer ends today."));
                content.Add(Button("No thanks, I do not want my 5 000 RM", delegate { Act(prize, "virus.claim", null, null); }));
                content.Add(Button("Back to the prize page", delegate { terms = false; Render(); }));
            }
            for (int i = 0; i < Hosts.Length; i++)
            {
                if (i == site) continue;
                int target = i; content.Add(Button(Names[i], delegate { if (!Session()) return; if (visit(Hosts[target])) Open(target); }));
            }
            content.Add(Button("Stalburg Web", delegate { if (!Session()) return; if (visit("www.stalburg.ts")) Window.style.display = DisplayStyle.None; }));
        }
        private void Act(TransactionClient client, string method, string item, string expectedFile)
        {
            if (!Session() || client.HasPending) return;
            try
            {
                JObject args = new JObject(); if (item != null) args["item"] = item;
                JObject result = client.Write(method, args, delegate(JToken r) { return ShadyReply.Valid(r as JObject, expectedFile); }) as JObject;
                bool blocked = (string)result["outcome"] == "blocked";
                notice.text = item != null ? (blocked ? "Download blocked. Your security software stopped " : "Download complete: ") + (string)result["file"] + "." : blocked ? "Claim blocked. Your security software stopped the prize." : "Congratulations! Your prize has been claimed.";
            }
            catch (Exception e) { Render(); notice.text = e.Message + " The action may have completed."; }
        }
    }
}
