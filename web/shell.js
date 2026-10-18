(function () {
  "use strict";

  const { el } = Apps;
  const THEMES = ["light", "dark", "system"];
  const WALLPAPERS = ["game", "sewer", "plain"];
  const WALL_LABELS = { game: "Computer wallpaper", sewer: "Sewer", plain: "Plain" };
  const WALL_KEY = "obx.os.wallpaper";
  const DAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const DEFAULT_ICON = {
    browser: "world",
    files: "folder-filled",
    settings: "settings",
    mail: "mail",
    exchange: "chart-line",
    terminal: "terminal-2",
    notes: "notes",
    recycle: "trash",
    bank: "building-bank",
    news: "news",
    chat: "message-circle",
    shop: "shopping-cart",
    minesweeper: "device-gamepad-2",
    solitaire: "device-gamepad-2",
  };
  const DEMO_APPS = [
    { id: "mail", title: "Stalburg Mail" },
    { id: "browser", title: "Stalburg Web" },
    { id: "files", title: "Explorer" },
    { id: "recycle", title: "Recycle Bin" },
    { id: "settings", title: "Control Panel" },
    { id: "exchange", title: "Stal-Ex" },
    { id: "terminal", title: "Command Prompt" },
    { id: "notes", title: "Notepad" },
    { id: "bank", title: "Stalburg Bank" },
    { id: "news", title: "Stalburg Times" },
    { id: "chat", title: "TOR-Turnip Chat v0.3.9" },
    { id: "shop", title: "Shop" },
    { id: "minesweeper", title: "Minesweeper" },
    { id: "solitaire", title: "Solitaire" },
  ];
  const PINNED = ["files", "browser"];
  const TOASTS_MAX = 4;
  const TOAST_MS = 6000;
  const ALERTS_MAX = 20;
  const BOOT_MS = 900;
  const PREVIEW_TOAST_GAP_MS = 1500;
  const TINTS = 6;
  const BAR_H = 30;
  const KEEP = 48;
  const MIN_W = 360;
  const MIN_H = 240;
  const CASCADE = 32;
  const DESK_GAP = 24;
  const host = window.chrome && window.chrome.webview;
  const state = { apps: [], user: "Stalburg resident", focus: "", alerts: [], wall: "", gameRev: -1, art: [], clock: null, money: null, binFull: false };
  const wins = new Map();
  const badges = new Map();
  const opening = new Map();
  const cascade = { x: 0, y: 0 };
  let zTop = 10;
  const $ = (id) => document.getElementById(id);
  const desktop = $("desktop");
  const clamp = (v, lo, hi) => Math.min(Math.max(v, lo), hi);
  const scale = () => desktop.getBoundingClientRect().width / desktop.offsetWidth || 1;

  function send(msg) {
    if (host) host.postMessage(Object.assign({ v: 1 }, msg));
  }

  function appById(id) {
    return state.apps.find((a) => a.id === id);
  }

  function maskOf(name) {
    return `url(icons/${name}.svg) center/contain no-repeat`;
  }

  function uiIcon(name) {
    const s = el("span", "ico");
    s.style.mask = maskOf(name);
    return s;
  }

  function icons(root) {
    for (const n of root.querySelectorAll("[data-icon]")) n.style.mask = maskOf(n.dataset.icon);
  }

  function tintOf(id) {
    let h = 0;
    for (const c of id) h = (h * 31 + c.charCodeAt(0)) % TINTS;
    return h;
  }

  function iconOf(id) {
    return id === "recycle" && state.binFull ? "trash-filled" : DEFAULT_ICON[id];
  }

  function paintBin() {
    for (const n of document.querySelectorAll('[data-app="recycle"] .ico')) n.style.mask = maskOf(iconOf("recycle"));
  }

  function tile(app, size) {
    const t = el("span", `tile ${size}`);
    t.style.setProperty("--tint", `var(--obx-os-tint-${tintOf(app.id)})`);
    const name = iconOf(app.id) || (typeof app.icon === "string" && /^[a-z0-9-]{1,40}$/.test(app.icon) ? app.icon : "");
    if (name) t.append(uiIcon(name));
    else if (typeof app.iconUrl === "string") t.append(ModFrame.icon(app));
    else t.textContent = [...app.title][0] || "";
    return t;
  }

  function appButton(app, cls, size) {
    const b = el("button", cls);
    b.type = "button";
    b.dataset.app = app.id;
    b.dataset.name = app.title.toLowerCase();
    b.append(tile(app, size), el("span", "app-label", app.title));
    paintBadge(b);
    return b;
  }

  function paintBadge(b) {
    b.querySelector(".badge")?.remove();
    const n = badges.get(b.dataset.app);
    if (n) b.append(el("span", "badge", Apps.num(n, 0)));
  }

  function setBadge(id, n) {
    if (n) badges.set(id, n);
    else badges.delete(id);
    for (const b of [...$("desk").children, ...$("smGrid").children, ...$("taskList").children]) if (b.dataset.app === id) paintBadge(b);
  }

  function renderDesk() {
    $("desk").replaceChildren(...state.apps.filter((a) => a.desk !== false).map((a) => appButton(a, "desk-icon", "lg")));
  }

  function renderStart() {
    $("smGrid").replaceChildren(...state.apps.map((a) => appButton(a, "start-item", "lg")));
    ShellStart.filter($("smSearch").value);
  }

  function taskBtn(id) {
    return [...$("taskList").children].find((b) => b.dataset.app === id);
  }

  function taskButton(app) {
    const b = el("button", "task");
    b.type = "button";
    b.dataset.app = app.id;
    b.title = app.title;
    b.setAttribute("aria-label", app.title);
    b.append(tile(app, "xs"));
    paintBadge(b);
    return b;
  }

  function renderTaskbar() {
    const kept = $("taskList").contains(document.activeElement) ? document.activeElement.dataset.app : "";
    const ids = [...new Set([...PINNED, ...state.apps.filter((a) => a.pin === true).map((a) => a.id), ...wins.keys()])];
    $("taskList").replaceChildren(
      ...ids.map(appById).filter(Boolean).map((app) => {
        const w = wins.get(app.id);
        const b = taskButton(app);
        b.classList.toggle("open", Boolean(w));
        b.setAttribute("aria-pressed", String(Boolean(w) && w.id === state.focus));
        return b;
      })
    );
    taskBtn(kept)?.focus();
  }

  function renderAll() {
    renderDesk();
    renderStart();
    renderTaskbar();
  }

  function setApps(list, user, sites) {
    state.apps = [];
    for (const a of list) {
      if (a && typeof a.id === "string" && a.id && typeof a.title === "string" && !appById(a.id)) state.apps.push(a);
    }
    state.user = typeof user === "string" && user ? user : "Stalburg resident";
    $("smUser").textContent = state.user;
    ModFrame.push(state.apps, sites);
    for (const id of [...wins.keys()]) if (!appById(id)) closeApp(id);
    renderAll();
    probeBin();
  }

  function hostApi() {
    return {
      live: Boolean(host),
      send,
      open: openApp,
      close: closeApp,
      info: () => ({ user: state.user, apps: state.apps, clock: state.clock, money: state.money }),
    };
  }

  function probeBin() {
    Apps.mount("recycle", el("div"), hostApi(), {}, true);
  }

  function selectDesk(b) {
    for (const x of $("desk").children) x.classList.toggle("sel", x === b);
    if (b) b.focus();
  }

  function deskKey(e) {
    const items = [...$("desk").children];
    const i = items.indexOf(e.target);
    if (i < 0) return;
    if (e.key === "Enter") {
      openApp(items[i].dataset.app);
      return;
    }
    const rows = getComputedStyle($("desk")).gridTemplateRows.split(" ").length;
    const step = { ArrowUp: -1, ArrowDown: 1, ArrowLeft: -rows, ArrowRight: rows }[e.key];
    if (step && items[i + step]) {
      e.preventDefault();
      selectDesk(items[i + step]);
    }
  }

  function place(w) {
    const r = w.max ? { x: 0, y: 0, w: desktop.offsetWidth, h: desktop.offsetHeight } : w.r;
    Object.assign(w.el.style, { left: `${r.x}px`, top: `${r.y}px`, width: `${r.w}px`, height: `${r.h}px` });
    w.el.classList.toggle("max", w.max);
    w.el.querySelector('[data-act="max"]').setAttribute("aria-label", w.max ? "Restore" : "Maximise");
  }

  function fitWin(w) {
    if (!desktop.offsetWidth || !desktop.offsetHeight) return;
    if (!w.max) {
      w.r.w = Math.min(w.r.w, desktop.offsetWidth);
      w.r.h = Math.min(w.r.h, desktop.offsetHeight);
      w.r.x = clamp(w.r.x, KEEP - w.r.w, desktop.offsetWidth - KEEP);
      w.r.y = clamp(w.r.y, 0, desktop.offsetHeight - BAR_H);
    }
    place(w);
  }

  function focusWin(w) {
    state.focus = w.id;
    w.z = ++zTop;
    w.el.style.zIndex = w.z;
    for (const x of wins.values()) x.el.classList.toggle("focused", x === w);
    renderTaskbar();
  }

  function refocus() {
    const top = [...wins.values()].filter((w) => !w.min).sort((a, b) => b.z - a.z)[0];
    if (top) focusWin(top);
    else {
      state.focus = "";
      renderTaskbar();
    }
  }

  function minimise(w) {
    const had = w.el.contains(document.activeElement);
    w.min = true;
    w.el.hidden = true;
    if (state.focus === w.id) refocus();
    else renderTaskbar();
    if (had) taskBtn(w.id)?.focus();
  }

  function restore(w) {
    w.min = false;
    w.el.hidden = false;
    focusWin(w);
  }

  function toggleMax(w) {
    w.max = !w.max;
    place(w);
  }

  function closeApp(id) {
    const w = wins.get(id);
    if (!w) return;
    const had = w.el.contains(document.activeElement);
    w.el.remove();
    wins.delete(id);
    Apps.forget(id);
    if (state.focus === id) refocus();
    else renderTaskbar();
    if (had) (taskBtn(state.focus) || $("start")).focus();
    send({ type: "closed", app: id });
    ModFrame.prune();
  }

  function deskEdge() {
    return ($("desk").getBoundingClientRect().right - desktop.getBoundingClientRect().left) / scale();
  }

  function cascadeRect(sz) {
    const W = Math.min(sz ? sz.w : 600, desktop.offsetWidth - 2 * CASCADE);
    const H = Math.min(sz ? sz.h : 400, desktop.offsetHeight - 2 * CASCADE);
    const ox = deskEdge() + DESK_GAP;
    cascade.x += CASCADE;
    cascade.y += CASCADE;
    if (ox + cascade.x + W > desktop.offsetWidth || cascade.y + H > desktop.offsetHeight) cascade.x = cascade.y = 0;
    return { x: ox + cascade.x, y: cascade.y, w: W, h: H };
  }

  function track(e, move) {
    const node = e.currentTarget;
    const k = scale();
    const x0 = e.clientX;
    const y0 = e.clientY;
    node.setPointerCapture(e.pointerId);
    const on = (ev) => move((ev.clientX - x0) / k, (ev.clientY - y0) / k);
    node.addEventListener("pointermove", on);
    node.addEventListener("lostpointercapture", () => node.removeEventListener("pointermove", on), { once: true });
  }

  function frameFor(app, slot, opts) {
    if (Apps.mount(app.id, slot, hostApi(), opts)) return;
    if (app.id === "settings") slot.append(settingsFrame());
    else {
      const tpl = $(`frame-${app.id}`);
      slot.append(tpl ? tpl.content.cloneNode(true) : el("p", "frame-empty", "No content yet."));
    }
  }

  function settingsFrame() {
    const box = el("div", "settings");
    box.append(
      row("Theme", el("span", "", "Follows the framework's Appearance setting")),
      row("Wallpaper", wallSelect())
    );
    return box;
  }

  function wallSelect() {
    const s = el("select", "");
    const game = el("optgroup", "");
    game.label = "Game";
    game.append(wallOption("game", WALL_LABELS.game));
    for (const a of state.art) game.append(wallOption(`art:${a.id}`, a.name));
    s.append(game, wallOption("sewer", WALL_LABELS.sewer), wallOption("plain", WALL_LABELS.plain));
    s.value = state.wall;
    s.addEventListener("change", () => setWallpaper(s.value));
    return s;
  }

  function wallOption(value, label) {
    const op = el("option", "", label);
    op.value = value;
    return op;
  }

  function row(label, control) {
    const r = el("label", "set-row");
    r.append(el("span", "obx-label", label), control);
    return r;
  }

  function openWindow(app, opts) {
    const node = $("winTpl").content.firstElementChild.cloneNode(true);
    const w = { id: app.id, app, el: node, r: cascadeRect(Apps.size(app.id)), max: false, min: false, z: 0 };
    const bar = node.querySelector(".window-bar");
    node.setAttribute("aria-label", app.title);
    node.dataset.app = app.id;
    node.querySelector(".win-icon").append(tile(app, "xs"));
    node.querySelector(".window-title").textContent = app.title;
    frameFor(app, node.querySelector(".frame-slot"), opts);
    icons(node);
    node.addEventListener("pointerdown", () => focusWin(w));
    node.addEventListener("click", (e) => {
      const act = e.target.closest("[data-act]")?.dataset.act;
      if (act === "min") minimise(w);
      else if (act === "max") toggleMax(w);
      else if (act === "close" && Apps.canClose(w.id)) closeApp(w.id);
    });
    bar.addEventListener("pointerdown", (e) => {
      if (e.button || e.target.closest("button") || w.max) return;
      const r0 = { ...w.r };
      track(e, (dx, dy) => {
        w.r.x = clamp(r0.x + dx, KEEP - r0.w, desktop.offsetWidth - KEEP);
        w.r.y = clamp(r0.y + dy, 0, desktop.offsetHeight - BAR_H);
        place(w);
      });
    });
    bar.addEventListener("dblclick", (e) => {
      if (!e.target.closest("button")) toggleMax(w);
    });
    node.querySelector(".grip").addEventListener("pointerdown", (e) => {
      if (e.button || w.max) return;
      const r0 = { ...w.r };
      track(e, (dx, dy) => {
        w.r.w = Math.max(MIN_W, r0.w + dx);
        w.r.h = Math.max(MIN_H, r0.h + dy);
        place(w);
      });
    });
    node.classList.add("enter");
    node.addEventListener("animationend", () => node.classList.remove("enter"), { once: true });
    place(w);
    desktop.append(node);
    wins.set(app.id, w);
    focusWin(w);
  }

  function openApp(id, opts) {
    const lag = window.VirusFx ? window.VirusFx.lag() : 0;
    if (!lag) return openNow(id, opts);
    if (!opening.has(id)) setTimeout(() => {
      const latest = opening.get(id);
      opening.delete(id);
      document.body.classList.toggle("busy", opening.size > 0);
      openNow(id, latest);
    }, lag);
    opening.set(id, opts);
    document.body.classList.add("busy");
  }

  function openNow(id, opts) {
    if (!appById(id)) return;
    send({ type: "open", app: id });
    ShellStart.set(false);
    if (appById(id)?.launch === true) return;
    const w = wins.get(id);
    if (w) {
      restore(w);
      Apps.reopen(id, opts);
    } else openWindow(appById(id), opts);
  }

  function artOf(name) {
    return state.art.find((a) => `art:${a.id}` === name) || null;
  }

  function applyWallpaper() {
    const art = artOf(state.wall);
    let src = "";
    if (state.wall === "game" && state.gameRev >= 0) src = `wallpapers/game.jpg?r=${state.gameRev}`;
    else if (art) src = `wallpapers/game/${art.id}.png?r=${art.rev}`;
    desktop.style.backgroundImage = src ? `url(${src})` : "";
    desktop.style.backgroundSize = src ? "cover" : "";
    desktop.style.backgroundPosition = src ? "center" : "";
  }

  function setGame(rev) {
    state.gameRev = rev;
    applyWallpaper();
  }

  function setArt(items) {
    state.art = items.filter((a) => a && typeof a.id === "string" && /^[a-z0-9-]{1,32}$/.test(a.id) && typeof a.name === "string" && Number.isInteger(a.rev) && a.rev >= 0);
    renderArt();
    applyWallpaper();
  }

  function renderArt() {
    const box = $("ctxArt");
    box.textContent = "";
    for (const a of state.art) {
      const b = el("button", "ctx-item", a.name);
      b.type = "button";
      b.dataset.wall = `art:${a.id}`;
      box.append(b);
    }
  }

  function setWallpaper(name) {
    const known = WALLPAPERS.includes(name) || (typeof name === "string" && name.startsWith("art:"));
    state.wall = known ? name : WALLPAPERS[0];
    document.querySelector(".os").dataset.wallpaper = state.wall.startsWith("art:") ? "game" : state.wall;
    applyWallpaper();
    try {
      localStorage.setItem(WALL_KEY, state.wall);
    } catch {}
  }

  function pad(n) {
    return String(n).padStart(2, "0");
  }

  function showClock(m) {
    state.clock = m;
    $("clockDay").textContent = `${m.day}.1.1843`;
    $("clockTime").textContent = `${pad(m.hour)}:${pad(m.minute)}`;
    $("clock").title = `${m.weekday}, day ${m.day}`;
  }

  function showMoney(m) {
    state.money = m;
    $("moneyOc").textContent = Apps.money(m.oc, "OC");
    $("moneyRm").textContent = Apps.money(m.rm, "RM");
    $("money").hidden = false;
  }

  function renderNotify() {
    $("notifyList").replaceChildren(
      ...state.alerts.map((a) => {
        const li = el("li", "note");
        if (a.from) li.append(el("span", "obx-label", a.from));
        li.append(el("span", "toast-title", a.title));
        if (a.body) li.append(el("span", "toast-body", a.body));
        return li;
      })
    );
    $("notifyEmpty").hidden = state.alerts.length > 0;
  }

  function setNotify(on) {
    if (!on && $("notify").hidden) return;
    $("notify").hidden = !on;
    $("bell").setAttribute("aria-expanded", String(on));
    if (on) {
      $("bellDot").hidden = true;
      renderNotify();
    }
  }

  function remember(m) {
    state.alerts.unshift({ from: typeof m.from === "string" ? m.from : "", title: m.title, body: typeof m.body === "string" ? m.body : "" });
    state.alerts.splice(ALERTS_MAX);
    if ($("notify").hidden) $("bellDot").hidden = false;
    renderNotify();
  }

  function toast(m) {
    remember(m);
    if (m.quiet === true) return;
    const t = el("div", "toast");
    const text = el("div", "toast-text");
    if (typeof m.from === "string") text.append(el("span", "obx-label", m.from));
    text.append(el("span", "toast-title", m.title));
    if (typeof m.body === "string") text.append(el("span", "toast-body", m.body));
    const x = el("button", "icon-btn", "×");
    x.type = "button";
    x.setAttribute("aria-label", "Dismiss");
    t.append(text, x);
    const box = $("toasts");
    box.append(t);
    while (box.children.length > TOASTS_MAX) box.firstElementChild.remove();
    let left = TOAST_MS;
    let held = 0;
    let since = Date.now();
    let timer = setTimeout(() => t.remove(), left);
    const hold = () => {
      if (held++) return;
      clearTimeout(timer);
      left -= Date.now() - since;
    };
    const release = () => {
      if (--held) return;
      since = Date.now();
      timer = setTimeout(() => t.remove(), left);
    };
    t.addEventListener("pointerenter", hold);
    t.addEventListener("pointerleave", release);
    t.addEventListener("focusin", hold);
    t.addEventListener("focusout", release);
  }

  function setCtx(on, e) {
    const ctx = $("ctx");
    if (!on && ctx.hidden) return;
    ctx.hidden = !on;
    $("ctxSub").hidden = true;
    $("ctxWall").setAttribute("aria-expanded", "false");
    if (!on) return;
    const r = desktop.getBoundingClientRect();
    const k = scale();
    ctx.style.left = `${Math.min((e.clientX - r.left) / k, desktop.offsetWidth - ctx.offsetWidth)}px`;
    ctx.style.top = `${Math.min((e.clientY - r.top) / k, desktop.offsetHeight - ctx.offsetHeight)}px`;
  }

  function setTheme(mode) {
    if (THEMES.includes(mode)) document.documentElement.dataset.theme = mode;
  }

  function apply(m) {
    if (m.v !== undefined && m.v !== 1) return;
    if (m.type === "apps" && Array.isArray(m.apps)) setApps(m.apps, m.user, m.sites);
    else if (m.type === "openapp" && typeof m.app === "string") openApp(m.app);
    else if (m.type === "theme") setTheme(m.mode);
    else if (m.type === "wallpaper" && m.name === "game" && Number.isInteger(m.rev) && m.rev >= 0) setGame(m.rev);
    else if (m.type === "gamewalls" && Array.isArray(m.items)) setArt(m.items);
    else if (m.type === "clock" && [m.day, m.hour, m.minute].every(Number.isFinite) && typeof m.weekday === "string") showClock(m);
    else if (m.type === "money" && Number.isInteger(m.oc) && Number.isFinite(m.rm)) showMoney(m);
    else if (m.type === "openfile" && typeof m.path === "string" && appById("notes")) openApp("notes", { path: m.path });
    else if (m.type === "toast" && typeof m.title === "string") toast(m);
    else if (m.type === "event" && typeof m.app === "string") Apps.emit(m.app, m);
    else if (m.type === "obx.frame") Apps.emit("wiki", m);
    else if (m.type === "badge" && typeof m.app === "string" && Number.isInteger(m.count) && m.count >= 0) setBadge(m.app, m.count);
    else if (m.type === "res") Apps.route(m);
  }

  function onMessage(e) {
    if (!host && e.source !== window) return;
    let m = e.data;
    if (typeof m === "string") {
      try {
        m = JSON.parse(m);
      } catch {
        return;
      }
    }
    if (m && typeof m === "object") apply(m);
  }

  function fit() {
    document.documentElement.style.zoom = clamp(Math.min(window.innerWidth / 1920, window.innerHeight / 1080), 0.75, 2);
  }

  $("smGrid").addEventListener("click", (e) => {
    const b = e.target.closest("[data-app]");
    if (b) openApp(b.dataset.app);
  });
  $("power").append(el("span", "", "Shut down"));
  $("power").addEventListener("click", () => send({ type: "close" }));
  $("bell").addEventListener("click", () => setNotify($("notify").hidden));
  $("clearAll").addEventListener("click", () => {
    state.alerts = [];
    renderNotify();
  });
  $("taskList").addEventListener("click", (e) => {
    const id = e.target.closest("[data-app]")?.dataset.app;
    const w = wins.get(id);
    if (!w) {
      if (id) openApp(id);
    } else if (w.min) restore(w);
    else if (state.focus === w.id) minimise(w);
    else focusWin(w);
  });
  $("desk").addEventListener("click", (e) => {
    const b = e.target.closest("[data-app]");
    if (b) selectDesk(b);
  });
  $("desk").addEventListener("dblclick", (e) => {
    const b = e.target.closest("[data-app]");
    if (b) openApp(b.dataset.app);
  });
  $("desk").addEventListener("keydown", deskKey);
  desktop.addEventListener("click", (e) => {
    if (e.target === desktop) selectDesk(null);
  });
  desktop.addEventListener("contextmenu", (e) => {
    if (e.target !== desktop) return;
    e.preventDefault();
    setCtx(true, e);
  });
  $("ctx").addEventListener("click", (e) => {
    const wall = e.target.closest("[data-wall]");
    if (wall) {
      setWallpaper(wall.dataset.wall);
      setCtx(false);
    } else if (e.target.closest("#ctxWall")) {
      const sub = $("ctxSub");
      sub.hidden = !sub.hidden;
      $("ctxWall").setAttribute("aria-expanded", String(!sub.hidden));
    } else if (e.target.closest("#ctxSettings")) {
      setCtx(false);
      if (appById("settings")) openApp("settings");
    } else if (e.target.closest("#ctxRefresh")) {
      setCtx(false);
      renderAll();
    }
  });
  $("toasts").addEventListener("click", (e) => {
    const t = e.target.closest(".toast");
    if (t) t.remove();
  });
  document.addEventListener("click", (e) => {
    if (!e.target.closest("#startMenu, #start")) ShellStart.set(false);
    if (!e.target.closest("#notify, #bell")) setNotify(false);
    if (!e.target.closest("#ctx")) setCtx(false);
  });
  function escape(typing, win) {
    if (!$("ctx").hidden) setCtx(false);
    else if (!$("startMenu").hidden) ShellStart.set(false, true);
    else if (!$("notify").hidden) setNotify(false);
    else if (win) closeApp(win);
    else if (!typing) send({ type: "close" });
  }
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && !e.repeat) escape(e.target.matches("input, textarea, select"));
  });

  icons(document);
  let saved = null;
  try {
    saved = localStorage.getItem(WALL_KEY);
  } catch {}
  Apps.on("recycle", (m) => {
    if (m.type === "bin") {
      state.binFull = m.count > 0;
      paintBin();
    } else if (m.type === "changed") {
      probeBin();
      Apps.reopen("recycle");
    }
  });
  setWallpaper(saved);
  fit();
  window.addEventListener("resize", () => {
    fit();
    for (const w of wins.values()) fitWin(w);
  });
  setTimeout(() => {
    $("boot").classList.add("out");
    setTimeout(() => $("boot").remove(), 150);
  }, BOOT_MS);

  ModFrame.init({ toast, badge: setBadge, win: (id) => wins.get(id), place, send, escape: (id) => escape(false, id) });
  (host || window).addEventListener("message", onMessage);
  Apps.mount("virusfx", desktop, hostApi(), {}, true);

  if (host) {
    send({ type: "ready" });
  } else {
    const demoClock = () => {
      const d = new Date();
      apply({ type: "clock", day: d.getDate(), weekday: DAYS[d.getDay()], hour: d.getHours(), minute: d.getMinutes() });
    };
    apply({ type: "apps", apps: DEMO_APPS });
    demoClock();
    setInterval(demoClock, 30000);
    apply({ type: "money", oc: 1234, rm: 56.78 });
    apply({ type: "toast", title: "Stalburg Bank - Suspicious activity detected", body: "Review your recent transactions." });
    setTimeout(() => apply({ type: "toast", title: "Preview mode", body: "No game host is attached." }), PREVIEW_TOAST_GAP_MS);
  }
})();
