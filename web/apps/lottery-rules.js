(function (root) {
  "use strict";

  const PICK = 6;
  const POOL = 40;
  const PRIZES = [0, 0, 0, 120, 1500, 20000, 50000];

  function matches(line, drawn) {
    let hits = 0;
    for (const n of line) if (drawn.indexOf(n) >= 0) hits++;
    return hits;
  }

  function prize(hits) {
    return PRIZES[hits] || 0;
  }

  const api = { PICK, POOL, PRIZES, matches, prize };
  root.LottoRules = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})(typeof window !== "undefined" ? window : globalThis);
