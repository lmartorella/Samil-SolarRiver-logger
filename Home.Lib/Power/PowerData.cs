using Lucky.Home.Db;
using System;

namespace Lucky.Home.Power
{
    class PowerData : ISupportAverage<PowerData>, ISupportCsv
    {
        public double PowerW;

        public double CurrentA;

        public double TensionV;

        public string CsvHeader
        {
            get
            {
                return "PowerW,CurrentA,TensionV";
            }
        }

        public PowerData Add(PowerData t1)
        {
            return new PowerData
            {
                PowerW = PowerW + t1.PowerW,
                CurrentA = CurrentA + t1.CurrentA,
                TensionV = TensionV + t1.TensionV,
            };
        }

        public PowerData Mul(double d)
        {
            return new PowerData
            {
                PowerW = PowerW * d,
                CurrentA = CurrentA * d,
                TensionV = TensionV * d,
            };
        }

        public PowerData Div(double d)
        {
            return new PowerData
            {
                PowerW = PowerW / d,
                CurrentA = CurrentA / d,
                TensionV = TensionV / d,
            };
        }

        public int CompareTo(PowerData other)
        {
            // Only compare Power for peak analysis
            return PowerW.CompareTo(other.PowerW);
        }

        public string ToCsv()
        {
            return string.Format("{0:0},{1:0.00},{2:0.0}", PowerW, CurrentA, TensionV);
        }
    }
}
