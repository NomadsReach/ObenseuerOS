(function () {
  "use strict";

  const HOME = "www.stalburg.ts";
  const URL_MAX = 300;
  const ADDRESS = /^[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,}(:\d+)?(\/\S*)?$/i;
  const SITES = [
    { host: HOME, title: "Stalburg Web", desc: "", keywords: [], app: null, brand: "web", mark: "Web" },
    { host: "mail.stalburg.ts", title: "Stalburg Mail", desc: "Email for Stalburg residents.", keywords: ["email", "inbox", "post"], alias: ["mail.stalburg.com"], app: "mail", brand: "mail", mark: "MAIL" },
    { host: "www.stalburgbank.ts", title: "Stalburg Bank", desc: "Online banking: balances and loans.", keywords: ["bank", "banking", "money", "loan", "account"], app: "bank", brand: "bank", mark: "SBB" },
    { host: "www.stalburgtimes.com", title: "Stalburg Times", desc: "News from the outside world.", keywords: ["news", "paper", "newspaper"], app: "news", bleed: true, brand: "times", mark: "Times" },
    { host: "www.stalex.ts", title: "Stal-Ex", desc: "The Stalburg stock exchange.", keywords: ["stocks", "shares", "market", "trade", "stal-ex", "exchange"], app: "exchange", brand: "stalex", mark: "STAL-EX" },
    { host: "jobs.stalburg.ts", title: "Jobs", desc: "Work stations and wages.", keywords: ["work", "job", "wages", "shift"], app: "jobs", bleed: true, brand: "jobs", mark: "JOBS" },
    { host: "shop.stalburg.ts", title: "Shop", desc: "Goods for sale.", keywords: ["buy", "store", "shopping"], app: "shop", bleed: true, brand: "shop", mark: "SHOP" },
    { host: "bets.stalburg.ts", title: "NaSpoBet", desc: "Football, ice hockey and volleyball odds.", keywords: ["bet", "odds", "football", "hockey", "volleyball"], app: "bets", brand: "bets", mark: "BETS" },
    { host: "lottery.stalburg.ts", title: "LOTTO by NaSpoBet", desc: "Scratch cards and a number draw.", keywords: ["lotto", "lottery", "draw", "numbers", "tickets", "scratch", "scratchcard"], app: "lotto", brand: "lotto", mark: "LOTTO" },
    { host: "wiki.stalburg.ts", title: "Stalburg Wiki", desc: "Articles on Stolland and Obenseuer.", keywords: ["wiki", "stolland", "stalburg", "lore"], alias: ["stalburg.net"], app: "wiki", pathTitle: true, brand: "wiki", mark: "WIKI" },
    { host: "www.eyebook.com", title: "EYEbook", desc: "International social network.", keywords: ["social", "friends", "eyebook", "profile", "postal", "messenger"], app: "eyebook", brand: "eyebook", mark: "EYEbook" },
    { host: "www.pcmaster.ts", title: "PC Master", desc: "Computers, parts and security software.", keywords: ["antivirus", "virus", "security", "scan", "protection", "pc", "computer"], app: "pcmaster", bleed: true, brand: "pcmaster", mark: "PC MASTER" },
    { host: "www.turvapro.ts", title: "TurvaPro 2000", desc: "The #1 antivirus. Free online scan!", keywords: ["antivirus", "virus", "security", "scan", "protection", "pc", "computer"], app: "turvasite", bleed: true, brand: "turvapro", mark: "TURVAPRO" },
    { host: "www.free-rm.ts", title: "FREE RM GENERATOR", desc: "Generate 1 000 RM in seconds. 100 % working.", keywords: ["free", "rm", "money", "generator", "cheat"], app: "freerm", bleed: true, brand: "freerm", mark: "FREE RM" },
    { host: "www.kolhola-warez.ts", title: "Kolhola Warez", desc: "Cracked games and tools. Top downloads.", keywords: ["crack", "keygen", "games", "download", "warez", "free"], app: "warez", bleed: true, brand: "warez", mark: "WAREZ" },
    { host: "www.voittaja.ts", title: "VOITTAJA!", desc: "You are our lucky visitor. Claim your prize.", keywords: ["prize", "win", "winner", "lucky", "claim"], app: "voittaja", bleed: true, brand: "voittaja", mark: "VOITTAJA" },
  ];
  const BANNER = { brand: "Osmo Olut", line: "The World's Strongest Beer." };
  const shelf = SITES.filter((s) => s.host !== HOME);
  const plain = (host) => host.replace(/^www\./, "");
  const siteOf = (host) => {
    const clean = String(host).toLowerCase().replace(/:\d+$/, "");
    return SITES.find((s) => [s.host, ...(s.alias || [])].some((h) => plain(h) === plain(clean)));
  };

  function setSites(mods) {
    for (let i = SITES.length - 1; i >= 0; i--) if (SITES[i].mod) SITES.splice(i, 1);
    for (const s of mods) {
      if (SITES.some((x) => x.host === s.host)) continue;
      SITES.push({
        host: s.host,
        title: s.title,
        desc: typeof s.description === "string" ? s.description : "",
        keywords: Array.isArray(s.keywords) ? s.keywords.filter((k) => typeof k === "string") : [],
        app: s.id,
        mod: true,
        bleed: true,
        brand: "mod",
        mark: [...s.title].slice(0, 8).join("").toUpperCase(),
      });
    }
    shelf.splice(0, shelf.length, ...SITES.filter((s) => s.host !== HOME));
  }

  const fold = (s) => s.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");

  function search(query) {
    const tokens = fold(query).split(/\s+/).filter(Boolean);
    if (!tokens.length) return [];
    const scored = [];
    for (const s of shelf) {
      const fields = [[s.title, 5], [plain(s.host), 3], [s.desc, 1]].concat(s.keywords.map((k) => [k, 3])).map(([t, w]) => [fold(t), w]);
      let ok = true;
      let score = 0;
      for (const tok of tokens) {
        const hit = fields.filter(([t]) => t.includes(tok));
        ok = ok && hit.length > 0;
        score += hit.reduce((n, f) => n + f[1], 0);
      }
      if (ok) scored.push({ s, score });
    }
    return scored.sort((a, b) => b.score - a.score).map((x) => x.s);
  }

  function split(url) {
    const m = /^([^/?#]*)([^?#]*)\??([^#]*)/.exec(url);
    return { host: m[1].toLowerCase(), path: m[2], query: m[3] };
  }

  function normal(url) {
    const { host, path, query } = split(url);
    const site = siteOf(host);
    return ((site ? site.host : host) + path.replace(/\/+$/, "") + (query ? "?" + query : "")).slice(0, URL_MAX);
  }

  function searchUrl(text) {
    return HOME + "/search?q=" + encodeURIComponent(text);
  }

  function resolve(text) {
    const s = text.trim();
    if (!s) return "";
    const bare = s.replace(/^https?:\/\//i, "");
    const known = siteOf(split(bare).host);
    return known || (!/\s/.test(bare) && ADDRESS.test(bare)) ? normal(bare) : searchUrl(s);
  }

  window.BrowserSites = { HOME, SITES, shelf, BANNER, search, split, normal, resolve, searchUrl, setSites, plain, siteOf };
})();
