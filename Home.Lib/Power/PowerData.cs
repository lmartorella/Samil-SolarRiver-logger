using System;
using Lucky.Home.Db;

namespace Lucky.Home.Power
{
    class PowerData : ISupportCsv
    {
        public double PowerW;

        public virtual string CsvHeader
        {
            get
            {
                return "PowerW";
            }
        }

        public virtual string ToCsv()
        {
            return string.Format("{0:0}", PowerW);
        }
    }

    internal class SamilPowerData : PowerData
    {
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

        public override string CsvHeader
        {
            get
            {
                return "PowerW,TotalEnergyKWh,Mode,EnergyTodayWh,GridCurrentA,PanelCurrentA,GridVoltageV,PanelVoltageV,GridFrequencyHz,Fault";
            }
        }

        public override string ToCsv()
        {
            return string.Format("{0:0},{1:0},{2:0},{3:0},{4:0.00},{5:0.00},{6:0.0},{7:0.0},{8:0.00},{9:0}", PowerW, TotalPowerKW, Mode, EnergyTodayW, GridCurrentA, PanelCurrentA, GridVoltageV, PanelVoltageV, GridFrequencyHz, Fault);
        }
    }
}
