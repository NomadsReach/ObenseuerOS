(function () {
  "use strict";
  const { el, button } = Apps;

  const SECTIONS = ["Markets", "Sport", "Lottery", "Weather", "Local"];
  const FOOT = [
    ["Sections", SECTIONS.map((s) => [s, s])],
    ["The Times", [["About us", "about"], ["Corrections", "corrections"]]],
    ["Legal", [["Privacy", "privacy"], ["Terms of use", "terms"]]],
  ];
  const PAGES = {
    about: ["About us", ["Stalburg Times LLC publishes the Stalburg Times from Stalburg. The paper has been printed since 1881.", "A new edition comes out each morning at 06:00 game time."]],
    corrections: ["Corrections", ["Errors are corrected in the next edition, on the page where the error was printed."]],
    privacy: ["Privacy", ["The Stalburg Times does not ask for your name or an account to read the paper."]],
    terms: ["Terms of use", ["The stories in this paper are for reading. Copy them only with the paper's permission."]],
  };
  const ADS = [
    { key: "osmo", label: "Osmo Olut", line: "The World's Strongest Beer." },
    { key: "steel", label: "Stalburg Steel", line: "Steel for the whole of Stolland." },
  ];
  const NO_HOST = "The Stalburg Times works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const NOT_YET = "The first edition comes out at 06:00 game time.";
  const EMPTY_COL = "Nothing today.";
  const NO_WEATHER = "No weather report today.";
  const BAD_REPLY = "The game sent an unexpected reply.";
  const CAPTION = "Stalburg Times file photo.";
  const TAGS = { Markets: "money", Sport: "football", Lottery: "lottery", Weather: "weather", Local: "street" };
  const MOST_READ = 5;
  const RELATED = 3;
  const RETRY_MS = 2000;
  const RETRY_MAX = 10;

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || "Something went wrong.";
  }

  function dayText(day, weekday) {
    return "Day " + Apps.num(day, 0) + ", " + weekday;
  }

  function figure(row) {
    if (row.unit === "OC") return Apps.money(row.value, "OC", { decimals: row.dec });
    const n = Apps.num(row.value, row.dec);
    return row.unit ? n + " " + row.unit : n;
  }

  function chips(nums) {
    const box = el("div", "times-chips");
    box.append(...nums.map((n) => el("span", "times-chip", Apps.num(n, 0))));
    return box;
  }

  function figures(rows) {
    const dl = el("dl", "times-figures");
    for (const row of rows) dl.append(el("dt", "", row.label), el("dd", "times-fig", figure(row)));
    return dl;
  }

  function photo(section, seed, thumb, cls, caption) {
    const p = window.PHOTOS && window.PHOTOS.pick(TAGS[section] || "scene", seed);
    if (!p) return [];
    const fig = el("figure", cls);
    const img = el("img");
    img.alt = "";
    img.loading = "lazy";
    img.addEventListener("error", () => fig.remove());
    img.src = thumb ? p.thumb : p.file;
    fig.append(img);
    if (caption) fig.append(el("figcaption", "", caption));
    return [fig];
  }

  function footer(openLink) {
    const foot = el("footer", "times-foot");
    const grid = el("div", "times-foot-grid");
    grid.append(
      ...FOOT.map(([title, items]) => {
        const col = el("section", "times-foot-col");
        const ul = el("ul", "times-foot-list");
        ul.append(
          ...items.map(([label, key]) => {
            const li = el("li");
            const b = button("times-foot-link", label);
            b.addEventListener("click", () => openLink(key));
            li.append(b);
            return li;
          })
        );
        col.append(el("h4", "times-foot-title", title), ul);
        return col;
      })
    );
    foot.append(grid, el("p", "times-copy", "© Stalburg Times LLC. All rights reserved."));
    return foot;
  }

  Apps.register("news", {
    mount(slot, ctx) {
      const st = { day: null, req: 0, days: [], stories: [] };

      const notice = el("p", "times-notice");
      notice.hidden = true;
      notice.setAttribute("role", "alert");

      function setNotice(text) {
        notice.textContent = text;
        notice.hidden = !text;
      }

      function call(method, args) {
        return ctx.call(method, args).catch((e) => {
          setNotice(textOf(e));
          return null;
        });
      }

      const dateline = el("span", "times-date");
      const weather = el("span", "times-weather");
      const picker = el("select", "times-pick");
      picker.setAttribute("aria-label", "Edition");
      picker.addEventListener("change", () => openFront(Number(picker.value)));
      const util = el("div", "times-util");
      const edition = el("span", "times-edition");
      edition.append(el("span", "", "Edition"), picker);
      util.append(dateline, weather, edition);

      const masthead = el("h2", "times-title", "Stalburg Times");
      const head = el("header", "times-head");
      head.append(masthead, el("p", "times-tag", "Established 1881"));

      const body = el("div", "times-body");
      const root = el("div", "times");

      function scrollTo(name) {
        const sec = body.querySelector(`[data-sec="${name}"]`);
        if (sec) sec.scrollIntoView({ block: "start" });
        else root.scrollTop = 0;
      }

      function goTo(name) {
        if (body.querySelector(".times-article")) openFront(st.day).then(() => scrollTo(name));
        else scrollTo(name);
      }

      function showPage(key) {
        const [title, paras] = PAGES[key];
        const box = el("article", "times-article");
        const crumbs = el("p", "times-crumbs");
        const front = button("times-crumb", "Front page");
        front.addEventListener("click", () => openFront(st.day));
        crumbs.append(front, el("span", "", "/ " + title));
        const text = el("div", "times-article-text");
        text.append(...paras.map((p) => el("p", "", p)));
        box.append(crumbs, el("h2", "times-article-title", title), text);
        body.replaceChildren(box);
        root.scrollTop = 0;
      }

      function openLink(key) {
        if (PAGES[key]) showPage(key);
        else goTo(key);
      }

      const nav = el("nav", "times-nav");
      nav.setAttribute("aria-label", "Sections");
      const home = button("times-nav-item", "ST NEWS");
      home.addEventListener("click", () => goTo(""));
      nav.append(home, ...SECTIONS.map((name) => {
        const b = button("times-nav-item", name);
        b.addEventListener("click", () => goTo(name));
        return b;
      }));

      root.append(head, nav, util, notice, body, footer(openLink));
      slot.append(root);

      function link(s, day) {
        const b = button("times-link", s.headline);
        b.addEventListener("click", () => openArticle(day, s.i));
        return b;
      }

      function card(day, s) {
        const box = el("article", "times-card");
        const title = el("h4", "times-card-title");
        title.append(link(s, day));
        const text = el("div", "times-card-body");
        text.append(el("p", "times-kicker", s.section), title);
        box.append(...photo(s.section, day * 7 + s.i, true, "times-thumb"), text);
        return box;
      }

      function cards(day, items) {
        const grid = el("div", "times-cards");
        grid.append(...items.map((s) => card(day, s)));
        return grid;
      }

      function leadBlock(day, lead) {
        const box = el("section", "times-lead");
        const title = el("h3", "times-lead-title");
        title.append(link(lead, day));
        const text = el("div", "times-lead-text");
        text.append(el("p", "times-kicker", lead.section), title);
        box.append(...photo(lead.section, day * 7 + lead.i, false, "times-hero", CAPTION), text, el("p", "times-lead-body", lead.body));
        if (lead.rows.length) box.append(figures(lead.rows));
        return box;
      }

      function sectionBlock(day, name, items) {
        const sec = el("section", "times-sec");
        sec.dataset.sec = name;
        sec.append(el("h3", "times-sec-title", name));
        if (items.length) sec.append(cards(day, items));
        else sec.append(el("p", "times-empty", EMPTY_COL));
        return sec;
      }

      function mostRead(day, items) {
        const box = el("section", "times-box");
        const list = el("ol", "times-most");
        list.append(
          ...items.map((s) => {
            const li = el("li");
            li.append(link(s, day));
            return li;
          })
        );
        box.append(el("h3", "times-box-title", "Latest"), list);
        return box;
      }

      function ad(a) {
        const box = el("section", "times-ad");
        box.dataset.ad = a.key;
        box.append(el("p", "times-ad-label", "Advertisement"), el("p", "times-ad-brand", a.label), el("p", "times-ad-line", a.line));
        return box;
      }

      function frontView(day, lead, stories) {
        const rest = stories.slice(1);
        const main = el("div", "times-main");
        main.append(leadBlock(day, lead));
        for (const name of SECTIONS) main.append(sectionBlock(day, name, rest.filter((s) => s.section === name)));
        const side = el("aside", "times-side");
        side.append(mostRead(day, rest.slice(0, MOST_READ)), ...ADS.map(ad));
        const grid = el("div", "times-front");
        grid.append(main, side);
        return grid;
      }

      function related(a) {
        if (a.day !== st.day) return null;
        const others = st.stories.filter((s) => s.i !== a.i);
        const same = others.filter((s) => s.section === a.section);
        const pick = (same.length ? same : others).slice(0, RELATED);
        if (!pick.length) return null;
        const box = el("section", "times-related");
        box.append(el("h3", "times-sec-title", "Related stories"), cards(a.day, pick));
        return box;
      }

      function articleView(a) {
        const box = el("article", "times-article");
        const crumbs = el("p", "times-crumbs");
        const front = button("times-crumb", "Front page");
        front.addEventListener("click", () => openFront(st.day));
        crumbs.append(front, el("span", "", "/ " + a.section));
        const weekday = st.days.find((d) => d.day === a.day);
        const byline = el("p", "times-byline", "By Stalburg Times staff" + (weekday ? " · " + dayText(a.day, weekday.weekday) : ""));
        const text = el("div", "times-article-text");
        for (const para of String(a.body || "").split(/\n+/)) {
          if (para.trim()) text.append(el("p", "", para.trim()));
        }
        box.append(crumbs, el("p", "times-kicker", a.section), el("h2", "times-article-title", a.headline), byline, ...photo(a.section, a.day * 7 + a.i, false, "times-hero", CAPTION), text);
        if (a.rows.length) box.append(figures(a.rows));
        if (a.nums.length) box.append(chips(a.nums));
        const more = related(a);
        if (more) box.append(more);
        return box;
      }

      function loadDays() {
        return call("news.editions", {}).then((r) => {
          if (!r || !Array.isArray(r.days)) return;
          st.days = r.days;
          picker.replaceChildren(...r.days.map((d) => {
            const o = el("option", "", dayText(d.day, d.weekday));
            o.value = String(d.day);
            return o;
          }));
        });
      }

      let tries = 0;
      let retry = 0;

      function later() {
        clearTimeout(retry);
        if (tries >= RETRY_MAX) return;
        tries++;
        retry = setTimeout(() => {
          if (root.isConnected) openFront();
        }, RETRY_MS);
      }

      function openFront(day) {
        const args = Number.isInteger(day) ? { day } : {};
        const req = ++st.req;
        setNotice("");
        return ctx.call("news.front", args).then(
          (r) => showFront(req, r),
          (e) => {
            setNotice(textOf(e));
            if (e && e.error === "busy") later();
          }
        );
      }

      function showFront(req, r) {
        if (req !== st.req) return;
        if (!r || !Array.isArray(r.stories)) return;
        if (!r.stories.length || !r.lead) {
          st.stories = [];
          dateline.textContent = "";
          weather.replaceChildren();
          body.replaceChildren(el("p", "times-empty", NOT_YET));
          later();
          return;
        }
        clearTimeout(retry);
        tries = 0;
        st.day = r.day;
        st.stories = r.stories;
        picker.value = String(r.day);
        dateline.textContent = dayText(r.day, r.weekday);
        const w = r.stories.find((s) => s.section === "Weather");
        weather.replaceChildren(el("span", "times-util-label", "Weather"), w ? link(w, r.day) : el("span", "times-empty", NO_WEATHER));
        body.replaceChildren(frontView(r.day, r.lead, r.stories));
        root.scrollTop = 0;
      }

      function openArticle(day, i) {
        setNotice("");
        return call("news.article", { day, i }).then((a) => {
          if (!a || typeof a.headline !== "string") {
            if (a) setNotice(BAD_REPLY);
            return;
          }
          body.replaceChildren(articleView(a));
          root.scrollTop = 0;
        });
      }

      function refresh() {
        const top = st.days.length ? st.days[0].day : null;
        return loadDays().then(() => {
          if (st.days.length && st.days[0].day !== top) return openFront();
          if (st.day !== null) picker.value = String(st.day);
        });
      }

      loadDays().then(() => openFront());
      return { onOpen: refresh };
    },
  });
})();
