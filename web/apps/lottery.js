(function () {
  "use strict";
  const { el, button } = Apps;
  const { PICK, POOL, prize, matches } = LottoRules;

  const COLS = 10;
  const DAY_MIN = 1440;
  const POLL_MS = 2000;
  const MATCHES = [3, 4, 5, 6];
  const SCOPES = [["open", "Open"], ["done", "Done"]];
  const NAV = [["draw", "Draw"], ["scratch", "Scratch cards"]];
  const DRAW_AT = "20:00";
  const CLOSE_AT = "19:30";
  const AGE_NOTICE = "You must be 18 or over to play. Play within your means. Lottery games are entertainment, not a way to make money.";
  const SCRATCH_R = 0.075;
  const OC_ART = { src: "apps/lottery/oc-pot.jpg", alt: "OC Pot scratch card", panel: { x: 6.15, y: 74.15, w: 87.6, h: 14.91 } };
  const SPADE_ART = { src: "apps/lottery/spade.jpg", alt: "Spade of Aces scratch card", panel: { x: 6.15, y: 70.25, w: 87.8, h: 18.81 } };
  const NO_HOST = "The Lottery works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const BAD_REPLY = "The game sent an unexpected reply.";

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || "Something went wrong.";
  }

  function token() {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function span(min) {
    const m = Math.max(0, min);
    const h = Math.floor(m / 60);
    return h ? `${Apps.num(h)} h ${Apps.num(m % 60)} min` : `${Apps.num(m % 60)} min`;
  }

  function chips(nums, drawn) {
    const box = el("span", "lotto-chips");
    box.append(...nums.map((n) => el("span", drawn && drawn.indexOf(n) >= 0 ? "lotto-chip hit" : "lotto-chip", Apps.num(n))));
    return box;
  }

  function table(heads, rows) {
    const headRow = el("tr");
    headRow.append(...heads.map((h) => el("th", "", h)));
    const thead = el("thead");
    thead.append(headRow);
    const tbody = el("tbody");
    tbody.append(...rows.map((cells) => {
      const tr = el("tr");
      tr.append(...cells.map((c) => {
        const td = el("td");
        if (c instanceof Node) td.append(c);
        else td.textContent = c;
        return td;
      }));
      return tr;
    }));
    const t = el("table", "lotto-tbl");
    t.append(thead, tbody);
    return t;
  }

  function panel(cls, title, ...kids) {
    const box = el("section", `lotto-panel ${cls}`);
    box.append(el("h3", "lotto-heading", title), ...kids);
    return box;
  }

  function row(...parts) {
    const r = el("div", "lotto-row");
    r.append(...parts);
    return r;
  }

  function footer(link) {
    const foot = el("footer", "lotto-foot");
    const age = el("div", "lotto-age");
    age.append(el("span", "lotto-age-badge", "18+"), el("p", "", AGE_NOTICE));
    foot.append(age, el("p", "lotto-copy", "© LOTTO by NaSpoBet. Please play responsibly."), link);
    return foot;
  }

  function prizeTable() {
    return table(["Numbers right", "Prize"], MATCHES.map((m) => [Apps.num(m), Apps.money(prize(m), "OC")]));
  }

  function settled(img) {
    return new Promise((done) => {
      if (img.complete) done();
      img.addEventListener("load", done, { once: true });
      img.addEventListener("error", done, { once: true });
    });
  }

  function artOf(k) {
    return k.kind === "spade" ? SPADE_ART : OC_ART;
  }

  function place(node, panelBox) {
    Object.assign(node.style, { left: `${panelBox.x}%`, top: `${panelBox.y}%`, width: `${panelBox.w}%`, height: `${panelBox.h}%` });
    return node;
  }

  function cardBox(art, cls) {
    const card = el("div", cls ? `lotto-ticket ${cls}` : "lotto-ticket");
    const img = el("img", "lotto-art");
    img.src = art.src;
    img.alt = art.alt;
    img.draggable = false;
    card.append(img);
    return card;
  }

  function coverFrom(img, cover, panelBox) {
    const sx = (panelBox.x / 100) * img.naturalWidth;
    const sy = (panelBox.y / 100) * img.naturalHeight;
    const sw = (panelBox.w / 100) * img.naturalWidth;
    const sh = (panelBox.h / 100) * img.naturalHeight;
    cover.width = Math.round(sw);
    cover.height = Math.round(sh);
    cover.getContext("2d").drawImage(img, sx, sy, sw, sh, 0, 0, cover.width, cover.height);
  }

  function paintResult(play, c) {
    if (c.prize > 0) play.replaceChildren(el("span", "lotto-prize-big", Apps.money(c.prize, c.currency)), el("span", "lotto-stamp", "WINNER"));
    else play.replaceChildren(el("span", "lotto-nowin", "No win. Try again."));
  }

  Apps.register("lotto", {
    mount(slot, ctx, opts) {
      const st = { loaded: false, price: 0, oc: 0, rm: 0, perDraw: 0, perBuy: 0, draw: 0, salesOpen: false, closesIn: 0, drawsIn: 0, anchor: null, bought: 0, owed: 0, picks: [], lines: [], cid: null, pending: false, quickPending: false, scope: "open", page: 0, total: 0, tickets: [], results: [], ticketSeq: 0, lastDraw: null, cards: [], kinds: [], today: 0, perDay: 0, scratchCid: null, scratchPending: false };

      const notice = el("p", "lotto-notice");
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

      const wallet = el("span", "lotto-wallet");
      const logo = el("img", "lotto-logo");
      logo.src = "apps/img/lotto-logo.png";
      logo.alt = "LOTTO by NaSpoBet";
      const brand = el("h2", "lotto-brand");
      brand.append(logo);
      const navButtons = NAV.map(([v, label]) => {
        const b = button("lotto-nav-link", label);
        b.addEventListener("click", () => showTab(v));
        return [v, b];
      });
      const nav = el("nav", "lotto-nav");
      nav.setAttribute("aria-label", "Sections");
      nav.append(...navButtons.map(([, b]) => b));
      const head = el("header", "lotto-head");
      head.append(brand, nav, wallet);

      const grid = el("table", "lotto-grid");
      grid.setAttribute("aria-label", "Numbers");
      const cells = [];
      for (let n = 1; n <= POOL; n++) {
        const b = button("lotto-num", Apps.num(n));
        b.setAttribute("aria-label", "Number " + Apps.num(n));
        b.addEventListener("click", () => togglePick(n));
        cells.push(b);
      }
      const rows = [];
      for (let i = 0; i < cells.length; i += COLS) {
        const tr = el("tr");
        for (const b of cells.slice(i, i + COLS)) {
          const td = el("td");
          td.append(b);
          tr.append(td);
        }
        rows.push(tr);
      }
      const tbody = el("tbody");
      tbody.append(...rows);
      grid.append(tbody);

      const picked = el("p", "lotto-count");
      const pickNote = el("p", "lotto-note");
      pickNote.hidden = true;
      const addLine = button("lotto-btn", "Add line");
      const quick = button("lotto-btn", "Quick pick");
      const clear = button("lotto-btn ghost", "Clear");
      const actions = el("div", "lotto-actions");
      actions.append(addLine, quick, clear);
      const lineList = el("ul", "lotto-lines");
      const priceText = el("p", "lotto-price");
      const buy = button("lotto-btn primary block", "Buy tickets");
      const pick = panel("lotto-buy", "Pick numbers and buy", grid, picked, pickNote, actions, lineList, priceText, buy);

      const drawTitle = el("p", "lotto-draw-title");
      const drawTime = el("p", "lotto-note", `Draws at ${DRAW_AT} game time. Sales close at ${CLOSE_AT}.`);
      const closeText = el("p", "lotto-count");
      const drawText = el("p", "lotto-count");
      const boughtText = el("p", "lotto-count");
      const owedText = el("p", "lotto-owed");
      const next = panel("lotto-next", "Next draw", drawTitle, drawTime, closeText, drawText, boughtText, owedText, prizeTable());

      const past = el("div", "lotto-list");
      const results = panel("lotto-results", "Last results", past);

      const scopeButtons = SCOPES.map(([v, label]) => {
        const b = button("", label);
        b.addEventListener("click", () => {
          if (st.scope === v) return;
          st.scope = v;
          syncScope();
          loadTickets(0);
        });
        return [v, b];
      });
      const scope = el("div", "lotto-seg");
      scope.append(...scopeButtons.map(([, b]) => b));
      const tickets = el("div", "lotto-list");
      const more = button("lotto-btn sm lotto-more", "Load more");
      more.hidden = true;
      more.addEventListener("click", () => loadTickets(st.page + 1));
      const mine = panel("lotto-tickets", "My tickets", scope, tickets, more);

      const cols = el("div", "lotto-cols");
      cols.append(pick, next);
      const drawTab = el("div", "lotto-draw-tab");
      drawTab.append(cols, results, mine);

      const shipBox = el("input", "lotto-ship-box");
      shipBox.type = "checkbox";
      shipBox.addEventListener("change", renderKinds);
      const shipLabel = el("label", "lotto-ship");
      shipLabel.append(shipBox, " Ship the real card by parcel");
      const shipNote = el("p", "lotto-meta");
      const kindList = el("div", "lotto-kinds");
      const buyCards = panel("lotto-buy-cards", "Buy a card", kindList, shipLabel, shipNote);
      const cardCount = el("p", "lotto-meta");
      const cardList = el("div", "lotto-list");
      const cardsBox = panel("lotto-cards", "My cards", cardCount, cardList);
      const scratchTab = el("div", "lotto-scratch");
      scratchTab.append(buyCards, cardsBox);

      const root = el("div", "lotto");
      const crossLink = button("lotto-foot-link", "Sports betting at NaSpoBet");
      crossLink.hidden = !opts.visit;
      crossLink.addEventListener("click", () => opts.visit("bets.stalburg.ts"));
      root.append(head, notice, drawTab, scratchTab, footer(crossLink));
      slot.append(root);

      function showTab(name) {
        scratchTab.hidden = name !== "scratch";
        drawTab.hidden = name !== "draw";
        navButtons.forEach(([v, b]) => b.setAttribute("aria-pressed", String(v === name)));
      }

      function renderWallet() {
        wallet.textContent = st.rm > 0 ? `${Apps.money(st.oc, "OC")}, ${Apps.money(st.rm, "RM")}` : Apps.money(st.oc, "OC");
      }

      function applyWallet(r) {
        if (Number.isFinite(r.balance)) st.oc = r.balance;
        if (Number.isFinite(r.rm)) st.rm = r.rm;
        renderWallet();
      }

      function kindRow(k) {
        const ship = shipBox.checked;
        const cost = ship && k.shipTotal ? k.shipTotal : k.price;
        const info = el("div", "lotto-info");
        info.append(el("span", "lotto-name", k.name), el("span", "lotto-meta", `Wins up to ${Apps.money(k.top, k.currency)}`));
        const go = button("lotto-btn primary sm", `Buy for ${Apps.money(cost, "OC")}`);
        go.disabled = st.scratchPending || (ship && !k.shipTotal);
        go.addEventListener("click", () => buyCard(k));
        const r = row(cardBox(artOf(k), "thumb"), info, el("span", "lotto-meta", ship ? "Sent by parcel" : "Scratch it here"), go);
        return r;
      }

      function renderKinds() {
        const ship = shipBox.checked;
        shipBox.disabled = !st.kinds.some((k) => k.shipTotal);
        shipNote.textContent = ship ? "Delivered by parcel, like a shop order, with the delivery fee added. Scratch it in the game, which pays the prize." : "Scratch it in this window. The prize goes to your wallet.";
        kindList.replaceChildren(...(st.kinds.length ? st.kinds.map(kindRow) : [el("p", "lotto-empty", "Cards are not ready yet.")]));
      }

      function buyCard(k) {
        if (st.scratchPending) return;
        setNotice("");
        const ship = shipBox.checked;
        const key = `${k.kind}:${ship ? "ship" : "wallet"}`;
        if (!st.scratchCid || st.scratchCid.key !== key) st.scratchCid = { key, id: token() };
        st.scratchPending = true;
        renderKinds();
        call("scratch.buy", { cid: st.scratchCid.id, kind: k.kind, ship }).then((r) => {
          st.scratchPending = false;
          if (r && r.card) {
            st.scratchCid = null;
            applyWallet(r);
            setNotice(ship ? `Card ${Apps.num(r.card.id)} is on its way by parcel.` : `Card ${Apps.num(r.card.id)} is ready. Scratch it below.`);
            loadCards();
          }
          renderKinds();
        });
      }

      function scratchBox(c, k, info) {
        const art = artOf(k);
        const card = cardBox(art);
        const img = card.querySelector("img");
        const play = place(el("div", "lotto-play"), art.panel);
        const cover = place(el("canvas", "lotto-cover"), art.panel);
        cover.setAttribute("aria-label", "Scratch cover. Scratch it, or press Reveal.");
        card.append(play, cover);
        const reveal = button("lotto-btn sm", "Reveal");
        const g = cover.getContext("2d");
        const ready = settled(img).then(() => {
          if (img.naturalWidth) coverFrom(img, cover, art.panel);
        });
        let drawing = false;
        let busy = false;

        function scratch(e) {
          const rect = cover.getBoundingClientRect();
          g.globalCompositeOperation = "destination-out";
          g.beginPath();
          g.arc((e.clientX - rect.left) * (cover.width / rect.width), (e.clientY - rect.top) * (cover.height / rect.height), cover.width * SCRATCH_R, 0, Math.PI * 2);
          g.fill();
        }

        function uncovered() {
          const px = g.getImageData(0, 0, cover.width, cover.height).data;
          let clear = 0;
          for (let i = 3; i < px.length; i += 4) if (px[i] === 0) clear++;
          return clear / (cover.width * cover.height);
        }

        function finish() {
          cover.remove();
          reveal.remove();
        }

        function settle() {
          if (c.revealed && uncovered() >= 0.5) finish();
        }

        function open() {
          if (c.revealed) return Promise.resolve(true);
          if (busy) return Promise.resolve(false);
          busy = true;
          return ready.then(() => call("scratch.reveal", { id: c.id })).then((r) => {
            busy = false;
            if (!r || !r.card) return false;
            Object.assign(c, r.card);
            applyWallet(r);
            paintResult(play, c);
            return true;
          });
        }

        cover.addEventListener("pointerdown", (e) => {
          drawing = true;
          cover.setPointerCapture(e.pointerId);
          scratch(e);
          open().then(settle);
        });
        cover.addEventListener("pointermove", (e) => {
          if (drawing) scratch(e);
        });
        cover.addEventListener("pointerup", () => {
          drawing = false;
          settle();
        });
        reveal.addEventListener("click", () => {
          open().then((ok) => {
            if (ok) finish();
          });
        });
        info.append(reveal);
        return card;
      }

      function cardRow(c) {
        const k = st.kinds.find((x) => x.kind === c.kind) || { name: "Card" };
        const info = el("div", "lotto-info");
        info.append(el("span", "lotto-name", `${k.name}, card ${Apps.num(c.id)}`), el("span", "lotto-meta", `Paid ${Apps.money(c.paidOc, "OC")}`));
        const r = el("div", "lotto-row lotto-card");
        if (c.shipped) {
          r.append(info, el("span", "lotto-meta", "Shipped. Scratch it in the game."));
        } else if (c.revealed) {
          const art = artOf(k);
          const small = cardBox(art, "sm");
          const play = place(el("div", "lotto-play"), art.panel);
          paintResult(play, c);
          small.append(play);
          r.append(small, info);
        } else {
          r.append(scratchBox(c, k, info), info);
        }
        return r;
      }

      function renderCards() {
        cardCount.textContent = `${Apps.num(st.today)} of ${Apps.num(st.perDay)} cards bought today.`;
        cardList.replaceChildren(...(st.cards.length ? st.cards.map(cardRow) : [el("p", "lotto-empty", "No cards yet.")]));
      }

      function loadCards() {
        return call("scratch.history", {}).then((r) => {
          if (!r || !Array.isArray(r.cards)) return;
          st.cards = r.cards;
          st.kinds = r.kinds;
          st.today = r.today;
          st.perDay = r.perDay;
          applyWallet(r);
          renderKinds();
          renderCards();
        });
      }

      function clockMinute() {
        const c = ctx.info().clock;
        return c && Number.isInteger(c.hour) && Number.isInteger(c.minute) ? c.hour * 60 + c.minute : null;
      }

      function left(total) {
        const now = clockMinute();
        if (now === null || st.anchor === null) return total;
        return total - ((now - st.anchor + DAY_MIN) % DAY_MIN);
      }

      function salesOpenNow() {
        return st.salesOpen && left(st.closesIn) > 0;
      }

      function setLines(next) {
        st.lines = next;
        st.cid = null;
        renderLines();
        syncControls();
      }

      function togglePick(n) {
        setNotice("");
        const i = st.picks.indexOf(n);
        if (i >= 0) st.picks.splice(i, 1);
        else if (st.picks.length < PICK) st.picks.push(n);
        st.picks.sort((a, b) => a - b);
        syncControls();
      }

      function lineRow(line, i) {
        const li = el("li", "lotto-line");
        const remove = button("lotto-btn sm ghost", "Remove");
        remove.disabled = st.pending || st.quickPending;
        remove.setAttribute("aria-label", "Remove line " + Apps.num(i + 1));
        remove.addEventListener("click", () => setLines(st.lines.filter((_, j) => j !== i)));
        li.append(chips(line), remove);
        return li;
      }

      function renderLines() {
        lineList.replaceChildren(...(st.lines.length ? st.lines.map(lineRow) : [el("li", "lotto-empty", "No lines yet.")]));
        const count = st.lines.length;
        priceText.textContent = `${Apps.num(count)} ${count === 1 ? "line" : "lines"}, ${Apps.money(st.price * count, "OC")} (${Apps.money(st.price, "OC")} per line)`;
      }

      function syncControls() {
        const busy = st.pending || st.quickPending;
        const open = salesOpenNow();
        const room = open && !busy && st.bought + st.lines.length < st.perDraw && st.lines.length < st.perBuy;
        cells.forEach((b, i) => {
          const on = st.picks.indexOf(i + 1) >= 0;
          b.setAttribute("aria-pressed", String(on));
          b.disabled = busy || !open || (!on && st.picks.length >= PICK);
        });
        addLine.disabled = !room || st.picks.length !== PICK;
        quick.disabled = !room;
        clear.disabled = busy || st.picks.length === 0;
        buy.disabled = busy || !open || st.lines.length === 0;
        picked.textContent = `${Apps.num(st.picks.length)} of ${Apps.num(PICK)} chosen`;
        pickNote.hidden = !st.loaded || open;
        pickNote.textContent = "Sales for this draw are closed. Tickets go on sale again after the draw.";
      }

      function renderTop() {
        if (!st.loaded) {
          drawTitle.textContent = "Loading the draw...";
          return;
        }
        const closes = left(st.closesIn);
        const drawsAt = left(st.drawsIn);
        drawTitle.textContent = `Draw ${Apps.num(st.draw)}`;
        closeText.textContent = salesOpenNow() ? `Sales close in ${span(closes)}.` : "Sales are closed for this draw.";
        drawText.textContent = drawsAt > 0 ? `The draw is in ${span(drawsAt)}.` : "The draw is being run.";
        boughtText.textContent = `You have ${Apps.num(st.bought)} of ${Apps.num(st.perDraw)} tickets for it.`;
        owedText.hidden = st.owed <= 0;
        owedText.textContent = st.owed > 0 ? `${Apps.money(st.owed, "OC")} is waiting to be paid out.` : "";
      }

      function refreshState() {
        return call("lotto.state", {}).then((r) => {
          if (!r || !r.nextDraw) return;
          if (!st.loaded) renderLines();
          st.loaded = true;
          st.price = r.price;
          st.oc = r.oc;
          st.perDraw = r.maxPerDraw;
          st.perBuy = r.maxPerBuy;
          st.draw = r.nextDraw.n;
          st.salesOpen = r.nextDraw.salesOpen;
          st.closesIn = r.nextDraw.closesIn;
          st.drawsIn = r.nextDraw.drawsIn;
          st.anchor = r.clockMinute;
          st.bought = r.boughtForNext;
          st.owed = r.owedOc;
          if (notice.textContent === BUSY) setNotice("");
          renderWallet();
          renderTop();
          syncControls();
          const last = r.last ? r.last.n : null;
          if (last !== st.lastDraw) {
            st.lastDraw = last;
            loadTickets(0);
            loadResults();
          }
        });
      }

      function drawnFor(n) {
        const d = st.results.find((x) => x.n === n);
        return d ? d.nums : null;
      }

      function ticketRow(t) {
        const drawn = drawnFor(t.draw);
        const info = el("div", "lotto-info");
        info.append(el("span", "lotto-name", `Ticket ${Apps.num(t.id)}, draw ${Apps.num(t.draw)}`), chips(t.nums, drawn));
        let status;
        if (t.state === "open") status = "Waiting for the draw";
        else if (t.state === "won") status = `Won ${Apps.money(t.oc, "OC")}`;
        else status = `Lost, ${Apps.num(drawn ? matches(t.nums, drawn) : t.matches)} right`;
        const r = row(info, el("span", `lotto-status ${t.state}`, status));
        return r;
      }

      function renderTickets() {
        syncScope();
        tickets.replaceChildren(...(st.tickets.length ? st.tickets.map(ticketRow) : [el("p", "lotto-empty", st.scope === "open" ? "No open tickets." : "No finished tickets.")]));
        more.hidden = st.tickets.length >= st.total;
      }

      function syncScope() {
        scopeButtons.forEach(([v, b]) => b.setAttribute("aria-pressed", String(v === st.scope)));
      }

      function loadTickets(page) {
        const n = ++st.ticketSeq;
        return call("lotto.tickets", { scope: st.scope, page }).then((r) => {
          if (!r || !Array.isArray(r.tickets)) return;
          if (n !== st.ticketSeq) return;
          st.page = page;
          st.total = r.total;
          st.tickets = page ? st.tickets.concat(r.tickets) : r.tickets;
          renderTickets();
        });
      }

      function resultRow(d) {
        const draw = el("span", "lotto-name", `Draw ${Apps.num(d.n)}`);
        if (d.late) draw.append(" ", el("span", "lotto-badge", "late"));
        return [draw, chips(d.nums), MATCHES.map((k, i) => `${Apps.num(k)} right: ${Apps.num(d.winners[i])}`).join(", "), Apps.money(d.paidOc, "OC")];
      }

      function renderResults() {
        past.replaceChildren(st.results.length ? table(["Draw", "Numbers", "Winners", "Paid out"], st.results.map(resultRow)) : el("p", "lotto-empty", "No draws yet."));
      }

      function loadResults() {
        return call("lotto.results", {}).then((r) => {
          if (!r || !Array.isArray(r.draws)) return;
          st.results = r.draws;
          renderResults();
          renderTickets();
        });
      }

      quick.addEventListener("click", () => {
        setNotice("");
        st.quickPending = true;
        syncControls();
        call("lotto.quick", {}).then((r) => {
          st.quickPending = false;
          if (r && Array.isArray(r.lines) && r.lines[0]) setLines(st.lines.concat([r.lines[0]]));
          else syncControls();
        });
      });

      addLine.addEventListener("click", () => {
        setNotice("");
        const line = st.picks.slice();
        st.picks = [];
        setLines(st.lines.concat([line]));
      });

      clear.addEventListener("click", () => {
        st.picks = [];
        syncControls();
      });

      buy.addEventListener("click", () => {
        if (st.pending || st.quickPending || !st.lines.length) return;
        setNotice("");
        st.pending = true;
        st.cid = st.cid || token();
        syncControls();
        renderLines();
        call("lotto.buy", { cid: st.cid, lines: st.lines }).then((r) => {
          st.pending = false;
          if (r && Array.isArray(r.tickets) && r.tickets.length) {
            const n = r.tickets.length;
            st.oc = r.balance;
            setLines([]);
            setNotice(`Bought ${Apps.num(n)} ${n === 1 ? "ticket" : "tickets"} for draw ${Apps.num(r.tickets[0].draw)}.`);
            renderWallet();
            refreshState();
            loadTickets(0);
            return;
          }
          if (r) setNotice(BAD_REPLY);
          renderLines();
          syncControls();
        });
      });

      const timer = setInterval(() => {
        if (!root.isConnected) {
          clearInterval(timer);
          clearInterval(poll);
          return;
        }
        if (st.salesOpen && left(st.closesIn) <= 0) refreshState();
        renderTop();
        syncControls();
      }, 1000);
      const poll = setInterval(refreshState, POLL_MS);

      showTab("draw");
      renderTop();
      renderLines();
      syncControls();
      renderTickets();
      renderResults();
      renderKinds();
      renderCards();
      refreshState();
      loadTickets(0);
      loadResults();
      loadCards();
    },
  });
})();
