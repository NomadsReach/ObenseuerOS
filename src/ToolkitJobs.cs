using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitJobs
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly Action checkSession;
        private readonly Func<int, bool> launch;
        private readonly ScrollView content;
        private readonly Label notice;
        private JArray jobs;
        private string skill = "Any skill", hardness = "Any hardness", sort = "pay";

        internal ToolkitJobs(TransactionClient client, Action checkSession, Func<int, bool> launch)
        {
            this.client = client; this.checkSession = checkSession; this.launch = launch;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 180; Window.style.top = 32; Window.style.width = 1000; Window.style.height = 680;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Jobs"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && jobs != null) Session(); }).Every(1000);
        }
        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Load(); }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button b = new UnityEngine.UIElements.Button(action); b.enableRichText = false; b.text = value; return b; }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { jobs = null; content.Clear(); notice.text = e.Message; return false; }
        }
        private void Load()
        {
            if (!Session()) return;
            try
            {
                JObject result = client.Read("jobs.list", null) as JObject;
                if (result == null || !(result["jobs"] is JArray)) throw new InvalidOperationException("The jobs list is unavailable.");
                jobs = (JArray)result["jobs"]; notice.text = ""; Render();
            }
            catch (Exception e) { jobs = null; content.Clear(); notice.text = e.Message; content.Add(Button("Refresh", Load)); }
        }
        private void Render()
        {
            if (jobs == null || !Session()) return;
            content.Clear(); Label heading = Text("Work in Stalburg"); heading.style.fontSize = 28; content.Add(heading);
            content.Add(Text("Paid work at stations in your area. Pay is per hour and is added while you work."));
            content.Add(Text("Applying sends a reply to Stalburg Mail. Go to the station in person to start work. Stations with the same name, pay and area share one application."));
            List<string> skills = new List<string> { "Any skill" }, levels = new List<string> { "Any hardness" };
            foreach (JObject job in jobs)
            {
                string s = (string)job["skill"] ?? "No skill needed", h = job["hardness"].ToString();
                if (!skills.Contains(s)) skills.Add(s); if (!levels.Contains(h)) levels.Add(h);
            }
            if (!skills.Contains(skill)) skill = skills[0]; if (!levels.Contains(hardness)) hardness = levels[0];
            VisualElement controls = Row(); DropdownField skillField = new DropdownField("Skill", skills, skills.IndexOf(skill));
            skillField.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { skill = e.newValue; Render(); }); controls.Add(skillField);
            DropdownField hardField = new DropdownField("Hardness", levels, levels.IndexOf(hardness));
            hardField.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { hardness = e.newValue; Render(); }); controls.Add(hardField);
            List<string> sorts = new List<string> { "pay", "pay_up", "name" }, labels = new List<string> { "Pay, highest first", "Pay, lowest first", "Name, A to Z" };
            DropdownField order = new DropdownField("Sort", labels, sorts.IndexOf(sort));
            order.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { sort = sorts[labels.IndexOf(e.newValue)]; Render(); }); controls.Add(order);
            controls.Add(Button("Refresh", Load)); content.Add(controls);
            List<JObject> visible = JobsList.Select(jobs, skill == "Any skill" ? "" : skill == "No skill needed" ? "none" : skill, hardness == "Any hardness" ? "" : hardness, sort);
            content.Add(Text(jobs.Count == 0 ? "No paid work is listed near you right now." : visible.Count + " of " + jobs.Count + " paid jobs shown"));
            foreach (JObject job in visible)
            {
                JObject selected = job; decimal oc = (decimal)job["oc"], rm = (decimal)job["rm"];
                string pay = (oc > 0 ? oc.ToString("N0", CultureInfo.CurrentCulture) + " OC" : "") + (oc > 0 && rm > 0 ? " and " : "") + (rm > 0 ? rm.ToString("N2", CultureInfo.CurrentCulture) + " RM" : "");
                content.Add(Text((string)job["name"] + " — " + pay + " per hour; hardness " + job["hardness"] + "; skill " + ((string)job["skill"] ?? "No skill needed") + "; " + (string)job["place"]));
                if ((bool)job["applied"]) content.Add(Text("Applied"));
                else content.Add(Button("Apply", delegate { Apply(selected); }));
            }
            VisualElement services = Row();
            services.Add(Button("Stalburg Mail", delegate { if (Session()) launch(0); }));
            services.Add(Button("Stalburg Bank", delegate { if (Session()) launch(8); }));
            services.Add(Button("Stal-Ex", delegate { if (Session()) launch(5); })); content.Add(services);
        }
        private void Apply(JObject job)
        {
            if (!Session()) return;
            JObject args = new JObject(new JProperty("name", job["name"].DeepClone()), new JProperty("place", job["place"].DeepClone()), new JProperty("oc", job["oc"].DeepClone()), new JProperty("rm", job["rm"].DeepClone()));
            try { client.Read("jobs.apply", args); }
            catch (Exception e) { string message = e.Message; Load(); notice.text = message + " Check the list and your mail before applying again."; return; }
            Load();
            string refreshError = jobs == null ? notice.text : "";
            notice.text = "Application sent. The reply is in Stalburg Mail. Go to the station in person to start work." + (refreshError.Length == 0 ? "" : " The list could not refresh: " + refreshError);
        }
    }
}
