// The welcome tour: dims the page and spotlights each part of the header in turn, with a short note on what it's for.
// New members see it once, after their first sign-in (data-tour-autostart on <body>, set by the layout); anyone can
// replay it with a [data-tour-start] button (the User guide has one). Parts are found by their data-tour names, and a
// part that isn't on the page is skipped. On narrow screens the collapsed menu is opened for the tour and closed after.
(() => {
  const steps = [
    { title: "Welcome to PhotoCommunity", text: "A quick tour of where everything is. It takes under a minute." },
    { target: "gallery", title: "Gallery", text: "Photos shared by members, newest first. Open one to like it, comment, or give feedback." },
    { target: "explore", title: "Explore", text: "Browse by tag, place and camera, and find photographers to follow." },
    { target: "blogs", title: "Blogs", text: "Stories, trips and workshop write-ups from members. Following shows posts from the people you follow." },
    { target: "community", title: "Community", text: "Camera clubs, photo walks, online workshops and challenges: the places to meet other photographers and take part." },
    { target: "places", title: "Places", text: "Save your favourite spots and check the light before you go: sunrise, sunset and the weather." },
    { target: "search", title: "Search", text: "Find photos, members, posts, clubs and walks by name." },
    { target: "create", title: "＋ Create", text: "Everything you can start: upload photos, write a blog post, organise a walk, host a workshop, start a club or a collection." },
    { target: "activity", title: "Activity", text: "Likes, comments, replies and new followers. A number shows when something's new." },
    { target: "account", title: "Your account", text: "Your profile, collections, clubs, portfolio and insights. Settings is where you choose your emails and light or dark mode." },
    { target: "guide", title: "User guide", text: "Step-by-step help for every part of the site. You can take this tour again from there." },
    { title: "You're all set", text: "Why not start by sharing your first photo?", finish: true },
  ];

  const collapse = document.querySelector(".site-nav .navbar-collapse");
  const toggler = document.querySelector(".site-nav .navbar-toggler");
  let active = [], index = 0, openedMenu = false, lastFocus = null;
  let blocker, spot, card;

  const partOf = (step) => step.target ? document.querySelector(`[data-tour="${step.target}"]`) : null;
  const isShown = (el) => !!el && el.getClientRects().length > 0;
  const menuCollapsed = () => toggler && getComputedStyle(toggler).display !== "none";

  function showMenu(show) {
    if (!collapse || !window.bootstrap) return Promise.resolve();
    const menu = bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false });
    if (show === collapse.classList.contains("show")) return Promise.resolve();
    return new Promise((resolve) => {
      collapse.addEventListener(show ? "shown.bs.collapse" : "hidden.bs.collapse", resolve, { once: true });
      show ? menu.show() : menu.hide();
    });
  }

  async function start() {
    if (card) return;
    lastFocus = document.activeElement;
    if (menuCollapsed()) { openedMenu = !collapse.classList.contains("show"); await showMenu(true); }
    active = steps.filter((s) => !s.target || isShown(partOf(s)));
    index = 0;
    build();
    render();
  }

  function build() {
    blocker = Object.assign(document.createElement("div"), { className: "tour-blocker" });
    spot = Object.assign(document.createElement("div"), { className: "tour-spot" });
    card = document.createElement("div");
    card.className = "tour-card";
    card.setAttribute("role", "dialog");
    card.setAttribute("aria-modal", "true");
    card.setAttribute("aria-labelledby", "tour-title");
    card.setAttribute("aria-describedby", "tour-text");
    card.innerHTML = `
      <p class="tour-count" aria-live="polite"></p>
      <h2 class="tour-title" id="tour-title"></h2>
      <p class="tour-text" id="tour-text"></p>
      <div class="tour-actions">
        <button type="button" class="link-button tour-skip" data-tour-skip>Skip tour</button>
        <div class="tour-buttons">
          <button type="button" class="btn btn-ghost btn-sm" data-tour-back>Back</button>
          <a class="btn btn-ghost btn-sm" data-tour-upload hidden>Share a photo</a>
          <button type="button" class="btn btn-accent btn-sm" data-tour-next>Next</button>
        </div>
      </div>`;
    card.querySelector("[data-tour-skip]").addEventListener("click", end);
    card.querySelector("[data-tour-back]").addEventListener("click", () => go(-1));
    card.querySelector("[data-tour-next]").addEventListener("click", () => go(1));
    const upload = document.querySelector("[data-tour-upload-link]");
    if (upload) card.querySelector("[data-tour-upload]").href = upload.href;
    document.body.append(blocker, spot, card);
    document.addEventListener("keydown", onKey);
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, { passive: true });
  }

  function go(by) {
    const next = index + by;
    if (next >= active.length) return end();
    if (next < 0) return;
    index = next;
    render();
  }

  function render() {
    const step = active[index];
    const first = index === 0, last = index === active.length - 1;
    card.querySelector(".tour-count").textContent = first || last ? "" : `${index} of ${active.length - 2}`;
    card.querySelector(".tour-title").textContent = step.title;
    card.querySelector(".tour-text").textContent = step.text;
    card.querySelector("[data-tour-back]").hidden = first || last;
    card.querySelector("[data-tour-skip]").hidden = last;
    const upload = card.querySelector("[data-tour-upload]");
    upload.hidden = !(step.finish && upload.getAttribute("href"));
    const next = card.querySelector("[data-tour-next]");
    next.textContent = first ? "Show me around" : last ? "Done" : "Next";
    partOf(step)?.scrollIntoView({ block: "nearest" });
    place();
    next.focus();
  }

  // The spotlight sits over the part (its huge shadow dims everything else); with no part it shrinks to a point in
  // the middle, so the whole page is dimmed. The note goes below the part, or above it when there's no room.
  function place() {
    if (!card) return;
    const el = partOf(active[index]);
    const margin = 16, gap = 12, pad = 6;
    // The visible page, without the scrollbar.
    const innerWidth = document.documentElement.clientWidth, innerHeight = document.documentElement.clientHeight;
    const width = Math.min(360, innerWidth - margin * 2);
    card.style.width = `${width}px`;
    if (!el) {
      Object.assign(spot.style, { top: `${innerHeight / 2}px`, left: `${innerWidth / 2}px`, width: "0", height: "0" });
      card.style.left = `${(innerWidth - width) / 2}px`;
      card.style.top = `${Math.max(margin, (innerHeight - card.offsetHeight) / 2)}px`;
      return;
    }
    const r = el.getBoundingClientRect();
    Object.assign(spot.style, {
      top: `${r.top - pad}px`, left: `${r.left - pad}px`, width: `${r.width + pad * 2}px`, height: `${r.height + pad * 2}px`,
    });
    let top = r.bottom + pad + gap;
    if (top + card.offsetHeight > innerHeight - margin) top = Math.max(margin, r.top - pad - gap - card.offsetHeight);
    const left = Math.min(Math.max(margin, r.left + r.width / 2 - width / 2), innerWidth - margin - width);
    card.style.top = `${top}px`;
    card.style.left = `${left}px`;
  }

  function onKey(e) {
    if (e.key === "Escape") { e.preventDefault(); end(); }
    else if (e.key === "ArrowRight") go(1);
    else if (e.key === "ArrowLeft") go(-1);
    else if (e.key === "Tab") {
      // Keep the keyboard inside the note while the page behind is dimmed.
      const items = [...card.querySelectorAll("button, a[href]")].filter((b) => !b.hidden);
      const i = items.indexOf(document.activeElement);
      e.preventDefault();
      items[(i + (e.shiftKey ? -1 : 1) + items.length) % items.length].focus();
    }
  }

  function end() {
    document.removeEventListener("keydown", onKey);
    window.removeEventListener("resize", place);
    window.removeEventListener("scroll", place);
    blocker?.remove(); spot?.remove(); card?.remove();
    blocker = spot = card = null;
    if (openedMenu) { openedMenu = false; showMenu(false); }
    lastFocus?.focus?.();
  }

  document.querySelectorAll("[data-tour-start]").forEach((b) => b.addEventListener("click", start));
  if (document.body.dataset.tourAutostart === "true") start();
})();
