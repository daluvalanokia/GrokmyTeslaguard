using System.Net.Http.Headers;
using System.Text.Json;
using MyTeslaGuard.Models;

namespace MyTeslaGuard.Services
{
    /// <summary>
    /// Live Tesla data via Fleet API vehicle_data endpoint.
    /// Docs: https://developer.tesla.com / Fleet API vehicle endpoints
    ///
    /// Requires:
    /// - Tesla developer application + OAuth access token (vehicle_device_data, vehicle_location scopes)
    /// - Vehicle ID or VIN the token is authorized for
    ///
    /// Note: Browser Bluetooth to Tesla is not available. In-car browser can only use network APIs.
    /// For sub-second streaming, use Fleet Telemetry (separate streaming infra) instead of polling vehicle_data.
    /// </summary>
    public class FleetTeslaDataService : ITeslaDataService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<FleetTeslaDataService> _logger;
        private VehicleSpeedData? _lastGood;

        public bool IsLiveTesla => true;
        public string DataSourceLabel => "Tesla Fleet API";

        public FleetTeslaDataService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<FleetTeslaDataService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public void SetMockScenario(string scenario)
        {
            // no-op for live service
        }

        public async Task<VehicleSpeedData> GetCurrentSpeedDataAsync()
        {
            var token = _config["Tesla:AccessToken"];
            var vehicleId = _config["Tesla:VehicleId"]; // numeric id or VIN depending on endpoint form
            var baseUrl = (_config["Tesla:ApiBaseUrl"] ?? "https://fleet-api.prd.na.vn.cloud.tesla.com").TrimEnd('/');

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(vehicleId))
            {
                _logger.LogWarning("Tesla AccessToken or VehicleId not configured");
                return Fallback("Tesla not configured");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("tesla");
                // vehicle_data returns charge_state, drive_state, vehicle_state, climate_state, etc.
                var url = $"{baseUrl}/api/1/vehicles/{Uri.EscapeDataString(vehicleId)}/vehicle_data";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var res = await client.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tesla API {Status}: {Body}", (int)res.StatusCode, Truncate(body));
                    return Fallback($"Tesla API {(int)res.StatusCode}");
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var response = root.TryGetProperty("response", out var r) ? r : root;

                var charge = GetObject(response, "charge_state");
                var drive = GetObject(response, "drive_state");
                var vehicle = GetObject(response, "vehicle_state");

                double battery = GetDouble(charge, "battery_level", _lastGood?.BatteryPercent ?? 0);
                double range = GetDouble(charge, "battery_range", _lastGood?.RangeMiles ?? 0);
                // drive_state.speed is often mph when display is imperial; some regions use metric — config can override
                double speed = GetDouble(drive, "speed", 0);
                if (double.IsNaN(speed) || speed < 0) speed = 0;

                // Posted limit is not provided by Tesla API; keep last mock/config or 0
                double posted = _lastGood?.PostedSpeedLimitMph ?? _config.GetValue("Tesla:DefaultPostedSpeedMph", 65.0);

                // TPMS — vehicle_state fields (names vary slightly by firmware)
                double fl = GetDouble(vehicle, "tpms_pressure_fl", _lastGood?.TirePressureFl ?? 42);
                double fr = GetDouble(vehicle, "tpms_pressure_fr", _lastGood?.TirePressureFr ?? 42);
                double rl = GetDouble(vehicle, "tpms_pressure_rl", _lastGood?.TirePressureRl ?? 42);
                double rr = GetDouble(vehicle, "tpms_pressure_rr", _lastGood?.TirePressureRr ?? 42);

                // Convert bar → PSI if values look like bar (< 10)
                fl = MaybeBarToPsi(fl);
                fr = MaybeBarToPsi(fr);
                rl = MaybeBarToPsi(rl);
                rr = MaybeBarToPsi(rr);

                var over = Math.Max(0, speed - posted);
                string band = over switch
                {
                    <= 0 => "none",
                    <= 5 => "white",
                    <= 10 => "yellow",
                    <= 15 => "orange",
                    _ => "red"
                };

                var data = new VehicleSpeedData
                {
                    PostedSpeedLimitMph = Math.Round(posted, 0),
                    CurrentSpeedMph = Math.Round(speed, 1),
                    OverspeedBand = band,
                    Timestamp = DateTime.UtcNow,
                    VehicleName = GetString(response, "display_name") ?? "Tesla",
                    BatteryPercent = Math.Round(battery, 1),
                    RangeMiles = Math.Round(range, 0),
                    TirePressureFl = Math.Round(fl, 1),
                    TirePressureFr = Math.Round(fr, 1),
                    TirePressureRl = Math.Round(rl, 1),
                    TirePressureRr = Math.Round(rr, 1)
                };

                _lastGood = data;
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Tesla vehicle_data");
                return Fallback(ex.Message);
            }
        }

        private VehicleSpeedData Fallback(string reason)
        {
            if (_lastGood != null)
            {
                _lastGood.Timestamp = DateTime.UtcNow;
                return _lastGood;
            }

            return new VehicleSpeedData
            {
                VehicleName = $"Tesla ({reason})",
                Timestamp = DateTime.UtcNow,
                BatteryPercent = 0,
                RangeMiles = 0,
                PostedSpeedLimitMph = 65,
                CurrentSpeedMph = 0,
                OverspeedBand = "none",
                TirePressureFl = 0,
                TirePressureFr = 0,
                TirePressureRl = 0,
                TirePressureRr = 0
            };
        }

        private static JsonElement GetObject(JsonElement parent, string name)
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var el)
                && el.ValueKind == JsonValueKind.Object)
                return el;
            return default;
        }

        private static double GetDouble(JsonElement obj, string name, double fallback)
        {
            if (obj.ValueKind != JsonValueKind.Object) return fallback;
            if (!obj.TryGetProperty(name, out var el)) return fallback;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined) return fallback;
            if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var s)) return s;
            return fallback;
        }

        private static string? GetString(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            if (!obj.TryGetProperty(name, out var el)) return null;
            return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
        }

        private static double MaybeBarToPsi(double v)
        {
            // TPMS sometimes reported in bar (~2.9) vs PSI (~42)
            if (v > 0 && v < 10) return v * 14.5038;
            return v;
        }

        private static string Truncate(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= 300 ? s : s[..300] + "…");
    }
}
