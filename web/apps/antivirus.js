(function () {
  "use strict";
  const { el, button, money } = Apps;
  const SCAN_MS = 5000;
  const BAR_TICK_MS = 200;
  const NO_HOST = "This program works inside the game.";
  const BUSY = "The game is saving, try again in a moment.";
  const FAILED = "The scan did not finish. Try again.";
  const CLEAN = "No threats found. Your computer is clean.";

  function errorText(e) {
    if (e && e.message === "No game host") return NO_HOST;
    if (e && e.error === "busy") return BUSY;
    return (e && e.message) || FAILED;
  }

  function table(heads, rows) {
    const t = el("table", "av-table");
    const top = el("tr");
    for (const h of heads) top.append(el("th", "", h));
    t.append(top);
    for (const row of rows) {
      const tr = el("tr");
      for (const c of row) tr.append(el("td", "", c));
      t.append(tr);
    }
    return t;
  }

  function progressBar() {
    const bar = el("div", "av-bar");
    bar.append(el("div", "av-fill"));
    bar.setAttribute("role", "progressbar");
    bar.setAttribute("aria-valuemin", "0");
    bar.setAttribute("aria-valuemax", "100");
    bar.setAttribute("aria-valuenow", "0");
    return bar;
  }

  function animate(bar, ms) {
    bar.classList.remove("run");
    void bar.offsetWidth;
    bar.classList.add("run");
    const start = Date.now();
    const step = () => {
      const pct = Math.min(100, Math.round(((Date.now() - start) / ms) * 100));
      bar.setAttribute("aria-valuenow", String(pct));
      if (pct < 100 && bar.isConnected) setTimeout(step, BAR_TICK_MS);
    };
    bar.setAttribute("aria-valuenow", "0");
    setTimeout(step, BAR_TICK_MS);
  }

  function guard(slot, ctx) {
    const page = el("div", "av av-guard");
    const head = el("div", "av-head");
    const title = el("div");
    title.append(el("h1", "av-name", "PC Master Guard"), el("p", "av-sub", "Version 3.0. Stay safe out there."));
    head.append(el("span", "av-shield"), title);

    const status = el("fieldset", "av-status");
    const led = el("p", "av-row");
    led.append(el("span", "av-led"), el("span", "", "Real-time protection: ON"));
    const blocked = el("p", "av-row");
    const last = el("p", "av-row");
    status.append(el("legend", "", "Protection status"), led, blocked, last);

    const scanBtn = button("av-scan", "SCAN NOW");
    const bar = progressBar();
    const note = el("p", "av-note");
    const out = el("div", "av-results");
    out.setAttribute("role", "status");
    const stolen = el("div", "av-stolen");

    function render(s) {
      blocked.textContent = "Threats blocked by real-time protection: " + s.blocked;
      last.textContent = "Last full scan: " + (s.lastScan === null ? "never" : "day " + s.lastScan);
      stolen.replaceChildren();
      if (!s.quarantine.length) {
        stolen.append(el("p", "", "Nothing has been stolen from this computer."));
        return;
      }
      stolen.append(table(["Virus", "Last theft (day)", "Stolen OC", "Stolen RM"], s.quarantine.map((q) => [q.name, String(q.day), money(q.stolenOc, "OC"), money(q.stolenRm, "RM")])));
    }

    function report(r) {
      out.replaceChildren();
      if (!r.found.length) out.append(el("p", "av-clean", CLEAN));
      else out.append(table(["Threat", "Result"], r.found.map((f) => [f.name, f.result === "removed" ? "Removed" : f.result])));
      out.append(el("p", "av-files", "Files put back: " + r.files.restored + ". Junk files removed: " + r.files.junk + "."));
      if (r.files.left > 0) out.append(el("p", "av-files", (r.files.left === 1 ? "1 file" : r.files.left + " files") + " could not be put back. Scan again."));
    }

    scanBtn.addEventListener("click", () => {
      scanBtn.disabled = true;
      out.replaceChildren();
      animate(bar, SCAN_MS);
      note.textContent = "Scanning memory and your documents...";
      setTimeout(() => {
        ctx.call("av.scan", {}).then((r) => {
          report(r);
          render(r.state);
        }, (e) => {
          out.textContent = errorText(e);
        }).then(() => {
          note.textContent = "";
          scanBtn.disabled = false;
        });
      }, SCAN_MS);
    });

    ctx.call("av.state", {}).then(render, (e) => {
      out.textContent = errorText(e);
    });

    page.append(head, status, scanBtn, bar, note, out, el("h2", "av-h", "Stolen money report"), stolen);
    slot.append(page);
  }

  const TURVA_SCAN_MS = 6000;
  const COOL_POLL_MS = 15000;
  const SITE_URL = "www.turvapro.ts";
  const TURVA_NAMES = { active: "PROTECTED", lapsed: "EXPIRED", ended: "ENDED", none: "CANCELLED" };
  const TURVA_RESULT = { removed: "Removed", missed: "Not removed", quarantined: "Quarantined", trusted: "Trusted partner" };
  const CANCELLED = "Subscription cancelled. TurvaPro 2000 is off.";

  function turva(slot, ctx) {
    const page = el("div", "av tvp");
    const head = el("div", "av-head");
    const title = el("div");
    title.append(el("h1", "av-name", "TurvaPro 2000"), el("p", "av-sub", "The #1 antivirus. Trusted by 9 out of 10 PCs!!!"));
    head.append(el("span", "av-shield"), title);
    const banner = el("div", "tvp-banner");
    const status = el("fieldset", "av-status");
    const days = el("p", "av-row");
    const lapse = el("p", "av-row");
    const last = el("p", "av-row");
    status.append(el("legend", "", "Subscription"), days, lapse, last);
    const pay = button("tvp-pay", "PAY NOW");
    pay.addEventListener("click", () => ctx.open("browser", { url: SITE_URL }));
    const scanBtn = button("av-scan", "SCAN NOW");
    const bar = progressBar();
    const note = el("p", "av-note");
    const out = el("div", "av-results");
    out.setAttribute("role", "status");
    const cookies = el("p", "tvp-cookies");
    const cancelBtn = button("tvp-cancel", "Cancel subscription");

    function render(s) {
      banner.textContent = TURVA_NAMES[s.status] || TURVA_NAMES.none;
      banner.className = "tvp-banner " + (s.status === "active" ? "on" : "off");
      days.textContent = s.owned ? "Days to the next bill: " + s.daysToBill : "";
      lapse.textContent = s.status === "lapsed" ? "Unpaid for " + s.lapsedDays + " days. Real-time protection is OFF." : (s.owned ? "Real-time protection: ON" : "");
      last.textContent = "Last scan: " + (s.lastScan === null ? "never" : "day " + s.lastScan);
      pay.hidden = s.status !== "lapsed";
      scanBtn.disabled = !s.owned || s.scanReadyIn > 0;
      scanBtn.textContent = s.scanReadyIn > 0 ? "SCAN IN " + s.scanReadyIn + " MIN" : "SCAN NOW";
      cancelBtn.disabled = !s.owned;
      if (s.scanReadyIn > 0) setTimeout(refresh, COOL_POLL_MS);
    }

    function refresh() {
      if (!page.isConnected) return;
      ctx.call("av.state", {}).then(render, (e) => {
        out.textContent = errorText(e);
      });
    }

    function report(r) {
      out.replaceChildren();
      if (!r.found.length) out.append(el("p", "av-clean", CLEAN));
      else out.append(table(["Threat", "Result"], r.found.map((f) => [f.name, TURVA_RESULT[f.result] || f.result])));
      cookies.textContent = "Tracking cookies: " + r.cookies;
    }

    scanBtn.addEventListener("click", () => {
      scanBtn.disabled = true;
      out.replaceChildren();
      animate(bar, TURVA_SCAN_MS);
      note.textContent = "Scanning memory, your documents and your cookies...";
      setTimeout(() => {
        ctx.call("av.scan", {}).then((r) => {
          report(r);
          render(r.state);
          note.textContent = "";
        }, (e) => {
          out.textContent = errorText(e);
          note.textContent = "";
          scanBtn.disabled = false;
        });
      }, TURVA_SCAN_MS);
    });

    cancelBtn.addEventListener("click", () => {
      cancelBtn.disabled = true;
      ctx.call("av.cancel", {}).then((s) => {
        render(s);
        note.textContent = CANCELLED;
      }, (e) => {
        cancelBtn.disabled = false;
        note.textContent = errorText(e);
      });
    });

    page.append(head, banner, status, pay, scanBtn, bar, note, out, cookies, cancelBtn);
    slot.append(page);
    ctx.call("av.state", {}).then(render, (e) => {
      out.textContent = errorText(e);
    });
  }

  Apps.register("turvapro", { size: { w: 470, h: 600 }, mount: turva });
  Apps.register("pcguard", { size: { w: 470, h: 580 }, mount: guard });
})();
