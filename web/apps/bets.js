(function () {
  "use strict";
  const { el, button, field } = Apps;

  const SPORTS = [["football", "Football"], ["hockey", "Ice hockey"], ["volleyball", "Volleyball"]];
  const PICKS = {
    football: [["home", "1"], ["draw", "X"], ["away", "2"]],
    hockey: [["home", "1"], ["away", "2"]],
    volleyball: [["home", "1"], ["away", "2"]],
  };
  const PICK_NAMES = { home: "Home win", draw: "Draw", away: "Away win" };
  const STATUS = { upcoming: "Upcoming", live: "In play", final: "Final" };
  const TABS = [["sports", "Sports"], ["mine", "My bets"], ["results", "Results"], ["standings", "Standings"]];
  const STAKES = [10, 50, 100, 500];
  const DEBOUNCE_MS = 200;
  const NO_HOST = "NaSpoBet works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const BAD_REPLY = "The game sent an unexpected reply.";
  const OT_NOTE = "Overtime and shootouts count as a win.";
  const NOT_WHOLE = "Enter a whole number of OC.";
  const PROMO = { kicker: "This weekend", title: "Back your pick", text: "Odds on every fixture below. Keep it fun and keep it within your means." };
  const FOOT_COLS = [
    ["Sports", ["Football", "Ice hockey", "Volleyball", "Results", "My bets"]],
    ["Help", [["Responsible gambling", "responsible"], ["Contact us", "contact"]]],
    ["NaSpoBet", ["About us", "Terms and conditions", "Privacy", "Odds and payouts", "Accessibility"]],
  ];
  const SMALL_PRINT = "Gambling can be addictive. Only bet what you can afford to lose. NaSpoBet is for people aged 18 and over. Odds are set by the NaSpoBet trading desk and can change before kick-off.";

  function photo() {
    const item = window.PHOTOS && window.PHOTOS.pick("football", 0);
    if (!item) return null;
    const fig = el("figure", "bets-photo");
    const img = el("img");
    img.src = item.thumb;
    img.alt = "";
    img.draggable = false;
    img.addEventListener("error", () => fig.remove());
    fig.append(img, el("figcaption", "", "Photo: " + item.credit));
    return fig;
  }

  function footer(open) {
    const foot = el("footer", "bets-foot");
    const inner = el("div", "bets-foot-inner");
    const cols = el("div", "bets-foot-cols");
    for (const [heading, links] of FOOT_COLS) {
      const col = el("div", "bets-foot-col");
      const list = el("ul");
      list.append(...links.map((x) => {
        if (typeof x === "string") return el("li", "", x);
        const item = el("li");
        const b = button("bets-foot-link", x[0]);
        b.addEventListener("click", () => open(x[1]));
        item.append(b);
        return item;
      }));
      col.append(el("h3", "", heading), list);
      cols.append(col);
    }
    const care = el("div", "bets-care");
    care.append(el("span", "bets-age", "18+"), el("p", "", SMALL_PRINT));
    inner.append(cols, care, el("p", "bets-copy", "© NaSpoBet. Gamble responsibly."));
    foot.append(inner);
    return foot;
  }

  function promo(title) {
    const box = el("section", "bets-promo");
    const copy = el("div", "bets-promo-copy");
    copy.append(el("p", "bets-promo-kicker", PROMO.kicker), title, el("p", "bets-promo-text", PROMO.text));
    box.append(copy);
    const fig = photo();
    if (fig) box.prepend(fig);
    return box;
  }

  function token() {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || "Something went wrong.";
  }

  function stakeOf(text) {
    const digits = text.replace(/[^0-9]/g, "");
    return digits ? Number(digits) : null;
  }

  function quoteOk(q) {
    return Boolean(q && typeof q.ok === "boolean" && Array.isArray(q.problems));
  }

  function money(value) {
    return Apps.money(value, "OC");
  }

  function odds(value) {
    return Apps.num(value / 100, 2);
  }

  function sumRow(label, value) {
    const r = el("div", "bets-sum");
    r.append(el("span", "bets-label", label), value);
    return r;
  }

  function keepFocus(box, render) {
    const key = box.contains(document.activeElement) ? document.activeElement.dataset.key : undefined;
    render();
    const again = [...box.querySelectorAll("[data-key]")].find((n) => n.dataset.key === key);
    if (again) again.focus();
  }

  Apps.register("bets", {
    mount(slot, ctx) {
      const st = { tab: "sports", sport: "football", info: {}, rows: [], slip: null, cid: null, quote: null, pending: false, seq: 0, listSeq: 0 };
      let timer = 0;

      const notice = el("p", "bets-notice");
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

      const title = el("h2", "bets-brand");
      const logo = el("img", "bets-logo");
      logo.src = "apps/img/naspobet-logo.png";
      logo.alt = "NaSpoBet";
      title.append(logo);
      const tabs = el("nav", "bets-tabs");
      tabs.setAttribute("aria-label", "Sections");
      const tabBtns = TABS.map(([key, label]) => {
        const b = button("bets-tab", label);
        b.dataset.key = key;
        b.addEventListener("click", () => show(key));
        return b;
      });
      tabs.append(...tabBtns);
      const wallet = el("span", "bets-num bets-wallet");
      const head = el("header", "bets-head");
      head.append(title, tabs, wallet);

      const promoTitle = el("h2", "bets-promo-title", PROMO.title);
      const sportBar = el("div", "bets-sports bets-sportlist");
      sportBar.setAttribute("aria-label", "Sports");
      const sportBtns = SPORTS.map(([key, label]) => {
        const b = button("bets-sport", label);
        b.dataset.key = key;
        b.addEventListener("click", () => pickSport(key));
        return b;
      });
      sportBar.append(...sportBtns);
      const league = el("p", "bets-league");
      const fixtures = el("div", "bets-fixtures");
      const sportsView = el("div", "bets-view");
      sportsView.append(promo(promoTitle), sportBar, league, fixtures);

      const main = el("section", "bets-main");
      const slipBox = el("aside", "bets-card bets-slip");
      slipBox.setAttribute("aria-label", "Bet slip");
      const slipTitle = el("h3", "bets-heading", "Bet slip");
      const slipEmpty = el("p", "bets-empty", "Press the odds of a match to start a bet.");
      const selection = el("p", "bets-selection");
      const pickLine = el("p", "bets-label bets-pick");
      const oddsValue = el("span", "bets-num");
      const stakeInput = field("text", "bets-input bets-stake", "Stake in OC");
      stakeInput.inputMode = "numeric";
      stakeInput.placeholder = "Stake";
      const quick = el("div", "bets-quick");
      quick.append(...STAKES.map((s) => {
        const b = button("bets-btn sm", money(s));
        b.addEventListener("click", () => {
          stakeInput.value = String(s);
          clearTimeout(timer);
          refreshQuote();
        });
        return b;
      }));
      const stakeField = el("label", "bets-field");
      stakeField.append(el("span", "bets-label", "Stake"), stakeInput);
      const possible = el("span", "bets-num");
      const problems = el("p", "bets-problems");
      const place = button("bets-btn primary block", "Place bet");
      const details = el("div", "bets-details");
      details.append(selection, pickLine, sumRow("Odds", oddsValue), stakeField, quick, sumRow("Possible return", possible), problems, place);
      slipBox.append(slipTitle, slipEmpty, details);

      const body = el("div", "bets-body");
      body.append(main, slipBox);
      const root = el("div", "bets");
      root.append(head, notice, body, footer(show));
      slot.append(root);

      function paintTabs() {
        for (const b of tabBtns) b.setAttribute("aria-pressed", String(b.dataset.key === st.tab));
      }

      function paintSports() {
        for (const b of sportBtns) b.setAttribute("aria-pressed", String(b.dataset.key === st.sport));
        const i = st.info[st.sport];
        league.textContent = i ? i.league + ", season " + Apps.num(i.season, 0) + ", round " + Apps.num(i.round, 0) + " of " + Apps.num(i.rounds, 0) : "";
      }

      function paintSlip() {
        const s = st.slip;
        slipEmpty.hidden = Boolean(s);
        details.hidden = !s;
        if (!s) return;
        selection.textContent = s.home + " v " + s.away;
        pickLine.textContent = PICK_NAMES[s.pick] + (s.sport === "hockey" ? ". " + OT_NOTE : "");
        oddsValue.textContent = odds(s.odds);
        possible.textContent = st.quote && st.quote.ok ? money(st.quote.ret) : "-";
        problems.textContent = st.quote && st.quote.problems ? st.quote.problems.join(" ") : "";
        place.disabled = st.pending || !st.quote || !st.quote.ok;
      }

      function fixtureRow(f) {
        const row = el("tr");
        const match = el("td");
        match.append(el("span", "bets-team", f.home + " v " + f.away), el("span", "bets-time", BetPages.clock(f.kickoff) + " · " + (STATUS[f.status] || f.status)));
        row.append(match);
        for (const [pick] of PICKS[st.sport]) {
          const cell = el("td", "bets-odds");
          const price = f.odds[pick];
          if (price) {
            const b = button("bets-btn bets-odd", odds(price));
            b.setAttribute("aria-label", PICK_NAMES[pick] + " " + odds(price));
            b.dataset.key = f.id + "|" + pick;
            b.disabled = st.pending || f.status !== "upcoming";
            b.setAttribute("aria-pressed", String(Boolean(st.slip && st.slip.fixture === f.id && st.slip.pick === pick)));
            b.addEventListener("click", () => choose(f, pick));
            cell.append(b);
          }
          row.append(cell);
        }
        return row;
      }

      function fixtureTable(rows) {
        const headRow = el("tr");
        headRow.append(el("th", "", "Match"), ...PICKS[st.sport].map(([, label]) => el("th", "", label)));
        const thead = el("thead");
        thead.append(headRow);
        const tbody = el("tbody");
        tbody.append(...rows.map(fixtureRow));
        const t = el("table", "bets-tbl bets-events");
        t.append(thead, tbody);
        return t;
      }

      function paintFixtures() {
        const groups = [];
        for (const f of st.rows) {
          const last = groups[groups.length - 1];
          if (last && last.round === f.round) last.rows.push(f);
          else groups.push({ round: f.round, rows: [f] });
        }
        const nodes = groups.flatMap((g) => [el("h3", "bets-round", "Round " + Apps.num(g.round, 0)), fixtureTable(g.rows)]);
        keepFocus(fixtures, () => fixtures.replaceChildren(...(nodes.length ? nodes : [el("p", "bets-empty", "No matches.")])));
      }

      function refreshQuote() {
        const seq = ++st.seq;
        const s = st.slip;
        st.quote = null;
        if (!s) {
          paintSlip();
          return;
        }
        const stake = stakeOf(stakeInput.value);
        if (stake === null) {
          st.quote = { ok: false, problems: [NOT_WHOLE] };
          paintSlip();
          return;
        }
        paintSlip();
        call("bet.quote", { fixture: s.fixture, pick: s.pick, stake }).then((q) => {
          if (seq !== st.seq || st.slip !== s) return;
          if (q && !quoteOk(q)) setNotice(BAD_REPLY);
          st.quote = quoteOk(q) ? q : null;
          paintSlip();
        });
      }

      function choose(f, pick) {
        if (st.pending || f.status !== "upcoming") return;
        const same = st.slip && st.slip.fixture === f.id && st.slip.pick === pick;
        st.slip = same ? null : { fixture: f.id, pick, sport: st.sport, home: f.home, away: f.away, odds: f.odds[pick] };
        st.cid = st.slip ? token() : null;
        st.quote = null;
        paintSlip();
        paintFixtures();
        if (st.slip) refreshQuote();
      }

      function refreshInfo() {
        return call("bet.sports", {}).then((r) => {
          if (!r) return;
          if (r.wallet) wallet.textContent = money(r.wallet.oc);
          if (r.minStake) promoTitle.textContent = PROMO.title + " from " + money(r.minStake);
          for (const s of r.sports || []) st.info[s.id] = s;
          paintSports();
        });
      }

      function loadFixtures() {
        const n = ++st.listSeq;
        return call("bet.fixtures", { sport: st.sport }).then((r) => {
          if (!r || n !== st.listSeq) return;
          st.rows = r.rows || [];
          paintFixtures();
        });
      }

      function pickSport(key) {
        if (st.sport === key) return;
        st.sport = key;
        paintSports();
        loadFixtures();
      }

      function show(tab) {
        st.tab = tab;
        paintTabs();
        slipBox.hidden = tab !== "sports";
        if (tab === "sports") {
          main.replaceChildren(sportsView);
          paintSports();
          refreshInfo();
          loadFixtures();
        } else if (tab === "mine") {
          BetPages.mine(main, call);
        } else if (tab === "results") {
          BetPages.results(main, call);
        } else if (tab === "standings") {
          BetPages.standings(main, call);
        } else {
          main.replaceChildren(BetPages.page(tab));
        }
      }

      stakeInput.addEventListener("input", () => {
        stakeInput.value = stakeInput.value.replace(/[^0-9]/g, "");
        clearTimeout(timer);
        timer = setTimeout(refreshQuote, DEBOUNCE_MS);
      });

      place.addEventListener("click", () => {
        const s = st.slip;
        if (!s || !st.quote || !st.quote.ok || st.pending) return;
        st.pending = true;
        paintSlip();
        call("bet.place", { cid: st.cid, fixture: s.fixture, pick: s.pick, stake: stakeOf(stakeInput.value) }).then((r) => {
          st.pending = false;
          if (!r || !r.bet) {
            if (r) setNotice(BAD_REPLY);
            paintSlip();
            loadFixtures();
            refreshQuote();
            return;
          }
          if (r.wallet) wallet.textContent = money(r.wallet.oc);
          setNotice("Bet " + Apps.num(r.bet.n, 0) + " placed: " + money(r.bet.stake) + " on " + PICK_NAMES[r.bet.pick] + " at " + odds(r.bet.odds) + ".");
          st.slip = null;
          st.cid = null;
          st.quote = null;
          stakeInput.value = "";
          paintSlip();
          loadFixtures();
        });
      });

      show("sports");
      paintSlip();
    },
  });
})();
