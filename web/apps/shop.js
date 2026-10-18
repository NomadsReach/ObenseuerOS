(function () {
  "use strict";
  const { el, button, field, num } = Apps;
  const { money, plural, accent, card, detail, storeTile, line, orderTable, hero } = ShopView;

  const VIEW_STORES = "stores";
  const VIEW_PRODUCTS = "products";
  const VIEW_DETAIL = "detail";
  const VIEW_ORDER = "order";
  const VIEW_ORDERS = "orders";
  const SORTS = [["name", "Name"], ["price_up", "Price up"], ["price_down", "Price down"], ["unlocked", "Unlocked first"]];
  const DEBOUNCE_MS = 200;
  const PAGE_SIZE = 16;
  const TERMS = "Prices are in OC and RM. Delivery time depends on the speed you choose.";
  const NO_HOST = "The Shop works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const BAD_REPLY = "The game sent an unexpected reply.";

  function option(value, label) {
    const o = el("option", "", label);
    o.value = value;
    return o;
  }

  function textOf(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || "Something went wrong.";
  }

  function token() {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function labelled(text, control) {
    const l = el("label", "shop-field");
    l.append(el("span", "shop-label", text), control);
    return l;
  }

  function sumRow(label, value, cls) {
    const r = el("div", cls ? `shop-sum ${cls}` : "shop-sum");
    r.append(el("span", "", label), value);
    return r;
  }

  function crumb(text, go) {
    if (!go) {
      const c = el("span", "shop-crumb current", text);
      c.setAttribute("aria-current", "page");
      return c;
    }
    const b = button("shop-crumb", text);
    b.addEventListener("click", go);
    return b;
  }

  function keepFocus(box, render) {
    const key = box.contains(document.activeElement) ? document.activeElement.dataset.key : undefined;
    render();
    const again = [...box.querySelectorAll("[data-key]")].find((n) => n.dataset.key === key);
    if (again) again.focus();
  }

  function quoteOk(q) {
    return Boolean(q && q.subtotal && q.fee && q.total);
  }

  Apps.register("shop", {
    mount(slot, ctx) {
      const st = { view: VIEW_STORES, store: "", category: "", q: "", total: 0, page: 0, listSeq: 0, storesSeq: 0, detailSeq: 0, accountSeq: 0, ordersSeq: 0, cats: [], stores: [], items: [], detail: null, orders: [], cart: [], quote: null, seq: 0, pending: false, cid: null };
      let timer = 0;

      const notice = el("p", "shop-notice");
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

      const wordmark = el("span", "shop-wordmark", "Shop");
      const brand = button("shop-brand", "");
      brand.append(wordmark);

      const search = field("search", "shop-search-input", "Search products");
      search.placeholder = "Search products";
      const clear = button("shop-search-clear", "×");
      clear.setAttribute("aria-label", "Clear search");
      const go = button("shop-search-go", "Go");
      const searchBox = el("div", "shop-search-box");
      searchBox.append(search, clear, go);

      const wallet = el("span", "shop-wallet");
      const account = el("div", "shop-account");
      account.append(el("span", "shop-label", "Balance"), wallet);

      const orderLink = button("shop-cart-btn", "");
      const orderCount = el("span", "shop-count");
      const orderPrice = el("span", "shop-cart-total");
      orderLink.append(el("span", "", "Cart"), orderCount, orderPrice);

      const top = el("div", "shop-top");
      top.append(brand, searchBox, orderLink);

      const historyLink = button("shop-util-link", "Order history");
      const utility = el("div", "shop-utility");
      utility.append(el("span", "", "Parcels arrive at your parcel locker"), account, historyLink);

      const allStores = button("shop-tab", "All stores");
      const tabs = el("nav", "shop-tabs");
      tabs.setAttribute("aria-label", "Stores");

      const head = el("header", "shop-head");
      head.append(utility, top, tabs);

      const cats = el("nav", "shop-cats");
      cats.setAttribute("aria-label", "Categories");

      const crumbs = el("nav", "shop-crumbs");
      crumbs.setAttribute("aria-label", "Breadcrumb");

      const pageTitle = el("h3", "shop-title");
      const count = el("span", "shop-count");
      const titlebar = el("div", "shop-titlebar");
      titlebar.append(pageTitle, count);
      const sort = el("select", "shop-input shop-sort");
      sort.setAttribute("aria-label", "Sort");
      sort.append(...SORTS.map(([v, t]) => option(v, t)));
      const lockedBox = field("checkbox", "", "Show locked");
      lockedBox.checked = true;
      const showLocked = el("label", "shop-check");
      showLocked.append(lockedBox, el("span", "", "Show locked"));
      const tools = el("div", "shop-tools");
      tools.append(sort, showLocked);

      const heroBox = hero(() => pick(VIEW_PRODUCTS, "", ""));
      const storeGrid = el("div", "shop-stores");
      const productGrid = el("div", "shop-products");
      const pager = el("nav", "shop-pager");
      pager.setAttribute("aria-label", "Pages");
      const fine = el("p", "shop-fine", TERMS);
      const detailBox = el("section", "shop-detail");
      const orders = el("div", "shop-orders");

      const cartBox = el("section", "shop-checkout");
      cartBox.setAttribute("aria-label", "Order");
      const cartEmpty = el("div", "shop-empty-box");
      const keepShopping = button("shop-btn primary", "Continue shopping");
      keepShopping.addEventListener("click", () => pick(VIEW_PRODUCTS, "", ""));
      cartEmpty.append(el("p", "", "Your cart is empty."), keepShopping);
      const linesHead = el("tr");
      linesHead.append(...["", "Product", "Quantity", "Price", ""].map((h) => el("th", "", h)));
      const thead = el("thead");
      thead.append(linesHead);
      const linesBody = el("tbody");
      const lines = el("table", "shop-table shop-lines");
      lines.append(thead, linesBody);
      const items = el("div", "shop-cart-items");
      items.append(cartEmpty, lines);
      const delivery = el("select", "shop-input");
      const via = el("select", "shop-input");
      via.replaceChildren(option("parcel", "Parcel, picked up at the parcel locker"), option("mail", "Mail, a cardboard box in your mailbox"));
      const pay = el("select", "shop-input");
      const subtotal = el("span", "shop-value");
      const fee = el("span", "shop-value");
      const total = el("span", "shop-value");
      const problems = el("p", "shop-problems");
      const place = button("shop-btn primary block big", "Place order");
      const summary = el("aside", "shop-summary");
      summary.append(
        el("h4", "shop-summary-title", "Order summary"),
        labelled("Speed", delivery),
        labelled("Deliver by", via),
        labelled("Pay with", pay),
        sumRow("Subtotal", subtotal),
        sumRow("Fee", fee),
        sumRow("Total", total, "total"),
        problems,
        place
      );
      cartBox.append(items, summary);

      const main = el("section", "shop-page");
      main.append(notice, crumbs, heroBox, titlebar, tools, storeGrid, productGrid, pager, fine, detailBox, cartBox, orders);

      const footStores = el("div", "shop-foot-col");
      const footOrders = el("div", "shop-foot-col");
      const footCart = button("shop-foot-link", "Cart");
      const footHistory = button("shop-foot-link", "Order history");
      footOrders.append(el("h4", "", "Orders"), footCart, footHistory);
      const footInfo = el("div", "shop-foot-col");
      footInfo.append(el("h4", "", "Information"), el("p", "shop-foot-text", TERMS), el("p", "shop-foot-text", "Parcels arrive at your parcel locker. Mail orders arrive in your mailbox as a cardboard box. You will get a notice either way."));
      const foot = el("footer", "shop-foot");
      const cols = el("div", "shop-foot-cols");
      cols.append(footStores, footOrders, footInfo);
      foot.append(cols, el("p", "shop-copy", "© Shop, Stalburg"));

      const sheet = el("div", "shop-sheet");
      sheet.hidden = true;
      const confirm = el("div", "shop-confirm");
      confirm.setAttribute("role", "dialog");
      confirm.setAttribute("aria-modal", "true");
      confirm.setAttribute("aria-label", "Order placed");
      const orderName = el("p", "shop-heading");
      const sheetNote = el("p", "");
      const done = button("shop-btn primary", "OK");
      confirm.append(
        el("p", "shop-label", "Order placed"),
        orderName,
        sheetNote,
        done
      );
      sheet.append(confirm);

      function hideSheet() {
        sheet.hidden = true;
        (place.disabled ? search : place).focus();
      }

      sheet.addEventListener("keydown", (e) => {
        if (e.key === "Tab") {
          e.preventDefault();
          done.focus();
          return;
        }
        if (e.key !== "Escape") return;
        e.stopPropagation();
        hideSheet();
      });
      done.addEventListener("click", hideSheet);

      const body = el("div", "shop-body");
      body.append(cats, main);
      const root = el("div", "shop");
      root.append(head, body, foot);
      root.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && e.target.matches("select")) e.stopPropagation();
      });
      slot.append(root, sheet);

      function currentStore() {
        return st.stores.find((x) => x.id === st.store);
      }

      function titleText() {
        if (st.view === VIEW_STORES) return "Choose a store";
        if (st.view === VIEW_ORDER) return "Your cart";
        if (st.view === VIEW_ORDERS) return "Order history";
        const cur = currentStore();
        return st.category || (st.q ? "Search results" : cur ? cur.name : "All products");
      }

      function lockAll() {
        for (const b of main.querySelectorAll("button")) b.disabled = st.pending || b.dataset.off === "true";
        delivery.disabled = st.pending;
        via.disabled = st.pending;
        pay.disabled = st.pending;
      }

      function renderCrumbs() {
        const cur = currentStore();
        const trail = [["Home", () => pick(VIEW_STORES, "", "")]];
        if (st.view === VIEW_ORDER || st.view === VIEW_ORDERS) trail.push([titleText()]);
        else if (st.view !== VIEW_STORES) {
          if (cur) trail.push([cur.name, () => pick(VIEW_PRODUCTS, cur.id, "")]);
          if (st.category) trail.push([st.category, () => pick(VIEW_PRODUCTS, st.store, st.category)]);
          if (st.view === VIEW_DETAIL && st.detail) trail.push([st.detail.title]);
        }
        const nodes = [];
        trail.forEach(([text, act], i) => {
          if (i) nodes.push(el("span", "shop-crumb-sep", ">"));
          nodes.push(crumb(text, i === trail.length - 1 ? null : act));
        });
        crumbs.replaceChildren(...nodes);
        crumbs.hidden = st.view === VIEW_STORES;
      }

      function storeLinks(cls) {
        return st.stores.map((s) => {
          const b = button(cls, s.name);
          b.setAttribute("aria-pressed", String(st.store === s.id));
          b.addEventListener("click", () => pick(VIEW_PRODUCTS, s.id, ""));
          return b;
        });
      }

      function renderStoreBar() {
        allStores.setAttribute("aria-pressed", String(st.view === VIEW_STORES));
        tabs.replaceChildren(allStores, ...storeLinks("shop-tab"));
        footStores.replaceChildren(el("h4", "", "Stores"), ...storeLinks("shop-foot-link"));
      }

      function renderMain() {
        const products = st.view === VIEW_PRODUCTS;
        const cur = currentStore();
        root.className = `shop ${accent(st.store)}`;
        sheet.className = `shop-sheet ${accent(st.store)}`;
        wordmark.textContent = cur ? cur.name : "Shop";
        pageTitle.textContent = titleText();
        count.textContent = products ? plural(st.total, "product", "products") : st.view === VIEW_STORES ? plural(st.stores.length, "store", "stores") : "";
        titlebar.hidden = st.view === VIEW_DETAIL;
        tools.hidden = !products;
        heroBox.hidden = st.view !== VIEW_STORES;
        cats.hidden = st.view !== VIEW_PRODUCTS && st.view !== VIEW_DETAIL;
        storeGrid.hidden = st.view !== VIEW_STORES;
        productGrid.hidden = !products;
        pager.hidden = !products || st.total <= PAGE_SIZE;
        fine.hidden = !products;
        detailBox.hidden = st.view !== VIEW_DETAIL;
        cartBox.hidden = st.view !== VIEW_ORDER;
        orders.hidden = st.view !== VIEW_ORDERS;
        renderCrumbs();
        renderStoreBar();
        lockAll();
      }

      function navBtn(label, n, view, store, category) {
        const b = button("shop-cat", label);
        b.dataset.key = `${view}:${store}:${category}`;
        b.setAttribute("aria-pressed", String(view === st.view && store === st.store && category === st.category));
        if (n !== undefined) b.append(el("span", "shop-count", num(n, 0)));
        b.addEventListener("click", () => pick(view, store, category));
        return b;
      }

      function renderCats() {
        const all = st.cats.reduce((n, c) => n + (c.count || 0), 0);
        const nav = [navBtn("All products", all, VIEW_PRODUCTS, st.store, "")];
        for (const c of st.cats) nav.push(navBtn(c.name, c.count, VIEW_PRODUCTS, st.store, c.name));
        keepFocus(cats, () => cats.replaceChildren(...nav));
      }

      function pick(view, store, category) {
        ++st.detailSeq;
        clearTimeout(timer);
        st.view = view;
        st.store = store;
        st.category = category;
        renderCats();
        renderMain();
        if (view === VIEW_STORES) {
          search.value = "";
          loadStores();
          loadList();
        } else if (view === VIEW_PRODUCTS) loadList();
        else if (view === VIEW_ORDERS) refreshOrders();
      }

      function addLine(item, qty) {
        if (st.pending) return;
        const found = st.cart.find((l) => l.id === item.id);
        if (found) found.qty = Math.min(found.max, found.qty + qty);
        else st.cart.push({ id: item.id, title: item.title, icon: item.icon, qty, max: Math.max(1, item.maxQty || 1), oc: item.priceOc || 0, rm: item.priceRm || 0 });
        cartChanged();
      }

      function openProduct(id) {
        const n = ++st.detailSeq;
        return call("shop.item", { id }).then((item) => {
          if (!item || n !== st.detailSeq) return;
          setNotice("");
          st.detail = item;
          st.view = VIEW_DETAIL;
          renderCats();
          renderDetail();
          const first = detailBox.querySelector("button");
          if (first) first.focus();
        });
      }

      function backButton() {
        const b = button("shop-btn ghost sm", "Back");
        b.addEventListener("click", () => pick(VIEW_PRODUCTS, st.store, st.category));
        return b;
      }

      function renderDetail() {
        detailBox.replaceChildren(...(st.detail ? [backButton(), detail(st.detail, { add: addLine })] : []));
        renderMain();
      }

      function renderStores() {
        keepFocus(storeGrid, () => storeGrid.replaceChildren(...(st.stores.length ? st.stores.map((s) => storeTile(s, () => pick(VIEW_PRODUCTS, s.id, ""))) : [el("p", "shop-empty", "No stores yet.")])));
        renderMain();
      }

      function pageBtn(label, n, disabled, current) {
        const off = disabled || current;
        const b = button("shop-pagebtn", label);
        b.dataset.off = String(off);
        b.disabled = off;
        if (current) b.setAttribute("aria-current", "page");
        b.addEventListener("click", () => {
          if (off) return;
          root.scrollTop = 0;
          loadList(n);
        });
        return b;
      }

      function renderPager() {
        const pages = Math.ceil(st.total / PAGE_SIZE);
        const nodes = [];
        if (pages > 1) {
          nodes.push(pageBtn("<<", st.page - 1, st.page === 0, false));
          for (let i = 0; i < pages; i++) nodes.push(pageBtn(String(i + 1), i, false, i === st.page));
          nodes.push(pageBtn(">>", st.page + 1, st.page >= pages - 1, false));
        }
        pager.replaceChildren(...nodes);
      }

      function renderProducts() {
        productGrid.replaceChildren(...(st.items.length ? st.items.map((item) => card(item, { open: openProduct, add: addLine })) : [el("p", "shop-empty", "No results.")]));
        renderPager();
        renderMain();
      }

      function changeLine(l, d) {
        l.qty = Math.min(l.max, Math.max(1, l.qty + d));
        cartChanged();
      }

      function removeLine(l) {
        st.cart = st.cart.filter((x) => x !== l);
        cartChanged();
      }

      function renderLines() {
        keepFocus(linesBody, () => linesBody.replaceChildren(...st.cart.map((l) => line(l, { change: changeLine, remove: removeLine }))));
        cartEmpty.hidden = st.cart.length > 0;
        lines.hidden = st.cart.length === 0;
        summary.hidden = st.cart.length === 0;
        orderCount.textContent = st.cart.length ? `(${num(st.cart.reduce((n, l) => n + l.qty, 0), 0)})` : "";
        lockAll();
      }

      function cartChanged() {
        setNotice("");
        st.cid = null;
        renderLines();
        refreshQuote();
      }

      function linesArg() {
        return st.cart.map((l) => ({ id: l.id, qty: l.qty }));
      }

      function renderTotals() {
        const q = st.quote;
        orderPrice.textContent = q ? money(q.subtotal) : "";
        subtotal.textContent = q ? money(q.subtotal) : "-";
        fee.textContent = q ? money(q.fee) : "-";
        total.textContent = q ? money(q.total) : "-";
        problems.textContent = q && Array.isArray(q.problems) ? q.problems.join(" ") : "";
        place.dataset.off = String(!q || !q.ok || !delivery.value);
        lockAll();
      }

      function refreshQuote() {
        const seq = ++st.seq;
        setNotice("");
        st.quote = null;
        renderTotals();
        if (!st.cart.length) return;
        call("shop.quote", { lines: linesArg(), tier: delivery.value, pay: pay.value, via: via.value }).then((q) => {
          if (seq !== st.seq) return;
          if (q && !quoteOk(q)) setNotice(BAD_REPLY);
          st.quote = quoteOk(q) ? q : null;
          renderTotals();
        });
      }

      function fillTiers(tiers) {
        const keep = delivery.value;
        delivery.replaceChildren(...tiers.map((t) => {
          const o = option(t.id, t.locked ? `${t.label}: ${t.hint}` : `${t.label}, arrives in about ${num(t.hours, 0)} hours`);
          o.disabled = Boolean(t.locked);
          return o;
        }));
        const open = tiers.filter((t) => !t.locked).map((t) => t.id);
        delivery.value = open.includes(keep) ? keep : open[0] || "";
      }

      function fillPay(accounts, canPay) {
        const keep = pay.value;
        pay.replaceChildren(option("wallet", "Wallet"), ...accounts.map((a) => {
          const o = option(`bank:${a.bank}`, `${a.bank}: ${money(a)}${a.frozen ? " (frozen)" : ""}`);
          o.disabled = !canPay || Boolean(a.frozen);
          return o;
        }));
        pay.value = [...pay.options].some((o) => o.value === keep && !o.disabled) ? keep : "wallet";
      }

      function refreshAccount() {
        const n = ++st.accountSeq;
        return call("shop.account", {}).then((a) => {
          if (!a || n !== st.accountSeq) return;
          setNotice("");
          wallet.textContent = money(a.wallet || {});
          fillTiers(a.tiers || []);
          fillPay(a.accounts || [], Boolean(a.canPayBank));
          refreshQuote();
        });
      }

      function loadStores() {
        const n = ++st.storesSeq;
        return call("shop.stores", {}).then((r) => {
          if (!r || n !== st.storesSeq) return;
          setNotice("");
          st.stores = r.stores || [];
          renderStores();
        });
      }

      function loadList(page) {
        const n = ++st.listSeq;
        const q = search.value.trim();
        const args = { q, sort: sort.value, showLocked: lockedBox.checked };
        if (st.store) args.shop = st.store;
        if (st.category) args.category = st.category;
        if (page) args.page = page;
        return call("shop.list", args).then((r) => {
          if (!r || n !== st.listSeq) return;
          setNotice("");
          st.q = q;
          st.total = r.total || 0;
          st.cats = r.categories || [];
          st.items = r.items || [];
          st.page = page || 0;
          renderCats();
          renderProducts();
        });
      }

      function dismiss(n) {
        return call("shop.dismiss", { n }).then(refreshOrders);
      }

      function renderOrders() {
        orders.replaceChildren(st.orders.length ? orderTable(st.orders, dismiss) : el("p", "shop-empty", "No orders yet."));
      }

      function refreshOrders() {
        const n = ++st.ordersSeq;
        return call("shop.orders", {}).then((r) => {
          if (!r || n !== st.ordersSeq) return;
          setNotice("");
          st.orders = r.orders || [];
          renderOrders();
        });
      }

      function buy() {
        setNotice("");
        st.pending = true;
        renderTotals();
        st.cid = st.cid || token();
        return call("shop.buy", { lines: linesArg(), tier: delivery.value, pay: pay.value, via: via.value, cid: st.cid }).then((r) => {
          st.pending = false;
          if (!r || !r.order || !r.order.name) {
            if (r) setNotice(BAD_REPLY);
            renderTotals();
            const msg = notice.textContent;
            Promise.all([refreshAccount(), refreshOrders()]).then(() => setNotice(msg));
            return;
          }
          st.cart = [];
          st.cid = null;
          orderName.textContent = r.order.name;
          sheetNote.textContent = (r.order.via === "mail" ? "Your order will come in your mailbox as a cardboard box." : "Your parcel will arrive at your parcel locker.") + (r.order.arrives ? " Expected " + r.order.arrives + "." : "");
          sheet.hidden = false;
          done.focus();
          renderLines();
          refreshQuote();
          refreshAccount();
          loadList();
          refreshOrders();
        });
      }

      function runSearch() {
        if (st.view === VIEW_PRODUCTS) loadList();
        else if (search.value.trim()) pick(VIEW_PRODUCTS, "", "");
      }

      place.addEventListener("click", buy);
      search.addEventListener("input", () => {
        clearTimeout(timer);
        timer = setTimeout(runSearch, DEBOUNCE_MS);
      });
      search.addEventListener("keydown", (e) => {
        if (e.key !== "Enter") return;
        clearTimeout(timer);
        runSearch();
      });
      clear.addEventListener("click", () => {
        search.value = "";
        clearTimeout(timer);
        runSearch();
        search.focus();
      });
      go.addEventListener("click", () => {
        clearTimeout(timer);
        runSearch();
      });
      brand.addEventListener("click", () => pick(VIEW_STORES, "", ""));
      allStores.addEventListener("click", () => pick(VIEW_STORES, "", ""));
      orderLink.addEventListener("click", () => pick(VIEW_ORDER, st.store, st.category));
      historyLink.addEventListener("click", () => pick(VIEW_ORDERS, st.store, st.category));
      footCart.addEventListener("click", () => pick(VIEW_ORDER, st.store, st.category));
      footHistory.addEventListener("click", () => pick(VIEW_ORDERS, st.store, st.category));
      sort.addEventListener("change", () => loadList());
      lockedBox.addEventListener("change", () => loadList());
      delivery.addEventListener("change", refreshQuote);
      via.addEventListener("change", refreshQuote);
      pay.addEventListener("change", refreshQuote);

      renderCats();
      renderMain();
      renderLines();
      refreshQuote();
      refreshAccount();
      loadStores();
      loadList();
      const off = Apps.on("shop", (m) => {
        if (!root.isConnected) off();
        else if (m.name === "changed") Promise.all([refreshAccount(), refreshOrders()]);
      });
      return { onOpen: () => Promise.all([refreshAccount(), refreshOrders()]) };
    },
  });
})();
