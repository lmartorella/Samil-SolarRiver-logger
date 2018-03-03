using Lucky.Home.Db;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lucky.Home.Power
{
    class DayPowerData
    {
        /// <summary>
        /// Time of day
        /// </summary>
        public TimeSpan First { get; set; }

        /// <summary>
        /// Time of day
        /// </summary>
        public TimeSpan Last { get; set; }

        /// <summary>
        /// Total power
        /// </summary>
        public double PowerKW { get; set; }
    }

    internal class PowerData : ISample<PowerData, DayPowerData>
    {
        public double PowerW;
        public double PanelVoltageV;
        public double PanelCurrentA;
        // Mode: 1: ON, 0: OFF, 2: Fault
        public int Mode;
        public double EnergyTodayW;
        public double GridCurrentA;
        public double GridVoltageV;
        public double GridFrequencyHz;
        public double TotalPowerKW;
        // Bitwise? 0x800 = no grid power
        public ushort Fault;

        public string CsvHeader
        {
            get
            {
                return "PowerW,TotalEnergyKWh,Mode,EnergyTodayWh,GridCurrentA,PanelCurrentA,GridVoltageV,PanelVoltageV,GridFrequencyHz,Fault";
            }
        }

        public string ToCsv()
        {
            return string.Format("{0:0},{1:0},{2:0},{3:0},{4:0.00},{5:0.00},{6:0.0},{7:0.0},{8:0.00},{9:0}", PowerW, TotalPowerKW, Mode, EnergyTodayW, GridCurrentA, PanelCurrentA, GridVoltageV, PanelVoltageV, GridFrequencyHz, Fault);
        }

        public virtual DayPowerData Aggregate(IEnumerable<Tuple<PowerData, DateTime>> data)
        {
            // Find first/last valid samples
            DateTime? first = data.FirstOrDefault(t => t.Item1.PowerW > 0)?.Item2;
            DateTime? last = data.LastOrDefault(t => t.Item1.PowerW > 0)?.Item2;
            if (first.HasValue && last.HasValue)
            {
                // Take total power from the last sample
                return new DayPowerData() { First = first.Value.TimeOfDay, Last = last.Value.TimeOfDay, PowerKW = data.Last().Item1.TotalPowerKW };
            }
            else
            {
                return null;
            }
        }
    }
}
