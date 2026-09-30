// MyTeslaGuard - Navigation Map + Speed HUD Overlay
// Keeps all previous speed, color bands, and beep logic

const POLL_INTERVAL_MS = 5000;
const BEEP_CHECK_INTERVAL_MS = 3 * 60 * 1000;

// Beep configuration
let beepEnabled = true;
let beepOrangeEnabled = true;
let beepRedEnabled = true;

// State
let lastBand = "none";
let lastBeepTime = 0;
let audioCtx = null;
let currentData = null;
let map = null;
let routeLayer = null;
let carMarker = null;
let isNavigating = false;
let navInterval = null;

// DOM
const postedEl = document.getElementById("postedSpeed");
const currentEl = document.getElementById("currentSpeed");
const currentCircle = document.getElementById("currentCircle");
const overValueEl = document.getElementById("overValue");
const lastUpdateEl = document.getElementById("lastUpdate");
const statusEl = document.getElementById("connectionStatus");
const navStatusEl = document.getElementById("navStatus");
const bands = document.querySelectorAll(".band");
const destInput = document.getElementById("destinationInput");
const avoidHighwaysChk = document.getElementById("avoidHighways");
const goBtn = document.getElementById("goBtn");

// ---------- Audio / Beep (unchanged behavior) ----------
function ensureAudioContext() {
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (audioCtx.state === "suspended") {
        audioCtx.resume();
    }
    return audioCtx;
}

function playBeep(type = "orange") {
    if (!beepEnabled) return;
    if (type === "orange" && !beepOrangeEnabled) return;
    if (type === "red" && !beepRedEnabled) return;

    const now = Date.now();
    if (now - lastBeepTime < BEEP_CHECK_INTERVAL_MS && lastBand === type) {
        return;
    }

    try {
        const ctx = ensureAudioContext();
        const oscillator = ctx.createOscillator();
        const gain = ctx.createGain();

        oscillator.connect(gain);
        gain.connect(ctx.destination);

        if (type === "red") {
            oscillator.frequency.value = 880;
            gain.gain.value = 0.25;
            oscillator.type = "square";
        } else {
            oscillator.frequency.value = 660;
            gain.gain.value = 0.18;
            oscillator.type = "sine";
        }

        oscillator.start();
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
        oscillator.stop(ctx.currentTime + 0.4);

        lastBeepTime = now;

        if (Notification.permission === "granted") {
            new Notification("MyTeslaGuard Alert", {
                body: type === "red" ? "⚠️ Speed 15+ mph over limit!" : "⚠️ Speed 10-15 mph over limit",
                icon: "/favicon.ico",
                silent: false
            });
        }
    } catch (e) {
        console.warn("Beep failed:", e);
    }
}

// ---------- UI Update (speed HUD + vehicle status) ----------
function setTire(el, psi, label) {
    if (!el) return;
    el.textContent = `${label} ${psi.toFixed(1)}`;
    el.classList.remove("low", "critical");
    if (psi < 32) el.classList.add("critical");
    else if (psi < 36) el.classList.add("low");
}

function updateUI(data) {
    currentData = data;

    postedEl.textContent = Math.round(data.postedSpeedLimitMph);
    currentEl.textContent = data.currentSpeedMph.toFixed(0);

    const over = data.speedOverLimitMph;
    overValueEl.textContent = over > 0 ? `${over.toFixed(1)} mph over` : "Within limit";

    // Battery / range (top corner)
    const battEl = document.getElementById("batteryPct");
    const rangeEl = document.getElementById("rangeMiles");
    if (battEl) battEl.textContent = `${data.batteryPercent.toFixed(0)}%`;
    if (rangeEl) rangeEl.textContent = `${data.rangeMiles.toFixed(0)} mi`;

    // Tire pressures
    setTire(document.getElementById("tpFl"), data.tirePressureFl, "FL");
    setTire(document.getElementById("tpFr"), data.tirePressureFr, "FR");
    setTire(document.getElementById("tpRl"), data.tirePressureRl, "RL");
    setTire(document.getElementById("tpRr"), data.tirePressureRr, "RR");

    currentCircle.classList.remove("over-yellow", "over-orange", "over-red");
    bands.forEach(b => b.classList.remove("active"));

    const band = data.overspeedBand;
    if (band && band !== "none") {
        const activeBand = document.querySelector(`.band.${band}`);
        if (activeBand) activeBand.classList.add("active");

        if (band === "yellow") currentCircle.classList.add("over-yellow");
        if (band === "orange") currentCircle.classList.add("over-orange");
        if (band === "red") currentCircle.classList.add("over-red");
    }

    if ((band === "orange" || band === "red") && band !== lastBand) {
        playBeep(band);
    }
    lastBand = band;

    lastUpdateEl.textContent = new Date(data.timestamp).toLocaleTimeString();
    statusEl.textContent = "Live";
    statusEl.classList.add("live");
}

// ---------- Polling ----------
async function pollSpeed() {
    try {
        const res = await fetch("/Home/GetSpeedData");
        if (!res.ok) throw new Error("Network error");
        const data = await res.json();
        updateUI(data);
    } catch (err) {
        statusEl.textContent = "Disconnected";
        statusEl.classList.remove("live");
        console.error(err);
    }
}

// ---------- Demo scenario ----------
async function setScenario(name) {
    try {
        await fetch("/Home/SetScenario", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ scenario: name })
        });
        pollSpeed();
    } catch (e) {
        console.error(e);
    }
}
window.setScenario = setScenario;

// ---------- Map + Navigation ----------
function initMap() {
    // Default view: approximate US center / can be overridden by geolocation
    map = L.map("map", {
        zoomControl: true,
        attributionControl: true
    }).setView([37.7749, -122.4194], 12); // San Francisco default

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    // Try browser geolocation for "current position"
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (pos) => {
                const lat = pos.coords.latitude;
                const lng = pos.coords.longitude;
                map.setView([lat, lng], 14);
                carMarker = L.marker([lat, lng], {
                    title: "My Tesla",
                    icon: L.divIcon({
                        className: "car-marker",
                        html: '<div style="background:#3fb950;width:18px;height:18px;border-radius:50%;border:3px solid white;box-shadow:0 0 8px rgba(0,0,0,0.5);"></div>',
                        iconSize: [18, 18],
                        iconAnchor: [9, 9]
                    })
                }).addTo(map).bindPopup("My Tesla (current)");
            },
            () => {
                // fallback marker
                carMarker = L.marker([37.7749, -122.4194]).addTo(map).bindPopup("My Tesla (demo)");
            },
            { enableHighAccuracy: true, timeout: 8000 }
        );
    } else {
        carMarker = L.marker([37.7749, -122.4194]).addTo(map).bindPopup("My Tesla (demo)");
    }
}

async function geocode(address) {
    // Proxy through our backend — Nominatim blocks most browser-origin calls on public sites.
    // Server sends a proper User-Agent and returns { lat, lng, display }.
    const base = (window.location.pathname.replace(/\/$/, "") || "");
    // Support both root deploy and subpath (e.g. /teslaguard)
    const candidates = [
        `${base}/Home/Geocode?q=${encodeURIComponent(address)}`,
        `/Home/Geocode?q=${encodeURIComponent(address)}`,
        `/teslaguard/Home/Geocode?q=${encodeURIComponent(address)}`
    ];

    let lastErr = null;
    for (const url of candidates) {
        try {
            const res = await fetch(url);
            if (res.status === 404) {
                lastErr = new Error("Address not found");
                // try next path only on hard 404 from wrong route; body may still be address-not-found
                const body = await res.json().catch(() => ({}));
                if (body.error === "Address not found") throw lastErr;
                continue;
            }
            if (!res.ok) {
                const body = await res.json().catch(() => ({}));
                throw new Error(body.error || body.detail || "Geocode failed");
            }
            const data = await res.json();
            if (data.lat == null || data.lng == null) throw new Error("Address not found");
            return {
                lat: parseFloat(data.lat),
                lng: parseFloat(data.lng),
                display: data.display || address
            };
        } catch (e) {
            lastErr = e;
            // network / wrong base path — try next candidate
            if (e.message === "Address not found") throw e;
        }
    }
    throw lastErr || new Error("Geocode failed");
}

async function getRoute(from, to, avoidHighways) {
    // OSRM public demo server (no key required for light use)
    // Note: real production should use your own OSRM / Mapbox / Google Directions
    let url = `https://router.project-osrm.org/route/v1/driving/${from.lng},${from.lat};${to.lng},${to.lat}?overview=full&geometries=geojson`;
    // OSRM public instance has limited exclude support; we pass a preference flag for future real engines
    if (avoidHighways) {
        // For demo we still request the route; a production engine would use exclude=motorway
        console.log("Avoid highways requested (demo: best-effort)");
    }

    const res = await fetch(url);
    if (!res.ok) throw new Error("Routing failed");
    const data = await res.json();
    if (data.code !== "Ok" || !data.routes || data.routes.length === 0) {
        throw new Error("No route found");
    }
    return data.routes[0];
}

function startNavigation(route, destDisplay) {
    if (routeLayer) {
        map.removeLayer(routeLayer);
    }

    const coords = route.geometry.coordinates.map(c => [c[1], c[0]]); // geojson is [lng,lat]
    routeLayer = L.polyline(coords, {
        color: "#58a6ff",
        weight: 6,
        opacity: 0.85
    }).addTo(map);

    map.fitBounds(routeLayer.getBounds(), { padding: [60, 60] });

    // Destination marker
    const end = coords[coords.length - 1];
    L.marker(end).addTo(map).bindPopup(`Destination<br>${destDisplay}`).openPopup();

    isNavigating = true;
    navStatusEl.textContent = `Navigating • ${(route.distance / 1609.34).toFixed(1)} mi`;
    goBtn.textContent = "Stop";

    // Simple simulated progress along route (for visual feedback)
    let idx = 0;
    if (navInterval) clearInterval(navInterval);
    navInterval = setInterval(() => {
        if (!isNavigating || idx >= coords.length) {
            clearInterval(navInterval);
            if (idx >= coords.length) {
                navStatusEl.textContent = "Arrived";
                goBtn.textContent = "Go";
                isNavigating = false;
            }
            return;
        }
        if (carMarker) {
            carMarker.setLatLng(coords[idx]);
        }
        idx += Math.max(1, Math.floor(coords.length / 80)); // rough progress steps
    }, 1200);
}

function stopNavigation() {
    isNavigating = false;
    if (navInterval) clearInterval(navInterval);
    navStatusEl.textContent = "Ready";
    goBtn.textContent = "Go";
}

// ---------- Go button ----------
async function onGoClick() {
    if (isNavigating) {
        stopNavigation();
        return;
    }

    const address = (destInput.value || "").trim();
    if (!address) {
        destInput.focus();
        navStatusEl.textContent = "Enter a destination";
        return;
    }

    goBtn.disabled = true;
    navStatusEl.textContent = "Searching...";

    try {
        const dest = await geocode(address);
        const from = carMarker ? carMarker.getLatLng() : map.getCenter();
        const fromPt = { lat: from.lat, lng: from.lng };

        navStatusEl.textContent = "Calculating route...";
        const route = await getRoute(fromPt, dest, avoidHighwaysChk.checked);
        startNavigation(route, dest.display);
    } catch (err) {
        console.error(err);
        navStatusEl.textContent = err.message || "Navigation error";
        goBtn.textContent = "Go";
        isNavigating = false;
    } finally {
        goBtn.disabled = false;
    }
}

// ---------- Init ----------
document.addEventListener("DOMContentLoaded", () => {
    // Beep toggles
    document.getElementById("beepEnabled").addEventListener("change", e => {
        beepEnabled = e.target.checked;
    });
    document.getElementById("beepOrange").addEventListener("change", e => {
        beepOrangeEnabled = e.target.checked;
    });
    document.getElementById("beepRed").addEventListener("change", e => {
        beepRedEnabled = e.target.checked;
    });

    if ("Notification" in window && Notification.permission === "default") {
        Notification.requestPermission();
    }

    document.body.addEventListener("click", () => ensureAudioContext(), { once: true });
    document.body.addEventListener("touchstart", () => ensureAudioContext(), { once: true });

    goBtn.addEventListener("click", onGoClick);
    destInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter") onGoClick();
    });

    initMap();

    // Start speed polling
    pollSpeed();
    setInterval(pollSpeed, POLL_INTERVAL_MS);
});
