// Blog post editor: Quill with a photo block that embeds the author's own PhotoWorld photos, plus a cover photo choice.
// The API re-sanitizes everything on save, so this is about a pleasant editor, not security.
(() => {
  const script = document.currentScript;
  const photosUrl = script.dataset.photosUrl;
  const uploadUrl = script.dataset.uploadUrl;
  const form = document.getElementById("post-form");
  const bodyInput = document.getElementById("body-html");
  const token = form.querySelector("input[name=__RequestVerificationToken]").value;

  // <figure class="pw-photo" data-photo-id="…"><img src alt><figcaption>…</figcaption></figure>
  const BlockEmbed = Quill.import("blots/block/embed");
  class PhotoBlot extends BlockEmbed {
    static blotName = "photo";
    static tagName = "figure";
    static className = "pw-photo";

    static create(value) {
      const node = super.create();
      node.setAttribute("data-photo-id", value.id);
      node.setAttribute("contenteditable", "false");
      const img = document.createElement("img");
      img.src = value.src;
      img.alt = value.alt || "";
      node.appendChild(img);
      if (value.caption) {
        const caption = document.createElement("figcaption");
        caption.textContent = value.caption;
        node.appendChild(caption);
      }
      return node;
    }

    static value(node) {
      const img = node.querySelector("img");
      return {
        id: node.getAttribute("data-photo-id"),
        src: img?.getAttribute("src"),
        alt: img?.getAttribute("alt") || "",
        caption: node.querySelector("figcaption")?.textContent || "",
      };
    }
  }
  Quill.register(PhotoBlot);
  Quill.import("ui/icons").photo =
    '<svg viewBox="0 0 18 18"><rect class="ql-stroke" height="10" width="12" x="3" y="4"></rect>' +
    '<circle class="ql-fill" cx="7" cy="8" r="1"></circle><polyline class="ql-even ql-fill" points="5 12 5 11 7 9 8 10 11 7 13 9 13 12 5 12"></polyline></svg>';

  const quill = new Quill("#editor", {
    theme: "snow",
    placeholder: "Tell the story behind your photos…",
    modules: {
      toolbar: {
        container: [
          [{ header: [2, 3, false] }],
          ["bold", "italic", "underline", "strike"],
          [{ list: "ordered" }, { list: "bullet" }],
          ["blockquote", "link"],
          ["photo"],
          ["clean"],
        ],
        handlers: { photo: () => openPicker("insert") },
      },
    },
  });

  if (bodyInput.value) quill.clipboard.dangerouslyPasteHTML(bodyInput.value);

  form.addEventListener("submit", () => {
    // Quill 2 writes spaces as &nbsp; in semantic HTML, which stops text wrapping; keep normal spaces.
    bodyInput.value = quill.getSemanticHTML().replaceAll("&nbsp;", " ");
  });

  // ----- Photo picker -----
  const modalEl = document.getElementById("photo-picker");
  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
  const grid = modalEl.querySelector("[data-picker-grid]");
  const empty = modalEl.querySelector("[data-picker-empty]");
  const more = modalEl.querySelector("[data-picker-more]");
  const captionInput = modalEl.querySelector("#picker-caption");
  const uploadForm = modalEl.querySelector("[data-picker-upload]");
  const uploadError = modalEl.querySelector("[data-picker-error]");
  const uploadButton = modalEl.querySelector("[data-picker-submit]");
  const title = modalEl.querySelector("#photo-picker-title");
  const captionField = modalEl.querySelector(".picker-caption");
  let insertAt = 0;
  let page = 0;
  // "insert" puts the chosen photo in the post; "cover" makes it the post's cover.
  let mode = "insert";

  function openPicker(nextMode = "insert") {
    mode = nextMode;
    if (mode === "insert") insertAt = (quill.getSelection(true) ?? { index: quill.getLength() }).index;
    title.textContent = mode === "cover" ? "Choose a cover photo" : "Add a photo";
    uploadButton.textContent = uploadLabel();
    captionField.hidden = mode === "cover";
    captionInput.value = "";
    if (page === 0) loadPage();
    modal.show();
  }

  const uploadLabel = () => (mode === "cover" ? "Upload and use" : "Upload and add");

  async function loadPage() {
    page += 1;
    const response = await fetch(`${photosUrl}?page=${page}`, { headers: { "X-Requested-With": "fetch" } });
    if (!response.ok) return;
    const data = await response.json();
    for (const photo of data.items) addTile(photo);
    empty.hidden = grid.children.length > 0;
    more.hidden = !data.hasNext;
  }

  function addTile(photo, prepend = false) {
    const tile = document.createElement("button");
    tile.type = "button";
    tile.className = "picker-tile";
    tile.title = photo.title;
    const img = document.createElement("img");
    img.src = photo.thumbnailUrl;
    img.alt = photo.title;
    img.loading = "lazy";
    tile.appendChild(img);
    tile.addEventListener("click", () => choose(photo));
    prepend ? grid.prepend(tile) : grid.appendChild(tile);
  }

  function choose(photo) {
    if (mode === "cover") setCover(photo.id, photo.thumbnailUrl);
    else {
      quill.insertEmbed(insertAt, "photo",
        { id: photo.id, src: photo.thumbnailUrl, alt: photo.title, caption: captionInput.value.trim() }, "user");
      quill.setSelection(insertAt + 1, 0, "user");
    }
    modal.hide();
  }

  more.addEventListener("click", loadPage);

  uploadForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const button = uploadForm.querySelector("button[type=submit]");
    button.disabled = true;
    button.textContent = "Uploading…";
    uploadError.hidden = true;
    try {
      const data = new FormData(uploadForm);
      data.append("__RequestVerificationToken", token);
      const response = await fetch(uploadUrl, { method: "POST", body: data, headers: { "X-Requested-With": "fetch" } });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        uploadError.textContent = body.error || "The upload didn't work. Try again.";
        uploadError.hidden = false;
        return;
      }
      addTile(body, true);
      empty.hidden = true;
      uploadForm.reset();
      choose(body);
    } finally {
      button.disabled = false;
      button.textContent = uploadLabel();
    }
  });

  // ----- Cover photo (the post's tile in blog lists; empty means the first photo in the post) -----
  const cover = document.querySelector("[data-cover]");
  const coverId = document.getElementById("cover-photo-id");
  const coverUrl = document.getElementById("cover-url");
  const coverImg = cover.querySelector("[data-cover-img]");
  const coverAuto = cover.querySelector("[data-cover-auto]");
  const coverClear = cover.querySelector("[data-cover-clear]");

  function setCover(id, url) {
    coverId.value = id || "";
    coverUrl.value = url || "";
    if (url) coverImg.src = url;
    coverImg.hidden = !url;
    coverAuto.hidden = !!url;
    coverClear.hidden = !url;
  }

  cover.querySelector("[data-cover-choose]").addEventListener("click", () => openPicker("cover"));
  coverClear.addEventListener("click", () => setCover(null, null));
})();
