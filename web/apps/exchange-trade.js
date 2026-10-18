(function () {
  "use strict";
  const { el, button, field, money, num } = Apps;

  const SIDES = [["buy", "Buy", "Bought"], ["sell", "Sell", "Sold"], ["short", "Short", "Shorted"], ["cover", "Cover", "Covered"]];
  const PRESETS = [1, 10, 100, 1000];
  const MAX_SHARES = 1000000;
  const DEBOUNCE_MS = 200;
  const CLOSED = "The market is closed.";
  const FACADE_CLOSED = "The exchange is closed.";
  const NO_ANSWER = "No answer from the exchange. Check your portfolio before trying again.";
  const BUSY = "The game is saving, try again in a moment.";
  const REFUSED = "The market refused this order.";
  const DENIED = {
    funds: "You do not have enough money for this order.",
    range: "Enter a whole number of shares from 1 to " + num(MAX_SHARES, 0) + ".",
    holding: "You do not hold enough shares for this order.",
    side: "This order does not fit your position.",
    limit: "This order is too large for the market.",
    small: "This order is too small.",
    settle: "The last trade is still settling. Try again in a moment.",
  };

  function token() {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function option(value, label) {
    const o = el("option", "", label);
    o.value = value;
    return o;
  }

  function errorText(e) {
    if (!e) return NO_ANSWER;
    if (e.error === "failed") return e.message === FACADE_CLOSED ? CLOSED : NO_ANSWER;
    if (e.error === "busy") return BUSY;
    if (e.error === "denied") return DENIED[e.message] || REFUSED;
    return e.message || REFUSED;
  }

  function labelOf(action, margin, amount) {
    if (action === "buy") return margin ? "Down payment" : "Cost";
    if (action === "sell") return "Proceeds";
    if (action === "short") return "Collateral";
    return amount < 0 ? "You pay" : "You receive";
  }

  window.ExchangeTrade = {
    panel(ctx, done) {
      const st = { action: "buy", ticker: "", currency: "RM", seq: 0, timer: 0, pending: false, cid: null, cidKey: "" };

      const root = el("section", "stalex-trade stalex-card");
      root.setAttribute("aria-label", "Trade");
      const heading = el("h3", "stalex-heading", "Trade");

      const sides = el("div", "stalex-seg stalex-sides");
      sides.setAttribute("role", "group");
      sides.setAttribute("aria-label", "Order side");
      const sideButtons = SIDES.map(([id, label]) => {
        const b = button("", label);
        b.dataset.action = id;
        b.addEventListener("click", () => setAction(id));
        return b;
      });
      sides.append(...sideButtons);

      const qty = field("number", "stalex-input stalex-qty", "Shares");
      qty.min = "1";
      qty.max = String(MAX_SHARES);
      qty.step = "1";
      qty.value = "1";
      const qtyLabel = el("label", "stalex-field");
      qtyLabel.append(el("span", "stalex-label", "Shares"), qty);
      const presets = el("div", "stalex-presets");
      presets.append(...PRESETS.map((n) => {
        const b = button("stalex-btn sm", num(n, 0));
        b.addEventListener("click", () => {
          qty.value = String(n);
          onInput();
        });
        return b;
      }));

      const funding = el("select", "stalex-input stalex-funding");
      funding.append(option("cash", "Pay from cash"), option("margin", "Margin loan"));
      const fund = el("label", "stalex-field");
      fund.append(el("span", "stalex-label", "Funding"), funding);

      const preview = el("p", "stalex-preview stalex-num");
      const note = el("p", "stalex-note");
      note.setAttribute("role", "status");
      const outcome = el("p", "stalex-outcome");
      outcome.setAttribute("role", "status");
      const review = button("stalex-btn primary block", "Review order");
      review.disabled = true;

      const confirm = el("div", "stalex-confirm");
      confirm.hidden = true;
      const question = el("p", "stalex-question");
      const go = button("stalex-btn primary", "Confirm");
      const back = button("stalex-btn", "Back");
      const actions = el("div", "stalex-actions");
      actions.append(go, back);
      confirm.append(question, actions);

      root.append(heading, sides, qtyLabel, presets, fund, preview, note, review, confirm, outcome);

      function shares() {
        const n = Number(qty.value);
        return Number.isInteger(n) && n >= 1 && n <= MAX_SHARES ? n : 0;
      }

      function margin() {
        return st.action === "buy" && funding.value === "margin";
      }

      function hideConfirm() {
        confirm.hidden = true;
      }

      function schedule() {
        clearTimeout(st.timer);
        st.timer = setTimeout(check, DEBOUNCE_MS);
      }

      function onInput() {
        outcome.textContent = "";
        hideConfirm();
        schedule();
      }

      function setAction(action) {
        st.action = action;
        for (const b of sideButtons) b.setAttribute("aria-pressed", String(b.dataset.action === action));
        fund.hidden = action !== "buy";
        outcome.textContent = "";
        hideConfirm();
        schedule();
      }

      function check() {
        const n = ++st.seq;
        const count = shares();
        review.disabled = true;
        preview.textContent = "";
        note.textContent = "";
        if (!st.ticker) return;
        if (!count) {
          note.textContent = DENIED.range;
          return;
        }
        ctx.call("exchange.preview", { action: st.action, ticker: st.ticker, shares: count, margin: margin() }).then((r) => {
          if (n !== st.seq || !r) return;
          if (!r.allowed) {
            note.textContent = DENIED[r.reason] || REFUSED;
            return;
          }
          const amount = Number(r.amount) || 0;
          preview.textContent = labelOf(st.action, margin(), amount) + ": " + money(Math.abs(amount), st.currency);
          review.disabled = false;
        }, (e) => {
          if (n === st.seq) note.textContent = errorText(e);
        });
      }

      function submit() {
        if (st.pending) return;
        const action = st.action;
        const count = shares();
        const key = [action, st.ticker, count, margin()].join("|");
        if (!st.cid || st.cidKey !== key) {
          st.cid = token();
          st.cidKey = key;
        }
        st.pending = true;
        go.disabled = true;
        ctx.call("exchange." + action, { ticker: st.ticker, shares: count, margin: margin(), cid: st.cid }).then(() => {
          st.cid = null;
          outcome.textContent = SIDES.find((s) => s[0] === action)[2] + " " + num(count, 0) + " " + st.ticker + ".";
          finish();
          done();
        }, (e) => {
          outcome.textContent = errorText(e);
          finish();
          schedule();
          if (!e || e.error !== "denied") done();
        });
      }

      function finish() {
        st.pending = false;
        go.disabled = false;
        hideConfirm();
      }

      function set(ticker, currency) {
        if ((ticker || "") !== st.ticker) outcome.textContent = "";
        st.ticker = ticker || "";
        st.currency = currency || st.currency;
        hideConfirm();
        schedule();
      }

      function stop() {
        clearTimeout(st.timer);
      }

      review.addEventListener("click", () => {
        question.textContent = SIDES.find((s) => s[0] === st.action)[1] + " " + num(shares(), 0) + " " + st.ticker + "?";
        confirm.hidden = false;
        go.focus();
      });
      go.addEventListener("click", submit);
      back.addEventListener("click", () => {
        hideConfirm();
        review.focus();
      });
      confirm.addEventListener("keydown", (e) => {
        if (e.key !== "Escape") return;
        e.stopPropagation();
        hideConfirm();
        review.focus();
      });
      qty.addEventListener("input", onInput);
      funding.addEventListener("change", onInput);

      setAction("buy");
      return { el: root, set, stop };
    },
  };
})();
