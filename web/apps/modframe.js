(function () {
  "use strict";

  const CALLS_IN_FLIGHT = 16;
  const CALLS_PER_SEC = 30;
  const ARGS_MAX = 12 * 1024;
  const NOTES_PER_10S = 3;
  const URLS_PER_10S = 5;
  const NOTE_TITLE = 60;
  const NOTE_BODY = 200;
  const URL_MAX = 300;
  const MIN_W = 360;
  const MIN_H = 240;
  const frames = new Set();
  const subbed = new Set();
  const spent = new Map();
  let callSeq = 0;
  let hooks = {};

  function post(rec, msg) {
    const w = rec.frame.contentWindow;
    if (w) w.postMessage(Object.assign({ obxos: 1 }, msg), "*");
  }

  function plain(o) {
    const out = {};
    if (o && typeof o.path === "string") out.path = o.path;
    if (o && typeof o.query === "string") out.query = o.query;
    return out;
  }

  function themeOf() {
    const t = document.documentElement.dataset.theme;
    return t === "dark" || (t !== "light" && matchMedia("(prefers-color-scheme: dark)").matches) ? "dark" : "light";
  }

  function drop(rec) {
    frames.delete(rec);
    for (const id of rec.pending.values()) fail(rec, id, "denied", "The app was closed or removed");
    rec.pending.clear();
    rec.frame.src = "about:blank";
    if (rec.embedded) hooks.send({ v: 1, type: "closed", app: rec.appId });
  }

  function prune() {
    for (const rec of frames) if (!rec.frame.isConnected) drop(rec);
  }

  function allow(key, max) {
    const now = Date.now();
    const list = (spent.get(key) || []).filter((t) => now - t < 10000);
    spent.set(key, list);
    if (list.length >= max) return false;
    list.push(now);
    return true;
  }

  function recOf(win) {
    prune();
    if (!win) return null;
    for (const rec of frames) if (rec.frame.contentWindow === win) return rec;
    return null;
  }

  function answer(rec, id, out) {
    post(rec, Object.assign({ kind: "res", id }, out));
  }

  function fail(rec, id, error, message) {
    answer(rec, id, { ok: false, error, message });
  }

  function call(rec, m) {
    if (typeof m.id !== "number" && typeof m.id !== "string") return;
    const now = Date.now();
    rec.stamps = rec.stamps.filter((t) => now - t < 1000);
    if (rec.stamps.length >= CALLS_PER_SEC || rec.pending.size >= CALLS_IN_FLIGHT) return fail(rec, m.id, "rate_limited", "Too many calls");
    if (typeof m.method !== "string" || !m.method || m.method.length > 64) return fail(rec, m.id, "bad_args", "Method name missing or too long");
    const args = m.args === undefined ? {} : m.args;
    if (!args || typeof args !== "object" || Array.isArray(args)) return fail(rec, m.id, "bad_args", "Args must be a JSON object");
    let json;
    try {
      json = JSON.stringify(args);
    } catch {
      return fail(rec, m.id, "bad_args", "Args must be JSON data");
    }
    if (new TextEncoder().encode(json).length > ARGS_MAX) return fail(rec, m.id, "too_large", "Args are over 12 KB");
    rec.stamps.push(now);
    const key = ++callSeq;
    rec.pending.set(key, m.id);
    rec.ctx.call(m.method, args).then(
      (result) => {
        if (rec.pending.delete(key)) answer(rec, m.id, { ok: true, result });
      },
      (e) => {
        if (rec.pending.delete(key)) fail(rec, m.id, e && typeof e.error === "string" ? e.error : "failed", e && typeof e.message === "string" ? e.message : "");
      }
    );
  }

  function note(rec, m) {
    if (typeof m.title !== "string" || !m.title || !allow("notify:" + rec.mod, NOTES_PER_10S)) return;
    hooks.toast({ title: m.title.slice(0, NOTE_TITLE), body: typeof m.body === "string" ? m.body.slice(0, NOTE_BODY) : "", from: rec.title });
  }

  function title(id, text) {
    const w = hooks.win(id);
    if (w) w.el.querySelector(".window-title").textContent = text;
  }

  function size(id, w, h) {
    const win = hooks.win(id);
    if (!win || win.max) return;
    const desk = document.getElementById("desktop");
    win.r.w = Math.min(Math.max(Math.round(w), MIN_W), desk.offsetWidth);
    win.r.h = Math.min(Math.max(Math.round(h), MIN_H), desk.offsetHeight);
    hooks.place(win);
  }

  function handle(rec, m) {
    if (m.kind === "hello") post(rec, { kind: "init", app: rec.appId, theme: themeOf(), user: rec.ctx.info().user, opts: rec.opts });
    else if (m.kind === "call") call(rec, m);
    else if (m.kind === "title") {
      if (!rec.embedded && typeof m.text === "string" && m.text.length >= 1 && m.text.length <= 40) title(rec.appId, m.text);
    } else if (m.kind === "size") {
      if (!rec.embedded && Number.isFinite(m.w) && Number.isFinite(m.h)) size(rec.appId, m.w, m.h);
    } else if (m.kind === "close") rec.ctx.close();
    else if (m.kind === "notify") note(rec, m);
    else if (m.kind === "key" && m.key === "Escape") hooks.escape(rec.embedded ? "browser" : rec.appId);
    else if (m.kind === "url") {
      if (typeof m.url === "string" && m.url && m.url.length <= URL_MAX && allow("url:" + rec.mod, URLS_PER_10S)) rec.ctx.open("browser", { url: m.url });
    }
  }

  function mount(entry, slot, ctx, opts) {
    prune();
    const o = plain(opts);
    const node = document.createElement("iframe");
    node.src = entry.frame + (o.path ? "#" + encodeURI(o.path) : "");
    node.setAttribute("sandbox", "allow-scripts");
    node.title = entry.title;
    node.style.cssText = "display:block;width:100%;height:100%;border:0";
    const rec = { appId: entry.id, mod: entry.mod, url: entry.frame, title: entry.title, ctx, frame: node, opts: o, embedded: Boolean(ctx.embedded), pending: new Map(), stamps: [] };
    frames.add(rec);
    slot.append(node);
    if (rec.embedded) hooks.send({ v: 1, type: "open", app: entry.id });
    return { onOpen: (next) => post(rec, { kind: "open", opts: plain(next) }) };
  }

  function define(entry) {
    const size = entry.w && entry.h ? { w: entry.w, h: entry.h } : undefined;
    Apps.register(entry.id, { size, mount: (slot, ctx, opts) => mount(entry, slot, ctx, opts) });
    if (subbed.has(entry.id)) return;
    subbed.add(entry.id);
    Apps.on(entry.id, (m) => {
      prune();
      for (const rec of frames) if (rec.appId === entry.id) post(rec, { kind: "event", name: String(m.name), data: m.data });
    });
  }

  function icon(app) {
    const img = document.createElement("img");
    img.alt = "";
    img.style.width = "100%";
    img.style.height = "100%";
    img.src = app.iconUrl;
    img.addEventListener("error", () => img.replaceWith(Apps.el("span", "", [...app.title][0] || "")));
    return img;
  }

  function push(apps, sites) {
    for (const a of apps) {
      if (typeof a.frame === "string") define(a);
      if (Number.isInteger(a.badge)) hooks.badge(a.id, a.badge);
    }
    const ok = (sites || []).filter((s) => s && typeof s.id === "string" && typeof s.host === "string" && typeof s.title === "string" && typeof s.frame === "string");
    const live = new Map(apps.concat(ok).map((a) => [a.id, a]));
    for (const rec of frames) {
      const entry = live.get(rec.appId);
      if (!entry || entry.frame !== rec.url || entry.mod !== rec.mod) drop(rec);
    }
    for (const s of ok) define(s);
    BrowserSites.setSites(ok);
  }

  function init(h) {
    hooks = h;
    new MutationObserver(() => {
      prune();
      for (const rec of frames) post(rec, { kind: "theme", mode: themeOf() });
    }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
  }

  window.addEventListener("message", (e) => {
    const m = e.data;
    if (!m || typeof m !== "object" || m.obxos !== 1) return;
    const rec = recOf(e.source);
    if (rec) handle(rec, m);
  });

  window.ModFrame = { define, push, icon, prune, init };
})();
