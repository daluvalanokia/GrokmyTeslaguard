using System.Net.Http.Headers;
using System.Text.Json;
using MyTeslaGuard.Models;

namespace MyTeslaGuard.Services
{
    /// <summary>
    /// Live vehicle data via Smartcar API (Tesla via Smartcar — no direct Tesla OAuth).
    ///
    /// Auth: OAuth2 client_credentials → https://iam.smartcar.com/oauth2/token
    /// Vehicle APIs (v2.0 Tesla-specific + standard):
    ///   GET /v2.0/vehicles/{id}/tesla/battery      → percentRemaining, range (read_battery)
    ///   GET /v2.0/vehicles/{id}/tesla/charge       → isPluggedIn, state, socLimit (read_charge)
    ///   GET /v2.0/vehicles/{id}/tesla/speedometer  → speed km/h (read_speedometer)
    ///   GET /v2.0/vehicles/{id}/location           → lat/lng (read_location)
    ///   GET /v2.0/vehicles/{id}/tires/pressure     → kPa (read_tires)
    ///   GET /v2.0/vehicles/{id}/tesla/attributes   → extended info (read_extended_vehicle_info)
    ///
    /// Docs: https://smartcar.com/docs  |  https://smartcar.com/brand/tesla
    /// </summary>
    public class SmartcarDataService : ITeslaDataService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<SmartcarDataService> _logger;

        private string? _accessToken;
        private DateTime _tokenExpiresUtc = DateTime.MinValue;
        private VehicleSpeedData? _lastGood;

        public bool IsLiveTesla => true;
        public string DataSourceLabel => "Smartcar";

        public SmartcarDataService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<SmartcarDataService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public void SetMockScenario(string scenario) { /* no-op */ }

        public async Task<VehicleSpeedData> GetCurrentSpeedDataAsync()
        {
            var vehicleId = _config["Smartcar:VehicleId"];
            var make = (_config["Smartcar:Make"] ?? "TESLA").ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                _logger.LogWarning("Smartcar:VehicleId not configured");
                return Fallback("Smartcar VehicleId missing");
            }

            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return Fallback("Smartcar token failed");

                var client = _httpClientFactory.CreateClient("smartcar");
                var baseApi = (_config["Smartcar:ApiBaseUrl"] ?? "https://api.smartcar.com/v2.0").TrimEnd('/');
                var userId = _config["Smartcar:UserId"]; // sc-unit / user id when required

                // Parallel fetch of key endpoints
                var batteryTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/{make}/battery", userId);
                var chargeTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/{make}/charge", userId);
                var speedTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/{make}/speedometer", userId);
                var locTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/location", userId);
                var tiresTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/tires/pressure", userId);
                var attrsTask = GetJsonAsync(client, token, $"{baseApi}/vehicles/{vehicleId}/{make}/attributes", userId);

                await Task.WhenAll(batteryTask, chargeTask, speedTask, locTask, tiresTask, attrsTask);

                var battery = await batteryTask;
                var charge = await chargeTask;
                var speedJson = await speedTask;
                var loc = await locTask;
                var tires = await tiresTask;
                var attrs = await attrsTask;

                // Battery: percentRemaining is 0–1 fraction in Tesla battery endpoint
                double batteryPct = GetDouble(battery, "percentRemaining", double.NaN);
                if (!double.IsNaN(batteryPct) && batteryPct <= 1.0)
                    batteryPct *= 100.0;
                if (double.IsNaN(batteryPct))
                    batteryPct = _lastGood?.BatteryPercent ?? 0;

                // Range from Tesla battery endpoint is often km
                double range = GetDouble(battery, "rangeEstimated",
                    GetDouble(battery, "range", _lastGood?.RangeMiles ?? 0));
                if (range > 0 && (_config.GetValue("Smartcar:RangeUnit", "km") == "km"))
                    range *= 0.621371; // km → miles

                // Speed is km/h → mph
                double speedKmh = GetDouble(speedJson, "speed", 0);
                double speedMph = speedKmh * 0.621371;

                double posted = _lastGood?.PostedSpeedLimitMph
                    ?? _config.GetValue("Smartcar:DefaultPostedSpeedMph", 65.0);

                // Tires in kPa → PSI
                double fl = KpaToPsi(GetDouble(tires, "frontLeft", 0));
                double fr = KpaToPsi(GetDouble(tires, "frontRight", 0));
                double rl = KpaToPsi(GetDouble(tires, "backLeft", 0));
                double rr = KpaToPsi(GetDouble(tires, "backRight", 0));
                if (fl <= 0) fl = _lastGood?.TirePressureFl ?? 0;
                if (fr <= 0) fr = _lastGood?.TirePressureFr ?? 0;
                if (rl <= 0) rl = _lastGood?.TirePressureRl ?? 0;
                if (rr <= 0) rr = _lastGood?.TirePressureRr ?? 0;

                double? lat = GetNullableDouble(loc, "latitude");
                double? lng = GetNullableDouble(loc, "longitude");

                string? name = GetString(attrs, "nickname")
                    ?? BuildName(attrs)
                    ?? _lastGood?.VehicleName
                    ?? "Tesla";

                var over = Math.Max(0, speedMph - posted);
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
                    CurrentSpeedMph = Math.Round(speedMph, 1),
                    OverspeedBand = band,
                    Timestamp = DateTime.UtcNow,
                    VehicleName = name,
                    BatteryPercent = Math.Round(batteryPct, 1),
                    RangeMiles = Math.Round(range, 0),
                    TirePressureFl = Math.Round(fl, 1),
                    TirePressureFr = Math.Round(fr, 1),
                    TirePressureRl = Math.Round(rl, 1),
                    TirePressureRr = Math.Round(rr, 1),
                    Latitude = lat,
                    Longitude = lng,
                    IsPluggedIn = GetBool(charge, "isPluggedIn"),
                    ChargeState = GetString(charge, "state"),
                    MakeModelYear = BuildName(attrs)
                };

                _lastGood = data;
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Smartcar fetch failed");
                return Fallback(ex.Message);
            }
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresUtc.AddSeconds(-60))
                return _accessToken;

            // Prefer a pre-issued management/user access token if provided
            var staticToken = _config["Smartcar:AccessToken"];
            if (!string.IsNullOrWhiteSpace(staticToken))
            {
                _accessToken = staticToken;
                _tokenExpiresUtc = DateTime.UtcNow.AddMinutes(50);
                return _accessToken;
            }

            var clientId = _config["Smartcar:ClientId"];
            var clientSecret = _config["Smartcar:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogWarning("Smartcar ClientId/ClientSecret or AccessToken required");
                return null;
            }

            var client = _httpClientFactory.CreateClient("smartcar");
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };

            using var res = await client.PostAsync(
                "https://iam.smartcar.com/oauth2/token",
                new FormUrlEncodedContent(form));
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Smartcar token error {Status}: {Body}", (int)res.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32()
                : 3600;
            _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn);
            return _accessToken;
        }

        private async Task<JsonElement> GetJsonAsync(HttpClient client, string token, string url, string? userId)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(userId))
                req.Headers.TryAddWithoutValidation("sc-unit-id", userId);
            // Some Smartcar vehicle calls also accept / document sc-user-id
            if (!string.IsNullOrWhiteSpace(userId))
                req.Headers.TryAddWithoutValidation("sc-user-id", userId);

            using var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogDebug("Smartcar {Url} → {Status}: {Body}", url, (int)res.StatusCode, Truncate(body));
                return default;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.Clone();
            }
            catch
            {
                return default;
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
                VehicleName = $"Smartcar ({reason})",
                Timestamp = DateTime.UtcNow,
                PostedSpeedLimitMph = _config.GetValue("Smartcar:DefaultPostedSpeedMph", 65.0),
                OverspeedBand = "none"
            };
        }

        private static double KpaToPsi(double kpa) => kpa <= 0 ? 0 : kpa * 0.145038;

        private static double GetDouble(JsonElement obj, string name, double fallback)
        {
            if (obj.ValueKind != JsonValueKind.Object) return fallback;
            if (!obj.TryGetProperty(name, out var el)) return fallback;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
            if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var s)) return s;
            return fallback;
        }

        private static double? GetNullableDouble(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            if (!obj.TryGetProperty(name, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
            if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var s)) return s;
            return null;
        }

        private static string? GetString(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return null;
            return el.GetString();
        }

        private static bool? GetBool(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            if (!obj.TryGetProperty(name, out var el)) return null;
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
            return null;
        }

        private static string? BuildName(JsonElement attrs)
        {
            if (attrs.ValueKind != JsonValueKind.Object) return null;
            var year = attrs.TryGetProperty("year", out var y) ? y.ToString() : "";
            var model = GetString(attrs, "model") ?? "";
            var make = GetString(attrs, "make") ?? "Tesla";
            var s = $"{year} {make} {model}".Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static string Truncate(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= 200 ? s : s[..200] + "…");
    }
}
