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
