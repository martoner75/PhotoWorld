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

// Theme pickers (footer, account settings): show the current choice and apply a new one straight away.
document.querySelectorAll("[data-theme-picker]").forEach((select) => {
  select.value = document.documentElement.dataset.themeChoice || "auto";
  select.addEventListener("change", () => {
    window.pwSetTheme(select.value);
    document.querySelectorAll("[data-theme-picker]").forEach((other) => { other.value = select.value; });
  });
});
