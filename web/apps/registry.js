(function () {
  "use strict";

  const TIMEOUT_MS = 10000;
  const NO_FILE = "no such file";
  const WRITE_CHUNK = 2000;
  const defs = new Map();
  const hooks = new Map();
  const pending = new Map();
  const writes = new Map();
  const subs = new Map();
  let seq = 0;

  function el(tag, cls, text) {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function ico(name) {
    const s = el("span", "ico");
    s.dataset.icon = name;
    s.setAttribute("aria-hidden", "true");
    return s;
  }

  function button(cls, text) {
    const b = el("button", cls, text);
    b.type = "button";
    return b;
  }

  function field(type, cls, label) {
    const i = el("input", cls);
    i.type = type;
    i.setAttribute("aria-label", label);
    return i;
  }

  const GROUP = " ";
  const NBSP = " ";
  const NO_VALUE = "-";
  const UNITS = [[1e21, "Td"], [1e18, "Tn"], [1e15, "Bd"], [1e12, "Bn"], [1e9, "Md"], [1e6, "Mn"]];

  function group(digits) {
    let out = "";
    for (let i = digits.length; i > 0; i -= 3) out = digits.slice(Math.max(0, i - 3), i) + (out ? GROUP + out : "");
    return out;
  }

  function fixed(abs, d) {
    if (abs < 1e21) return abs.toFixed(d);
    const [m, e] = String(abs).split("e");
    const [i, f = ""] = m.split(".");
    return i + f.padEnd(Number(e), "0") + (d ? "." + "0".repeat(d) : "");
  }

  function num(value, decimals) {
    if (!Number.isFinite(value)) return NO_VALUE;
    const d = Math.max(0, Math.min(6, decimals | 0));
    const s = fixed(Math.abs(value), d);
    const [i, f] = s.split(".");
    return (value < 0 && Number(s) !== 0 ? "-" : "") + group(i) + (f !== undefined ? "," + f : "");
  }

  function scaled(abs, k, d) {
    const s = fixed(abs / UNITS[k][0], d);
    if (Number(s) >= 1000 && k > 0) return scaled(abs, k - 1, d);
    const [i, f] = s.split(".");
    return group(i) + (f !== undefined ? "," + f : "") + NBSP + UNITS[k][1];
  }

  function money(value, currency, opts) {
    const o = opts || {};
    if (!Number.isFinite(value)) return NO_VALUE;
    const abs = Math.abs(value);
    const short = o.short && abs >= 1e6;
    const d = o.decimals !== undefined ? o.decimals : short || currency === "RM" ? 2 : 0;
    const k = UNITS.findIndex((u) => abs >= u[0]);
    const body = short ? scaled(abs, k, d) : num(abs, d);
    return (value < 0 && /[1-9]/.test(body) ? "-" : "") + body + NBSP + currency;
  }

  function web(slot, tab, url, embedded) {
    if (embedded) {
      const inner = el("div", "web-page");
      slot.append(inner);
      return inner;
    }
    const root = el("div", "web");
    const tabs = el("div", "web-tabs");
    tabs.append(el("span", "web-tab", tab));
    const bar = el("div", "web-url");
    bar.append(ico("lock"), el("span", "", url));
    const page = el("div", "web-page");
    root.append(tabs, bar, page);
    slot.append(root);
    return page;
  }

  function chunks(s) {
    const out = [];
    for (let i = 0; i < s.length; ) {
      let end = Math.min(i + WRITE_CHUNK, s.length);
      const c = s.charCodeAt(end - 1);
      if (end < s.length && c >= 0xd800 && c <= 0xdbff) end--;
      out.push(s.slice(i, end));
      i = end;
    }
    return out.length ? out : [""];
  }

  async function stage(ctx, path, text) {
    const parts = chunks(text);
    for (let i = 0; i < parts.length; i++) {
      await ctx.call(i ? "os.fs.append" : "os.fs.write", { path, text: parts[i], more: i < parts.length - 1 });
    }
  }

  function writeFile(ctx, path, text) {
    const next = (writes.get(path) || Promise.resolve()).then(() => stage(ctx, path, text));
    writes.set(path, next.catch(() => {}));
    return next;
  }

  function exists(ctx, path) {
    return ctx.call("os.fs.size", { path }).then(
      () => true,
      (e) => {
        if (e.message === NO_FILE) return false;
        throw e;
      }
    );
  }

  function ctxFor(appId, host, embedded) {
    return {
      appId,
      live: host.live,
      embedded,
      call(method, args) {
        return new Promise((resolve, reject) => {
          if (!host.live) {
            reject({ error: "failed", message: "No game host" });
            return;
          }
          const id = ++seq;
          const timer = setTimeout(() => {
            pending.delete(id);
            reject({ error: "failed", message: "No response from game host" });
          }, TIMEOUT_MS);
          pending.set(id, { resolve, reject, timer });
          host.send({ type: "req", id, app: appId, method, args: args || {} });
        });
      },
      open(id, opts) {
        host.open(id, opts);
      },
      info() {
        return host.info();
      },
      close() {
        host.close(appId);
      },
      mount(id, slot, opts) {
        return id !== appId && window.Apps.mount(id, slot, host, opts, true);
      },
    };
  }

  function route(m) {
    const p = pending.get(m.id);
    if (!p) return;
    pending.delete(m.id);
    clearTimeout(p.timer);
    if (m.ok === true) p.resolve(m.result);
    else p.reject({ error: String(m.error || "failed"), message: String(m.message || "") });
  }

  window.Apps = {
    el,
    ico,
    button,
    field,
    web,
    route,
    num,
    money,
    exists,
    writeFile,
    register(id, def) {
      defs.set(id, def);
    },
    size(id) {
      const def = defs.get(id);
      return def ? def.size : undefined;
    },
    mount(id, slot, host, opts, embedded) {
      const def = defs.get(id);
      if (!def) return false;
      const hook = def.mount(slot, ctxFor(id, host, Boolean(embedded)), opts || {}) || {};
      if (!embedded) hooks.set(id, hook);
      return true;
    },
    reopen(id, opts) {
      const hook = hooks.get(id);
      if (hook && hook.onOpen) hook.onOpen(opts || {});
    },
    forget(id) {
      hooks.delete(id);
    },
    canClose(id) {
      const hook = hooks.get(id);
      return !hook || !hook.canClose || hook.canClose();
    },
    on(appId, handler) {
      if (!subs.has(appId)) subs.set(appId, new Set());
      subs.get(appId).add(handler);
      return () => subs.get(appId).delete(handler);
    },
    emit(appId, msg) {
      for (const h of [...(subs.get(appId) || [])]) h(msg);
    },
  };
})();
