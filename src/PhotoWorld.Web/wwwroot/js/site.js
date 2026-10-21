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
