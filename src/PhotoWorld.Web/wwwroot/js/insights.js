// Insights charts: one single-series SVG column chart per measure (never two scales on one plot). Bars <= 24px,
// 4px rounded data-end and square at the baseline, 2px surface gap, hairline grid, clean ticks, a tooltip on hover
// and keyboard focus. Text uses textContent only. The table view on the page carries every value too.
(() => {
  const days = JSON.parse(document.getElementById("insights-data").textContent);
  const NS = "http://www.w3.org/2000/svg";
  const el = (name, attrs = {}) => {
    const node = document.createElementNS(NS, name);
    for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, String(v));
    return node;
  };
  const fmtDay = (iso) => new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" });
  const compact = (n) => (n >= 1000 ? `${(n / 1000).toFixed(n >= 10000 ? 0 : 1)}K` : String(n));

  // Clean tick step: 1, 2 or 5 × 10^n, about four ticks.
  const niceMax = (max) => {
    if (max <= 4) return { top: 4, step: 1 };
    const raw = max / 4, mag = 10 ** Math.floor(Math.log10(raw));
    const step = [1, 2, 5, 10].map((m) => m * mag).find((s) => s >= raw);
    return { top: Math.ceil(max / step) * step, step };
  };

  // A column with 4px rounded top corners and a square foot on the baseline.
  const columnPath = (x, y, w, h, r) => {
    r = Math.min(r, w / 2, h);
    return `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`;
  };

  const tooltip = document.createElement("div");
  tooltip.className = "chart-tooltip";
  tooltip.hidden = true;
  document.body.append(tooltip);
  const showTip = (target, value, label, day) => {
    tooltip.replaceChildren();
    const v = document.createElement("strong"); v.textContent = `${value.toLocaleString()} ${label}`;
    const d = document.createElement("span"); d.textContent = fmtDay(day);
    tooltip.append(v, d);
    tooltip.hidden = false;
    const r = target.getBoundingClientRect();
    tooltip.style.left = `${r.left + r.width / 2 + window.scrollX}px`;
    tooltip.style.top = `${r.top + window.scrollY - 8}px`;
  };
  const hideTip = () => { tooltip.hidden = true; };

  for (const card of document.querySelectorAll("[data-chart]")) {
    const key = card.dataset.chart;              // "views" or "likes"
    const values = days.map((d) => d[key]);
    const host = card.querySelector(".chart");
    const W = 640, H = 220, left = 36, right = 8, top = 12, bottom = 26;
    const plotW = W - left - right, plotH = H - top - bottom;
    const { top: yMax, step } = niceMax(Math.max(0, ...values));
    const y = (v) => top + plotH - (v / yMax) * plotH;

    const svg = el("svg", { viewBox: `0 0 ${W} ${H}`, class: "chart-svg", preserveAspectRatio: "none" });
    // Hairline grid and y ticks (recessive).
    for (let t = 0; t <= yMax; t += step) {
      svg.append(el("line", { x1: left, x2: W - right, y1: y(t), y2: y(t), class: t === 0 ? "chart-base" : "chart-grid" }));
      const label = el("text", { x: left - 6, y: y(t) + 4, class: "chart-tick", "text-anchor": "end" });
      label.textContent = compact(t);
      svg.append(label);
    }
    // Columns: the slot is plotW/30; the bar is capped at 24px, leaving the rest as air (>= the 2px gap).
    const slot = plotW / values.length, barW = Math.min(24, slot - 2);
    values.forEach((v, i) => {
      const x = left + i * slot + (slot - barW) / 2;
      const g = el("g", { class: "chart-bar", tabindex: 0, role: "img", "aria-label": `${fmtDay(days[i].date)}: ${v} ${key}` });
      // Hit target: the whole slot height, bigger than the mark.
      g.append(el("rect", { x: left + i * slot, y: top, width: slot, height: plotH, class: "chart-hit" }));
      if (v > 0) g.append(el("path", { d: columnPath(x, y(v), barW, top + plotH - y(v), 4), class: "chart-mark" }));
      const show = () => showTip(g.lastChild, v, key, days[i].date);
      g.addEventListener("pointerenter", show);
      g.addEventListener("focus", show);
      g.addEventListener("pointerleave", hideTip);
      g.addEventListener("blur", hideTip);
      svg.append(g);
      // A date under every 7th column, and the last.
      if (i % 7 === 0 || i === values.length - 1) {
        const label = el("text", { x: left + i * slot + slot / 2, y: H - 8, class: "chart-tick", "text-anchor": "middle" });
        label.textContent = new Date(`${days[i].date}T00:00:00`).toLocaleDateString(undefined, { day: "numeric", month: "short" });
        svg.append(label);
      }
    });
    host.replaceChildren(svg);
  }
})();
