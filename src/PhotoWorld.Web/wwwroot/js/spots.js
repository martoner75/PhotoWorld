// The Spots map: one pin per place, showing the photos taken there. Built with DOM nodes, never HTML strings,
// because titles and place names are whatever members typed.
(() => {
  const { spots, focus } = JSON.parse(document.getElementById("spots-data").textContent);
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  const map = L.map("spots-map", { worldCopyJump: true });
  L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);

  // Photos at the same coordinates share a pin.
  const groups = new Map();
  for (const spot of spots) {
    const key = `${spot.latitude.toFixed(5)},${spot.longitude.toFixed(5)}`;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(spot);
  }

  const icon = (count) => L.divIcon({
    className: "place-pin spot-pin",
    html: count > 1 ? `<span></span><b>${count}</b>` : "<span></span>",
    iconSize: [28, 28],
    iconAnchor: [14, 28],
    popupAnchor: [0, -26],
  });

  const popupFor = (photos) => {
    const box = document.createElement("div");
    box.className = "spot-popup";
    const name = document.createElement("strong");
    name.textContent = photos[0].placeName;
    box.append(name);
    const grid = document.createElement("div");
    grid.className = "spot-photos";
    for (const p of photos.slice(0, 12)) {
      const link = document.createElement("a");
      link.href = `/Photos/Details/${p.photoId}`;
      link.title = `${p.title} by ${p.ownerName}`;
      const img = document.createElement("img");
      img.src = p.thumbnailUrl;
      img.alt = p.title;
      img.loading = "lazy";
      link.append(img);
      grid.append(link);
    }
    box.append(grid);
    if (photos.length > 12) {
      const more = document.createElement("span");
      more.className = "text-muted small";
      more.textContent = `and ${photos.length - 12} more`;
      box.append(more);
    }
    return box;
  };

  const markers = [];
  let focused = null;
  for (const photos of groups.values()) {
    const marker = L.marker([photos[0].latitude, photos[0].longitude], { icon: icon(photos.length), title: photos[0].placeName })
      .bindPopup(() => popupFor(photos), { maxWidth: 320 })
      .addTo(map);
    markers.push(marker);
    if (focus && photos.some((p) => p.photoId === focus)) focused = marker;
  }

  if (focused) {
    map.setView(focused.getLatLng(), 12, { animate: !reduceMotion });
    focused.openPopup();
  } else if (markers.length > 0) {
    map.fitBounds(L.featureGroup(markers).getBounds().pad(0.2), { maxZoom: 12 });
  } else {
    map.setView([25, 10], 2);
  }
})();
