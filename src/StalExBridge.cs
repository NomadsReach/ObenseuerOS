using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    [Serializable]
    public class StalExCidRec
    {
        public string Cid;
        public string Print;
        public string Result;
    }

    [Serializable]
    public class StalExSave
    {
        public List<StalExCidRec> Cids = new List<StalExCidRec>();
    }

    internal static class StalExBridge
    {
        private const string AppId = "exchange";
        private const string StoreId = "nomad.obenseuer.os.exchange";
        private const string ConflictText = "This request id was already used for a different request.";
        private const string FailedText = "The market could not complete the request.";
        private const int MaxCids = 200;
        private const int MaxCid = 64;
        private static StalExSave save = new StalExSave();

        internal static void Attach()
        {
            SaveData.Register<StalExSave>(StoreId, () => save, loaded => save = loaded ?? new StalExSave());
            Os.Handle(AppId, "exchange.status", (args) => Read(StalExMarket.Get().Status()));
            Os.Handle(AppId, "exchange.indices", (args) => Read(StalExMarket.Get().Indices()));
            Os.Handle(AppId, "exchange.list", Companies);
            Os.Handle(AppId, "exchange.quote", Quote);
            Os.Handle(AppId, "exchange.chart", Chart);
            Os.Handle(AppId, "exchange.news", News);
            Os.Handle(AppId, "exchange.portfolio", (args) => Read(StalExMarket.Get().Portfolio()));
            Os.Handle(AppId, "exchange.preview", Preview);
            Os.Handle(AppId, "exchange.buy", (args) => Place("buy", args));
            Os.Handle(AppId, "exchange.sell", (args) => Place("sell", args));
            Os.Handle(AppId, "exchange.short", (args) => Place("short", args));
            Os.Handle(AppId, "exchange.cover", (args) => Place("cover", args));
        }

        private static string Companies(string args)
        {
            return Read(StalExMarket.Get().List(OsBridge.Text(OsBridge.Args(args), "sector")));
        }

        private static string Quote(string args)
        {
            return Read(StalExMarket.Get().Quote(Required(OsBridge.Args(args), "ticker")));
        }

        private static string Chart(string args)
        {
            JObject a = OsBridge.Args(args);
            return Read(StalExMarket.Get().Chart(Required(a, "ticker"), Number(a, "hours")));
        }

        private static string News(string args)
        {
            return Read(StalExMarket.Get().News(Number(OsBridge.Args(args), "limit")));
        }

        private static string Preview(string args)
        {
            JObject a = OsBridge.Args(args);
            return Without(StalExMarket.Get().Preview(Required(a, "action"), Required(a, "ticker"), Number(a, "shares"), OsBridge.Flag(a, "margin")), "message");
        }

        private static string Place(string action, string args)
        {
            JObject a = OsBridge.Args(args);
            string ticker = Required(a, "ticker");
            double shares = Number(a, "shares");
            bool margin = OsBridge.Flag(a, "margin");
            string cid = Cid(a);
            string print = JsonConvert.SerializeObject(new object[] { action, ticker, shares, margin });
            string saved = Replay(save, cid, print);
            if (saved != null) return Without(saved, "message");
            OsBridge.CheckWritable();
            string raw = StalExMarket.Get().Trade(action, ticker, shares, margin);
            Ok(raw);
            Remember(save, cid, print, raw);
            return Without(raw, "message");
        }

        private static string Read(string json)
        {
            Ok(json);
            return json;
        }

        private static string Without(string json, string key)
        {
            JObject o = Ok(json);
            o.Remove(key);
            return o.ToString(Formatting.None);
        }

        private static JObject Ok(string json)
        {
            JObject o = JObject.Parse(json);
            if (o.Value<bool>("ok")) return o;
            string code = o.Value<string>("code") ?? "failed";
            string text = code == "denied" ? (o.Value<string>("reason") ?? "unknown") : (o.Value<string>("message") ?? FailedText);
            throw new OsBridgeException(code, text);
        }

        private static string Required(JObject a, string key)
        {
            string text = OsBridge.Text(a, key);
            if (string.IsNullOrEmpty(text)) throw new OsBridgeException("bad_args", key + " is required");
            return text;
        }

        private static double Number(JObject a, string key)
        {
            JToken t = a[key];
            if (t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float)) throw new OsBridgeException("bad_args", key + " must be a number");
            return (double)t;
        }

        private static string Cid(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }

        internal static string SelfCheck()
        {
            StalExSave state = new StalExSave();
            for (int i = 0; i < MaxCids + 5; i++) Remember(state, "c" + i, "p", "r");
            if (state.Cids.Count != MaxCids || state.Cids[0].Cid != "c5") return "cid cap";
            if (Replay(state, "c10", "p") != "r") return "cid replay";
            try
            {
                Replay(state, "c10", "other");
                return "cid conflict";
            }
            catch (OsBridgeException)
            {
            }
            return null;
        }

        private static string Replay(StalExSave state, string cid, string print)
        {
            foreach (StalExCidRec rec in state.Cids)
            {
                if (rec.Cid != cid) continue;
                if (rec.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return rec.Result;
            }
            return null;
        }

        private static void Remember(StalExSave state, string cid, string print, string result)
        {
            StalExCidRec rec = new StalExCidRec();
            rec.Cid = cid;
            rec.Print = print;
            rec.Result = result;
            state.Cids.Add(rec);
            while (state.Cids.Count > MaxCids) state.Cids.RemoveAt(0);
        }
    }
}
