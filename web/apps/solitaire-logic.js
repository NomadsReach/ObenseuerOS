(function () {
  "use strict";

  const WASTE_SHOWN = 3;
  const SCORE = { wasteTab: 5, wasteFound: 10, tabFound: 10, reveal: 5, foundTab: -15 };

  function isRed(c) {
    return c.s === 1 || c.s === 2;
  }

  function deck(rand) {
    const cards = [];
    for (let s = 0; s < 4; s++) for (let r = 1; r <= 13; r++) cards.push({ s, r, up: false });
    for (let k = cards.length - 1; k > 0; k--) {
      const j = Math.floor(rand() * (k + 1));
      const t = cards[k];
      cards[k] = cards[j];
      cards[j] = t;
    }
    return cards;
  }

  function deal(draw, rand) {
    const cards = deck(rand || Math.random);
    const tab = [];
    for (let p = 0; p < 7; p++) {
      const pile = [];
      for (let k = 0; k <= p; k++) pile.push(cards.pop());
      pile[pile.length - 1].up = true;
      tab.push(pile);
    }
    return { draw: draw === 3 ? 3 : 1, stock: cards, waste: [], found: [[], [], [], []], tab, score: 0 };
  }

  function legalTab(card, pile) {
    if (!pile.length) return card.r === 13;
    const top = pile[pile.length - 1];
    return top.up && top.r === card.r + 1 && isRed(top) !== isRed(card);
  }

  function legalFound(card, pile) {
    if (!pile.length) return card.r === 1;
    const top = pile[pile.length - 1];
    return top.s === card.s && top.r === card.r - 1;
  }

  function run(pile, at) {
    if (at < 0 || at >= pile.length) return null;
    for (let i = at; i < pile.length; i++) {
      if (!pile[i].up) return null;
      if (i > at && (pile[i - 1].r !== pile[i].r + 1 || isRed(pile[i - 1]) === isRed(pile[i]))) return null;
    }
    return pile.slice(at);
  }

  function source(g, from, pile, at) {
    if (from === "waste") return g.waste.length ? [g.waste[g.waste.length - 1]] : null;
    if (from === "found") return g.found[pile].length ? [g.found[pile][g.found[pile].length - 1]] : null;
    return run(g.tab[pile], at);
  }

  function take(g, from, pile, at) {
    if (from === "waste") return g.waste.splice(g.waste.length - 1, 1);
    if (from === "found") return g.found[pile].splice(g.found[pile].length - 1, 1);
    return g.tab[pile].splice(at);
  }

  function gain(g, delta) {
    g.score = Math.max(0, g.score + delta);
  }

  function move(g, from, pile, at, to, dest) {
    const cards = source(g, from, pile, at);
    if (!cards) return false;
    if (to === "found") {
      if (cards.length !== 1 || !legalFound(cards[0], g.found[dest])) return false;
    } else if (!legalTab(cards[0], g.tab[dest])) return false;
    take(g, from, pile, at);
    const target = to === "found" ? g.found[dest] : g.tab[dest];
    target.push(...cards);
    if (from === "tab" && g.tab[pile].length && !g.tab[pile][g.tab[pile].length - 1].up) {
      g.tab[pile][g.tab[pile].length - 1].up = true;
      gain(g, SCORE.reveal);
    }
    if (from === "waste") gain(g, to === "found" ? SCORE.wasteFound : SCORE.wasteTab);
    else if (from === "tab") gain(g, to === "found" ? SCORE.tabFound : 0);
    else gain(g, SCORE.foundTab);
    return true;
  }

  function toFoundation(g, from, pile, at) {
    for (let f = 0; f < 4; f++) if (move(g, from, pile, at, "found", f)) return true;
    return false;
  }

  function draw(g) {
    if (g.stock.length) {
      const n = Math.min(g.draw, g.stock.length);
      for (let k = 0; k < n; k++) {
        const c = g.stock.pop();
        c.up = true;
        g.waste.push(c);
      }
      return true;
    }
    if (!g.waste.length) return false;
    while (g.waste.length) {
      const c = g.waste.pop();
      c.up = false;
      g.stock.push(c);
    }
    return true;
  }

  function count(g) {
    return g.found.reduce((n, p) => n + p.length, 0);
  }

  function won(g) {
    return count(g) === 52;
  }

  function canFinish(g) {
    return !g.stock.length && !g.waste.length && g.tab.every((p) => p.every((c) => c.up));
  }

  function step(g) {
    if (toFoundation(g, "waste")) return true;
    for (let p = 0; p < 7; p++) {
      const pile = g.tab[p];
      if (pile.length && toFoundation(g, "tab", p, pile.length - 1)) return true;
    }
    return false;
  }

  const api = { WASTE_SHOWN, isRed, deal, legalTab, legalFound, move, toFoundation, draw, count, won, canFinish, step };
  if (typeof module !== "undefined") module.exports = api;
  else window.Klondike = api;
})();
