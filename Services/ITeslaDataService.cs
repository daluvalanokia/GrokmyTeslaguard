using MyTeslaGuard.Models;

namespace MyTeslaGuard.Services
{
    public interface ITeslaDataService
    {
        Task<VehicleSpeedData> GetCurrentSpeedDataAsync();
        void SetMockScenario(string scenario); // for demo: "highway", "city", "overspeed"
    }
}
