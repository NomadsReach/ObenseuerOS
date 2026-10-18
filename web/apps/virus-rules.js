(function (root) {
  "use strict";

  const MAX_ON_SCREEN = 4;
  const MAX_DODGES = 2;
  const DODGE_CHANCE = 0.35;
  const SPAWN_CHANCE = 0.15;
  const SPAWN_COUNT = 2;
  const MAX_LAG_MS = 3000;

  function roomFor(onScreen, want) {
    return Math.max(0, Math.min(want, MAX_ON_SCREEN - onScreen));
  }

  function closeAction(dodges, roll) {
    return dodges < MAX_DODGES && roll < DODGE_CHANCE ? "dodge" : "close";
  }

  function spawnsOnClose(onScreen, spawned, roll) {
    if (spawned || roll >= SPAWN_CHANCE) return 0;
    return onScreen + SPAWN_COUNT <= MAX_ON_SCREEN ? SPAWN_COUNT : 0;
  }

  function clampLag(ms) {
    return Math.max(0, Math.min(MAX_LAG_MS, Math.round(Number(ms) || 0)));
  }

  const api = { MAX_ON_SCREEN, MAX_DODGES, DODGE_CHANCE, SPAWN_CHANCE, SPAWN_COUNT, MAX_LAG_MS, roomFor, closeAction, spawnsOnClose, clampLag };
  root.VirusFxRules = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})(typeof window !== "undefined" ? window : globalThis);
