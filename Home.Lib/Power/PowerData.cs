using System;
using Lucky.Home.Db;

namespace Lucky.Home.Power
{
    class PowerData : ISupportAverage<PowerData>, ISupportCsv
    {
        public double PowerW;

        public virtual string CsvHeader
        {
            get
            {
                return "PowerW";
            }
        }

        public PowerData Add(PowerData t1)
        {
            return new PowerData
            {
                PowerW = PowerW + t1.PowerW
            };
        }

        public PowerData Mul(double d)
        {
            return new PowerData
            {
                PowerW = PowerW * d
            };
        }

        public PowerData Div(double d)
        {
            return new PowerData
            {
                PowerW = PowerW / d
            };
        }

        public int CompareTo(PowerData other)
        {
            // Only compare Power for peak analysis
            return PowerW.CompareTo(other.PowerW);
        }

        public virtual string ToCsv()
        {
            return string.Format("{0:0}", PowerW);
        }
    }

    internal class SamilPowerData : PowerData, ISupportAverage<SamilPowerData>
    {
        public double PanelVoltageV;
        public double PanelCurrentA;
        public int Mode;
        public double EnergyTodayW;
        public double GridCurrentA;
        public double GridVoltageV;
        public double GridFrequencyHz;
        public double TotalPowerKW;

        public override string CsvHeader
        {
            get
            {
                return "PowerW,TotalPowerKW,Mode,EnergyTodayW,GridCurrentA,PanelCurrentA,GridVoltageV,PanelVoltageV,GridFrequencyHz";
            }
        }

        public override string ToCsv()
        {
            return string.Format("{0:0},{1:0},{2:0},{3:0},{4:0.00},{5:0.00},{6:0.0},{7:0.0},{8:0.00}", PowerW, TotalPowerKW, Mode, EnergyTodayW, GridCurrentA, PanelCurrentA, GridVoltageV, PanelVoltageV, GridFrequencyHz);
        }

        SamilPowerData ISupportAverage<SamilPowerData>.Add(SamilPowerData t1)
        {
            return new SamilPowerData
            {
                PowerW = PowerW + t1.PowerW
            };
        }

        SamilPowerData ISupportAverage<SamilPowerData>.Mul(double d)
        {
            return new SamilPowerData
            {
                PowerW = PowerW * d
            };
        }

        SamilPowerData ISupportAverage<SamilPowerData>.Div(double d)
        {
            return new SamilPowerData
            {
                PowerW = PowerW / d
            };
        }

        int IComparable<SamilPowerData>.CompareTo(SamilPowerData other)
        {
            return base.CompareTo(other);
        }
    }
}
