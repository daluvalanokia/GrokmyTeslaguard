# MyTeslaGuard

ASP.NET Core 8 MVC navigation + speed-guard dashboard for Tesla.

## Features

- **Full-screen navigation map** (Leaflet + OpenStreetMap)
  - Destination address input
  - **Avoid Highways** checkbox
  - **Go** button starts route (OSRM public router) and animates progress
- **Transparent HUD overlay** on the map with all previous speed features:
  - Two glanceable circles (Posted / My Speed)
  - Color-coded overspeed bar (white / yellow / orange / red)
  - Configurable beep alerts for orange & red bands (3-min throttle)
  - Browser notifications + background-friendly audio
- PWA-ready (Add to Home Screen)

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
