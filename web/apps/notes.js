(function () {
  "use strict";
  const { el, button, field, exists, writeFile } = Apps;

  const PAGE = 50;
  const READ = 2000;
  const MAX_BYTES = 262144;

  function segOk(s, file) {
    if (!s || s[0] === " " || s[s.length - 1] === " " || s.includes("..") || !/^[A-Za-z0-9_ ().-]+$/.test(s)) return false;
    if (!file) return !s.includes(".");
    const dot = s.lastIndexOf(".");
    return dot > 0 && dot < s.length - 1;
  }

  function validPath(p) {
    if (!p || p.length > 128 || p[0] !== "/") return false;
    const parts = p.slice(1).split("/");
    return parts.length === 2 && segOk(parts[0], false) && segOk(parts[1], true);
  }

  function nameOf(p) {
    return p.slice(p.lastIndexOf("/") + 1);
  }

  function listDocs(ctx) {
    const got = [];
    const next = () =>
      ctx.call("os.fs.list", { dir: "/docs", offset: got.length, limit: PAGE }).then((r) => {
        if (!r.items.length) return got;
        got.push(...r.items);
        return got.length < r.total ? next() : got;
      });
    return next();
  }

  Apps.register("notes", {
    mount(slot, ctx, opts) {
      const win = slot.parentElement && slot.parentElement.querySelector(".window-title");
      const fileBtn = button("np-menu-btn", "File");
      const editBtn = button("np-menu-btn", "Edit");
      const bar = el("nav", "menu-bar");
      bar.append(fileBtn, editBtn);
      const menu = el("div", "np-menu");
      menu.hidden = true;
      const panel = el("div", "np-panel");
      panel.hidden = true;
      const text = el("textarea", "notes-text");
      text.setAttribute("aria-label", "Notepad text");
      const status = el("p", "np-status");
      status.setAttribute("role", "status");
      const pad = el("div", "notepad");
      pad.append(bar, menu, panel, text, status);
      slot.append(pad);

      let path = null;
      let saved = "";
      let armed = "";
      let wrap = true;
      let busy = false;

      const dirty = () => text.value !== saved;

      function setTitle() {
        if (win) win.textContent = `${dirty() ? "● " : ""}${path ? nameOf(path) : "Untitled"} - Notepad`;
      }

      function say(msg) {
        status.textContent = msg;
      }

      function closeMenu() {
        menu.hidden = true;
        menu.dataset.menu = "";
        fileBtn.setAttribute("aria-expanded", "false");
        editBtn.setAttribute("aria-expanded", "false");
      }

      function closePanel() {
        armed = "";
        panel.hidden = true;
        panel.replaceChildren();
      }

      function showPanel(nodes) {
        closeMenu();
        panel.replaceChildren(...nodes);
        panel.hidden = false;
      }

      function openMenu(name) {
        if (!menu.hidden && menu.dataset.menu === name) {
          closeMenu();
          return;
        }
        menu.dataset.menu = name;
        menu.replaceChildren(...entries(name).map(([label, run]) => {
          const b = button("np-item", label);
          b.addEventListener("click", () => {
            closeMenu();
            run();
          });
          return b;
        }));
        menu.hidden = false;
        fileBtn.setAttribute("aria-expanded", String(name === "File"));
        editBtn.setAttribute("aria-expanded", String(name === "Edit"));
      }

      function entries(name) {
        if (name === "File") return [["New", newDoc], ["Open...", openList], ["Save", save], ["Save as...", saveAs]];
        return [["Select all", selectAll], [wrap ? "✓ Word wrap" : "Word wrap", toggleWrap]];
      }

      function selectAll() {
        text.focus();
        text.select();
      }

      function toggleWrap() {
        wrap = !wrap;
        text.wrap = wrap ? "soft" : "off";
      }

      function guard() {
        if (!busy) return true;
        say("Wait for the save to finish.");
        return false;
      }

      function discard(label) {
        if (!dirty() || armed === label) {
          armed = "";
          return true;
        }
        armed = label;
        say(`Unsaved changes. Choose ${label} again to discard them.`);
        return false;
      }

      function newDoc() {
        if (!guard() || !discard("New")) return;
        text.value = "";
        saved = "";
        path = null;
        setTitle();
        say("");
      }

      async function load(p) {
        armed = "";
        busy = true;
        say("Opening...");
        try {
          const total = await ctx.call("os.fs.size", { path: p });
          let got = "";
          while (got.length < total) {
            const part = await ctx.call("os.fs.read", { path: p, offset: got.length, length: Math.min(READ, total - got.length) });
            if (!part) throw new Error("the file changed while opening");
            got += part;
          }
          text.value = got;
          saved = got;
          path = p;
          setTitle();
          say("");
          ctx.call("os.file.opened", { path: p }).catch(() => {});
        } catch (e) {
          say(`Not opened: ${e.message}`);
        } finally {
          busy = false;
        }
      }

      async function write(p) {
        const snap = text.value;
        if (new TextEncoder().encode(snap).length > MAX_BYTES) {
          say("Not saved: the file is over 256 KB. Nothing was written.");
          return;
        }
        armed = "";
        busy = true;
        say("Saving...");
        try {
          await writeFile(ctx, p, snap);
          path = p;
          saved = snap;
          setTitle();
          say("Saved to this computer");
        } catch (e) {
          say(`Not saved: ${e.message}. Nothing was written.`);
        } finally {
          busy = false;
        }
      }

      async function commitAs(file) {
        const p = `/docs/${file}`;
        if (!validPath(p)) {
          say("Name not allowed: use letters, digits, space, _ - . ( ), with an extension and no '..', up to 122 characters.");
          return;
        }
        if (!guard()) return;
        busy = true;
        let taken;
        try {
          taken = await exists(ctx, p);
        } catch (e) {
          busy = false;
          say(`Not saved: ${e.message}. Nothing was written.`);
          return;
        }
        busy = false;
        if (taken && armed !== "overwrite " + p) {
          armed = "overwrite " + p;
          say(`${p} already exists. Choose Save as again to overwrite it.`);
          return;
        }
        closePanel();
        await write(p);
      }

      function save() {
        if (!guard()) return;
        if (path) write(path);
        else saveAs();
      }

      function saveAs() {
        if (!guard()) return;
        const name = field("text", "obx-input np-name", "File name");
        name.placeholder = "name.txt";
        name.value = path ? nameOf(path) : "";
        const commit = () => commitAs(name.value.trim());
        const ok = button("obx-btn primary sm", "Save");
        const no = button("obx-btn ghost sm", "Cancel");
        ok.addEventListener("click", commit);
        name.addEventListener("keydown", (e) => {
          if (e.key === "Enter") commit();
        });
        no.addEventListener("click", closePanel);
        showPanel([el("span", "np-label", "Documents /docs/"), name, ok, no]);
        name.focus();
      }

      function openList() {
        if (!guard() || !discard("Open")) return;
        const list = el("div", "np-list");
        const no = button("obx-btn ghost sm", "Cancel");
        no.addEventListener("click", closePanel);
        showPanel([el("span", "np-label", "Open from Documents"), list, no]);
        listDocs(ctx).then(
          (files) => {
            if (!files.length) list.append(el("p", "", "No files in Documents."));
            for (const f of files) {
              const b = button("np-item", nameOf(f.path));
              b.addEventListener("click", () => {
                if (!guard()) return;
                closePanel();
                load(f.path);
              });
              list.append(b);
            }
          },
          (e) => list.replaceChildren(el("p", "", ctx.live ? e.message : "Files are available in game"))
        );
      }

      const onOpen = (o) => {
        if (o && o.path && guard() && discard("Open")) load(o.path);
      };

      fileBtn.addEventListener("click", () => openMenu("File"));
      editBtn.addEventListener("click", () => openMenu("Edit"));
      pad.addEventListener("click", (e) => {
        if (!e.target.closest(".menu-bar, .np-menu")) closeMenu();
      });
      pad.addEventListener("keydown", (e) => {
        if (e.key !== "Escape" || (menu.hidden && panel.hidden)) return;
        e.stopPropagation();
        closeMenu();
        closePanel();
        text.focus();
      });
      text.addEventListener("input", () => {
        armed = "";
        setTitle();
      });

      setTitle();
      onOpen(opts);
      return { onOpen, canClose: () => discard("Close") };
    },
  });
})();
