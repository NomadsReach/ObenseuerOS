(function () {
  "use strict";
  const { el, button } = Apps;

  const DAY = 1440;
  const PICK_NAMES = { home: "Home", draw: "Draw", away: "Away" };
  const STATE_NAMES = { open: "Open", won: "Won", lost: "Lost" };
  const SPORTS = [["football", "Football"], ["hockey", "Ice hockey"], ["volleyball", "Volleyball"]];

  function pad(n) {
    return (n < 10 ? "0" : "") + Apps.num(n, 0);
  }

  function clock(minutes) {
    const m = minutes % DAY;
    return "Day " + Apps.num(Math.floor(minutes / DAY), 0) + ", " + pad(Math.floor(m / 60)) + ":" + pad(m % 60);
  }

  function scores(text) {
    return text.split(",").map((part) => part.split("-").map((n) => Apps.num(Number(n), 0)).join("-")).join(", ");
  }

  function table(heads, rows) {
    const headRow = el("tr");
    headRow.append(...heads.map((h) => el("th", "", h)));
    const thead = el("thead");
    thead.append(headRow);
    const tbody = el("tbody");
    tbody.append(...rows.map((cells) => {
      const tr = el("tr");
      tr.append(...cells.map((c) => el("td", "", c)));
      return tr;
    }));
    const t = el("table", "bets-tbl bets-table");
    t.append(thead, tbody);
    return t;
  }

  function group(title, result, rowsOf) {
    const part = el("section", "bets-group");
    part.append(el("h3", "bets-heading", title));
    const rows = (result && result.rows) || [];
    if (!rows.length) {
      part.append(el("p", "bets-empty", "None."));
      return part;
    }
    part.append(table(rowsOf.heads, rows.map(rowsOf.cells)));
    if (result.total > rows.length) part.append(el("p", "bets-empty", "Showing the newest " + Apps.num(rows.length, 0) + " of " + Apps.num(result.total, 0) + "."));
    return part;
  }

  const MINE = {
    heads: ["Match", "Pick", "Stake", "Odds", "Return", "Placed"],
    cells: (r) => [r.match, PICK_NAMES[r.pick], Apps.money(r.stake, "OC"), Apps.num(r.odds / 100, 2), Apps.money(r.state === "lost" ? 0 : r.ret, "OC"), (STATE_NAMES[r.state] || r.state) + ", " + clock(r.at)],
  };

  function mine(box, call) {
    Promise.all([call("bet.mine", { status: "open" }), call("bet.mine", { status: "settled" })]).then(([open, settled]) => {
      if (!open || !settled) return;
      box.replaceChildren(group("Open", open, MINE), group("Settled", settled, MINE));
    });
  }

  function results(box, call) {
    const st = { sport: "football", seq: 0 };
    const bar = el("div", "bets-sports");
    const list = el("div", "bets-results");
    const buttons = SPORTS.map(([key, label]) => {
      const b = button("bets-sport", label);
      b.dataset.key = key;
      b.addEventListener("click", () => {
        st.sport = key;
        load();
      });
      return b;
    });
    bar.append(...buttons);
    box.replaceChildren(bar, list);
    function load() {
      for (const b of buttons) b.setAttribute("aria-pressed", String(b.dataset.key === st.sport));
      const n = ++st.seq;
      call("bet.results", { sport: st.sport }).then((r) => {
        if (!r || n !== st.seq) return;
        const heads = ["Match", "Score", "Winner", "Round"];
        const rows = (r.rows || []).map((f) => {
          const score = scores(f.score) + (f.decider ? " " + f.decider : "") + (f.sets ? " (" + scores(f.sets) + ")" : "");
          const winner = f.winner === "home" ? f.home : f.winner === "away" ? f.away : PICK_NAMES.draw;
          return [f.home + " v " + f.away, score, winner, "Round " + Apps.num(f.round, 0)];
        });
        list.replaceChildren(rows.length ? table(heads, rows) : el("p", "bets-empty", "No results yet."));
      });
    }
    load();
  }

  const RULES = {
    football: "3 points for a win, 1 for a draw, 0 for a defeat.",
    hockey: "3 points for a win in regulation, 2 for a win in overtime or a shootout, 1 for a defeat in overtime or a shootout, 0 for a defeat in regulation.",
    volleyball: "3 points for a 3-0 or 3-1 win, 2 for a 3-2 win, 1 for a 2-3 defeat, 0 for a 0-3 or 1-3 defeat.",
  };

  function standingGroup(s) {
    const part = el("section", "bets-group");
    part.append(el("h3", "bets-heading", s.name), el("p", "bets-rule", RULES[s.id]));
    const rows = s.rows || [];
    if (!rows.length) {
      part.append(el("p", "bets-empty", "No results yet."));
      return part;
    }
    const n = (v) => Apps.num(v, 0);
    const signed = (v) => (v > 0 ? "+" : "") + n(v);
    const draws = s.id === "football";
    const heads = ["Team", "P", "W"].concat(draws ? ["D"] : [], ["L", s.id === "volleyball" ? "SD" : "GD", "Pts"]);
    const cells = (r) => [r.team, n(r.played), n(r.won)].concat(draws ? [n(r.drawn)] : [], [n(r.lost), signed(r.diff), n(r.points)]);
    part.append(table(heads, rows.map(cells)));
    return part;
  }

  function standings(box, call) {
    call("bet.table", {}).then((r) => {
      if (!r) return;
      box.replaceChildren(el("p", "bets-league", "Season " + Apps.num(r.season, 0)), ...(r.sports || []).map(standingGroup));
    });
  }

  const PAGES = {
    responsible: ["Responsible gambling", [
      "NaSpoBet is for people aged 18 and over. Check your age before you place a bet.",
      "Bet for fun, with OC you can spare. Gambling is not a way to make money, and the odds always carry a margin for NaSpoBet.",
      "Do not chase losses. Do not bet to win back what you have lost, and do not borrow money to bet.",
      "Bets close at kick-off. Decide before the match starts, not during it.",
      "Every bet you have placed is listed under My bets. If betting stops being fun, stop, and talk to someone you trust or to a doctor in your town.",
    ]],
    contact: ["Contact us", [
      "Questions about a bet: open My bets and note the bet number, and quote it in every letter.",
      "Write to the NaSpoBet customer desk through your Stalburg Mail account.",
      "NaSpoBet staff will never ask for your password or your card details. Do not send them.",
    ]],
  };

  function page(key) {
    const [title, paras] = PAGES[key];
    const part = el("section", "bets-group bets-page");
    part.append(el("h3", "bets-heading", title), ...paras.map((p) => el("p", "", p)));
    return part;
  }

  window.BetPages = { clock, scores, mine, results, standings, page };
})();
