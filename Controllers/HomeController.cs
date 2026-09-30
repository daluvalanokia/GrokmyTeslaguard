using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MyTeslaGuard.Models;
using MyTeslaGuard.Services;

namespace MyTeslaGuard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITeslaDataService _teslaService;
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(
            ITeslaDataService teslaService,
            ILogger<HomeController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _teslaService = teslaService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSpeedData()
        {
            var data = await _teslaService.GetCurrentSpeedDataAsync();
            return Json(new
            {
                postedSpeedLimitMph = data.PostedSpeedLimitMph,
                currentSpeedMph = data.CurrentSpeedMph,
                speedOverLimitMph = data.SpeedOverLimitMph,
                overspeedBand = data.OverspeedBand,
                timestamp = data.Timestamp,
                isMoving = data.IsMoving,
                vehicleName = data.VehicleName,
                batteryPercent = data.BatteryPercent,
                rangeMiles = data.RangeMiles,
                tirePressureFl = data.TirePressureFl,
                tirePressureFr = data.TirePressureFr,
                tirePressureRl = data.TirePressureRl,
                tirePressureRr = data.TirePressureRr,
                latitude = data.Latitude,
                longitude = data.Longitude,
                isPluggedIn = data.IsPluggedIn,
                chargeState = data.ChargeState,
                makeModelYear = data.MakeModelYear,
                isLiveTesla = _teslaService.IsLiveTesla,
                dataSource = _teslaService.DataSourceLabel
            });
        }

        [HttpPost]
        public IActionResult SetScenario([FromBody] ScenarioRequest req)
        {
            _teslaService.SetMockScenario(req?.Scenario ?? "highway");
            return Ok(new { success = true, scenario = req?.Scenario });
        }

        /// <summary>
        /// Server-side geocode proxy. Nominatim blocks most browser-origin requests
        /// on public domains; calling from the backend with a proper User-Agent works.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Geocode([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { error = "Missing query parameter q" });

            try
            {
                var client = _httpClientFactory.CreateClient("nominatim");
                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(q.Trim())}&limit=1";

                using var response = await client.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim returned {Status}: {Body}", (int)response.StatusCode, body);
                    return StatusCode((int)response.StatusCode, new { error = "Geocode provider error", detail = body });
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.GetArrayLength() == 0)
                    return NotFound(new { error = "Address not found" });

                var first = root[0];
                var result = new
                {
                    lat = double.Parse(first.GetProperty("lat").GetString() ?? "0"),
                    lng = double.Parse(first.GetProperty("lon").GetString() ?? "0"),
                    display = first.GetProperty("display_name").GetString()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Geocode failed for {Query}", q);
                return StatusCode(502, new { error = "Geocode failed", detail = ex.Message });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }

    public class ScenarioRequest
    {
        public string? Scenario { get; set; }
    }
}
