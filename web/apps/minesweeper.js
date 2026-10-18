(function () {
  "use strict";
  const { el, button } = Apps;
  const M = window.Minesweeper;
  const KEY = "obx.os.minesweeper";
  const LEVELS = [["beginner", "Beginner"], ["intermediate", "Intermediate"], ["expert", "Expert"]];
  const TICK_MS = 1000;
  const MAX_TIME = 999;
  const MIN_CELL = 16;

  function load() {
    try {
      const v = JSON.parse(localStorage.getItem(KEY));
      if (v && M.PRESETS[v.level]) return v.level;
    } catch (e) {}
    return "beginner";
  }

  function save(level) {
    try {
      localStorage.setItem(KEY, JSON.stringify({ level }));
    } catch (e) {}
  }

  function lcd(v) {
    const n = Math.max(-99, Math.min(999, v));
    return n < 0 ? "-" + ("0" + -n).slice(-2) : ("00" + n).slice(-3);
  }

  function paintCell(node, g, i) {
    const c = g.cells[i];
    const lost = g.status === "lost";
    const cls = ["ms-cell"];
    let text = "";
    if (c.open) {
      cls.push("open");
      if (c.n) {
        cls.push("n" + c.n);
        text = String(c.n);
      }
    } else if (lost && c.mine && c.mark !== M.FLAG) {
      cls.push("open", "mine");
      if (i === g.boom) cls.push("boom");
    } else if (c.mark === M.FLAG) {
      cls.push("flag");
      if (lost && !c.mine) cls.push("wrong");
    } else if (c.mark === M.QUESTION) {
      cls.push("q");
      text = "?";
    }
    node.className = cls.join(" ");
    node.textContent = text;
  }

  Apps.register("minesweeper", {
    size: { w: 720, h: 560 },
    mount(slot) {
      const root = el("div", "g-root ms");
      const levels = el("div", "ms-levels");
      const panel = el("div", "ms-panel");
      const bar = el("div", "ms-bar");
      const mineLcd = el("span", "ms-lcd", "000");
      const timeLcd = el("span", "ms-lcd", "000");
      const face = button("ms-face", "");
      face.setAttribute("aria-label", "New game");
      const board = el("div", "ms-board");
      const wrap = el("div", "ms-wrap");
      const msg = el("p", "ms-msg");
      msg.setAttribute("role", "status");
      wrap.append(board);
      bar.append(mineLcd, face, timeLcd);
      panel.append(bar, wrap);
      root.append(levels, panel, msg);
      slot.append(root);

      const levelBtns = LEVELS.map(([id, label]) => {
        const b = button("g-btn", label);
        b.dataset.level = id;
        b.addEventListener("click", () => setLevel(id));
        return b;
      });
      levels.append(...levelBtns);

      let level = load();
      let game = null;
      let cells = [];
      let secs = 0;
      let clock = null;
      let suppress = false;
      let pressed = -1;

      function stopClock() {
        if (clock) clearInterval(clock);
        clock = null;
      }

      function startClock() {
        if (clock) return;
        clock = setInterval(() => {
          if (!slot.isConnected) {
            stopClock();
            return;
          }
          secs = Math.min(MAX_TIME, secs + 1);
          timeLcd.textContent = lcd(secs);
        }, TICK_MS);
      }

      function paint() {
        const flags = game.cells.filter((c) => c.mark === M.FLAG).length;
        mineLcd.textContent = lcd(game.mines - flags);
        timeLcd.textContent = lcd(secs);
        face.className = "ms-face " + game.status;
        msg.textContent = game.status === "won" ? "You cleared the field in " + secs + " seconds." : game.status === "lost" ? "You hit a mine." : "";
        cells.forEach((node, i) => paintCell(node, game, i));
        levelBtns.forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.level === level)));
      }

      function update() {
        if (game.placed && game.status === "playing") startClock();
        if (game.status !== "playing") stopClock();
        paint();
      }

      function layout() {
        if (!game) return;
        const cs = Math.max(MIN_CELL, Math.floor(Math.min(wrap.clientWidth / game.cols, wrap.clientHeight / game.rows)));
        board.style.setProperty("--cs", cs + "px");
        board.style.gridTemplateColumns = `repeat(${game.cols}, ${cs}px)`;
      }

      function newGame() {
        stopClock();
        secs = 0;
        game = M.board(M.PRESETS[level]);
        cells = game.cells.map((c, i) => {
          const node = el("div", "ms-cell");
          node.dataset.i = i;
          return node;
        });
        board.replaceChildren(...cells);
        layout();
        update();
      }

      function setLevel(id) {
        level = id;
        save(level);
        newGame();
      }

      function cellIndex(e) {
        const node = e.target.closest(".ms-cell");
        return node ? Number(node.dataset.i) : -1;
      }

      board.addEventListener("contextmenu", (e) => e.preventDefault());
      board.addEventListener("mousedown", (e) => {
        const i = cellIndex(e);
        if (i < 0 || game.status !== "playing") return;
        if ((e.buttons & 3) === 3 || e.button === 1) {
          suppress = true;
          if (M.chord(game, i)) update();
        } else if (e.button === 2) {
          if (M.toggle(game, i)) update();
        } else if (e.button === 0 && e.buttons === 1) {
          suppress = false;
          pressed = i;
        }
      });
      board.addEventListener("mouseup", (e) => {
        const i = cellIndex(e);
        if (e.buttons) return;
        if (e.button === 0 && !suppress && i === pressed && M.reveal(game, i)) update();
        suppress = false;
        pressed = -1;
      });

      face.addEventListener("click", newGame);
      new ResizeObserver(layout).observe(wrap);
      newGame();
    },
  });
})();
