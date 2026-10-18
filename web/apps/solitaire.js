(function () {
  "use strict";
  const { el, button } = Apps;
  const KL = window.Klondike;
  const KEY = "obx.os.solitaire";
  const RANK = ["", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
  const SUIT = ["♠", "♥", "♦", "♣"];
  const CARD_W = 66;
  const CARD_H = 92;
  const MIN_CARD = 40;
  const COL_GAP = 10;
  const UP_GAP = 22;
  const DOWN_GAP = 9;
  const FAN = 16;
  const DRAG_PX = 4;
  const DBL_MS = 400;
  const TICK_MS = 1000;
  const AUTO_MS = 150;
  const CELEBRATE_MS = 9000;
  const GRAVITY = 0.6;
  const DAMP = 0.7;

  function load() {
    try {
      const v = JSON.parse(localStorage.getItem(KEY));
      if (v && (v.draw === 1 || v.draw === 3)) return v.draw;
    } catch (e) {}
    return 1;
  }

  function save(draw) {
    try {
      localStorage.setItem(KEY, JSON.stringify({ draw }));
    } catch (e) {}
  }

  function fmt(s) {
    return Math.floor(s / 60) + ":" + ("0" + (s % 60)).slice(-2);
  }

  function cardNode(c) {
    const n = el("div", "sol-card");
    if (!c.up) {
      n.classList.add("down");
      return n;
    }
    n.classList.add(KL.isRed(c) ? "red" : "black");
    n.append(el("span", "sol-tl", RANK[c.r] + SUIT[c.s]), el("span", "sol-mid", SUIT[c.s]));
    return n;
  }

  function mark(n, from, pile, at) {
    n.dataset.from = from;
    n.dataset.pile = pile;
    n.dataset.at = at;
    return n;
  }

  Apps.register("solitaire", {
    size: { w: 720, h: 700 },
    mount(slot) {
      const root = el("div", "g-root sol");
      const bar = el("div", "sol-bar");
      const newBtn = button("g-btn", "New game");
      const undoBtn = button("g-btn", "Undo");
      const modes = [1, 3].map((n) => {
        const b = button("g-btn", "Draw " + n);
        b.dataset.draw = n;
        return b;
      });
      const score = el("span", "sol-stat");
      const time = el("span", "sol-stat");
      bar.append(newBtn, undoBtn, ...modes, score, time);

      const table = el("div", "sol-table");
      const top = el("div", "sol-row");
      const stockEl = el("div", "sol-slot");
      stockEl.dataset.act = "stock";
      const wasteEl = el("div", "sol-slot");
      const foundEls = [0, 1, 2, 3].map((p) => {
        const f = el("div", "sol-slot");
        f.dataset.drop = "found";
        f.dataset.pile = p;
        return f;
      });
      top.append(stockEl, wasteEl, el("div", "sol-gap"), ...foundEls);
      const cols = el("div", "sol-row");
      const colEls = [0, 1, 2, 3, 4, 5, 6].map((p) => {
        const c = el("div", "sol-col");
        c.dataset.drop = "tab";
        c.dataset.pile = p;
        return c;
      });
      cols.append(...colEls);
      table.append(top, cols);
      const msg = el("p", "sol-msg");
      msg.setAttribute("role", "status");
      root.append(bar, table, msg);
      slot.append(root);

      let draw = load();
      let g = null;
      let history = [];
      let secs = 0;
      let clock = null;
      let auto = null;
      let done = false;
      let press = null;
      let drag = null;
      let lastTap = { key: "", t: 0 };
      let fx = null;
      let frame = 0;
      let unit = 1;

      function layout() {
        const cw = Math.max(MIN_CARD, Math.floor((cols.clientWidth - COL_GAP * 6) / 7));
        root.style.setProperty("--sol-w", cw + "px");
        root.style.setProperty("--sol-h", Math.round((cw * CARD_H) / CARD_W) + "px");
        unit = cw / CARD_W;
        if (g) render();
      }

      function scale() {
        return root.getBoundingClientRect().width / root.offsetWidth || 1;
      }

      function stopClock() {
        clearInterval(clock);
        clock = null;
      }

      function stopAuto() {
        clearInterval(auto);
        auto = null;
      }

      function startClock() {
        if (clock) return;
        clock = setInterval(() => {
          if (!slot.isConnected) return stopClock();
          secs++;
          paintStats();
        }, TICK_MS);
      }

      function paintStats() {
        score.textContent = "Score " + g.score;
        time.textContent = "Time " + fmt(secs);
        undoBtn.disabled = !history.length || done;
        modes.forEach((b) => b.setAttribute("aria-pressed", String(Number(b.dataset.draw) === draw)));
      }

      function render() {
        stockEl.replaceChildren();
        if (g.stock.length) stockEl.append(cardNode({ up: false }));
        stockEl.classList.toggle("recycle", !g.stock.length && g.waste.length > 0);

        const shown = g.waste.slice(-(draw === 3 ? KL.WASTE_SHOWN : 1));
        const fan = draw === 3 ? FAN * unit : 0;
        wasteEl.replaceChildren(...shown.map((c, k) => {
          const n = cardNode(c);
          n.style.left = k * fan + "px";
          if (k === shown.length - 1) mark(n, "waste", 0, 0);
          return n;
        }));

        foundEls.forEach((f, p) => {
          const pile = g.found[p];
          f.replaceChildren(pile.length ? mark(cardNode(pile[pile.length - 1]), "found", p, pile.length - 1) : el("span", "sol-ph", SUIT[p]));
        });

        colEls.forEach((col, p) => {
          let y = 0;
          const kids = g.tab[p].map((c, k) => {
            const n = cardNode(c);
            n.style.top = y + "px";
            if (c.up) mark(n, "tab", p, k);
            y += (c.up ? UP_GAP : DOWN_GAP) * unit;
            return n;
          });
          col.replaceChildren(...kids);
          col.style.height = y + CARD_H * unit + "px";
        });
        paintStats();
      }

      function removeFx() {
        cancelAnimationFrame(frame);
        if (fx) fx.remove();
        fx = null;
      }

      function celebrate() {
        removeFx();
        fx = el("div", "sol-fx");
        root.append(fx);
        const R = root.getBoundingClientRect();
        const s = scale();
        const W = root.clientWidth;
        const H = root.clientHeight;
        const parts = [];
        foundEls.forEach((f, p) => {
          const b = f.getBoundingClientRect();
          const x0 = (b.left - R.left) / s;
          const y0 = (b.top - R.top) / s;
          g.found[p].forEach((c) => {
            const n = cardNode(c);
            fx.append(n);
            parts.push({ n, x: x0, y: y0, vx: Math.random() * 6 - 3, vy: -2 - Math.random() * 10 });
          });
        });
        const t0 = performance.now();
        const tick = (now) => {
          if (!fx || !fx.isConnected || now - t0 > CELEBRATE_MS) return removeFx();
          parts.forEach((p) => {
            p.vy += GRAVITY;
            p.x += p.vx;
            p.y += p.vy;
            if (p.x < 0) {
              p.x = 0;
              p.vx = -p.vx;
            } else if (p.x > W - CARD_W * unit) {
              p.x = W - CARD_W * unit;
              p.vx = -p.vx;
            }
            if (p.y > H - CARD_H * unit) {
              p.y = H - CARD_H * unit;
              p.vy = -p.vy * DAMP;
              if (Math.abs(p.vy) < 4) p.vy = -(8 + Math.random() * 8);
            }
            p.n.style.transform = `translate(${p.x}px, ${p.y}px)`;
          });
          frame = requestAnimationFrame(tick);
        };
        frame = requestAnimationFrame(tick);
      }

      function finish() {
        done = true;
        stopAuto();
        stopClock();
        msg.textContent = "You won in " + fmt(secs) + " with a score of " + g.score + ".";
        celebrate();
        paintStats();
      }

      function startAuto() {
        auto = setInterval(() => {
          if (!slot.isConnected) return stopAuto();
          const before = JSON.stringify(g);
          if (!KL.step(g)) return stopAuto();
          history.push(before);
          render();
          if (KL.won(g)) finish();
        }, AUTO_MS);
      }

      function after() {
        render();
        if (KL.won(g)) finish();
        else if (KL.canFinish(g) && !auto) startAuto();
      }

      function act(run) {
        if (done) return false;
        const before = JSON.stringify(g);
        if (!run()) return false;
        history.push(before);
        startClock();
        after();
        return true;
      }

      function undo() {
        if (!history.length || done) return;
        stopAuto();
        g = JSON.parse(history.pop());
        render();
      }

      function newGame() {
        stopAuto();
        stopClock();
        removeFx();
        secs = 0;
        done = false;
        history = [];
        press = null;
        drag = null;
        msg.textContent = "";
        g = KL.deal(draw);
        render();
      }

      function setDraw(n) {
        draw = n;
        save(n);
        newGame();
      }

      function startDrag(p) {
        const nodes = p.from === "tab" ? Array.from(colEls[p.pile].children).slice(p.at) : [p.node];
        const R = root.getBoundingClientRect();
        const s = scale();
        const first = nodes[0].getBoundingClientRect();
        const ghost = el("div", "sol-ghost");
        ghost.style.left = (first.left - R.left) / s + "px";
        ghost.style.top = (first.top - R.top) / s + "px";
        nodes.forEach((n) => {
          const r = n.getBoundingClientRect();
          const c = n.cloneNode(true);
          c.style.left = "0px";
          c.style.top = (r.top - first.top) / s + "px";
          ghost.append(c);
          n.classList.add("sol-hide");
        });
        root.append(ghost);
        return { ghost };
      }

      function drop(p, e) {
        const hit = document.elementFromPoint(e.clientX, e.clientY);
        const target = hit && hit.closest("[data-drop]");
        if (!target) return false;
        const to = target.dataset.drop;
        const dest = Number(target.dataset.pile);
        return act(() => KL.move(g, p.from, p.pile, p.at, to, dest));
      }

      function tap(p) {
        if (p.stock) {
          act(() => KL.draw(g));
          return;
        }
        const key = p.from + p.pile + ":" + p.at;
        const now = Date.now();
        if (lastTap.key === key && now - lastTap.t < DBL_MS) {
          lastTap = { key: "", t: 0 };
          act(() => KL.toFoundation(g, p.from, p.pile, p.at));
        } else lastTap = { key, t: now };
      }

      root.addEventListener("pointerdown", (e) => {
        if (e.button !== 0 || done) return;
        const card = e.target.closest(".sol-card[data-from]");
        const stock = e.target.closest("[data-act='stock']");
        if (card) press = { node: card, from: card.dataset.from, pile: Number(card.dataset.pile), at: Number(card.dataset.at), x: e.clientX, y: e.clientY, id: e.pointerId };
        else if (stock) press = { stock: true, x: e.clientX, y: e.clientY, id: e.pointerId };
        else return;
        root.setPointerCapture(e.pointerId);
      });

      root.addEventListener("pointermove", (e) => {
        if (!press || press.id !== e.pointerId) return;
        const dx = e.clientX - press.x;
        const dy = e.clientY - press.y;
        if (!drag && !press.stock && Math.abs(dx) + Math.abs(dy) > DRAG_PX) drag = startDrag(press);
        if (drag) {
          const s = scale();
          drag.ghost.style.transform = `translate(${dx / s}px, ${dy / s}px)`;
        }
      });

      root.addEventListener("pointerup", (e) => {
        if (!press || press.id !== e.pointerId) return;
        const p = press;
        const d = drag;
        press = null;
        drag = null;
        if (d) {
          d.ghost.remove();
          if (!drop(p, e)) render();
        } else tap(p);
      });

      root.addEventListener("pointercancel", () => {
        press = null;
        if (drag) {
          drag.ghost.remove();
          drag = null;
          render();
        }
      });

      newBtn.addEventListener("click", newGame);
      undoBtn.addEventListener("click", undo);
      modes.forEach((b) => b.addEventListener("click", () => setDraw(Number(b.dataset.draw))));
      new ResizeObserver(layout).observe(table);
      newGame();
    },
  });
})();
