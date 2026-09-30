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
        private double _battery = 72;
        private double _range = 210;
        private string _scenario = "highway";

        // Nominal cold pressures (PSI) – typical Model Y range
        private double _tpFl = 42.0;
        private double _tpFr = 42.0;
        private double _tpRl = 42.0;
        private double _tpRr = 42.0;

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

            // Gentle battery drain while "driving"
            if (_current > 5)
            {
                _battery = Math.Max(5, _battery - _rnd.NextDouble() * 0.02);
                _range = Math.Max(10, _range - _rnd.NextDouble() * 0.08);
            }

            // Tiny tire pressure noise
            _tpFl = ClampPressure(_tpFl + (_rnd.NextDouble() - 0.5) * 0.15);
            _tpFr = ClampPressure(_tpFr + (_rnd.NextDouble() - 0.5) * 0.15);
            _tpRl = ClampPressure(_tpRl + (_rnd.NextDouble() - 0.5) * 0.15);
            _tpRr = ClampPressure(_tpRr + (_rnd.NextDouble() - 0.5) * 0.15);

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
                VehicleName = "Model Y (Mock)",
                BatteryPercent = Math.Round(_battery, 1),
                RangeMiles = Math.Round(_range, 0),
                TirePressureFl = Math.Round(_tpFl, 1),
                TirePressureFr = Math.Round(_tpFr, 1),
                TirePressureRl = Math.Round(_tpRl, 1),
                TirePressureRr = Math.Round(_tpRr, 1)
            };

            return Task.FromResult(data);
        }

        private static double ClampPressure(double p) => Math.Max(28, Math.Min(48, p));
    }
}
