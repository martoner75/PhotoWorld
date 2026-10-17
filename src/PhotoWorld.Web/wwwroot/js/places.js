// The "My places" map: pins for every saved place, plus a draggable pin for the one being added or edited.
// Saving is a normal form post; this script fills in the coordinates and keeps the map and list in step.
(() => {
  const places = JSON.parse(document.getElementById("places-data").textContent);
  const byId = new Map(places.map((p) => [p.id, p]));
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  const form = document.getElementById("place-form");
  const field = (name) => form.elements.namedItem(name);
  const addButton = document.querySelector("[data-place-add]");
  const hint = form.querySelector("[data-place-coords]");
  const searchInput = document.getElementById("place-search");
  const searchButton = form.querySelector("[data-place-search]");
  const results = form.querySelector("[data-place-results]");
  const title = document.getElementById("place-form-title");

  // ----- Map -----
  const map = L.map("places-map", { worldCopyJump: true, zoomControl: true });
  L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);

  const pin = (pending) => L.divIcon({
    className: pending ? "place-pin is-pending" : "place-pin",
    html: "<span></span>",
    iconSize: [28, 28],
    iconAnchor: [14, 28],
    popupAnchor: [0, -26],
  });

  const move = (latLng, zoom) => {
    if (reduceMotion) map.setView(latLng, zoom ?? map.getZoom());
    else map.flyTo(latLng, zoom ?? map.getZoom(), { duration: 0.8 });
  };

  const formatDate = (iso) => new Date(`${iso}T00:00:00`)
    .toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });

  // Built with DOM nodes, never HTML strings: names and notes are whatever the member typed.
  const popupFor = (place) => {
    const box = document.createElement("div");
    box.className = "place-popup";
    const name = document.createElement("strong");
    name.textContent = place.name;
    box.append(name);
    if (place.visitedOn) {
      const date = document.createElement("span");
      date.className = "text-muted";
      date.textContent = formatDate(place.visitedOn);
      box.append(date);
    }
    if (place.note) {
      const note = document.createElement("p");
      note.textContent = place.note;
      box.append(note);
    }
    const edit = document.createElement("button");
    edit.type = "button";
    edit.className = "btn btn-ghost btn-sm";
    edit.textContent = "Edit";
    edit.addEventListener("click", () => openForm(place));
    box.append(edit);
    return box;
  };

  const listItem = (id) => document.querySelector(`.place-item[data-place-id="${id}"]`);
  const highlight = (id) => {
    document.querySelectorAll(".place-item.is-active").forEach((el) => el.classList.remove("is-active"));
    const item = listItem(id);
    if (!item) return;
    item.classList.add("is-active");
    item.scrollIntoView({ block: "nearest", behavior: reduceMotion ? "auto" : "smooth" });
  };

  const markers = new Map();
  for (const place of places) {
    const marker = L.marker([place.latitude, place.longitude], { icon: pin(false), title: place.name, keyboard: true })
      .bindPopup(() => popupFor(place))
      .on("click", () => highlight(place.id))
      .addTo(map);
    markers.set(place.id, marker);
  }

  const showPlace = (id) => {
    const marker = markers.get(id);
    if (!marker) return;
    move(marker.getLatLng(), Math.max(map.getZoom(), 8));
    marker.openPopup();
    highlight(id);
  };

  if (places.length > 0) {
    map.fitBounds(L.latLngBounds(places.map((p) => [p.latitude, p.longitude])), { padding: [40, 40], maxZoom: 9 });
  } else {
    map.setView([25, 10], 2);
  }

  // ----- The pin being placed -----
  let pending = null;
  let editingMarker = null;

  const setPending = (lat, lng) => {
    const latLng = L.latLng(lat, lng).wrap();
    if (!pending) {
      pending = L.marker(latLng, { icon: pin(true), draggable: true, autoPan: true, title: "New place" })
        .on("dragend", () => setPending(pending.getLatLng().lat, pending.getLatLng().lng))
        .addTo(map);
    } else {
      pending.setLatLng(latLng);
    }
    field("Latitude").value = latLng.lat.toFixed(5);
    field("Longitude").value = latLng.lng.toFixed(5);
    hint.textContent = `Pinned at ${latLng.lat.toFixed(4)}, ${latLng.lng.toFixed(4)}. Drag the pin to adjust.`;
  };

  const clearPending = () => {
    pending?.remove();
    pending = null;
    if (editingMarker) editingMarker.addTo(map);
    editingMarker = null;
  };

  // ----- Form -----
  function openForm(place) {
    map.closePopup();
    clearPending();
    form.reset();
    for (const name of ["Id", "Name", "VisitedOn", "Note", "Latitude", "Longitude"]) field(name).value = "";
    form.querySelectorAll(".field-error, .field-validation-error").forEach((el) => { el.textContent = ""; });
    form.querySelector(".validation-summary-errors")?.setAttribute("hidden", "");
    results.hidden = true;
    hint.textContent = "…or click the map to drop a pin. You can drag it to adjust.";

    if (place) {
      title.textContent = "Edit place";
      field("Id").value = place.id;
      field("Name").value = place.name;
      field("VisitedOn").value = place.visitedOn ?? "";
      field("Note").value = place.note ?? "";
      // Swap the saved pin for a draggable one until the edit is saved or cancelled.
      editingMarker = markers.get(place.id)?.remove() ?? null;
      setPending(place.latitude, place.longitude);
      move([place.latitude, place.longitude], Math.max(map.getZoom(), 8));
    } else {
      title.textContent = "Add a place";
    }
    form.hidden = false;
    addButton.hidden = true;
    (place ? field("Name") : searchInput).focus();
  }

  const closeForm = () => {
    clearPending();
    form.hidden = true;
    addButton.hidden = false;
    addButton.focus();
  };

  addButton.addEventListener("click", () => openForm(null));
  form.querySelector("[data-place-cancel]").addEventListener("click", closeForm);

  // Back from the server with validation errors: put the pin back where it was.
  if (!form.hidden && field("Latitude").value && field("Longitude").value) {
    const id = field("Id").value;
    if (id && markers.has(id)) editingMarker = markers.get(id).remove();
    setPending(Number(field("Latitude").value), Number(field("Longitude").value));
    map.setView(pending.getLatLng(), Math.max(map.getZoom(), 8));
  }

  // ----- Place search and naming (OpenStreetMap Nominatim; only runs when the member asks) -----
  const nominatim = async (path, params) => {
    const url = new URL(`https://nominatim.openstreetmap.org/${path}`);
    url.search = new URLSearchParams({
      format: "jsonv2", addressdetails: "1", "accept-language": navigator.language, ...params,
    }).toString();
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Nominatim ${response.status}`);
    return response.json();
  };

  const shortName = (result) => {
    const a = result.address ?? {};
    const local = result.name || a.city || a.town || a.village || a.municipality || a.county || a.state;
    return [...new Set([local, a.country].filter(Boolean))].join(", ") || result.display_name || "";
  };

  const showMessage = (text) => {
    const li = document.createElement("li");
    li.className = "place-results-message";
    li.textContent = text;
    results.replaceChildren(li);
    results.hidden = false;
  };

  const search = async () => {
    const q = searchInput.value.trim();
    if (!q) { searchInput.focus(); return; }
    searchButton.disabled = true;
    try {
      const found = await nominatim("search", { q, limit: "6" });
      if (found.length === 0) { showMessage("No places found. Try another spelling, or click the map."); return; }
      results.replaceChildren(...found.map((r) => {
        const li = document.createElement("li");
        const button = document.createElement("button");
        button.type = "button";
        button.textContent = r.display_name;
        button.addEventListener("click", () => {
          setPending(Number(r.lat), Number(r.lon));
          const [south, north, west, east] = r.boundingbox.map(Number);
          map.fitBounds([[south, west], [north, east]], { maxZoom: 12, animate: !reduceMotion });
          if (!field("Name").value.trim()) field("Name").value = shortName(r);
          results.hidden = true;
          field("Name").focus();
        });
        li.append(button);
        return li;
      }));
      results.hidden = false;
    } catch {
      showMessage("Search isn't available right now. You can still click the map to drop a pin.");
    } finally {
      searchButton.disabled = false;
    }
  };

  searchButton.addEventListener("click", search);
  searchInput.addEventListener("keydown", (event) => {
    if (event.key !== "Enter") return;
    event.preventDefault(); // Enter searches; it mustn't submit the half-filled form.
    search();
  });

  // Clicking the map drops (or moves) the pin, and suggests a name if there isn't one yet.
  // A click that just dismissed a popup (Leaflet closes it on the same click) shouldn't also drop a pin.
  let popupClosedAt = 0;
  map.on("popupclose", () => { popupClosedAt = performance.now(); });

  let lookup = 0;
  map.on("click", async (event) => {
    if (performance.now() - popupClosedAt < 250) return;
    if (form.hidden) openForm(null);
    setPending(event.latlng.lat, event.latlng.lng);
    if (field("Name").value.trim()) return;

    const ticket = ++lookup;
    try {
      const found = await nominatim("reverse", { lat: event.latlng.lat, lon: event.latlng.lng, zoom: "10" });
      if (ticket === lookup && !found.error && !field("Name").value.trim()) field("Name").value = shortName(found);
    } catch {
      // Naming is a nicety; the member can type it.
    }
  });

  // ----- List -----
  document.querySelectorAll(".place-item").forEach((item) => {
    const place = byId.get(item.dataset.placeId);
    item.querySelector("[data-place-show]").addEventListener("click", () => showPlace(place.id));
    item.querySelector("[data-place-edit]").addEventListener("click", () => openForm(place));
  });

  // After saving, the server sends us back with ?focus=<id>.
  const focus = new URLSearchParams(location.search).get("focus");
  if (focus && markers.has(focus)) {
    map.setView(markers.get(focus).getLatLng(), Math.max(map.getZoom(), 6));
    markers.get(focus).openPopup();
    highlight(focus);
  }
})();
