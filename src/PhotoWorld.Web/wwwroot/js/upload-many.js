// "Upload several photos": a title per photo, everything else shared; photos go up one at a time so a failure only
// costs that photo, and finished ones are skipped if the member tries again. Built with DOM nodes, never HTML strings.
(() => {
  const form = document.querySelector("[data-upload-many]");
  if (!form) return;
  const input = form.querySelector("[data-many-input]");
  const list = form.querySelector("[data-many-list]");
  const submit = form.querySelector("[data-many-submit]");
  const error = form.querySelector("[data-many-error]");
  const progress = form.querySelector("[data-many-progress]");
  const maxFiles = Number(form.dataset.maxFiles), maxBytes = Number(form.dataset.maxBytes);
  let items = [];

  const titleFrom = (name) => name.replace(/\.[^.]+$/, "").replace(/[_-]+/g, " ").trim().slice(0, 120);
  const showError = (text) => { error.textContent = text; error.hidden = !text; };

  const render = () => {
    list.replaceChildren();
    for (const item of items) {
      const li = document.createElement("li");
      li.className = "upload-item" + (item.done ? " is-done" : "");
      const img = document.createElement("img");
      img.src = item.preview; img.alt = "";
      const fields = document.createElement("div");
      fields.className = "upload-item-fields";
      const title = document.createElement("input");
      title.value = item.title; title.maxLength = 120; title.required = true; title.disabled = item.done;
      title.setAttribute("aria-label", `Title for ${item.file.name}`);
      title.addEventListener("input", () => { item.title = title.value; });
      const status = document.createElement("span");
      status.className = "upload-item-status small";
      status.textContent = item.status;
      fields.append(title, status);
      const remove = document.createElement("button");
      remove.type = "button"; remove.className = "btn-link-muted"; remove.textContent = "Remove"; remove.hidden = item.done;
      remove.addEventListener("click", () => { items = items.filter((i) => i !== item); render(); });
      li.append(img, fields, remove);
      list.append(li);
    }
    list.hidden = items.length === 0;
    submit.disabled = !items.some((i) => !i.done);
  };

  const add = (files) => {
    showError("");
    for (const file of files) {
      if (items.length >= maxFiles) { showError(`Up to ${maxFiles} photos at once.`); break; }
      if (file.size > maxBytes) { showError(`${file.name} is too large (up to ${Math.round(maxBytes / 1048576)} MB).`); continue; }
      items.push({ file, title: titleFrom(file.name), preview: URL.createObjectURL(file), status: "", done: false });
    }
    render();
  };

  input.addEventListener("change", () => { add(input.files); input.value = ""; });
  const zone = input.closest(".dropzone");
  zone.addEventListener("dragover", (e) => { e.preventDefault(); zone.classList.add("is-dragging"); });
  zone.addEventListener("dragleave", () => zone.classList.remove("is-dragging"));
  zone.addEventListener("drop", (e) => { e.preventDefault(); zone.classList.remove("is-dragging"); add(e.dataTransfer.files); });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const todo = items.filter((i) => !i.done);
    if (todo.some((i) => !i.title.trim())) { showError("Give every photo a title."); return; }
    showError("");
    submit.disabled = true;
    const shared = new FormData(form);
    let failed = 0, last = null;
    for (let n = 0; n < todo.length; n++) {
      const item = todo[n];
      progress.hidden = false;
      progress.textContent = `Uploading ${n + 1} of ${todo.length}…`;
      item.status = "Uploading…"; render();
      const body = new FormData();
      for (const [key, value] of shared) if (typeof value === "string") body.append(key, value);
      body.append("photo", item.file);
      body.append("title", item.title.trim());
      try {
        const response = await fetch(form.action, { method: "POST", body, headers: { "X-Requested-With": "fetch" } });
        if (response.redirected) { location.href = response.url; return; } // signed out
        const data = await response.json().catch(() => ({}));
        if (response.ok) { item.done = true; item.status = "✓ Uploaded"; last = data.url; }
        else { item.status = data.error || "That photo couldn't be uploaded."; failed++; }
      } catch {
        item.status = "Couldn't reach PhotoWorld. Try again."; failed++;
      }
      render();
    }
    progress.hidden = true;
    if (failed === 0) {
      location.href = todo.length === 1 && last ? last : form.dataset.done || "/Members/Me";
    } else {
      showError(`${todo.length - failed} of ${todo.length} uploaded. Fix the others (see the list) and press Share again.`);
      submit.disabled = false;
    }
  });
})();
