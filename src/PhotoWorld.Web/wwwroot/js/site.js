// Buttons for irreversible actions stay disabled until the exact confirmation text is typed.
document.querySelectorAll("[data-confirm-text]").forEach((input) => {
  const button = document.querySelector(input.dataset.confirmTarget);
  input.addEventListener("input", () => {
    button.disabled = input.value.trim() !== input.dataset.confirmText;
  });
});

// Forms that need a yes/no first: the question is in data-confirm (attribute text, so names can't break the script).
document.querySelectorAll("form[data-confirm]").forEach((form) => {
  form.addEventListener("submit", (e) => {
    if (!confirm(form.dataset.confirm)) e.preventDefault();
  });
});

// Messages such as "Welcome back" close themselves after data-auto-dismiss milliseconds (they can still be closed
// with the X). The countdown waits while the pointer is over the message, so it can be read in full.
document.querySelectorAll("[data-auto-dismiss]").forEach((el) => {
  const delay = Number(el.dataset.autoDismiss) || 3000;
  let timer;
  const start = () => { timer = setTimeout(() => bootstrap.Alert.getOrCreateInstance(el).close(), delay); };
  el.addEventListener("mouseenter", () => clearTimeout(timer));
  el.addEventListener("mouseleave", start);
  start();
});

// The Activity icon in the menu: how many activity items are unread (asked once per page, after it has loaded).
const bell = document.querySelector("[data-activity-bell]");
if (bell && !location.pathname.toLowerCase().startsWith("/activity")) {
  fetch(bell.getAttribute("href").replace(/\/?$/, "/Unread"), { headers: { "X-Requested-With": "fetch" } })
    .then((r) => (r.ok ? r.json() : null))
    .then((data) => {
      if (!data || data.count <= 0) return;
      const badge = bell.querySelector("[data-activity-count]");
      badge.textContent = data.count > 99 ? "99+" : String(data.count);
      badge.hidden = false;
      bell.setAttribute("aria-label", `Activity, ${data.count} new`);
    })
    .catch(() => {});
}

// Times stored in UTC, shown in the visitor's own time zone: <time datetime="…" data-local-time="full|time">.
document.querySelectorAll("time[data-local-time]").forEach((el) => {
  const d = new Date(el.getAttribute("datetime"));
  if (Number.isNaN(d.getTime())) return;
  el.textContent = el.dataset.localTime === "time"
    ? d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })
    : d.toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
});

// Photos whose owner turned downloads off: no "Save image as…" menu and no dragging the image out. A deterrent only.
document.querySelectorAll("[data-no-save]").forEach((img) => {
  img.addEventListener("contextmenu", (e) => e.preventDefault());
  img.addEventListener("dragstart", (e) => e.preventDefault());
});

// Light/dark: the header icon (signed out) flips between the two, saving an explicit choice; the account menu's
// Light / Dark / Auto buttons and the picker in account settings also offer Automatic. All stay in step.
(() => {
  const root = document.documentElement;
  const pickers = document.querySelectorAll("[data-theme-picker]");
  const toggles = document.querySelectorAll("[data-theme-toggle]");
  const choices = document.querySelectorAll("[data-theme-choice]");
  const sync = () => {
    pickers.forEach((select) => { select.value = root.dataset.themeChoice || "auto"; });
    choices.forEach((button) => button.setAttribute("aria-pressed", button.dataset.themeChoice === (root.dataset.themeChoice || "auto")));
    const label = root.dataset.theme === "dark" ? "Switch to light mode" : "Switch to dark mode";
    toggles.forEach((button) => { button.setAttribute("aria-label", label); button.title = label; });
  };
  pickers.forEach((select) => select.addEventListener("change", () => { window.pwSetTheme(select.value); sync(); }));
  // stopPropagation keeps the account menu open, so the member sees the new theme take effect.
  choices.forEach((button) => button.addEventListener("click", (e) => { e.stopPropagation(); window.pwSetTheme(button.dataset.themeChoice); sync(); }));
  toggles.forEach((button) => button.addEventListener("click", () => {
    window.pwSetTheme(root.dataset.theme === "dark" ? "light" : "dark");
    sync();
  }));
  // Automatic follows the device, which can change while the page is open.
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change", sync);
  sync();
})();

// Cookie message and settings (_CookieBanner). The choice is kept for a year; "preferences" also lets the theme
// choice be remembered in this browser.
(() => {
  const dialog = document.getElementById("cookie-settings");
  const preferences = dialog?.querySelector("[data-cookie-preferences]");
  const choose = (value) => {
    document.cookie = `PhotoWorld.Consent=${value}; Max-Age=${60 * 60 * 24 * 365}; Path=/; SameSite=Lax${location.protocol === "https:" ? "; Secure" : ""}`;
    window.pwMoveTheme?.();
    document.querySelector("[data-cookie-banner]")?.remove();
    if (preferences) preferences.checked = value === "preferences";
  };
  document.querySelectorAll("[data-cookie-choice]").forEach((button) =>
    button.addEventListener("click", () => choose(button.dataset.cookieChoice)));
  document.querySelectorAll("[data-cookie-settings]").forEach((button) =>
    button.addEventListener("click", () => dialog?.showModal()));
  dialog?.addEventListener("close", () => {
    if (dialog.returnValue === "save") choose(preferences.checked ? "preferences" : "essential");
  });
})();
