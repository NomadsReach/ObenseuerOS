(function () {
  "use strict";
  const { el, ico, button, field, num } = Apps;
  const ACCENT = { "o-market": "green", "skeida-pharmacy": "teal", books: "blue", goldshop: "gold", bakery: "rose", "mines-market": "slate" };
  function money(m) {
    const parts = [];
    if (m.oc) parts.push(Apps.money(m.oc, "OC"));
    if (m.rm) parts.push(Apps.money(m.rm, "RM"));
    return parts.join(" + ") || Apps.money(0, "OC");
  }

  function plural(n, one, many) {
    return `${num(n, 0)} ${n === 1 ? one : many}`;
  }

  function accent(id) {
    return `shop-accent-${ACCENT[id] || "brand"}`;
  }

  function crate() {
    return el("span", "shop-crate", "No image");
  }

  function picture(url) {
    const box = el("span", "shop-pic");
    if (!url) {
      box.append(crate());
      return box;
    }
    const img = el("img");
    img.alt = "";
    img.src = url;
    img.addEventListener("error", () => img.replaceWith(crate()), { once: true });
    box.append(img);
    return box;
  }

  function qtyInput(max) {
    const q = field("number", "shop-input shop-qtyfield", "Quantity");
    q.min = "1";
    q.max = String(Math.max(1, max || 1));
    q.value = "1";
    return q;
  }

  function readQty(input, max) {
    const n = Math.floor(Number(input.value));
    const q = Math.min(Math.max(1, max || 1), Math.max(1, n || 1));
    input.value = String(q);
    return q;
  }

  function card(item, on) {
    const locked = Boolean(item.locked);
    const c = el("article", locked ? "shop-card locked" : "shop-card");
    const media = button("shop-card-media", "");
    media.setAttribute("aria-label", item.title);
    media.append(picture(item.icon));
    media.addEventListener("click", () => on.open(item.id));
    const info = el("div", "shop-card-info");
    const title = button("shop-card-title", item.title);
    title.addEventListener("click", () => on.open(item.id));
    info.append(title, el("span", "shop-card-store", item.shop || ""), el("p", "shop-price", money({ oc: item.priceOc, rm: item.priceRm })));
    if (locked) info.append(el("span", "shop-badge locked", "Locked"), el("p", "shop-hint", item.hint || ""));
    else {
      const add = button("shop-add", "Add to cart");
      add.addEventListener("click", () => on.add(item, 1));
      info.append(el("span", "shop-badge", "In stock"), add);
    }
    c.append(media, info);
    return c;
  }

  function detail(d, on) {
    const wrap = el("article", d.locked ? "shop-item locked" : "shop-item");
    const info = el("div", "shop-item-info");
    info.append(
      el("span", "shop-card-store", [d.shop, d.area, d.category].filter(Boolean).join(" · ")),
      el("h3", "shop-item-title", d.title),
      el("p", "shop-price big", money({ oc: d.priceOc, rm: d.priceRm }))
    );
    if (d.description) info.append(el("p", "shop-desc", d.description));
    if (d.locked) info.append(el("span", "shop-badge locked", "Locked"), el("p", "shop-hint", d.hint || ""));
    else {
      const qty = qtyInput(d.maxQty);
      const add = button("shop-add big", "Add to cart");
      add.addEventListener("click", () => on.add(d, readQty(qty, d.maxQty)));
      qty.addEventListener("keydown", (e) => {
        if (e.key !== "Enter") return;
        e.preventDefault();
        add.click();
      });
      const buy = el("div", "shop-buy");
      buy.append(el("span", "shop-badge", "In stock"), el("span", "shop-meta", `Up to ${num(d.maxQty, 0)} per order`), qty, add);
      info.append(buy);
    }
    wrap.append(picture(d.icon), info);
    return wrap;
  }

  function storeTile(s, open) {
    const b = button(`shop-store ${accent(s.id)}`, "");
    b.dataset.key = `store:${s.id}`;
    const body = el("span", "shop-store-body");
    body.append(el("span", "shop-store-name", s.name), el("span", "shop-meta", s.area), el("span", "shop-meta", plural(s.count, "product", "products")));
    b.append(picture(s.icon), body);
    b.addEventListener("click", open);
    return b;
  }

  function cell(cls, ...kids) {
    const td = el("td", cls);
    td.append(...kids);
    return td;
  }

  function line(l, on) {
    const row = el("tr", "shop-line");
    const less = button("shop-step", "−");
    less.setAttribute("aria-label", "Less");
    less.dataset.key = `${l.id}|-`;
    const plus = button("shop-step", "+");
    plus.setAttribute("aria-label", "More");
    plus.dataset.key = `${l.id}|+`;
    const remove = button("shop-remove", "Remove");
    remove.dataset.key = `${l.id}|x`;
    less.addEventListener("click", () => on.change(l, -1));
    plus.addEventListener("click", () => on.change(l, 1));
    remove.addEventListener("click", () => on.remove(l));
    const stepper = el("div", "shop-stepper");
    stepper.append(less, el("span", "shop-qty", num(l.qty, 0)), plus);
    row.append(
      cell("", picture(l.icon)),
      cell("shop-line-name", l.title),
      cell("", stepper),
      cell("shop-line-price", money({ oc: l.oc * l.qty, rm: l.rm * l.qty })),
      cell("", remove)
    );
    return row;
  }

  function orderRow(o, dismiss) {
    const arrived = String(o.state).toLowerCase() === "arrived";
    const action = el("td");
    if (arrived) {
      const b = button("shop-btn ghost sm", "Dismiss");
      b.addEventListener("click", () => dismiss(o.n));
      action.append(b);
    }
    const tr = el("tr");
    tr.append(
      el("td", "", o.name),
      el("td", "", o.items),
      el("td", "", money(o.paid)),
      el("td", "", arrived ? "Arrived" : o.via === "mail" ? "In the mail" : "In the post"),
      el("td", "", o.arrives || "-"),
      action
    );
    return tr;
  }

  function orderTable(list, dismiss) {
    const head = el("tr");
    head.append(...["Order", "Items", "Paid", "State", "Arrives", ""].map((h) => el("th", "", h)));
    const thead = el("thead");
    thead.append(head);
    const tbody = el("tbody");
    tbody.append(...list.map((o) => orderRow(o, dismiss)));
    const table = el("table", "shop-table");
    table.append(thead, tbody);
    return table;
  }

  function hero(browse) {
    const h = el("section", "shop-hero");
    const go = button("shop-btn", "Browse all products");
    go.addEventListener("click", browse);
    h.append(
      el("h2", "shop-hero-title", "Everything you need, delivered to your locker"),
      el("p", "shop-hero-sub", "Pick a store, add what you need and choose how the parcel travels."),
      go
    );
    return h;
  }

  window.ShopView = { money, plural, accent, card, detail, storeTile, line, orderTable, hero };
})();
