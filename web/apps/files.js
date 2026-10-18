(function () {
  "use strict";
  const { el, ico, button, field, exists } = Apps;

  const PAGE = 50;
  const PLACES = [
    { id: "docs", title: "Documents", dir: "/docs" },
    { id: "pics", title: "Pictures" },
    { id: "downloads", title: "Downloads", dir: "/downloads" },
  ];

  function size(n) {
    return n < 1024 ? `${n} bytes` : `${Math.round(n / 1024)} KB`;
  }

  function nameOf(p) {
    return p.slice(p.lastIndexOf("/") + 1);
  }

  function info(it) {
    return `JPEG image, ${it.w} x ${it.h}, ${size(it.bytes)}`;
  }

  function iconBtn(cls, label, icon) {
    const b = button(cls);
    b.setAttribute("aria-label", label);
    b.title = label;
    b.append(ico(icon));
    return b;
  }

  function table(heads, rows) {
    const t = el("table", "xp-table");
    const thead = el("thead");
    const tr = el("tr");
    tr.append(...heads.map((h) => el("th", "", h)));
    thead.append(tr);
    const tbody = el("tbody");
    tbody.append(...rows);
    t.append(thead, tbody);
    return t;
  }

  function isMod(id) {
    return id.startsWith("/mod-");
  }

  function deletable(id) {
    return id === "docs" || isMod(id);
  }

  function folderButton(p) {
    const b = button("xp-folder", p.title);
    b.dataset.place = p.id;
    return b;
  }

  function listAll(ctx, method, args) {
    const got = [];
    const next = () =>
      ctx.call(method, { ...args, offset: got.length, limit: PAGE }).then((r) => {
        if (!r.items.length) return got;
        got.push(...r.items);
        return got.length < r.total ? next() : got;
      });
    return next();
  }

  Apps.register("files", {
    mount(slot, ctx) {
      const xp = el("div", "xp explorer");
      const back = iconBtn("icon-btn xp-round", "Back", "arrow-left");
      const fwd = iconBtn("icon-btn xp-round", "Forward", "arrow-right");
      const up = button("obx-btn ghost sm", "Up");
      const crumb = el("div", "crumb");
      const refresh = iconBtn("icon-btn", "Refresh", "refresh");
      const newBtn = button("obx-btn sm", "New text file");
      const delBtn = button("obx-btn sm", "Delete");
      const viewBtn = button("obx-btn sm", "Details");
      const search = field("search", "obx-input xp-search", "Search");
      search.placeholder = "Search";
      const bar = el("div", "xp-bar");
      bar.append(back, fwd, up, crumb, refresh, newBtn, delBtn, viewBtn, search);

      const side = el("nav", "xp-side");
      side.setAttribute("aria-label", "Folders");
      const nav = {};
      const sideList = el("ul");
      for (const p of PLACES) {
        nav[p.id] = button("xp-nav", p.title);
        nav[p.id].addEventListener("click", () => go(p.id));
      }
      const recycle = button("xp-nav", "Recycle Bin");
      recycle.addEventListener("click", () => ctx.open("recycle"));
      for (const b of [...PLACES.map((p) => nav[p.id]), recycle]) {
        const li = el("li");
        li.append(b);
        sideList.append(li);
      }
      side.append(sideList);

      const content = el("div", "xp-content");
      const sure = button("obx-btn danger sm", "Delete");
      const keep = button("obx-btn sm", "Cancel");
      const question = el("span", "");
      const confirm = el("div", "xp-confirm");
      confirm.append(question, sure, keep);
      confirm.hidden = true;
      const strip = el("div", "xp-strip");
      strip.hidden = true;
      const status = el("p", "xp-status");
      status.setAttribute("role", "status");
      const main = el("div", "xp-main");
      main.append(content, confirm, strip, status);
      const body = el("div", "xp-body");
      body.append(side, main);

      const lb = el("div", "lightbox");
      lb.hidden = true;
      lb.tabIndex = -1;
      lb.setAttribute("role", "dialog");
      lb.setAttribute("aria-label", "Picture viewer");
      const count = el("span", "lb-count");
      const shut = iconBtn("icon-btn lb-btn", "Close picture", "x");
      const lbBar = el("div", "lb-bar");
      lbBar.append(count, shut);
      const prevBtn = iconBtn("icon-btn lb-btn", "Previous picture", "arrow-left");
      const nextBtn = iconBtn("icon-btn lb-btn", "Next picture", "arrow-right");
      const pic = el("img", "lb-img");
      const stage = el("div", "lb-stage");
      stage.append(prevBtn, pic, nextBtn);
      const cap = el("p", "lb-caption");
      lb.append(lbBar, stage, cap);
      xp.append(bar, body, lb);
      slot.append(xp);

      const hist = ["root"];
      let at = 0;
      let here = "root";
      let items = [];
      let shown = [];
      let picked = null;
      let doomed = null;
      let details = false;
      let filter = "";
      let token = 0;
      let lbAt = -1;
      let opener = null;

      function go(id) {
        if (id !== hist[at]) {
          hist.splice(at + 1);
          hist.push(id);
          at = hist.length - 1;
        }
        show(id);
      }

      function walk(d) {
        at += d;
        show(hist[at]);
      }

      function show(id) {
        here = id;
        const t = ++token;
        picked = null;
        items = [];
        filter = "";
        search.value = "";
        strip.hidden = true;
        hideConfirm();
        status.textContent = "";
        content.replaceChildren();
        const place = PLACES.find((p) => p.id === id) || (isMod(id) ? { title: nameOf(id), dir: id } : null);
        crumb.textContent = place ? `Computer ▸ ${place.title}` : "Computer";
        back.disabled = at === 0;
        fwd.disabled = at >= hist.length - 1;
        up.hidden = id === "root";
        newBtn.hidden = id !== "docs" || !ctx.live;
        newBtn.disabled = true;
        delBtn.hidden = !deletable(id) || !ctx.live;
        viewBtn.hidden = id !== "pics";
        for (const p of PLACES) nav[p.id].classList.toggle("sel", p.id === id);
        if (id === "root") {
          content.append(...PLACES.map(folderButton));
          listAll(ctx, "os.fs.folders", {}).then(
            (list) => {
              if (t === token) content.append(...list.map((path) => folderButton({ id: path, title: nameOf(path) })));
            },
            () => {}
          );
          return;
        }
        if (id === "pics") {
          items = window.PICTURES.items;
          render();
          return;
        }
        status.textContent = "Loading...";
        listAll(ctx, "os.fs.list", { dir: place.dir }).then(
          (list) => {
            if (t !== token) return;
            items = list.map((f) => ({ name: nameOf(f.path), path: f.path, bytes: f.bytes }));
            newBtn.disabled = false;
            render();
            status.textContent = items.length ? "" : "This folder is empty.";
          },
          (e) => {
            if (t === token) status.textContent = ctx.live ? e.message : "Files are available in game";
          }
        );
      }

      function render() {
        if (here === "root") return;
        const q = filter;
        shown = items.filter((it) => it.name.toLowerCase().includes(q));
        if (here !== "pics") fileRows();
        else if (details) pictureRows();
        else pictureTiles();
        viewBtn.textContent = details ? "Large icons" : "Details";
        mark();
      }

      function pictureTiles() {
        const grid = el("div", "pic-grid");
        grid.append(...shown.map((it, i) => {
          const t = el("div", "pic-tile");
          t.tabIndex = 0;
          t.dataset.i = i;
          const img = el("img");
          img.setAttribute("loading", "lazy");
          img.setAttribute("decoding", "async");
          img.width = 200;
          img.height = 113;
          img.alt = "";
          img.src = `pictures/${it.thumb}`;
          t.append(img, el("span", "pic-name", it.name));
          return t;
        }));
        content.replaceChildren(grid);
      }

      function pictureRows() {
        const rows = shown.map((it, i) => {
          const tr = el("tr");
          tr.tabIndex = 0;
          tr.dataset.i = i;
          const img = el("img", "pic-thumb");
          img.setAttribute("loading", "lazy");
          img.alt = "";
          img.src = `pictures/${it.thumb}`;
          const first = el("td");
          first.append(img);
          tr.append(first, el("td", "", it.name), el("td", "", "JPEG image"), el("td", "", `${it.w} x ${it.h}`), el("td", "", size(it.bytes)));
          return tr;
        });
        content.replaceChildren(table(["", "Name", "Type", "Dimensions", "Size"], rows));
      }

      function fileRows() {
        const rows = shown.map((it, i) => {
          const tr = el("tr");
          tr.tabIndex = 0;
          tr.dataset.i = i;
          tr.append(el("td", "", it.name), el("td", "", "Text file"), el("td", "", size(it.bytes)));
          return tr;
        });
        content.replaceChildren(table(["Name", "Type", "Size"], rows));
      }

      function mark() {
        for (const r of content.querySelectorAll("[data-i]")) r.classList.toggle("sel", shown[Number(r.dataset.i)] === picked);
        delBtn.disabled = !picked;
      }

      function select(it) {
        hideConfirm();
        picked = it;
        mark();
        if (here !== "pics") return;
        strip.replaceChildren(el("p", "", `${it.name} · ${info(it)}`));
        strip.hidden = false;
      }

      function open(it) {
        if (here === "pics") openLb(items.indexOf(it));
        else ctx.open("notes", { path: it.path });
      }

      function hideConfirm() {
        doomed = null;
        confirm.hidden = true;
      }

      function askDelete() {
        if (!picked || !deletable(here)) return;
        doomed = picked;
        question.textContent = `Move ${doomed.name} to the Recycle Bin?`;
        confirm.hidden = false;
        sure.focus();
      }

      async function remove() {
        const it = doomed;
        hideConfirm();
        if (!it) return;
        try {
          await ctx.call("os.fs.delete", { path: it.path });
          Apps.emit("recycle", { type: "changed" });
          show(here);
        } catch (e) {
          status.textContent = `Delete failed: ${e.message}`;
        }
      }

      async function newFile() {
        const at = (n) => `/docs/new-${n}.txt`;
        newBtn.disabled = true;
        try {
          let n = 1;
          while (items.some((it) => it.name === `new-${n}.txt`) || (await exists(ctx, at(n)))) n++;
          await ctx.call("os.fs.write", { path: at(n), text: "" });
          show(here);
          ctx.open("notes", { path: at(n) });
        } catch (e) {
          newBtn.disabled = false;
          status.textContent = `New file failed: ${e.message}`;
        }
      }

      function openLb(i) {
        if (i < 0) return;
        opener = document.activeElement;
        lb.hidden = false;
        showLb(i);
        lb.focus();
      }

      function showLb(i) {
        lbAt = i;
        const it = items[i];
        pic.alt = it.name;
        pic.src = `pictures/${it.name}`;
        count.textContent = `${i + 1} of ${items.length}`;
        cap.replaceChildren(el("span", "", `${it.name} · ${info(it)}`));
        prevBtn.disabled = i === 0;
        nextBtn.disabled = i === items.length - 1;
        if (document.activeElement && document.activeElement.disabled) lb.focus();
        if (i + 1 < items.length) new Image().src = `pictures/${items[i + 1].name}`;
      }

      function lbStep(d) {
        const i = lbAt + d;
        if (i >= 0 && i < items.length) showLb(i);
      }

      function closeLb() {
        lb.hidden = true;
        pic.removeAttribute("src");
        if (opener) opener.focus();
        opener = null;
      }

      back.addEventListener("click", () => walk(-1));
      fwd.addEventListener("click", () => walk(1));
      up.addEventListener("click", () => go("root"));
      refresh.addEventListener("click", () => show(here));
      newBtn.addEventListener("click", newFile);
      delBtn.addEventListener("click", askDelete);
      viewBtn.addEventListener("click", () => {
        details = !details;
        render();
      });
      search.addEventListener("input", () => {
        filter = search.value.trim().toLowerCase();
        render();
      });
      sure.addEventListener("click", remove);
      keep.addEventListener("click", hideConfirm);
      confirm.addEventListener("keydown", (e) => {
        if (e.key !== "Escape") return;
        e.stopPropagation();
        hideConfirm();
      });
      content.addEventListener("click", (e) => {
        const folder = e.target.closest("[data-place]");
        if (folder) go(folder.dataset.place);
        const row = e.target.closest("[data-i]");
        if (row) select(shown[Number(row.dataset.i)]);
      });
      content.addEventListener("dblclick", (e) => {
        const row = e.target.closest("[data-i]");
        if (row) open(shown[Number(row.dataset.i)]);
      });
      content.addEventListener("keydown", (e) => {
        const row = e.target.closest("[data-i]");
        if (row && (e.key === "Enter" || e.key === " ")) {
          e.preventDefault();
          const it = shown[Number(row.dataset.i)];
          if (!it) return;
          select(it);
          if (e.key === "Enter") open(it);
        } else if (e.key === "Delete") {
          if (row) {
            const it = shown[Number(row.dataset.i)];
            if (!it) return;
            select(it);
          }
          askDelete();
        }
      });
      prevBtn.addEventListener("click", () => lbStep(-1));
      nextBtn.addEventListener("click", () => lbStep(1));
      shut.addEventListener("click", closeLb);
      lb.addEventListener("keydown", (e) => {
        const act = { Escape: closeLb, ArrowLeft: () => lbStep(-1), ArrowRight: () => lbStep(1) }[e.key];
        if (!act) return;
        e.preventDefault();
        e.stopPropagation();
        act();
      });
      const off = Apps.on("files", (m) => {
        if (!xp.isConnected) off();
        else if (m.type === "changed") show(here);
      });

      show("root");
    },
  });
})();
