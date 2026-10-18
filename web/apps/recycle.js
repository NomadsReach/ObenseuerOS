(function () {
  "use strict";
  const { el, button } = Apps;

  const PAGE = 50;
  const EMPTY = "The Recycle Bin is empty.";
  const PLACES = { "/docs": "Documents", "/downloads": "Downloads" };

  function nameOf(p) {
    return p.slice(p.lastIndexOf("/") + 1);
  }

  function placeOf(p) {
    const dir = p.slice(0, p.lastIndexOf("/"));
    return PLACES[dir] ? `Computer ▸ ${PLACES[dir]}` : dir;
  }

  function list(ctx) {
    const got = [];
    const next = () =>
      ctx.call("os.bin.list", { offset: got.length, limit: PAGE }).then((r) => {
        if (!r.items.length) return got;
        got.push(...r.items);
        return got.length < r.total ? next() : got;
      });
    return next();
  }

  Apps.register("recycle", {
    mount(slot, ctx) {
      const restore = button("obx-btn sm", "Restore");
      const purge = button("obx-btn sm", "Delete");
      const empty = button("obx-btn sm", "Empty Recycle Bin");
      const bar = el("div", "xp-bar");
      bar.append(el("div", "crumb", "Recycle Bin"), restore, purge, empty);

      const content = el("div", "xp-content");
      const question = el("span", "");
      const sure = button("obx-btn danger sm", "Delete");
      const keep = button("obx-btn sm", "Cancel");
      const confirm = el("div", "xp-confirm");
      confirm.append(question, sure, keep);
      confirm.hidden = true;
      const status = el("p", "xp-status");
      status.setAttribute("role", "status");
      const xp = el("div", "xp");
      xp.append(bar, content, confirm, status);
      slot.append(xp);

      let items = [];
      let picked = null;
      let doomed = null;
      let token = 0;

      function load(note) {
        const t = ++token;
        picked = null;
        hideConfirm();
        status.textContent = "Loading...";
        list(ctx).then(
          (got) => {
            if (t !== token) return;
            items = got;
            Apps.emit("recycle", { type: "bin", count: items.length });
            render();
            status.textContent = note || (items.length ? "" : EMPTY);
          },
          (e) => {
            if (t === token) status.textContent = ctx.live ? e.message : "The Recycle Bin is available in game";
          }
        );
      }

      function render() {
        const rows = items.map((it, i) => {
          const tr = el("tr");
          tr.tabIndex = 0;
          tr.dataset.i = i;
          tr.append(el("td", "", nameOf(it.origin)), el("td", "", placeOf(it.origin)), el("td", "", it.deleted || "Unknown"));
          return tr;
        });
        const head = el("tr");
        head.append(...["Name", "Original location", "Date deleted"].map((h) => el("th", "", h)));
        const thead = el("thead");
        thead.append(head);
        const tbody = el("tbody");
        tbody.append(...rows);
        const table = el("table", "xp-table");
        table.append(thead, tbody);
        content.replaceChildren(table);
        empty.disabled = !items.length;
        mark();
      }

      function mark() {
        for (const r of content.querySelectorAll("[data-i]")) r.classList.toggle("sel", items[Number(r.dataset.i)] === picked);
        restore.disabled = !picked;
        purge.disabled = !picked;
      }

      function select(it) {
        hideConfirm();
        picked = it;
        mark();
      }

      function hideConfirm() {
        doomed = null;
        confirm.hidden = true;
      }

      function ask(text, label, run) {
        doomed = run;
        question.textContent = text;
        sure.textContent = label;
        confirm.hidden = false;
        sure.focus();
      }

      async function restoreItem() {
        const it = picked;
        if (!it) return;
        try {
          const at = await ctx.call("os.bin.restore", { path: it.path });
          Apps.emit("files", { type: "changed" });
          load(`Restored to ${at}`);
        } catch (e) {
          status.textContent = `Restore failed: ${e.message}`;
        }
      }

      async function purgeItem(it) {
        try {
          await ctx.call("os.bin.purge", { path: it.path });
          load();
        } catch (e) {
          status.textContent = `Delete failed: ${e.message}`;
        }
      }

      async function emptyBin() {
        try {
          await ctx.call("os.bin.empty", {});
          load();
        } catch (e) {
          status.textContent = `Empty failed: ${e.message}`;
        }
      }

      function askPurge() {
        const it = picked;
        if (it) ask(`Permanently delete ${nameOf(it.origin)}?`, "Delete", () => purgeItem(it));
      }

      function askEmpty() {
        const n = items.length;
        if (n) ask(`Permanently delete all ${n} ${n === 1 ? "item" : "items"} in the Recycle Bin?`, "Empty Recycle Bin", emptyBin);
      }

      restore.addEventListener("click", restoreItem);
      purge.addEventListener("click", askPurge);
      empty.addEventListener("click", askEmpty);
      sure.addEventListener("click", () => {
        const run = doomed;
        hideConfirm();
        if (run) run();
      });
      keep.addEventListener("click", hideConfirm);
      confirm.addEventListener("keydown", (e) => {
        if (e.key !== "Escape") return;
        e.stopPropagation();
        hideConfirm();
      });
      content.addEventListener("click", (e) => {
        const row = e.target.closest("[data-i]");
        if (row) select(items[Number(row.dataset.i)]);
      });
      content.addEventListener("keydown", (e) => {
        if (e.key === "Delete") askPurge();
      });

      load();
      return { onOpen: () => load() };
    },
  });
})();
