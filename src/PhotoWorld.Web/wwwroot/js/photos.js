// Progressive enhancement for photo pages: the forms all work without JavaScript; this just avoids reloads.
(() => {
  const send = async (form) => {
    const response = await fetch(form.action, {
      method: "POST",
      body: new FormData(form),
      headers: { "X-Requested-With": "fetch" },
    });
    // The session ran out: let the server redirect us to sign in.
    if (response.redirected || response.status === 401) { form.submit(); return null; }
    return response;
  };

  const setCount = (selector, delta) => {
    const el = document.querySelector(selector);
    if (el) el.textContent = String(Number(el.textContent) + delta);
  };

  document.addEventListener("submit", async (event) => {
    const form = event.target;
    const kind = form.dataset.ajax;
    if (!kind) return;
    event.preventDefault();
    const button = form.querySelector("button[type=submit]");
    if (button) button.disabled = true;

    try {
      if (kind === "like") {
        const response = await send(form);
        if (!response?.ok) return;
        const state = await response.json();
        const btn = form.querySelector(".btn-like");
        btn.classList.toggle("is-liked", state.likedByMe);
        btn.setAttribute("aria-pressed", String(state.likedByMe));
        form.querySelector("[data-like-label]").textContent = state.likedByMe ? "Liked" : "Like";
        form.querySelector("[data-like-count]").textContent = state.likeCount;
        form.querySelector("input[name=liked]").value = String(!state.likedByMe);
      }

      if (kind === "comment") {
        const error = form.querySelector("[data-comment-error]");
        const response = await send(form);
        if (!response) return;
        if (!response.ok) {
          const body = await response.json().catch(() => ({}));
          error.textContent = body.error || "Your comment couldn't be posted.";
          error.hidden = false;
          return;
        }
        error.hidden = true;
        document.getElementById("comment-list").insertAdjacentHTML("beforeend", await response.text());
        document.querySelector(".comment-empty")?.setAttribute("hidden", "");
        setCount("[data-comment-count]", 1);
        form.reset();
      }

      if (kind === "delete-comment") {
        if (!confirm("Delete this comment?")) return;
        const response = await send(form);
        if (!response?.ok) return;
        form.closest(".comment").remove();
        setCount("[data-comment-count]", -1);
        if (!document.querySelector("#comment-list .comment")) {
          document.querySelector(".comment-empty")?.removeAttribute("hidden");
        }
      }
    } finally {
      if (button) button.disabled = false;
    }
  });

  // Upload page: preview the chosen photo and catch oversized files before uploading.
  const input = document.querySelector("[data-preview-input]");
  if (input) {
    const preview = document.querySelector("[data-preview-target]");
    const hint = document.querySelector("[data-preview-hint]");
    const maxBytes = Number(input.dataset.maxBytes);

    const show = (file) => {
      if (!file) return;
      if (file.size > maxBytes) {
        input.setCustomValidity(`Photos can be up to ${Math.round(maxBytes / 1048576)} MB.`);
        input.reportValidity();
        return;
      }
      input.setCustomValidity("");
      preview.src = URL.createObjectURL(file);
      preview.hidden = false;
      hint.hidden = true;
    };

    input.addEventListener("change", () => show(input.files[0]));

    const zone = input.closest(".dropzone");
    zone.addEventListener("dragover", (e) => { e.preventDefault(); zone.classList.add("is-dragging"); });
    zone.addEventListener("dragleave", () => zone.classList.remove("is-dragging"));
    zone.addEventListener("drop", (e) => {
      e.preventDefault();
      zone.classList.remove("is-dragging");
      if (e.dataTransfer.files.length) {
        input.files = e.dataTransfer.files;
        show(input.files[0]);
      }
    });

    input.form.addEventListener("submit", () => {
      const submit = input.form.querySelector("[data-busy-label]");
      if (submit && input.form.checkValidity()) {
        submit.textContent = submit.dataset.busyLabel;
        setTimeout(() => (submit.disabled = true));
      }
    });
  }
})();
