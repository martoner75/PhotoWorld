// Photo walks: the map of upcoming walks, the meeting-point map on a walk, and the form's pin and local times.
(() => {
  const tiles = (map) => L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);
  const pin = () => L.divIcon({ className: "place-pin", html: "<span></span>", iconSize: [28, 28], iconAnchor: [14, 28], popupAnchor: [0, -26] });

  // List page: one pin per upcoming walk.
  const listMap = document.getElementById("walks-map");
  if (listMap) {
    const walks = JSON.parse(document.getElementById("walks-data").textContent);
    const map = L.map(listMap, { worldCopyJump: true });
    tiles(map);
    const markers = walks.map((w) => {
      const box = document.createElement("div");
      box.className = "place-popup";
      const a = document.createElement("a");
      a.href = `/Walks/Details/${w.id}`; a.textContent = w.title;
      const when = document.createElement("span");
      when.textContent = new Date(w.startsAt).toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
      const where = document.createElement("span");
      where.className = "text-muted"; where.textContent = w.meetingPoint;
      box.append(a, when, where);
      return L.marker([w.latitude, w.longitude], { icon: pin(), title: w.title }).bindPopup(box).addTo(map);
    });
    if (markers.length) map.fitBounds(L.featureGroup(markers).getBounds().pad(0.3), { maxZoom: 13 });
    else map.setView([25, 10], 2);
  }

  // A walk's page: its meeting point.
  const walkMap = document.getElementById("walk-map");
  if (walkMap) {
    const at = [Number(walkMap.dataset.lat), Number(walkMap.dataset.lng)];
    const map = L.map(walkMap, { scrollWheelZoom: false }).setView(at, 15);
    tiles(map);
    L.marker(at, { icon: pin() }).addTo(map);
  }

  // The form: click to set the pin, or pick one of your places; send the member's time zone with the local time.
  const form = document.querySelector("[data-walk-form]");
  if (form) {
    const lat = form.querySelector("[data-walk-lat]"), lng = form.querySelector("[data-walk-lng]");
    const name = form.querySelector("#MeetingPoint");
    const pickMap = L.map("walk-pick-map", { worldCopyJump: true });
    tiles(pickMap);
    let marker = null;
    const place = (latLng, zoom) => {
      if (marker) marker.setLatLng(latLng); else marker = L.marker(latLng, { icon: pin() }).addTo(pickMap);
      lat.value = latLng[0].toFixed(6); lng.value = latLng[1].toFixed(6);
      pickMap.setView(latLng, zoom ?? Math.max(pickMap.getZoom(), 15));
    };
    if (lat.value && lng.value) place([Number(lat.value), Number(lng.value)], 15);
    else pickMap.setView([51.5, -0.12], 5);
    pickMap.on("click", (e) => place([e.latlng.lat, e.latlng.lng]));

    form.querySelector("[data-walk-place]")?.addEventListener("change", (e) => {
      const option = e.target.selectedOptions[0];
      if (!option.value) return;
      const [a, b] = option.value.split(",").map(Number);
      place([a, b], 15);
      if (!name.value.trim()) name.value = option.dataset.name;
    });

    // Editing: show the stored start in the member's own time.
    const starts = form.querySelector("#starts-local");
    if (starts.dataset.startsUtc && !starts.value) {
      const d = new Date(starts.dataset.startsUtc);
      const pad = (n) => String(n).padStart(2, "0");
      starts.value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
    form.addEventListener("submit", () => {
      const local = starts.value ? new Date(starts.value) : new Date();
      form.querySelector("[data-utc-offset]").value = String(-local.getTimezoneOffset());
    });
  }
})();
