using Microsoft.AspNetCore.Mvc;
using MyTeslaGuard.Models;
using MyTeslaGuard.Services;

namespace MyTeslaGuard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITeslaDataService _teslaService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ITeslaDataService teslaService, ILogger<HomeController> logger)
        {
            _teslaService = teslaService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSpeedData()
        {
            var data = await _teslaService.GetCurrentSpeedDataAsync();
            return Json(data);
        }

        [HttpPost]
        public IActionResult SetScenario([FromBody] ScenarioRequest req)
        {
            _teslaService.SetMockScenario(req?.Scenario ?? "highway");
            return Ok(new { success = true, scenario = req?.Scenario });
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
