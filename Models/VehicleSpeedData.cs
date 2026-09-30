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

        // Battery / range
        public double BatteryPercent { get; set; }          // 0-100
        public double RangeMiles { get; set; }              // estimated remaining miles

        // Tire pressures (PSI)
        public double TirePressureFl { get; set; }          // Front Left
        public double TirePressureFr { get; set; }          // Front Right
        public double TirePressureRl { get; set; }          // Rear Left
        public double TirePressureRr { get; set; }          // Rear Right
    }
}
