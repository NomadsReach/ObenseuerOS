(function () {
  "use strict";
  const { el, ico, button, field } = Apps;
  const { HOME, SITES, shelf, BANNER, search, split, normal, resolve, searchUrl, plain } = BrowserSites;
  const HISTORY_MAX = 100;
  const MARKS_MAX = 50;
  const TITLE_MAX = 120;
  const SUGGEST_MAX = 6;
  const CHUNK = 4000;
  const PORTAL_HEADLINES = 3;
  const NO_FILE = "no such file";
  const NEWS_OFFLINE = "Headlines are shown inside the game.";
  const NO_NEWS = "No headlines yet.";
  const STATE_MAX = 7000;
  const FILE = "/browser/state.json";
  const KEY = "obx.os.browser";
  const NOT_FOUND = "Server not found";

  const OPTIONAL_MODS = [{ hosts: ["www.hello.ts"], name: "Hello OS", guid: "example.hello" }];
  const cleanHost = (value) => String(value).replace(/^https?:\/\//i, "").split(/[/?#]/)[0].replace(/:\d+$/, "").toLowerCase();
  const modForHost = (host) => OPTIONAL_MODS.find((m) => m.hosts.some((h) => plain(cleanHost(h)) === plain(cleanHost(host))));
  const classifyRoute = (url) => {
    const host = cleanHost(normal(url));
    const site = SITES.find((s) => s.host === host) || BrowserSites.siteOf(host);
    if (site) return { kind: "site", site };
    const mod = modForHost(host);
    return mod ? { kind: "mod", mod, host } : { kind: "notFound", host };
  };
  const modCardText = (host, mod) => [host + " belongs to the " + mod.name + " mod, which is not installed.", mod.page ? "Get " + mod.name + " from " + mod.page + ", install it, then restart the game." : "Install the " + mod.name + " mod and restart the game to open this page."];
  const listable = (url) => SITES.some((s) => s.host === split(url).host);
  window.BrowserMods = { OPTIONAL_MODS, modForHost, classifyRoute, modCardText, listable };

  const TIMES = "www.stalburgtimes.com";
  const SERVICES = [
    ["Stalburg Mail", "mail.stalburg.ts"],
    ["Stalburg Bank", "www.stalburgbank.ts"],
    ["Stal-Ex", "www.stalex.ts"],
  ];
  const FOOT = [
    ["Services", SERVICES],
    ["Betting and games", [["NaSpoBet", "bets.stalburg.ts"], ["LOTTO by NaSpoBet", "lottery.stalburg.ts"]]],
  ];

  function icon(name, node) {
    const s = node || ico(name);
    s.dataset.icon = name;
    s.style.mask = `url(icons/${name}.svg) center/contain no-repeat`;
    return s;
  }

  function tool(name, label, onClick) {
    const b = button("icon-btn");
    b.setAttribute("aria-label", label);
    b.title = label;
    b.append(icon(name));
    b.addEventListener("click", onClick);
    return b;
  }

  const empty = () => ({ marks: [], history: [] });
  const known = listable;

  function pathTitle(path) {
    try {
      return decodeURIComponent(path.slice(1)).slice(0, TITLE_MAX);
    } catch {
      return "";
    }
  }

  function entries(list, max) {
    if (!Array.isArray(list)) return [];
    return list
      .filter((e) => e && typeof e.url === "string" && e.url && typeof e.title === "string")
      .slice(-max)
      .map((e) => ({ url: normal(e.url), title: e.title.slice(0, TITLE_MAX) }));
  }

  function parse(text) {
    if (!text) return empty();
    let d;
    try {
      d = JSON.parse(text);
    } catch {
      return empty();
    }
    if (!d || typeof d !== "object") return empty();
    return { marks: entries(d.marks, MARKS_MAX), history: entries(d.history, HISTORY_MAX) };
  }

  function readAll(ctx) {
    return ctx.call("os.fs.size", { path: FILE }).then((size) => {
      const next = (offset, parts) => {
        if (offset >= size) return parts.join("");
        return ctx.call("os.fs.read", { path: FILE, offset, length: Math.min(CHUNK, size - offset) }).then((t) => {
          if (!t) throw { message: "short read" };
          return next(offset + t.length, parts.concat(t));
        });
      };
      return next(0, []);
    });
  }

  function localGet() {
    try {
      return localStorage.getItem(KEY);
    } catch {
      return null;
    }
  }

  function localSet(text) {
    try {
      localStorage.setItem(KEY, text);
    } catch {}
  }

  function load(ctx) {
    const source = ctx.live ? readAll(ctx) : Promise.resolve(localGet());
    return source
      .then(
        (text) => ({ state: parse(text), ok: true }),
        (err) => (err && err.message === NO_FILE ? { state: empty(), ok: true } : { state: empty(), ok: false })
      )
      .catch(() => ({ state: empty(), ok: false }));
  }

  Apps.register("browser", {
    mount(slot, ctx, opts) {
      const st = { marks: [], history: [], idx: -1 };
      const here = () => st.history[st.idx] || { url: "", title: "" };
      let writable = false;
      let retrying = false;
      let ready = false;
      let want = opts.url || "";
      let hits = [];
      let sel = -1;

      const addr = field("text", "br-input", "Address");
      addr.spellcheck = false;
      addr.autocomplete = "off";
      addr.disabled = true;
      const sug = el("ul", "br-suggest");
      sug.hidden = true;
      const wrap = el("div", "br-addr");
      wrap.append(icon("lock"), addr, sug);
      const back = tool("arrow-left", "Back", () => move(-1));
      const fwd = tool("arrow-right", "Forward", () => move(1));
      const star = tool("star", "Bookmark", toggleMark);
      const bar = el("div", "br-bar");
      bar.append(back, fwd, tool("refresh", "Reload", () => show()), wrap, star, tool("world", "Home", () => visit(HOME)));
      const markBar = el("nav", "br-marks");
      markBar.setAttribute("aria-label", "Bookmarks");
      const page = el("div", "br-page");
      const root = el("div", "br");
      root.append(bar, markBar, page);
      slot.append(root);

      function row(title, url, desc) {
        const li = el("li", "bw-result");
        const b = button("bw-result-title", title);
        b.addEventListener("click", () => visit(url));
        li.append(b, el("span", "bw-cite", url));
        if (desc) li.append(el("p", "bw-result-desc", desc));
        return li;
      }

      function listOf(rows, emptyText) {
        if (!rows.length) return el("p", "bw-empty", emptyText);
        const ul = el("ul", "bw-results");
        ul.append(...rows);
        return ul;
      }

      const shownMarks = () => st.marks.filter((m) => known(m.url));
      const markRows = () => shownMarks().map((m) => row(m.title, m.url));

      function logo(cls) {
        const b = button(cls);
        b.setAttribute("aria-label", "Stalburg Web home");
        b.title = "Stalburg Web";
        const word = el("span", "bw-word");
        word.append(el("span", "bw-word-a", "Stalburg"), " ", el("span", "bw-word-b", "Web"));
        b.append(word);
        b.addEventListener("click", () => visit(HOME));
        return b;
      }

      function searchBox(value) {
        const box = el("div", "bw-search");
        const input = field("search", "bw-input", "Search Stalburg Web");
        input.placeholder = "Search Stalburg Web";
        input.value = value;
        const go = () => visit(searchUrl(input.value.trim()));
        input.addEventListener("keydown", (e) => {
          if (e.key === "Enter") go();
        });
        const find = button("bw-go", "Search");
        find.addEventListener("click", go);
        box.append(input, find);
        return box;
      }

      function tile(s) {
        const b = button("bw-tile");
        b.addEventListener("click", () => visit(s.host));
        b.append(el("span", "bw-tile-title", s.title), el("span", "bw-tile-desc", s.desc));
        return b;
      }

      function tiles(list) {
        const grid = el("div", "bw-tiles");
        grid.append(...list.map(tile));
        return grid;
      }

      function footer(cols, copy) {
        const foot = el("footer", "bw-foot");
        const grid = el("div", "bw-foot-grid");
        grid.append(
          ...cols.map(([title, items]) => {
            const col = el("section", "bw-foot-col");
            const ul = el("ul", "bw-foot-list");
            ul.append(
              ...items.map(([label, host]) => {
                const li = el("li");
                const b = button("bw-foot-link", label);
                b.addEventListener("click", () => visit(host));
                li.append(b);
                return li;
              })
            );
            col.append(el("h4", "bw-foot-title", title), ul);
            return col;
          })
        );
        foot.append(grid, el("p", "bw-copy", copy));
        return foot;
      }

      function box(title, lines) {
        const s = el("section", "bw-box");
        const list = el("ul", "bw-plain");
        list.append(...lines.map((l) => el("li", "", l)));
        s.append(el("h3", "bw-box-title", title), list);
        return s;
      }

      function banner() {
        const b = el("section", "bw-banner");
        b.append(el("span", "bw-banner-brand", BANNER.brand), el("span", "", BANNER.line));
        return b;
      }

      function portalNews(strip, side) {
        ctx.call("news.front", {}).then(
          (r) => {
            const stories = r && Array.isArray(r.stories) ? r.stories : [];
            const heads = stories.filter((s) => s.section !== "Weather").slice(0, PORTAL_HEADLINES);
            if (!heads.length) strip.replaceChildren(el("li", "bw-empty", NO_NEWS));
            else
              strip.replaceChildren(
                ...heads.map((s) => {
                  const li = el("li");
                  const b = button("bw-headline", s.headline);
                  b.addEventListener("click", () => visit(TIMES));
                  li.append(b);
                  return li;
                })
              );
            const w = stories.find((s) => s.section === "Weather");
            side.replaceChildren(...(w ? [box("Weather", [w.headline])] : []));
          },
          () => strip.replaceChildren(el("li", "bw-empty", NEWS_OFFLINE))
        );
      }

      function portal(p) {
        const home = el("div", "bw bw-home");
        const top = el("nav", "bw-top");
        top.setAttribute("aria-label", "Stalburg Web links");
        const marks = button("bw-toplink", "Bookmarks");
        marks.addEventListener("click", () => visit(HOME + "/bookmarks"));
        const history = button("bw-toplink", "History");
        history.addEventListener("click", () => visit(HOME + "/history"));
        top.append(marks, history);
        const hero = el("section", "bw-hero");
        hero.append(logo("bw-logo-big"), searchBox(""), el("p", "bw-hint", "Search the web of Stolland, or type an address."));
        const news = el("section", "bw-news");
        const head = button("bw-news-head", "From the Stalburg Times");
        head.addEventListener("click", () => visit(TIMES));
        const strip = el("ul", "bw-headlines");
        news.append(head, strip);
        const sites = el("section", "bw-block");
        sites.append(el("h3", "bw-h", "Sites"), tiles(shelf));
        const side = el("aside", "bw-side");
        const cols = el("div", "bw-cols");
        cols.append(news, side);
        portalNews(strip, side);
        home.append(top, hero, cols, sites);
        if (shownMarks().length) {
          const saved = el("section", "bw-block");
          saved.append(el("h3", "bw-h", "Bookmarks"), listOf(markRows(), ""));
          home.append(saved);
        }
        home.append(banner(), footer(FOOT, "Stalburg Web. Stalburg, Stolland."));
        p.append(home);
      }

      function results(p, q) {
        const found = search(q);
        const root = el("div", "bw bw-serp");
        const top = el("header", "bw-serp-top");
        top.append(logo("bw-logo-small"), searchBox(q));
        const tabs = el("nav", "bw-serp-tabs");
        tabs.append(el("span", "bw-serp-tab on", "All"));
        const body = el("div", "bw-serp-body");
        if (!q.trim()) body.append(el("p", "bw-empty", "Enter a search term."));
        else if (found.length) {
          body.append(el("p", "bw-count", `About ${found.length} result${found.length === 1 ? "" : "s"}`), listOf(found.map((s) => row(s.title, s.host, s.desc)), ""));
        } else {
          body.append(el("p", "bw-empty", `Your search for "${q.slice(0, TITLE_MAX)}" did not match any site.`), el("h3", "bw-h", "Sites"), tiles(shelf));
        }
        root.append(top, tabs, body, footer(FOOT, "Stalburg Web. Stalburg, Stolland."));
        p.append(root);
      }

      function historyPage(p) {
        const root = el("div", "bw bw-doc");
        root.append(el("h2", "bw-doc-title", "History"), listOf(st.history.filter((e) => known(e.url)).reverse().map((e) => row(e.title, e.url)), "No history yet."));
        p.append(root);
      }

      function marksPage(p) {
        const root = el("div", "bw bw-doc");
        root.append(el("h2", "bw-doc-title", "Bookmarks"), listOf(markRows(), "No bookmarks yet."));
        p.append(root);
      }

      function needsMod(p, host, mod) {
        const root = el("div", "bw bw-error");
        const text = modCardText(host, mod);
        const back = button("bw-go", "Back to Stalburg Web");
        back.addEventListener("click", () => visit(HOME));
        root.append(el("h2", "bw-err-title", "This page needs a mod"), el("p", "bw-err-text", text[0]), el("p", "bw-err-text", text[1]), back);
        p.append(root);
      }

      function notFound(p, host) {
        const root = el("div", "bw bw-error");
        const tips = el("ul", "bw-tips");
        tips.append(el("li", "", "Check the address for typing mistakes."), el("li", "", "Search Stalburg Web for the site."));
        root.append(el("h2", "bw-err-title", NOT_FOUND), el("p", "bw-err-text", host + " could not be found on Stalburg Web."), el("p", "bw-err-text", "Try:"), tips, searchBox(""));
        p.append(root);
      }

      function route(url) {
        const { host, path, query } = split(url);
        const target = classifyRoute(url);
        const site = target.kind === "site" ? target.site : null;
        if (!site) {
          const mod = target.kind === "mod" ? target.mod : null;
          if (mod) return { title: "This page needs a mod", paint: (p) => needsMod(p, target.host, mod), bleed: true };
          return { title: NOT_FOUND, paint: (p) => notFound(p, host), bleed: true };
        }
        if (site.host !== HOME) {
          const paint = (p) => {
            if (!site.app || !ctx.mount(site.app, p, { path, query, visit })) notFound(p, host);
          };
          return { title: (site.pathTitle && pathTitle(path)) || site.title, paint, bleed: Boolean(site.bleed || !site.app) };
        }
        if (path === "/search") {
          const q = new URLSearchParams(query).get("q") || "";
          return { title: `Search: ${q}`.slice(0, TITLE_MAX), paint: (p) => results(p, q), bleed: true };
        }
        if (path === "/history") return { title: "History", paint: historyPage, bleed: true };
        if (path === "/bookmarks") return { title: "Bookmarks", paint: marksPage, bleed: true };
        return { title: site.title, paint: portal, bleed: true };
      }

      function retry() {
        if (retrying) return;
        retrying = true;
        load(ctx).then((r) => {
          retrying = false;
          if (!r.ok) return;
          const after = st.history.length - 1 - st.idx;
          st.marks = r.state.marks.filter((m) => !st.marks.some((x) => x.url === m.url)).concat(st.marks).slice(-MARKS_MAX);
          st.history = r.state.history.concat(st.history).slice(-HISTORY_MAX);
          st.idx = st.history.length - 1 - after;
          writable = true;
          save();
          chrome();
        });
      }

      function save() {
        if (!writable) return retry();
        const history = st.history.slice();
        let text = JSON.stringify({ marks: st.marks, history });
        while (text.length > STATE_MAX && history.length > 1) {
          history.shift();
          text = JSON.stringify({ marks: st.marks, history });
        }
        if (!ctx.live) localSet(text);
        else Apps.writeFile(ctx, FILE, text).catch(() => {});
      }

      function chrome() {
        const cur = here();
        back.disabled = st.idx <= 0;
        fwd.disabled = st.idx >= st.history.length - 1;
        icon(st.marks.some((m) => m.url === cur.url) ? "star-filled" : "star", star.firstChild);
        const shown = shownMarks();
        markBar.hidden = !shown.length;
        markBar.replaceChildren(
          ...shown.map((m) => {
            const b = button("br-mark", m.title);
            b.title = m.url;
            b.addEventListener("click", () => visit(m.url));
            return b;
          })
        );
      }

      function show() {
        if (!ready) return;
        const cur = here();
        addr.value = cur.url;
        const r = route(cur.url);
        page.replaceChildren();
        ModFrame.prune();
        page.classList.toggle("bw-bleed", Boolean(r.bleed));
        r.paint(page);
        page.scrollTop = 0;
        close();
        chrome();
      }

      function visit(url) {
        const u = normal(url);
        if (!ready || !u) return;
        if (u !== here().url) {
          st.history.splice(st.idx + 1);
          st.history.push({ url: u, title: route(u).title });
          st.history.splice(0, Math.max(0, st.history.length - HISTORY_MAX));
          st.idx = st.history.length - 1;
          save();
          browse(u);
        }
        show();
      }

      function browse(u) {
        ctx.call("virus.browse", { host: split(u).host }).catch(() => {});
      }

      function move(d) {
        const i = st.idx + d;
        if (i < 0 || i >= st.history.length) return;
        st.idx = i;
        show();
      }

      function toggleMark() {
        if (!ready) return;
        const cur = here();
        const i = st.marks.findIndex((m) => m.url === cur.url);
        if (i >= 0) st.marks.splice(i, 1);
        else if (st.marks.length < MARKS_MAX) st.marks.push({ url: cur.url, title: cur.title });
        chrome();
        save();
      }

      function go(text) {
        const url = resolve(text);
        if (url) visit(url);
      }

      function paintSug() {
        sug.hidden = !hits.length;
        sug.replaceChildren(
          ...hits.map((u, i) => {
            const li = el("li", i === sel ? "br-sug on" : "br-sug", u);
            li.addEventListener("mousedown", (e) => {
              e.preventDefault();
              go(u);
            });
            return li;
          })
        );
      }

      function close() {
        hits = [];
        sel = -1;
        paintSug();
      }

      function suggest() {
        const q = addr.value.trim().toLowerCase();
        const pool = [...SITES.map((s) => s.host), ...shownMarks().map((m) => m.url), ...st.history.filter((e) => known(e.url)).map((e) => e.url)];
        hits = [...new Set(pool)].filter((u) => q && u.includes(q)).slice(0, SUGGEST_MAX);
        sel = -1;
        paintSug();
      }

      addr.addEventListener("input", suggest);
      addr.addEventListener("blur", close);
      addr.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
          e.stopPropagation();
          if (hits.length) close();
          else {
            addr.value = here().url;
            addr.blur();
          }
        } else if (e.key === "Enter") {
          e.preventDefault();
          go(hits[sel] || addr.value);
        } else if (e.key === "ArrowDown" || e.key === "ArrowUp") {
          if (!hits.length) return;
          e.preventDefault();
          sel = Math.max(-1, Math.min(sel + (e.key === "ArrowDown" ? 1 : -1), hits.length - 1));
          paintSug();
        }
      });

      load(ctx).then((r) => {
        st.marks = r.state.marks;
        st.history = r.state.history;
        st.idx = st.history.length - 1;
        writable = r.ok;
        ready = true;
        addr.disabled = false;
        if (want) go(want);
        else if (st.history.length) show();
        else visit(HOME);
      });
      return {
        onOpen(o) {
          if (!o.url) return;
          if (ready) go(o.url);
          else want = o.url;
        },
      };
    },
  });
})();
