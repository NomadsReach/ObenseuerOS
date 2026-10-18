(function () {
  "use strict";
  const { el, button, field, web, money, num } = Apps;

  const POLL_MS = 5000;
  const MAX_RETRY_MS = 60000;
  const NEWS_LIMIT = 20;
  const RANGES = [["1D", 24], ["3D", 72], ["7D", 168]];
  const TABS = [["markets", "Markets"], ["portfolio", "Portfolio"], ["news", "News"]];
  const CLOSED = "The market is closed.";
  const OPENS_SOON = "Market opens soon";
  const BUSY = "The game is saving, try again in a moment.";
  const BAD_REPLY = "The market sent an unexpected reply.";
  const NBSP = "\u00A0";
  const NS = "http://www.w3.org/2000/svg";
  const FOOT_COLS = [
    ["Markets", [["Market overview", "overview"], ["Top gainers", "gainers"], ["Top losers", "losers"], ["Sectors", "sectors"], ["Market notices", "notices"]]],
    ["Trading", [["Order types", "orders"], ["Margin loans", "margin"], ["Short selling", "short"], ["Dividends", "dividends"]]],
    ["Help", [["Account statements", "statements"]]],
    ["Stal-Ex", [["About the exchange", "about"], ["Listings", "listings"]]],
  ];
  const INFO = {
    overview: ["Market overview", "The average of all listed companies over the last 24 hours, and how many companies rose and how many fell. The figures sit above the company list on the Markets tab."],
    gainers: ["Top gainers", "The companies with the biggest rise over the last day. They are listed under Biggest movers and Top gainers on the Markets tab."],
    losers: ["Top losers", "The companies with the biggest fall over the last day. They are listed under Biggest movers and Top losers on the Markets tab."],
    sectors: ["Sectors", "Filter the company list by sector with the Sector box above it. Choose All sectors to see every company again."],
    notices: ["Market notices", "A notice is an event that moves one company or a whole sector. Each notice shows how many hours it has left. Pick a company to see its notices."],
    orders: ["Order types", "Buy and Sell trade shares. Short sells shares you do not own, and Cover buys them back to close the short. Choose the order in the Trade panel and review it before it is sent."],
    margin: ["Margin loans", "Choose Margin loan under Funding to borrow part of the price. The initial margin is half of the order value. A long position is called when its equity falls below 30 per cent of its value. A margin loan accrues interest of 0.2 per cent a day."],
    short: ["Short selling", "A short sells shares you do not own. The sale money and a collateral are held in escrow. The position is called when the price rises too far against you. Cover closes it."],
    dividends: ["Dividends", "A company pays its dividend on its dividend cycle. The next dividend day and the cycle length are in the details of each company. Dividends you receive are counted in your portfolio."],
    statements: ["Account statements", "The Ledger on the Portfolio tab lists every entry on your account: buys, sells, dividends, margin interest and margin calls, with the day and hour."],
    about: ["About the exchange", "Stal-Ex is the stock exchange of Stalburg."],
    listings: ["Listings", "Every company that trades on Stal-Ex is in the company list on the Markets tab, with its ticker, name, price and one-day change. The Sector box filters the list, and the search box finds a company."],
  };
  const DISCLAIMER = "Stal-Ex is the stock exchange of Stalburg. Prices are indicative. Shares can fall as well as rise, and margin and short positions can lose more than the money put in. Nothing on this site is investment advice.";

  function share(v) {
    return num(v, 2) + NBSP + "%";
  }

  function pct(v) {
    if (!Number.isFinite(v)) return num(v, 2);
    const s = num(v, 2);
    return (v > 0 && /[1-9]/.test(s) ? "+" : "") + s + NBSP + "%";
  }

  function signed(v, currency) {
    const s = money(v, currency);
    return (v > 0 && /[1-9]/.test(s) ? "+" : "") + s;
  }

  function trend(v) {
    return v > 0 ? "stalex-up" : v < 0 ? "stalex-down" : "";
  }

  function when(day, hour) {
    return `Day ${num(day, 0)}, ${num(hour, 0).padStart(2, "0")}:00`;
  }

  function textOf(e) {
    if (!e || e.error === "failed") return CLOSED;
    if (e.error === "busy") return BUSY;
    return e.message || BAD_REPLY;
  }

  function mark() {
    const s = document.createElementNS(NS, "svg");
    s.setAttribute("viewBox", "0 0 24 24");
    s.setAttribute("class", "stalex-mark");
    s.setAttribute("aria-hidden", "true");
    const line = document.createElementNS(NS, "polyline");
    line.setAttribute("points", "2,17 8,10 13,13 22,4");
    const arrow = document.createElementNS(NS, "polyline");
    arrow.setAttribute("points", "16,4 22,4 22,10");
    s.append(line, arrow);
    return s;
  }

  function footer(open) {
    const foot = el("footer", "stalex-foot");
    const inner = el("div", "stalex-foot-inner");
    const cols = el("div", "stalex-foot-cols");
    for (const [heading, links] of FOOT_COLS) {
      const col = el("div", "stalex-foot-col");
      const list = el("ul");
      list.append(...links.map(([label, key]) => {
        const item = el("li");
        const b = button("stalex-foot-link", label);
        b.addEventListener("click", () => open(key));
        item.append(b);
        return item;
      }));
      col.append(el("h3", "", heading), list);
      cols.append(col);
    }
    inner.append(cols, el("p", "stalex-disclaimer", DISCLAIMER), el("p", "stalex-copy", "© Stal-Ex, Stalburg."));
    foot.append(inner);
    return foot;
  }

  function kpi(label, value, cls) {
    const box = el("div", "stalex-kpi");
    box.append(el("span", "stalex-label", label), el("span", `stalex-num stalex-kpi-value ${cls}`, value));
    return box;
  }

  function pair(label, value) {
    const d = el("div", "stalex-kv");
    d.append(el("dt", "stalex-label", label), el("dd", "stalex-num", value));
    return d;
  }

  function grid(heads, rows, empty) {
    const numeric = heads.map((_, i) => rows.some((r) => (r.cells[i].cls || "").includes("stalex-num")));
    const hr = el("tr");
    hr.append(...heads.map((h, i) => el("th", numeric[i] ? "stalex-num" : "", h)));
    const thead = el("thead");
    thead.append(hr);
    const body = el("tbody");
    if (!rows.length) {
      const tr = el("tr");
      const td = el("td", "stalex-empty", empty);
      td.colSpan = heads.length;
      tr.append(td);
      body.append(tr);
    }
    for (const r of rows) {
      const tr = el("tr", r.cls || "");
      if (r.pick) {
        tr.tabIndex = 0;
        tr.addEventListener("click", r.pick);
        tr.addEventListener("keydown", (e) => {
          if (e.key === "Enter") r.pick();
        });
      }
      tr.append(...r.cells.map((c) => el("td", c.cls || "", c.text)));
      body.append(tr);
    }
    const table = el("table", "stalex-tbl");
    table.append(thead, body);
    return table;
  }

  Apps.register("exchange", {
    mount(slot, ctx) {
      const st = { currency: "RM", key: null, loaded: false, range: 24, ticker: "", sector: "", list: [], indices: null, portfolio: null, news: [], quote: null, chart: null, seq: 0, loadSeq: 0, detailSeq: 0, retryMs: POLL_MS, retryAt: 0 };

      const notice = el("p", "stalex-notice");
      notice.setAttribute("role", "alert");
      notice.hidden = true;

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

      const title = el("h2", "stalex-brand");
      title.append(mark(), el("span", "", "Stal-Ex"));
      const search = field("search", "stalex-input stalex-search", "Search companies");
      search.placeholder = "Search companies";
      const cash = el("span", "stalex-num stalex-cash");
      const clock = el("span", "stalex-clock");
      const utility = el("div", "stalex-utility");
      utility.append(el("span", "", "The Stalburg stock exchange"), clock, cash);
      const masthead = el("div", "stalex-masthead");
      masthead.append(title, search);

      const tabs = el("nav", "stalex-tabs");
      tabs.setAttribute("aria-label", "Sections");
      const tabButtons = TABS.map(([id, label]) => {
        const b = button("stalex-tab", label);
        b.dataset.view = id;
        b.addEventListener("click", () => pick(id));
        return b;
      });
      tabs.append(...tabButtons);
      const head = el("header", "stalex-head");
      head.append(utility, masthead, tabs);
      const ticker = el("div", "stalex-ticker");

      const body = el("div", "stalex-body");
      body.hidden = true;
      const markets = el("section", "stalex-markets");
      const bar = el("div", "stalex-bar");
      const cols = el("div", "stalex-cols");
      const left = el("div", "stalex-left");
      const right = el("div", "stalex-right");
      cols.append(left, right);
      markets.append(bar, cols);
      const portfolio = el("section", "stalex-portfolio");
      const news = el("section", "stalex-news");
      body.append(markets, portfolio, news);

      const sectorSel = el("select", "stalex-input stalex-sector");
      sectorSel.setAttribute("aria-label", "Sector");
      const moversBox = el("div", "stalex-movers");
      const tableBox = el("div", "stalex-table");
      left.append(sectorSel, moversBox, tableBox);

      const detailHead = el("div", "stalex-detail");
      const rangeBar = el("div", "stalex-seg stalex-range");
      rangeBar.setAttribute("role", "group");
      rangeBar.setAttribute("aria-label", "Chart range");
      const rangeButtons = RANGES.map(([label, hours]) => {
        const b = button("", label);
        b.dataset.hours = String(hours);
        b.addEventListener("click", () => setRange(hours));
        return b;
      });
      rangeBar.append(...rangeButtons);
      const chartBox = el("div", "stalex-chart-box");
      const notices = el("ul", "stalex-notices");
      const trade = ExchangeTrade.panel(ctx, () => loadAll());
      right.append(detailHead, rangeBar, chartBox, notices, trade.el);

      const newsList = el("ul", "stalex-news-list");
      news.append(newsList);
      const summary = el("dl", "stalex-figures");
      const positions = el("div", "stalex-positions");
      const ledger = el("div", "stalex-ledger");
      portfolio.append(summary, positions, ledger);

      const info = el("section", "stalex-info");
      info.hidden = true;
      function showInfo(key) {
        const [title, text] = INFO[key];
        const close = button("stalex-btn", "Close");
        close.addEventListener("click", () => {
          info.hidden = true;
        });
        info.replaceChildren(el("h3", "stalex-name", title), el("p", "", text), close);
        info.hidden = false;
        info.scrollIntoView({ block: "start" });
      }

      const root = el("div", "stalex");
      root.append(head, ticker, notice, info, body, footer(showInfo));
      web(slot, "Stal-Ex", "https://www.stalex.ts/", ctx.embedded).append(root);

      function pick(view) {
        for (const b of tabButtons) {
          const on = b.dataset.view === view;
          b.classList.toggle("active", on);
          b.setAttribute("aria-selected", String(on));
        }
        markets.hidden = view !== "markets";
        portfolio.hidden = view !== "portfolio";
        news.hidden = view !== "news";
      }

      function closed() {
        st.key = null;
        st.loaded = false;
        body.hidden = true;
        cash.textContent = "";
        clock.textContent = "";
        setNotice(CLOSED);
      }

      function tick() {
        const n = ++st.seq;
        return call("exchange.status", {}).then((s) => {
          if (n !== st.seq) return;
          if (!s || !s.ready) {
            closed();
            return;
          }
          setNotice("");
          body.hidden = false;
          st.currency = s.currency;
          cash.textContent = "Cash " + money(s.cash, s.currency);
          clock.textContent = s.day < 0 ? OPENS_SOON : when(s.day, s.hour);
          if (s.key !== st.key || !st.loaded) {
            if (Date.now() < st.retryAt) return;
            st.key = s.key;
            loadAll();
          }
        });
      }

      function loadAll() {
        const n = ++st.loadSeq;
        return Promise.all([call("exchange.indices", {}), call("exchange.list", {}), call("exchange.portfolio", {}), call("exchange.news", { limit: NEWS_LIMIT })]).then(([ix, list, port, items]) => {
          if (n !== st.loadSeq) return;
          st.loaded = Boolean(ix && list && port && items);
          if (st.loaded) {
            st.retryMs = POLL_MS;
          } else {
            st.retryAt = Date.now() + st.retryMs;
            st.retryMs = Math.min(st.retryMs * 2, MAX_RETRY_MS);
          }
          if (ix) st.indices = ix;
          if (list) st.list = list.items || [];
          if (port) st.portfolio = port;
          if (items) st.news = items.items || [];
          if (!st.ticker && st.list.length) st.ticker = st.list[0].t;
          renderBar();
          renderMovers();
          renderList();
          renderPortfolio();
          renderNews();
          trade.set(st.ticker, st.currency);
          return loadDetail();
        });
      }

      function loadDetail() {
        if (!st.ticker) return Promise.resolve();
        const n = ++st.detailSeq;
        const t = st.ticker;
        return Promise.all([call("exchange.quote", { ticker: t }), call("exchange.chart", { ticker: t, hours: st.range })]).then(([q, c]) => {
          if (n !== st.detailSeq || t !== st.ticker) return;
          st.quote = q;
          st.chart = c;
          renderDetail();
        });
      }

      function select(t) {
        st.ticker = t;
        st.quote = null;
        st.chart = null;
        trade.set(t, st.currency);
        renderList();
        renderDetail();
        loadDetail();
      }

      function setRange(hours) {
        st.range = hours;
        for (const b of rangeButtons) b.setAttribute("aria-pressed", String(Number(b.dataset.hours) === hours));
        loadDetail();
      }

      function chip(item) {
        const b = button("stalex-chip", "");
        b.append(el("span", "stalex-tick", item.t), el("span", `stalex-num ${trend(item.c)}`, pct(item.c)));
        b.addEventListener("click", () => select(item.t));
        return b;
      }

      function renderBar() {
        const ix = st.indices;
        const movers = (ix && ix.movers) || [];
        const track = el("div", "stalex-track");
        track.append(...movers.map(chip), ...movers.map(chip));
        ticker.replaceChildren(el("span", "stalex-label stalex-ticker-label", "Biggest movers"), track);
        bar.replaceChildren(
          kpi("Market average, 24 h", ix ? pct(ix.avg24) : "-", ix ? trend(ix.avg24) : ""),
          kpi("Rising", ix ? num(ix.up, 0) : "-", "stalex-up"),
          kpi("Falling", ix ? num(ix.down, 0) : "-", "stalex-down")
        );
      }

      function renderMovers() {
        const ix = st.indices || {};
        const group = (title, items) => {
          const box = el("div", "stalex-group");
          box.append(el("h3", "stalex-heading", title), ...(items || []).map(chip));
          return box;
        };
        moversBox.replaceChildren(group("Top gainers", ix.gainers), group("Top losers", ix.losers));
      }

      function renderList() {
        const q = search.value.trim().toLowerCase();
        const sectors = [...new Set(st.list.map((i) => i.s))];
        const all = el("option", "", "All sectors");
        all.value = "";
        sectorSel.replaceChildren(all, ...sectors.map((s) => {
          const o = el("option", "", s);
          o.value = s;
          return o;
        }));
        if (!sectors.includes(st.sector)) st.sector = "";
        sectorSel.value = st.sector;
        const rows = st.list
          .filter((i) => (!st.sector || i.s === st.sector) && (!q || [i.n, i.t, i.s].some((x) => x.toLowerCase().includes(q))))
          .map((i) => ({
            cls: i.t === st.ticker ? "stalex-selected" : "",
            pick: () => select(i.t),
            cells: [{ text: i.t }, { text: i.n }, { text: money(i.p, st.currency, { decimals: 2 }), cls: "stalex-num" }, { text: pct(i.c), cls: `stalex-num ${trend(i.c)}` }],
          }));
        tableBox.replaceChildren(grid(["Ticker", "Company", "Price", "1 day"], rows, "No companies match."));
      }

      function renderDetail() {
        const q = st.quote;
        const cur = st.currency;
        const chartPoints = (st.chart && st.chart.points) || [];
        chartBox.replaceChildren(ExchangeChart.line(chartPoints));
        if (!q) {
          detailHead.replaceChildren(el("p", "stalex-empty", "Pick a company to see its details."));
          notices.replaceChildren();
          return;
        }
        const figures = el("dl", "stalex-figures");
        figures.append(
          pair("1 day", pct(q.c1)),
          pair("3 days", pct(q.c3)),
          pair("7 days", pct(q.c7)),
          pair("High, 7 days", money(q.hi7, cur, { decimals: 2 })),
          pair("Low, 7 days", money(q.lo7, cur, { decimals: 2 })),
          pair("Dividend yield", share(q.yield)),
          pair("Next dividend", `Day ${num(q.nextDividendDay, 0)}`),
          pair("Dividend cycle", `Every ${num(q.dividendDays, 0)} days`)
        );
        const price = el("div", "stalex-price");
        price.append(el("span", "stalex-num stalex-last", money(q.p, cur, { decimals: 2 })), el("span", `stalex-num ${trend(q.c1)}`, pct(q.c1)));
        detailHead.replaceChildren(el("h3", "stalex-name", q.n), el("p", "stalex-meta", `${q.t} \u00B7 ${q.s}`), price, figures);
        const events = (q.events || []).map((e) => el("li", "", `${e.target}${e.sector ? " (sector)" : ""}, ${num(e.hoursLeft, 0)} h left`));
        notices.replaceChildren(...(events.length ? events : [el("li", "stalex-empty", "No market notices.")]));
      }

      function kv(box, rows) {
        box.replaceChildren(...rows.map(([label, value]) => pair(label, value)));
      }

      function renderPortfolio() {
        const p = st.portfolio;
        const cur = st.currency;
        if (!p) {
          summary.replaceChildren();
          positions.replaceChildren(el("p", "stalex-empty", "No portfolio yet."));
          ledger.replaceChildren();
          return;
        }
        kv(summary, [
          ["Cash", money(p.cash, cur)],
          ["Holdings", money(p.holdings, cur, { short: true })],
          ["Day result", signed(p.dayPl, cur)],
          ["Dividends received", money(p.dividends, cur)],
        ]);
        const rows = p.positions.map((x) => ({
          cells: [
            { text: x.t },
            { text: x.short ? "Short" : "Long" },
            { text: num(x.shares, 0), cls: "stalex-num" },
            { text: money(x.avg, cur, { decimals: 2 }), cls: "stalex-num" },
            { text: money(x.price, cur, { decimals: 2 }), cls: "stalex-num" },
            { text: money(x.value, cur), cls: "stalex-num" },
            { text: signed(x.pnl, cur), cls: `stalex-num ${trend(x.pnl)}` },
          ],
        }));
        positions.replaceChildren(grid(["Ticker", "Side", "Shares", "Average", "Price", "Value", "P/L"], rows, "No positions."));
        const entries = p.ledger.map((x) => ({
          cells: [
            { text: when(x.d, x.h) },
            { text: x.kind },
            { text: x.t },
            { text: num(x.shares, 0), cls: "stalex-num" },
            { text: signed(x.amount, cur), cls: `stalex-num ${trend(x.amount)}` },
          ],
        }));
        ledger.replaceChildren(el("h3", "stalex-heading", "Ledger"), grid(["When", "Entry", "Ticker", "Shares", "Amount"], entries, "No entries yet."));
      }

      function renderNews() {
        newsList.replaceChildren(...st.news.map((n) => {
          const li = el("li", "stalex-news-item");
          li.append(el("span", "stalex-label", when(n.d, n.h)), el("span", "", n.text));
          return li;
        }));
        if (!st.news.length) newsList.replaceChildren(el("li", "stalex-empty", "No news yet."));
      }

      sectorSel.addEventListener("change", () => {
        st.sector = sectorSel.value;
        renderList();
      });
      search.addEventListener("input", renderList);
      pick("markets");
      setRange(st.range);

      const timer = setInterval(() => {
        if (root.isConnected) {
          tick();
          return;
        }
        clearInterval(timer);
        trade.stop();
      }, POLL_MS);
      tick();
    },
  });
})();
