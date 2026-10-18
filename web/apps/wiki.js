(function () {
  "use strict";
  const { el, button, num } = Apps;

  const HOST = "wiki.stalburg.ts";
  const LIVE = "https://stalburg.net/";
  const MAIN = "Main_Page";
  const TITLE = "Stalburg Wiki";
  const TAGLINE = "The community encyclopedia of Stolland and Obenseuer";
  const NOT_CONNECTED = "The wiki service is not connected.";
  const LOADING = "Loading...";
  const NO_RESULTS = "No results.";
  const FAILED = "The wiki service failed.";
  const UNREACHABLE = "The wiki can't be reached right now.";
  const UNREACHABLE_HERE = "The wiki can't be reached from here.";
  const ELSEWHERE = "That link goes to another website, which can't be opened here.";
  const CONSENT = "Pages are shown straight from stalburg.net, the wiki's own server. Your computer connects to it like a web browser would. Nothing about you, your save or your account is sent.";
  const KEPT = " Pages you open can be kept on this computer to read offline.";
  const LICENCE = "Text from the Stalburg Wiki, stalburg.net, is available under CC BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/";
  const NOT_AFFILIATED = "Not affiliated with or endorsed by the wiki. Images are not licensed with the text.";
  const OFF = "Online pages are switched off in the mod settings.";
  const DECLINED = "Online pages are off. Allow them to read the wiki.";
  const ONLINE = "Online";
  const SAVED_STATUS = "Saved copies only";
  const DISCONNECTED = "Not connected";
  const CANCELLED = 14;
  const BULK_MS = 3000;
  const STATES = { idle: "Not running", indexing: "Getting the page list", running: "Saving", paused: "Paused", done: "Finished", stopped: "Stopped" };
  const MESSAGES = new Map([
    ["unknown_method", "The wiki service isn't installed."],
    ["busy", "The wiki service is busy. Try again in a moment."],
    ["denied", "Allow online pages to read this."],
    ["too_large", "The wiki sent too much data to show."],
    ["offline", UNREACHABLE],
    ["not_found", "There is no wiki page with this name."],
    ["blocked", "This page can't be opened here."],
    ["rate_limited", "The wiki asked for a pause."],
    ["bad_response", "The wiki sent a reply this site can't read."],
    ["timeout", "The wiki didn't answer in time."],
    ["failed", "The page could not be saved."],
  ]);

  function liveUrl(t) {
    return LIVE + encodeURIComponent(String(t).replace(/ /g, "_")).replace(/%2F/g, "/").replace(/%3A/g, ":");
  }

  function titleOf(path) {
    let t = String(path || "").replace(/^\//, "");
    try {
      t = decodeURIComponent(t);
    } catch {}
    return t.replace(/ /g, "_") || MAIN;
  }

  function route(t) {
    const v = String(t).replace(/ /g, "_");
    return v === MAIN ? HOST : HOST + "/" + v.replace(/[?#%]/g, encodeURIComponent);
  }

  function count(v) {
    return num(Number.isFinite(v) ? v : 0, 0);
  }

  function megabytes(bytes) {
    return num(Number.isFinite(bytes) ? bytes / 1048576 : 0, 1) + " MB";
  }

  function textOf(e) {
    if (e && (e.error === "rate_limited" || e.error === "blocked") && e.message) return e.message;
    return (e && MESSAGES.get(e.error)) || FAILED;
  }
  const deniedView = (consented) => consented ? OFF : "consent";
  window.WikiLivePolicy = { deniedView, textOf };

  function snippet(text) {
    const p = el("p", "wk-snippet");
    let on = false;
    for (const part of String(text || "").split(/([\u0001\u0002])/)) {
      if (part === "\u0001" || part === "\u0002") on = part === "\u0001";
      else if (part) p.append(on ? el("span", "wk-hit-mark", part) : part);
    }
    return p;
  }

  function credit() {
    return [el("p", "", LICENCE), el("p", "", NOT_AFFILIATED)];
  }

  Apps.register("wiki", {
    mount(slot, ctx, opts) {
      const o = opts || {};
      const root = el("div", "wk");
      slot.append(root);
      if (!ctx.live) {
        root.append(el("p", "wk-note", NOT_CONNECTED));
        return;
      }
      const alive = () => root.isConnected;
      const net = WikiNet.create(ctx, alive);
      const start = titleOf(o.path);
      let token = 0;
      let consented = false;
      let busy = false;
      let allowed = false;
      let online = false;
      let saving = false;
      let current = null;

      const util = el("div", "wk-util");
      const status = el("span", "wk-status", SAVED_STATUS);
      util.append(el("span", "", TAGLINE), status);

      const input = el("input", "wk-search");
      input.type = "search";
      input.placeholder = "Search the wiki";
      input.setAttribute("aria-label", "Search the wiki");
      const find = button("wk-go", "Search");
      const pageList = button("wk-go", "Page list");
      const bar = el("div", "wk-searchbox");
      bar.append(input, find, pageList);

      const banner = el("p", "wk-banner");
      banner.setAttribute("role", "status");
      banner.hidden = true;

      const accept = button("wk-btn primary", "Allow online pages");
      const later = button("wk-btn", "Not now");
      const actions = el("div", "wk-actions");
      actions.append(accept, later);
      const card = el("section", "wk-card");
      card.hidden = true;
      const consentText = el("p", "", CONSENT);
      card.append(el("h2", "wk-card-title", "Read the wiki online?"), consentText, actions);

      const note = el("p", "wk-note");
      note.hidden = true;
      const found = el("div", "wk-found");
      found.hidden = true;
      const frame = el("iframe", "wk-frame");
      frame.setAttribute("sandbox", "");
      frame.title = TITLE;
      frame.hidden = true;
      const foot = el("footer", "wk-foot");
      foot.hidden = true;

      const bulk = el("section", "wk-bulk");
      bulk.hidden = true;
      const bulkInfo = el("p", "wk-meta");
      const bulkMsg = el("p", "wk-note");
      const meter = el("progress", "wk-meter");
      meter.hidden = true;
      const withImages = el("input");
      withImages.type = "checkbox";
      withImages.checked = true;
      const imgLabel = el("label", "wk-check");
      imgLabel.append(withImages, " Include pictures");
      const save = button("wk-btn sm", "Save the whole wiki on this computer");
      const pause = button("wk-btn sm", "Pause");
      const resume = button("wk-btn sm", "Resume");
      const cancel = button("wk-btn sm", "Cancel");
      const clear = button("wk-btn sm", "Clear saved pages");
      const bulkActions = el("div", "wk-actions");
      bulkActions.append(save, pause, resume, cancel, clear);
      bulk.append(el("h2", "wk-card-title", "Offline copy"), bulkInfo, meter, bulkMsg, imgLabel, bulkActions);

      root.append(util, bar, banner, card, note, found, frame, foot, bulk);

      function setBanner(text) {
        banner.textContent = text;
        banner.hidden = !text;
      }

      function showOnly(node) {
        for (const n of [note, found, frame]) n.hidden = n !== node;
      }

      function go(url) {
        if (o.visit) o.visit(url);
      }

      function present(src) {
        frame.src = src;
        showOnly(frame);
        foot.replaceChildren(...credit());
        foot.hidden = false;
      }

      function showLive(t) {
        current = t;
        present(liveUrl(t));
        if (saving) net.page(t).catch(() => {});
      }

      function fail(e) {
        const denied = e && e.error === "denied";
        if (denied && deniedView(consented) === "consent") card.hidden = false;
        foot.hidden = true;
        note.textContent = denied && consented ? deniedView(consented) : textOf(e);
        showOnly(note);
      }

      function fallback() {
        foot.hidden = true;
        note.textContent = UNREACHABLE;
        showOnly(note);
      }

      function open(t) {
        const id = ++token;
        foot.hidden = true;
        setBanner("");
        note.textContent = LOADING;
        showOnly(note);
        refresh().then((err) => {
          if (id !== token || !alive()) return;
          if (err) {
            note.textContent = err;
            showOnly(note);
          } else if (allowed) showLive(t);
          else if (!consented) showOnly(null);
          else {
            note.textContent = online ? UNREACHABLE_HERE : OFF;
            showOnly(note);
          }
        });
      }

      function report(m) {
        const url = typeof m.url === "string" ? m.url : "";
        if (m.ok === true) {
          if (url.startsWith(LIVE)) {
            current = titleOf(url.slice(LIVE.length));
            setBanner("");
          }
        } else if (m.status === CANCELLED) {
          if (Boolean(url && !url.startsWith(LIVE))) setBanner(ELSEWHERE);
        } else if (current && (url === "" || url === liveUrl(current) || (url.length >= 256 && liveUrl(current).startsWith(url)))) {
          fallback();
        } else if (url.startsWith(LIVE)) {
          fallback();
        }
      }

      function hit(item) {
        const li = el("li", "wk-hit");
        const link = button("wk-hit-title", String(item.title).replace(/_/g, " "));
        link.addEventListener("click", () => go(route(item.title)));
        li.append(link, snippet(item.snippet));
        if (Number.isFinite(item.words) && item.words > 0) li.append(el("p", "wk-meta", count(item.words) + " words"));
        return li;
      }

      function entry(t) {
        const li = el("li", "wk-hit");
        const link = button("wk-hit-title", String(t).replace(/_/g, " "));
        link.addEventListener("click", () => go(route(t)));
        li.append(link);
        return li;
      }

      function showResults(q, r) {
        const nodes = [el("p", "wk-meta", count(r.total) + " results")];
        if (r.items.length) {
          const list = el("ul", "wk-hits");
          list.append(...r.items.map(hit));
          nodes.push(list);
        } else nodes.push(el("p", "wk-note", NO_RESULTS));
        if (Number.isFinite(r.next)) {
          const more = button("wk-btn sm", "More results");
          more.addEventListener("click", () => search(q, r.next));
          nodes.push(more);
        }
        found.replaceChildren(...nodes);
        showOnly(found);
      }

      function search(q, offset) {
        const id = ++token;
        foot.hidden = true;
        setBanner("");
        found.replaceChildren(el("p", "wk-note", LOADING));
        showOnly(found);
        net.search(q, offset).then(
          (r) => {
            if (id === token && alive()) showResults(q, r);
          },
          (e) => {
            if (id === token && alive()) fail(e);
          }
        );
      }

      function listTitles(offset) {
        const id = ++token;
        foot.hidden = true;
        setBanner("");
        found.replaceChildren(el("p", "wk-note", LOADING));
        showOnly(found);
        net.titles(offset).then(
          (r) => {
            if (id !== token || !alive()) return;
            const next = (offset || 0) + r.items.length;
            const nodes = [el("p", "wk-meta", count(r.total) + " page titles.")];
            if (r.items.length) {
              const items = el("ul", "wk-hits");
              items.append(...r.items.map(entry));
              nodes.push(items);
            } else nodes.push(el("p", "wk-note", NO_RESULTS));
            if (r.items.length && next < r.total) {
              const more = button("wk-btn sm", "More titles");
              more.addEventListener("click", () => listTitles(next));
              nodes.push(more);
            }
            found.replaceChildren(...nodes);
            showOnly(found);
          },
          (e) => {
            if (id === token && alive()) fail(e);
          }
        );
      }

      function runSearch() {
        const q = input.value.trim();
        if (q) search(q, 0);
      }

      function paintBulk(s) {
        const b = s.bulk || {};
        const state = b.state || "idle";
        const running = state === "running" || state === "indexing";
        const paused = state === "paused";
        busy = running;
        bulkInfo.textContent = count(s.cachePages) + " pages, " + megabytes(s.cacheBytes) + " saved on this computer.";
        bulkMsg.textContent = (STATES[state] || state) + (b.total > 0 ? ": " + count(b.done) + " of " + count(b.total) + " pages, " + count(b.errors) + " errors." : ".") + (b.message ? " " + b.message : "");
        meter.hidden = !(b.total > 0);
        meter.max = Number.isFinite(b.total) && b.total > 0 ? b.total : 1;
        meter.value = Number.isFinite(b.done) ? b.done : 0;
        save.disabled = running || paused;
        pause.disabled = state !== "running";
        resume.disabled = !paused;
        cancel.disabled = !(running || paused);
      }

      function gate(s) {
        consented = s.consent === true;
        online = s.online === true;
        saving = s.saving === true;
        allowed = consented && online && s.live === true;
        card.hidden = consented;
        consentText.textContent = saving ? CONSENT + KEPT : CONSENT;
        bulk.hidden = saving !== true || (!consented && !(s.cachePages > 0));
        status.textContent = allowed ? ONLINE : saving ? SAVED_STATUS : DISCONNECTED;
        paintBulk(s);
      }

      function refresh() {
        return net.status().then(gate).catch((e) => {
          const t = textOf(e);
          setBanner(t);
          return t;
        });
      }

      function bulkAct(action) {
        net.bulk(action, withImages.checked).then(refresh, (e) => setBanner(textOf(e)));
      }

      find.addEventListener("click", runSearch);
      input.addEventListener("keydown", (e) => {
        if (e.key === "Enter") runSearch();
      });
      pageList.addEventListener("click", () => listTitles(0));
      accept.addEventListener("click", () => {
        net.consent(true).then(
          () => {
            card.hidden = true;
            open(start);
            refresh();
          },
          (e) => setBanner(textOf(e))
        );
      });
      later.addEventListener("click", () => {
        card.hidden = true;
        note.textContent = DECLINED;
        showOnly(note);
      });
      save.addEventListener("click", () => bulkAct("start"));
      pause.addEventListener("click", () => bulkAct("pause"));
      resume.addEventListener("click", () => bulkAct("resume"));
      cancel.addEventListener("click", () => bulkAct("cancel"));
      clear.addEventListener("click", () => {
        net.clear().then(refresh, (e) => setBanner(textOf(e)));
      });

      const timer = setInterval(() => {
        if (!alive()) {
          clearInterval(timer);
          return;
        }
        if (busy) refresh();
      }, BULK_MS);

      const offFrame = Apps.on("wiki", (m) => {
        if (!root.isConnected) offFrame();
        else if (m.type === "obx.frame" && !frame.hidden) report(m);
      });

      refresh();
      open(start);
    },
  });
})();
