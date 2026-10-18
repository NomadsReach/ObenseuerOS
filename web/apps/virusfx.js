(function () {
  "use strict";
  const { el, button } = Apps;
  const rules = window.VirusFxRules;
  const AD_KINDS = ["winner", "moca", "rm", "speed"];
  const WALL_TEXT = "MOCA-COLA ICE COLD  *  FREE CRATE  *  CLAIM NOW!!!";
  const WALL_LINES = 12;
  const SHUFFLE_CHANCE = 0.3;
  const KINDS = {
    winner: { brand: "VOITTAJA! Lucky visitor", big: "YOU WON 5 000 RM!", text: "Click CLAIM PRIZE now. Offer ends today!", go: "CLAIM PRIZE", claim: "winner" },
    moca: { brand: "Moca-cola", big: "Free crate of Moca-cola, claim now", text: "Ice cold, 100 % free. Only 3 crates left!", go: "CLAIM NOW", claim: "moca" },
    rm: { brand: "FREE RM GENERATOR", big: "Free RM generator, 100 % working", text: "Generate 1 000 RM in seconds.", go: "DOWNLOAD NOW", url: "www.free-rm.ts" },
    speed: { brand: "TurvaPro", big: "Your Viewports is SLOW!", text: "Speed up your PC for free.", go: "SPEED UP", url: "www.turvapro.ts" },
    error: { brand: "Viewports 8", big: "Error 0x0000E44 in KOLHOLA.DLL", text: "The application has performed an illegal operation.", ok: true },
    nag: { brand: "TurvaPro 2000", big: "TurvaPro found 3 threats! Scan now", lapsed: "Your protection has EXPIRED", text: "Protection off. Your PC is at risk.", go: "SCAN NOW", app: "turvapro", url: "www.turvapro.ts" },
    claimed: { brand: "VOITTAJA!", big: "Prize claimed, check your mail", text: "Your prize will arrive by post.", ok: true },
  };
  const DEMO_STATE = { fx: { wall: true, shuffle: true }, protect: { cheap: "none" } };

  let host = null;
  let desktop = null;
  let desk = null;
  let layer = null;
  let wall = null;
  let observer = null;
  let lagMs = 0;
  let lapsed = false;
  let demoMode = false;
  const live = [];

  function pick(list) {
    return list[Math.floor(Math.random() * list.length)];
  }

  function roll() {
    return demoMode ? 0 : Math.random();
  }

  function place(node) {
    node.style.left = `${Math.floor(Math.random() * Math.max(1, desktop.offsetWidth - node.offsetWidth))}px`;
    node.style.top = `${Math.floor(Math.random() * Math.max(1, desktop.offsetHeight - node.offsetHeight))}px`;
  }

  function clampAll() {
    for (const rec of live) {
      const n = rec.node;
      n.style.left = `${Math.max(0, Math.min(parseFloat(n.style.left), desktop.offsetWidth - n.offsetWidth))}px`;
      n.style.top = `${Math.max(0, Math.min(parseFloat(n.style.top), desktop.offsetHeight - n.offsetHeight))}px`;
    }
  }

  function dismiss(rec) {
    rec.node.remove();
    const i = live.indexOf(rec);
    if (i >= 0) live.splice(i, 1);
  }

  function clearAll() {
    while (live.length) dismiss(live[0]);
  }

  function installed(app) {
    return host.info().apps.some((a) => a.id === app);
  }

  function claim(kind) {
    host.call("virus.claim", { kind }).then((r) => {
      if (r.outcome !== "blocked") open("claimed", false);
    }, () => {});
  }

  function press(rec) {
    const k = KINDS[rec.kind];
    dismiss(rec);
    if (k.claim) claim(k.claim);
    else if (k.app && installed(k.app)) host.open(k.app);
    else host.open("browser", { url: k.url });
  }

  function pressClose(rec) {
    if (rules.closeAction(rec.dodges, roll()) === "dodge") {
      rec.dodges++;
      rec.corner = (rec.corner + 1 + Math.floor(roll() * 3)) % 4;
      rec.x.dataset.corner = String(rec.corner);
      return;
    }
    dismiss(rec);
    const n = rules.spawnsOnClose(live.length, rec.spawned, roll());
    for (let i = 0; i < n; i++) open(pick(AD_KINDS), true);
  }

  function open(kind, spawned) {
    if (!rules.roomFor(live.length, 1)) return;
    const k = KINDS[kind];
    const node = el("div", `vx-pop vx-${kind}`);
    const rec = { node, kind, dodges: 0, spawned, corner: 0, x: null };
    const x = el("button", "vx-x", k.ok ? "OK" : "X");
    x.type = "button";
    x.dataset.corner = "0";
    x.setAttribute("aria-label", k.ok ? "OK" : "Close");
    x.addEventListener("click", () => pressClose(rec));
    rec.x = x;
    const body = el("div", "vx-body");
    body.append(el("p", "vx-big", lapsed && k.lapsed ? k.lapsed : k.big));
    if (k.text) body.append(el("p", "vx-text", k.text));
    if (k.go) {
      const go = button("vx-go", k.go);
      go.addEventListener("click", () => press(rec));
      body.append(go);
    }
    node.append(el("div", "vx-bar", k.brand), body, x);
    live.push(rec);
    layer.append(node);
    place(node);
  }

  function pulse(m) {
    for (let i = 0; i < m.popups; i++) open(pick(AD_KINDS), false);
    if (m.error) open("error", false);
    if (m.nag) open("nag", false);
    if (observer && roll() < SHUFFLE_CHANCE) reorder();
  }

  function reorder() {
    for (const n of desk.children) n.style.order = String(Math.floor(Math.random() * desk.children.length));
  }

  function setShuffle(on) {
    if (on && !observer) {
      observer = new MutationObserver(reorder);
      observer.observe(desk, { childList: true });
      reorder();
    } else if (!on && observer) {
      observer.disconnect();
      observer = null;
      for (const n of desk.children) n.style.order = "";
    }
  }

  function setWall(on) {
    if (on && !wall) {
      wall = el("div", "vx-wall");
      for (let i = 0; i < WALL_LINES; i++) wall.append(el("p", "vx-wall-line", WALL_TEXT));
      desktop.prepend(wall);
    } else if (!on && wall) {
      wall.remove();
      wall = null;
    }
  }

  function applyState(s) {
    if (!s || !s.fx) return;
    lagMs = rules.clampLag(s.fx.lag);
    lapsed = Boolean(s.protect) && s.protect.cheap === "lapsed";
    if (!Object.values(s.fx).some(Boolean)) clearAll();
    setWall(Boolean(s.fx.wall));
    setShuffle(Boolean(s.fx.shuffle));
  }

  function refresh() {
    host.call("virus.state", {}).then(applyState, () => {});
  }

  function onEvent(m) {
    if (m.name === "pulse") pulse(m);
    else if (m.name === "state") {
      if (m.reset) clearAll();
      refresh();
    }
  }

  function demo() {
    demoMode = true;
    applyState(DEMO_STATE);
    pulse({ popups: 2, error: true });
  }

  Apps.register("virusfx", {
    mount(slot, ctx) {
      host = ctx;
      desktop = slot;
      desk = document.getElementById("desk");
      layer = el("div", "vx-layer");
      slot.append(layer);
      Apps.on("virusfx", onEvent);
      window.addEventListener("resize", clampAll);
      refresh();
    },
  });

  window.VirusFx = { lag: () => lagMs, demo };
})();
