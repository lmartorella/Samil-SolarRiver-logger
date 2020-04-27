using Lucky.Db;

namespace Lucky.Home.Power
{

    /// <summary>
    /// CSV for tick-by-tick power data
    /// </summary>
    public class PowerData : TimeSample
    {
        [Csv("0")]
        public double PowerW;
        [Csv("0")]
        public double TotalEnergyKWh;
        // Mode: 1: ON, 0: OFF, 2: Fault
        [Csv("0")]
        public int Mode;
        [Csv("0")]
        public double EnergyTodayWh;
        [Csv("0.00")]
        public double GridCurrentA;
        [Csv("0.00")]
        public double PanelCurrentA;
        [Csv("0.0")]
        public double GridVoltageV;
        [Csv("0.0")]
        public double PanelVoltageV;
        [Csv("0.00")]
        public double GridFrequencyHz;
        // Bitwise? 0x800 = no grid power
        [Csv("0")]
        public ushort Fault;
        // Home usage current, to calculate Net Energy Metering
        [Csv("0.00")]
        public double HomeUsageCurrentA;
    }
}
