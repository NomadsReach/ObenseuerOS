(function () {
  "use strict";

  const TIMEOUT_MS = 10000;
  const pending = new Map();
  const subs = new Map();
  let seq = 0;
  let resolveReady;
  const ready = new Promise((resolve) => {
    resolveReady = resolve;
  });

  function send(kind, fields) {
    window.parent.postMessage(Object.assign({ obxos: 1, kind }, fields), "*");
  }

  function emit(name, data) {
    for (const fn of [...(subs.get(name) || [])]) fn(data);
  }

  function setTheme(mode) {
    os.theme = mode === "dark" ? "dark" : "light";
    document.documentElement.dataset.theme = os.theme;
  }

  function settle(m) {
    const p = pending.get(m.id);
    if (!p) return;
    pending.delete(m.id);
    clearTimeout(p.timer);
    if (m.ok === true) p.resolve(m.result);
    else p.reject({ error: String(m.error || "failed"), message: String(m.message || "") });
  }

  function call(method, args) {
    return new Promise((resolve, reject) => {
      const id = ++seq;
      const timer = setTimeout(() => {
        pending.delete(id);
        reject({ error: "failed", message: "No response from the shell" });
      }, TIMEOUT_MS);
      pending.set(id, { resolve, reject, timer });
      send("call", { id, method, args: args || {} });
    });
  }

  window.addEventListener("message", (e) => {
    const m = e.data;
    if (e.source !== window.parent || !m || typeof m !== "object" || m.obxos !== 1) return;
    if (m.kind === "init") {
      setTheme(m.theme);
      resolveReady({ app: m.app, theme: os.theme, user: m.user, opts: m.opts });
    } else if (m.kind === "res") settle(m);
    else if (m.kind === "theme") {
      setTheme(m.mode);
      emit("theme", os.theme);
    } else if (m.kind === "open") emit("open", m.opts);
    else if (m.kind === "event") emit(String(m.name), m.data);
  });

  const os = {
    ready,
    theme: "light",
    call,
    on(name, fn) {
      if (!subs.has(name)) subs.set(name, new Set());
      subs.get(name).add(fn);
      return () => subs.get(name).delete(fn);
    },
    close: () => send("close", {}),
    notify: (title, body) => send("notify", { title, body: body || "" }),
    openUrl: (url) => send("url", { url }),
    openFile: (name) => call("os.mod.open", { name }),
    window: {
      title: (text) => send("title", { text }),
      size: (w, h) => send("size", { w, h }),
    },
    files: {
      read: (name) => call("os.mod.read", { name }),
      write: (name, text) => call("os.mod.write", { name, text }),
    },
  };

  window.addEventListener(
    "keydown",
    (e) => {
      if (e.key === "Escape" && !e.repeat && !e.target.matches("input, textarea, select") &&!e.target.isContentEditable) send("key", { key: "Escape" });
    },
    true
  );

  window.os = os;
  send("hello", {});
})();
