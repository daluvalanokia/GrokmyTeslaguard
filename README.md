# MyTeslaGuard

ASP.NET Core 8 MVC dashboard for monitoring Tesla posted speed limit vs current vehicle speed.

## Features

- **Two large glanceable circles**
  - Left: Posted speed limit
  - Right: Current vehicle speed (color changes with overspeed)
- **Color-coded overspeed bar** (4 bands of 5 mph):
  - 0–5 mph over → white
  - 5–10 mph over → light yellow
  - 10–15 mph over → light orange
  - 15–20+ mph over → light red
- **Beep alerts** (configurable)
  - Beeps when entering the **orange** or **red** band
  - Independent toggles for each level
  - Throttled to once every **3 minutes** per band transition (configurable in JS)
  - Uses Web Audio API + optional browser Notification
- **Background / not-in-focus support** (best-effort)
  - Browser Notifications when permission granted
  - PWA manifest for “Add to Home Screen”
  - Audio continues while tab is open (even if not focused)
  - True silent background is limited by browser policies; for always-on consider a native wrapper

## Quick Start

```bash
cd myteslaguard
dotnet restore
dotnet run
```

Open https://localhost:5xxx (or the URL shown) in a browser.

Use the demo buttons (City / Highway / Overspeed) to simulate different speeds.

## Configuration

- `appsettings.json` → `Tesla:UseMockData` (true by default)
- `wwwroot/js/site.js`:
  - `POLL_INTERVAL_MS` (default 5000)
  - `BEEP_CHECK_INTERVAL_MS` (default 3 minutes)
  - `beepEnabled`, `beepOrangeEnabled`, `beepRedEnabled`

## Real Tesla Integration

Replace `MockTeslaDataService` with a real implementation that calls:

- Tesla Fleet API / Vehicle Data endpoints
- Requires OAuth tokens + vehicle ID

See Tesla Developer documentation for the latest endpoints (`vehicle_data`, `drive_state`, etc.).

## Running while driving

1. **Phone browser (recommended)**  
   Open the site → Add to Home Screen → keep the tab open. Mount the phone. Glanceable design + optional beeps/notifications.

2. **Laptop / home server + phone**  
   Run `dotnet run` on a machine reachable from the car’s Wi-Fi/hotspot. Phone connects to the local IP.

3. **Background limitations**  
   Modern browsers suspend background tabs aggressively. Notifications + keeping the page open give the best practical experience. For guaranteed background beeps consider packaging as a PWA with a service worker or a lightweight native shell (Capacitor, etc.).

## Safety

This is a secondary aid only. Always keep primary attention on the road. Do not rely solely on visual or audio alerts while driving.
