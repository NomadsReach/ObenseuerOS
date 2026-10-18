(function () {
  "use strict";
  const { el, num } = Apps;

  const NS = "http://www.w3.org/2000/svg";
  const W = 600;
  const H = 240;
  const L = 56;
  const R = 584;
  const T = 12;
  const B = 216;
  const LINES = 4;

  function node(tag, attrs) {
    const n = document.createElementNS(NS, tag);
    for (const k of Object.keys(attrs)) n.setAttribute(k, attrs[k]);
    return n;
  }

  window.ExchangeChart = {
    line(points) {
      if (!points.length) return el("p", "stalex-empty", "No price history yet.");
      const n = points.length;
      const lo = Math.min(...points);
      const hi = Math.max(...points);
      const span = hi - lo;
      const x = (i) => (n > 1 ? L + (i * (R - L)) / (n - 1) : R);
      const y = (v) => (span ? B - ((v - lo) / span) * (B - T) : (T + B) / 2);
      const up = points[n - 1] >= points[0];

      const svg = node("svg", { viewBox: `0 0 ${W} ${H}`, class: up ? "stalex-chart up" : "stalex-chart down", role: "img", "aria-label": "Price history" });
      const grad = node("linearGradient", { id: "stalex-area", x1: "0", y1: "0", x2: "0", y2: "1" });
      grad.append(node("stop", { offset: "0", class: "stalex-stop-top" }), node("stop", { offset: "1", class: "stalex-stop-bottom" }));
      const defs = node("defs", {});
      defs.append(grad);
      svg.append(defs);

      for (let g = 0; g <= LINES; g++) {
        const gy = T + ((B - T) * g) / LINES;
        const value = hi - (span * g) / LINES;
        svg.append(node("line", { x1: L, x2: R, y1: gy, y2: gy, class: "stalex-grid" }));
        const label = node("text", { x: L - 6, y: gy + 4, "text-anchor": "end", class: "stalex-axis" });
        label.textContent = num(value, 2);
        svg.append(label);
      }

      const path = points.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`);
      svg.append(node("path", { d: `M${path.join(" L")} L${x(n - 1)},${B} L${x(0)},${B} Z`, fill: "url(#stalex-area)", class: "stalex-area" }));
      svg.append(node("polyline", { points: path.join(" "), class: up ? "stalex-line up" : "stalex-line down" }));

      const first = node("text", { x: L, y: H - 6, class: "stalex-axis" });
      first.textContent = `${num(n - 1, 0)} h ago`;
      const last = node("text", { x: R, y: H - 6, "text-anchor": "end", class: "stalex-axis" });
      last.textContent = "Now";
      const mid = node("text", { x: (L + R) / 2, y: H - 6, "text-anchor": "middle", class: "stalex-axis" });
      mid.textContent = `${num((n - 1) / 2, 0)} h ago`;
      svg.append(first, mid, last);

      const marker = node("circle", { r: 4, class: "stalex-marker", visibility: "hidden" });
      const tip = node("text", { class: "stalex-tip", visibility: "hidden" });
      const hit = node("rect", { x: L, y: T, width: R - L, height: B - T, class: "stalex-hit" });
      svg.append(marker, tip, hit);

      hit.addEventListener("pointermove", (e) => {
        const box = svg.getBoundingClientRect();
        const vx = ((e.clientX - box.left) / box.width) * W;
        const k = Math.max(0, Math.min(n - 1, Math.round(((vx - L) / (R - L)) * (n - 1))));
        const left = x(k) > (L + R) / 2;
        marker.setAttribute("cx", x(k));
        marker.setAttribute("cy", y(points[k]));
        marker.setAttribute("visibility", "visible");
        tip.textContent = `${num(points[k], 2)}, ${k === n - 1 ? "now" : num(n - 1 - k, 0) + " h ago"}`;
        tip.setAttribute("x", left ? x(k) - 8 : x(k) + 8);
        tip.setAttribute("y", T + 12);
        tip.setAttribute("text-anchor", left ? "end" : "start");
        tip.setAttribute("visibility", "visible");
      });
      hit.addEventListener("pointerleave", () => {
        marker.setAttribute("visibility", "hidden");
        tip.setAttribute("visibility", "hidden");
      });
      return svg;
    },
  };
})();
