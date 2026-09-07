// MyTeslaGuard - Frontend logic
// Beep alerts for orange / red bands + background support

const POLL_INTERVAL_MS = 5000;           // Poll vehicle data every 5 seconds
const BEEP_CHECK_INTERVAL_MS = 3 * 60 * 1000; // 3 minutes – used to throttle repeated beeps

// Beep configuration variables (user can toggle)
let beepEnabled = true;
let beepOrangeEnabled = true;
let beepRedEnabled = true;

// State
let lastBand = "none";
let lastBeepTime = 0;
let audioCtx = null;
let currentData = null;

// DOM elements
const postedEl = document.getElementById("postedSpeed");
const currentEl = document.getElementById("currentSpeed");
const currentCircle = document.getElementById("currentCircle");
const overValueEl = document.getElementById("overValue");
const lastUpdateEl = document.getElementById("lastUpdate");
const statusEl = document.getElementById("connectionStatus");
const bands = document.querySelectorAll(".band");

// ---------- Audio / Beep ----------
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
    // Throttle: do not beep more often than every 3 minutes for the same band transition logic
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
            // Stronger / higher pitch for red
            oscillator.frequency.value = 880;
            gain.gain.value = 0.25;
            oscillator.type = "square";
        } else {
            // Orange
            oscillator.frequency.value = 660;
            gain.gain.value = 0.18;
            oscillator.type = "sine";
        }

        oscillator.start();
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
        oscillator.stop(ctx.currentTime + 0.4);

        lastBeepTime = now;

        // Also try a system notification if permission granted (helps when tab is backgrounded)
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

// ---------- UI Update ----------
function updateUI(data) {
    currentData = data;

    postedEl.textContent = Math.round(data.postedSpeedLimitMph);
    currentEl.textContent = data.currentSpeedMph.toFixed(0);

    const over = data.speedOverLimitMph;
    overValueEl.textContent = over > 0
        ? `${over.toFixed(1)} mph over`
        : "Within limit";

    // Reset circle classes
    currentCircle.classList.remove("over-yellow", "over-orange", "over-red");

    // Highlight active band
    bands.forEach(b => b.classList.remove("active"));

    const band = data.overspeedBand;
    if (band && band !== "none") {
        const activeBand = document.querySelector(`.band.${band}`);
        if (activeBand) activeBand.classList.add("active");

        if (band === "yellow") currentCircle.classList.add("over-yellow");
        if (band === "orange") currentCircle.classList.add("over-orange");
        if (band === "red") currentCircle.classList.add("over-red");
    }

    // Beep logic: only when entering orange or red (or re-entering after leaving)
    if ((band === "orange" || band === "red") && band !== lastBand) {
        playBeep(band);
    }
    lastBand = band;

    lastUpdateEl.textContent = `Last update: ${new Date(data.timestamp).toLocaleTimeString()}`;
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
        // Force immediate refresh
        pollSpeed();
    } catch (e) {
        console.error(e);
    }
}

// ---------- Init ----------
document.addEventListener("DOMContentLoaded", () => {
    // Wire up checkboxes
    document.getElementById("beepEnabled").addEventListener("change", e => {
        beepEnabled = e.target.checked;
    });
    document.getElementById("beepOrange").addEventListener("change", e => {
        beepOrangeEnabled = e.target.checked;
    });
    document.getElementById("beepRed").addEventListener("change", e => {
        beepRedEnabled = e.target.checked;
    });

    // Request notification permission early (needed for background alerts)
    if ("Notification" in window && Notification.permission === "default") {
        Notification.requestPermission();
    }

    // Unlock audio on first user gesture (required by browsers)
    document.body.addEventListener("click", () => ensureAudioContext(), { once: true });
    document.body.addEventListener("touchstart", () => ensureAudioContext(), { once: true });

    // Start polling
    pollSpeed();
    setInterval(pollSpeed, POLL_INTERVAL_MS);

    // Keep tab somewhat alive for background beeps (best-effort)
    // Note: true background execution is limited by browser policies.
    // For real background, consider a native wrapper (Capacitor / PWA + service worker) or native app.
});

// Expose for buttons
window.setScenario = setScenario;
