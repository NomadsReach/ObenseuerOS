(function () {
  "use strict";
  const { el, button, field, web, money, num } = Apps;

  const NAV = [["accounts", "Accounts"], ["payments", "Payments"], ["loans", "Loans"], ["cards", "Cards"], ["help", "Help"]];
  const PAGE = 20;
  const MAX_AMOUNT = 16000000;
  const FALLBACK_FEE = 2;
  const MAX_PENDING = 20;
  const NO_ANSWER = "No response from game host";
  const UNKNOWN = "The game did not answer. Your payment or order may have gone through. Check Recent transactions before trying again.";
  const NO_HOST = "The bank works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const SAVING = "Saving...";
  const ENTER_AMOUNT = "Enter an amount.";
  const TOO_SMALL = "The amount is too small to cover the fee.";
  const REPAY_MIN = "Enter a whole amount of at least 1.";
  const NO_HISTORY = "History is not recorded in this save.";
  const NO_RECORDS = "No records yet.";
  const NO_INVOICES = "No unpaid invoices.";
  const NO_LOAN = "No loan to repay.";
  const FROZEN_NOTE = "A frozen account shows its last stored balance.";
  const LOCKED = "The bank is locked.";
  const CARD_ON = "A debit card is on file for this save. ATM use needs it.";
  const CARD_OFF = "No debit card on file. ATM use needs one.";
  const PRODUCTS = [
    ["money", "Current account", "Balances in OC and RM, kept at a Stalburg Bank branch."],
    ["computer", "Debit card", "Needed for ATM use. Ask at the branch for a replacement."],
    ["office", "Loans", "Borrow against your account and repay in OC or RM."],
    ["people", "Cash by post", "Order cash, debited now and mailed to you as a letter."],
  ];
  const HELP = [
    ["Opening an account", "Accounts are opened at a Stalburg Bank branch. Balances show once an account is open."],
    ["Customer service", "Customer service is available in Swedish, Finnish, Norwegian, English and German."],
    ["A payment is missing?", "Check Recent transactions before you try again, so you do not pay twice."],
    ["Cash orders", "Cash is debited now and sent by post. The fee is shown before you confirm."],
  ];
  const FOOT_COLS = [
    ["Personal", ["Accounts", "Payments", "Loans", "Cards"]],
    ["Help", ["Customer service", "Security", "Branches"]],
    ["About", ["About Stalburg Bank", "Careers", "Press"]],
  ];

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || "Something went wrong.";
  }

  function token() {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function signed(v, currency) {
    const s = money(v, currency);
    return (v > 0 && /[1-9]/.test(s) ? "+" : "") + s;
  }

  function quote(currency, amount, percent) {
    const scale = currency === "RM" ? 10 : 1;
    const units = Math.floor(currency === "RM" ? Math.round(amount * 10) : amount);
    if (!(units >= 1) || units > MAX_AMOUNT * scale) return null;
    const fee = Math.max(1, Math.ceil(units * percent / 100 - 1e-9));
    return { debit: units / scale, fee: fee / scale, sent: Math.max(0, units - fee) / scale };
  }

  function feeText(percent) {
    if (percent === null) return "Cash order fees are an estimate of " + num(FALLBACK_FEE, 0) + " % of the amount debited.";
    const shown = Number.isInteger(percent) ? num(percent, 0) : num(percent, 2);
    return "Cash order fee: " + shown + " % of the amount debited. The rest is sent by post.";
  }

  function cell(text) {
    return el("td", "", text);
  }

  function table(heads, rows) {
    const t = el("table", "bank-table");
    const tr = el("tr");
    tr.append(...heads.map((h) => el("th", "", h)));
    t.createTHead().append(tr);
    const body = el("tbody");
    body.append(...rows);
    t.append(body);
    return t;
  }

  function band(cls, ...kids) {
    const b = el("div", "bank-band " + cls);
    const inner = el("div", "bank-wrap");
    inner.append(...kids);
    b.append(inner);
    return b;
  }

  function pageHead(title, note) {
    const head = el("div", "bank-greet");
    head.append(el("h1", "bank-h1", title), el("p", "bank-note", note));
    return head;
  }

  function panel(title, ...kids) {
    const p = el("section", "bank-panel");
    p.append(el("h2", "bank-panel-title", title), ...kids);
    return p;
  }

  function photo(tag, seed) {
    return (window.PHOTOS && window.PHOTOS.pick(tag, seed)) || null;
  }

  function picture(p, cls) {
    if (!p) return null;
    const frame = el("div", "bank-pic " + cls);
    const img = el("img");
    img.alt = "";
    img.src = p.thumb;
    img.addEventListener("error", () => {
      frame.hidden = true;
    });
    frame.append(img, el("span", "bank-cap", p.credit));
    return frame;
  }

  function brand() {
    const logo = el("div", "bank-brand");
    logo.append(el("span", "bank-sbb", "SBB"), el("span", "bank-word", "Stalburg Bank"));
    return logo;
  }

  function balances(oc, rm) {
    const row = el("div", "bank-amounts");
    row.append(el("span", "bank-amt", money(oc, "OC")), el("span", "bank-amt", money(rm, "RM")));
    return row;
  }

  function accountCard(name, oc, rm, frozen) {
    const c = el("article", "bank-card");
    const head = el("div", "bank-card-head");
    head.append(el("h3", "bank-card-name", name));
    if (frozen) head.append(el("span", "bank-badge", "Frozen"));
    c.append(head, balances(oc, rm));
    return c;
  }

  function ledgerRow(e) {
    const entry = cell(e.kind);
    if (e.memo) entry.append(el("span", "bank-memo", e.memo));
    const debt = [e.debtOcDelta ? signed(e.debtOcDelta, "OC") : "", e.debtRmDelta ? signed(e.debtRmDelta, "RM") : ""].filter(Boolean).join(" / ");
    const tr = el("tr");
    tr.append(cell(num(e.day, 0)), entry, cell(signed(e.ocDelta, "OC")), cell(signed(e.rmDelta, "RM")), cell(debt), cell(money(e.ocAfter, "OC")), cell(money(e.rmAfter, "RM")));
    return tr;
  }

  function status(i) {
    if (i.expired) return "Expired";
    return num(i.daysLeft, 0) + (i.daysLeft === 1 ? " day left" : " days left");
  }

  function invoiceRow(i) {
    const tr = el("tr");
    tr.append(cell(i.debtor), cell(money(i.amount, i.currency)), cell("Day " + num(i.dueDay, 0)), cell(status(i)));
    return tr;
  }

  function labelled(text, control) {
    const l = el("label", "bank-field");
    l.append(el("span", "bank-label", text), control);
    return l;
  }

  function form(kids, onSubmit) {
    const f = el("form", "bank-form");
    f.noValidate = true;
    f.append(...kids);
    f.addEventListener("submit", (e) => {
      e.preventDefault();
      onSubmit();
    });
    return f;
  }

  function currencySelect() {
    const s = el("select", "bank-select");
    for (const c of ["OC", "RM"]) {
      const o = el("option", "", c);
      o.value = c;
      s.append(o);
    }
    return s;
  }

  function amountField(min, step) {
    const i = field("number", "bank-input bank-amount", "Amount");
    i.min = String(min);
    i.step = String(step);
    return i;
  }

  function sum(label, value) {
    const r = el("div", "bank-sum");
    r.append(el("span", "bank-label", label), el("span", "bank-value", value));
    return r;
  }

  Apps.register("bank", {
    mount(slot, ctx) {
      const st = { bank: "", accounts: [], wallet: null, history: false, card: false, ledger: [], nextId: 0, since: null, invoices: [], total: 0, loan: null, feePercent: null, paging: false, busy: false, seq: 0 };
      const pends = new Map();

      const notice = el("p", "bank-notice");
      notice.setAttribute("role", "alert");
      notice.hidden = true;

      function setNotice(text, ok) {
        notice.textContent = text || "";
        notice.hidden = !text;
        notice.classList.toggle("ok", Boolean(ok));
      }

      const acctSel = el("select", "bank-select bank-acct-sel");
      acctSel.setAttribute("aria-label", "Account");
      acctSel.addEventListener("change", () => {
        st.bank = acctSel.value;
        refresh();
      });
      const acctBox = el("label", "bank-acct");
      acctBox.append(el("span", "bank-acct-label", "Account"), acctSel);
      const reload = button("bank-btn line sm", "Refresh");
      reload.addEventListener("click", () => refresh());
      const util = band("bank-util", el("span", "bank-branch", "Stalburg Branch, Market Street 14A"), acctBox, reload, el("span", "bank-langs", "Language: English"));
      const masthead = band("bank-masthead", brand(), el("span", "bank-tag", "Online banking"));

      const nav = el("nav", "bank-nav");
      nav.setAttribute("aria-label", "Sections");
      const navButtons = NAV.map(([id, label]) => {
        const b = button("bank-navlink", label);
        b.dataset.view = id;
        b.addEventListener("click", () => pick(id));
        return b;
      });
      nav.append(...navButtons);
      const navBand = band("bank-navband", nav);

      const balance = el("div", "bank-balance");
      const accountsBox = el("div", "bank-cards");
      const frozenNote = el("p", "bank-note", FROZEN_NOTE);
      frozenNote.hidden = true;
      const histNote = el("p", "bank-note");
      const histBox = el("div", "bank-scroll");
      const moreBtn = button("bank-btn line sm", "Load older records");
      moreBtn.addEventListener("click", () => loadMore());
      const main = el("div", "bank-main");
      main.append(balance, accountsBox, frozenNote, panel("Recent transactions", histNote, histBox, moreBtn));

      const loanSide = el("div", "bank-stack");
      const quick = (label, view) => {
        const b = button("bank-btn line", label);
        b.addEventListener("click", () => pick(view));
        return b;
      };
      const actions = el("div", "bank-quick");
      actions.append(quick("Pay an invoice", "payments"), quick("Order cash", "payments"), quick("Repay loan", "loans"));
      const security = el("section", "bank-security");
      security.append(el("h2", "bank-panel-title", "Keep your account safe"), el("p", "", "Keep your debit card with you. Never give your PIN code or a security code to anyone, by mail or by phone."), el("p", "", "Check Recent transactions often and report anything you do not recognise at the branch."));
      const side = el("aside", "bank-side");
      side.append(panel("Quick actions", actions), panel("Loan", loanSide, quick("Manage loan", "loans")), security);
      const dash = el("div", "bank-dash");
      dash.append(main, side);
      const paneAccounts = el("section", "bank-pane");
      paneAccounts.append(pageHead("Your accounts", "Balances and recent transactions for the selected account."), dash);

      const invNote = el("p", "bank-note");
      const invBox = el("div", "bank-scroll");
      const paySel = el("select", "bank-select bank-pay");
      paySel.setAttribute("aria-label", "Invoice");
      const payBtn = button("bank-btn", "Pay invoice");
      payBtn.type = "submit";
      const payForm = form([labelled("Invoice", paySel), payBtn], paySubmit);

      const cashCur = currencySelect();
      const cashAmt = amountField(0, 1);
      const quoteBox = el("div", "bank-quote");
      const cashBtn = button("bank-btn", "Order cash");
      cashBtn.type = "submit";
      const cashForm = form([labelled("Currency", cashCur), labelled("Amount", cashAmt), quoteBox, cashBtn], cashSubmit);
      cashCur.addEventListener("change", preview);
      cashAmt.addEventListener("input", preview);
      const panePayments = el("section", "bank-pane");
      panePayments.append(pageHead("Payments", "Pay an invoice or order cash by post."), panel("Pay an invoice", invNote, invBox, payForm), panel("Order cash", el("p", "bank-note", "Cash is debited now and mailed to you as a letter."), cashForm));

      const loanBox = el("div", "bank-stack");
      const repayCur = currencySelect();
      const repayAmt = amountField(1, 1);
      const repayBtn = button("bank-btn", "Repay");
      repayBtn.type = "submit";
      const repayForm = form([labelled("Currency", repayCur), labelled("Amount", repayAmt), repayBtn], repaySubmit);
      const paneLoans = el("section", "bank-pane");
      paneLoans.append(pageHead("Loans", "Your loan balance and repayments."), panel("Loan balance", loanBox), panel("Repay", repayForm));

      const cardNote = el("p", "bank-note");
      const paneCards = el("section", "bank-pane");
      paneCards.append(pageHead("Cards", "Your Stalburg Bank debit card."), panel("Debit card", cardNote, el("p", "bank-note", "Ask at the Stalburg Branch, Market Street 14A.")));

      const helpBox = el("div", "bank-help");
      helpBox.append(...HELP.map(([title, text]) => {
        const item = el("article", "bank-help-item");
        item.append(el("h3", "", title), el("p", "", text));
        return item;
      }));
      const paneHelp = el("section", "bank-pane");
      paneHelp.append(pageHead("Help", "Answers about accounts, payments and loans."), helpBox);

      const panes = { accounts: paneAccounts, payments: panePayments, loans: paneLoans, cards: paneCards, help: paneHelp };
      const body = el("div", "bank-body");
      body.hidden = true;
      body.append(...Object.values(panes));

      const heroNote = el("div", "bank-hero-note");
      const hero = el("div", "bank-hero");
      const heroText = el("div", "bank-hero-text");
      heroText.append(el("span", "bank-kicker", "Stalburg Bank"), el("h1", "bank-hero-title", "Banking for the residents of Stalburg"), el("p", "bank-hero-lede", "Accounts, payments and loans, all in one place."), heroNote);
      hero.append(...[picture(photo("bank", 1), "bank-hero-pic")].filter(Boolean), heroText);
      const products = el("section", "bank-products");
      const productRows = PRODUCTS.map(([tag, title, text], i) => {
        const name = cell("");
        name.append(...[picture(photo(tag, i + 2), "bank-thumb")].filter(Boolean), title);
        const tr = el("tr");
        tr.append(name, cell(text));
        return tr;
      });
      products.append(el("h2", "bank-panel-title", "Products"), table(["Product", "Details"], productRows));
      const lock = el("section", "bank-lock");
      lock.hidden = true;
      lock.append(hero, products);

      const feeNote = el("p", "bank-fee");
      const footCols = el("div", "bank-foot-cols");
      footCols.append(...FOOT_COLS.map(([head, links]) => {
        const col = el("div", "bank-foot-col");
        col.append(el("h4", "", head), ...links.map((l) => el("span", "bank-foot-link", l)));
        return col;
      }));
      const small = el("p", "bank-small", "Stalburg Bank (SBB), Market Street 14A, 000011 STALBURG. Tel 010 242 40 96. Offices in West Stalburg and Orefell.");
      const foot = band("bank-foot", footCols, feeNote, small);

      const content = el("div", "bank-wrap bank-content");
      content.append(notice, lock, body);
      const root = el("div", "bank");
      root.append(util, masthead, navBand, content, foot);
      root.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && e.target.matches("input, select")) {
          e.stopPropagation();
          e.target.blur();
        }
      });
      const frame = web(slot, "Stalburg Bank", "https://www.stalburgbank.ts/", ctx.embedded);
      frame.classList.add("bank-page");
      frame.append(root);

      function pick(id) {
        for (const b of navButtons) {
          const on = b.dataset.view === id;
          b.classList.toggle("active", on);
          b.setAttribute("aria-current", on ? "page" : "false");
        }
        for (const [key, pane] of Object.entries(panes)) pane.hidden = key !== id;
      }

      function read(method, args) {
        return ctx.call(method, args).catch((e) => {
          setNotice(textOf(e));
          return null;
        });
      }

      function ledgerPage(beforeId) {
        return read("bank.ledger", { bank: st.bank, max: PAGE, beforeId });
      }

      function showLock(s) {
        const kids = [el("h2", "bank-lock-title", s.heading || LOCKED), el("p", "", s.hint || "")];
        const task = s.objective;
        if (task && (task.title || task.text)) {
          const box = el("div", "bank-task");
          box.append(el("span", "bank-label", "Current task"));
          if (task.title) box.append(el("h3", "", task.title));
          if (task.text) box.append(el("p", "", task.text));
          kids.push(box);
        }
        heroNote.replaceChildren(...kids);
        lock.hidden = false;
        body.hidden = true;
        nav.hidden = true;
        acctBox.hidden = true;
      }

      function showOpen() {
        lock.hidden = true;
        body.hidden = false;
        nav.hidden = false;
        acctBox.hidden = false;
        acctSel.replaceChildren(...st.accounts.map((a) => {
          const o = el("option", "", a.bank);
          o.value = a.bank;
          return o;
        }));
        acctSel.value = st.bank;
      }

      function renderBalance() {
        const cur = st.accounts.find((a) => a.bank === st.bank) || st.wallet;
        if (!cur) {
          balance.replaceChildren();
          return;
        }
        balance.replaceChildren(el("span", "bank-label", "Balance: " + (cur.bank || "Wallet")), el("span", "bank-big", money(cur.oc, "OC")), el("span", "bank-big", money(cur.rm, "RM")));
      }

      function renderAccounts() {
        accountsBox.replaceChildren(accountCard("Wallet", st.wallet.oc, st.wallet.rm, false), ...st.accounts.map((a) => accountCard(a.bank, a.oc, a.rm, a.frozen)));
        frozenNote.hidden = !st.accounts.some((a) => a.frozen);
        renderBalance();
      }

      function renderHistory() {
        let note = "";
        if (!st.history) note = NO_HISTORY;
        else if (st.since !== null) note = "Records from day " + num(st.since, 0) + ".";
        histNote.textContent = note;
        histNote.hidden = !note;
        histBox.replaceChildren(st.ledger.length ? table(["Day", "Entry", "OC", "RM", "Debt", "OC after", "RM after"], st.ledger.map(ledgerRow)) : el("p", "bank-empty", st.history ? NO_RECORDS : ""));
        moreBtn.hidden = st.nextId < 1;
      }

      function renderInvoices() {
        const payable = st.invoices.filter((i) => !i.expired);
        invBox.replaceChildren(st.invoices.length ? table(["Debtor", "Amount", "Due", "Status"], st.invoices.map(invoiceRow)) : el("p", "bank-empty", NO_INVOICES));
        invNote.textContent = st.total > st.invoices.length ? "Showing " + num(st.invoices.length, 0) + " of " + num(st.total, 0) + " invoices." : "";
        invNote.hidden = !invNote.textContent;
        paySel.replaceChildren(...payable.map((i) => {
          const o = el("option", "", "#" + num(i.id, 0) + " " + (i.debtor || "") + ": " + money(i.amount, i.currency));
          o.value = String(i.id);
          return o;
        }));
        payForm.hidden = !payable.length;
      }

      function loanNodes() {
        const l = st.loan;
        if (!l) return [];
        const owed = l.debtOc > 0 || l.debtRm > 0;
        const kids = [accountCard("Debt", l.debtOc, l.debtRm, l.frozen)];
        if (!owed) kids.push(el("p", "bank-note", NO_LOAN));
        return kids;
      }

      function renderLoan() {
        loanBox.replaceChildren(...loanNodes());
        loanSide.replaceChildren(...loanNodes());
      }

      function renderCards() {
        cardNote.textContent = st.card ? CARD_ON : CARD_OFF;
      }

      function renderFee() {
        feeNote.textContent = feeText(st.feePercent);
      }

      function preview() {
        const cur = cashCur.value;
        cashAmt.step = cur === "RM" ? "0.1" : "1";
        const q = quote(cur, Number(cashAmt.value), st.feePercent ?? FALLBACK_FEE);
        if (!q) quoteBox.replaceChildren(el("p", "bank-note", ENTER_AMOUNT));
        else if (q.sent <= 0) quoteBox.replaceChildren(el("p", "bank-note", TOO_SMALL));
        else quoteBox.replaceChildren(sum("Debited", money(q.debit, cur)), sum(st.feePercent === null ? "Fee (estimate)" : "Fee", money(q.fee, cur)), sum("Sent by post", money(q.sent, cur)));
      }

      async function refresh() {
        const n = ++st.seq;
        const s = await read("bank.status", {});
        if (n !== st.seq || !s) return;
        st.history = Boolean(s.history);
        st.card = Boolean(s.hasCard);
        st.feePercent = Number.isFinite(s.feePercent) ? s.feePercent : null;
        renderFee();
        renderCards();
        if (s.locked) {
          showLock(s);
          return;
        }
        const a = await read("bank.accounts", {});
        if (n !== st.seq || !a) return;
        st.wallet = a.wallet;
        st.accounts = a.accounts;
        if (!st.accounts.some((x) => x.bank === st.bank)) st.bank = st.accounts.length ? st.accounts[0].bank : "";
        showOpen();
        renderAccounts();
        if (!st.bank) return;
        const [page, inv, loan] = await Promise.all([ledgerPage(0), read("bank.invoices", {}), read("bank.loan.info", { bank: st.bank })]);
        if (n !== st.seq) return;
        const p = page || { entries: [], nextBeforeId: 0, since: null };
        st.ledger = p.entries;
        st.nextId = p.nextBeforeId;
        st.since = p.since;
        if (inv) {
          st.invoices = inv.items;
          st.total = inv.total;
        }
        st.loan = loan;
        renderHistory();
        renderInvoices();
        renderLoan();
      }

      async function loadMore() {
        if (st.paging) return;
        st.paging = true;
        moreBtn.disabled = true;
        const n = st.seq;
        const page = await ledgerPage(st.nextId);
        st.paging = false;
        moreBtn.disabled = false;
        if (!page || n !== st.seq) return;
        st.ledger = st.ledger.concat(page.entries);
        st.nextId = page.nextBeforeId;
        renderHistory();
      }

      const submits = [payBtn, repayBtn, cashBtn];

      function setBusy(on) {
        st.busy = on;
        for (const b of submits) b.disabled = on;
        root.setAttribute("aria-busy", String(on));
      }

      function cidFor(key) {
        if (!pends.has(key)) {
          if (pends.size >= MAX_PENDING) pends.delete(pends.keys().next().value);
          pends.set(key, token());
        }
        return pends.get(key);
      }

      async function write(method, args, done) {
        if (st.busy) return;
        const key = method + JSON.stringify(args);
        setBusy(true);
        setNotice(SAVING);
        let result = null;
        let failed = "";
        try {
          result = await ctx.call(method, Object.assign({ cid: cidFor(key) }, args));
          pends.delete(key);
        } catch (e) {
          failed = e && e.message === NO_ANSWER ? UNKNOWN : textOf(e);
        }
        if (result) done(result);
        await refresh();
        if (failed) setNotice(failed);
        setBusy(false);
      }

      function paySubmit() {
        const id = Number(paySel.value);
        if (!(id >= 1)) return;
        write("bank.payInvoice", { bank: st.bank, id }, (r) => setNotice("Invoice " + num(r.id, 0) + " paid: " + money(r.amount, r.currency) + ".", true));
      }

      function repaySubmit() {
        const amount = Math.floor(Number(repayAmt.value));
        if (!(amount >= 1)) {
          setNotice(REPAY_MIN);
          return;
        }
        write("bank.loan.repay", { bank: st.bank, currency: repayCur.value, amount }, (r) => {
          repayAmt.value = "";
          setNotice("Left to repay: " + money(r.debtOc, "OC") + " and " + money(r.debtRm, "RM") + ".", true);
        });
      }

      function cashSubmit() {
        const currency = cashCur.value;
        const amount = Number(cashAmt.value);
        const q = quote(currency, amount, st.feePercent ?? FALLBACK_FEE);
        if (!q || q.sent <= 0) {
          setNotice(q ? TOO_SMALL : ENTER_AMOUNT);
          return;
        }
        write("bank.cash.order", { bank: st.bank, currency, amount: q.debit }, (r) => {
          cashAmt.value = "";
          preview();
          setNotice("Debited " + money(r.debited, r.currency) + ", fee " + money(r.fee, r.currency) + ". " + money(r.sent, r.currency) + " is on its way by post.", true);
        });
      }

      const off = Apps.on("bank", (m) => {
        if (!root.isConnected) off();
        else if (m.name === "changed") refresh();
      });
      pick("accounts");
      renderFee();
      preview();
      refresh();
      return { onOpen: () => refresh() };
    },
  });
})();
