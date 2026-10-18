(function () {
  "use strict";

  const COVER = 0;
  const FLAG = 1;
  const QUESTION = 2;
  const PRESETS = {
    beginner: { rows: 9, cols: 9, mines: 10 },
    intermediate: { rows: 16, cols: 16, mines: 40 },
    expert: { rows: 16, cols: 30, mines: 99 },
  };

  function nearby(b, i) {
    const r = Math.floor(i / b.cols);
    const c = i % b.cols;
    const out = [];
    for (let dr = -1; dr <= 1; dr++) {
      for (let dc = -1; dc <= 1; dc++) {
        const nr = r + dr;
        const nc = c + dc;
        if ((dr || dc) && nr >= 0 && nr < b.rows && nc >= 0 && nc < b.cols) out.push(nr * b.cols + nc);
      }
    }
    return out;
  }

  function board(preset) {
    const cells = [];
    for (let i = 0; i < preset.rows * preset.cols; i++) cells.push({ mine: false, n: 0, open: false, mark: COVER });
    return { rows: preset.rows, cols: preset.cols, mines: preset.mines, cells, placed: false, status: "playing", opened: 0, boom: -1 };
  }

  function place(b, first, rand) {
    const near = nearby(b, first);
    const free = [];
    for (let i = 0; i < b.cells.length; i++) if (i !== first && near.indexOf(i) < 0) free.push(i);
    for (let k = 0; k < b.mines; k++) {
      const j = k + Math.floor(rand() * (free.length - k));
      const t = free[k];
      free[k] = free[j];
      free[j] = t;
      b.cells[free[k]].mine = true;
    }
    b.cells.forEach((c, i) => {
      c.n = nearby(b, i).filter((j) => b.cells[j].mine).length;
    });
    b.placed = true;
  }

  function flood(b, start) {
    const stack = [start];
    while (stack.length) {
      const i = stack.pop();
      const c = b.cells[i];
      if (c.open || c.mark === FLAG) continue;
      c.open = true;
      c.mark = COVER;
      b.opened++;
      if (c.n === 0) nearby(b, i).forEach((j) => { if (!b.cells[j].open) stack.push(j); });
    }
  }

  function lose(b, i) {
    b.status = "lost";
    b.boom = i;
  }

  function win(b) {
    b.status = "won";
    b.cells.forEach((c) => { if (c.mine) c.mark = FLAG; });
  }

  function reveal(b, i, rand) {
    const c = b.cells[i];
    if (b.status !== "playing" || c.open || c.mark === FLAG) return false;
    if (!b.placed) place(b, i, rand || Math.random);
    if (c.mine) {
      lose(b, i);
      return true;
    }
    flood(b, i);
    if (b.opened === b.cells.length - b.mines) win(b);
    return true;
  }

  function toggle(b, i) {
    const c = b.cells[i];
    if (b.status !== "playing" || c.open) return false;
    c.mark = c.mark === COVER ? FLAG : c.mark === FLAG ? QUESTION : COVER;
    return true;
  }

  function chord(b, i) {
    const c = b.cells[i];
    if (b.status !== "playing" || !c.open || c.n === 0) return false;
    const near = nearby(b, i);
    if (near.filter((j) => b.cells[j].mark === FLAG).length !== c.n) return false;
    near.forEach((j) => { if (!b.cells[j].open && b.cells[j].mark !== FLAG) reveal(b, j); });
    return true;
  }

  const api = { PRESETS, COVER, FLAG, QUESTION, nearby, board, reveal, toggle, chord };
  if (typeof module !== "undefined") module.exports = api;
  else window.Minesweeper = api;
})();
