(function () {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const clamp = (v, lo, hi) => Math.min(Math.max(v, lo), hi);

  function filter(q) {
    const s = q.trim().toLowerCase();
    for (const b of $("smGrid").children) b.hidden = !b.dataset.name.includes(s);
  }

  function set(on, focus) {
    if (!on && $("startMenu").hidden) return;
    if (on) {
      $("smSearch").value = "";
      filter("");
    }
    $("startMenu").hidden = !on;
    $("start").setAttribute("aria-expanded", String(on));
    if (focus) (on ? $("smSearch") : $("start")).focus();
  }

  function key(e) {
    const items = [...$("smGrid").children].filter((b) => !b.hidden);
    const i = items.indexOf(e.target);
    const step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -4, ArrowDown: 4 }[e.key];
    if (!step) return;
    if (i >= 0) {
      e.preventDefault();
      if (e.key === "ArrowUp" && i + step < 0) $("smSearch").focus();
      else items[clamp(i + step, 0, items.length - 1)].focus();
    } else if (e.target === $("smSearch") && e.key === "ArrowDown" && items.length) {
      e.preventDefault();
      items[0].focus();
    }
  }

  $("start").addEventListener("click", () => set($("startMenu").hidden, true));
  $("smSearch").addEventListener("input", (e) => filter(e.target.value));
  $("smSearch").addEventListener("keydown", (e) => {
    if (e.key === "Enter") $("smGrid").querySelector(".start-item:not([hidden])")?.click();
  });
  $("startMenu").addEventListener("keydown", key);

  window.ShellStart = { set, filter };
})();
