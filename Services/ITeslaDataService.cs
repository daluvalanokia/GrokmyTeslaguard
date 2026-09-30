using MyTeslaGuard.Models;

namespace MyTeslaGuard.Services
{
    public interface ITeslaDataService
    {
        Task<VehicleSpeedData> GetCurrentSpeedDataAsync();
        void SetMockScenario(string scenario); // mock only: "highway", "city", "overspeed"
        bool IsLiveTesla { get; }
        string DataSourceLabel { get; }
    }
}
