using MyTeslaGuard.Models;

namespace MyTeslaGuard.Services
{
    /// <summary>
    /// Mock service that simulates Tesla vehicle data.
    /// Replace with real Tesla Fleet API / Vehicle Data API calls for production.
    /// Real integration requires: Tesla Developer account, OAuth tokens, vehicle ID.
    /// </summary>
    public class MockTeslaDataService : ITeslaDataService
    {
        private readonly Random _rnd = new();
        private double _posted = 65;
        private double _current = 62;
        private string _scenario = "highway";

        public void SetMockScenario(string scenario)
        {
            _scenario = scenario?.ToLowerInvariant() ?? "highway";
            switch (_scenario)
            {
                case "city":
                    _posted = 35;
                    _current = 32 + _rnd.NextDouble() * 8;
                    break;
                case "overspeed":
                    _posted = 55;
                    _current = 68 + _rnd.NextDouble() * 12; // 13-25 over
                    break;
                case "highway":
                default:
                    _posted = 65;
                    _current = 60 + _rnd.NextDouble() * 20;
                    break;
            }
        }

        public Task<VehicleSpeedData> GetCurrentSpeedDataAsync()
        {
            // Simulate small speed fluctuations
            _current += (_rnd.NextDouble() - 0.5) * 3;
            _current = Math.Max(0, Math.Min(120, _current));

            var over = Math.Max(0, _current - _posted);
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
                PostedSpeedLimitMph = Math.Round(_posted, 0),
                CurrentSpeedMph = Math.Round(_current, 1),
                OverspeedBand = band,
                Timestamp = DateTime.UtcNow,
                VehicleName = "Model Y (Mock)"
            };

            return Task.FromResult(data);
        }
    }
}
