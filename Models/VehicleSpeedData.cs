namespace MyTeslaGuard.Models
{
    public class VehicleSpeedData
    {
        public double PostedSpeedLimitMph { get; set; }
        public double CurrentSpeedMph { get; set; }
        public double SpeedOverLimitMph => Math.Max(0, CurrentSpeedMph - PostedSpeedLimitMph);
        public string OverspeedBand { get; set; } = "none"; // none, white, yellow, orange, red
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsMoving => CurrentSpeedMph > 1.0;
        public string VehicleName { get; set; } = "My Tesla";
    }
}
