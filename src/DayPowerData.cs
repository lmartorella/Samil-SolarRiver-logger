using Lucky.Db;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lucky.Home.Power
{
    /// <summary>
    /// CSV for daily power summary
    /// </summary>
    public class DayPowerData : DayTimeSample<PowerData>
    {
        /// <summary>
        /// Time of day of the first sample with power > 0
        /// </summary>
        [Csv("hh\\:mm\\:ss")]
        public TimeSpan First;

        /// <summary>
        /// Time of day of the last sample with power > 0
        /// </summary>
        [Csv("hh\\:mm\\:ss")]
        public TimeSpan Last;

        /// <summary>
        /// Total power
        /// </summary>
        [Csv("0.00")]
        public double PowerKWh;

        /// <summary>
        /// Has at least a fault?
        /// </summary>
        [Csv("0")]
        public int Fault;

        /// <summary>
        /// Peak power
        /// </summary>
        [Csv("0.00")]
        public double PeakPowerW;

        /// <summary>
        /// Time of day of the peak power
        /// </summary>
        [Csv("hh\\:mm\\:ss")]
        public TimeSpan PeakTimestamp;

        public override bool Aggregate(DateTime date, IEnumerable<PowerData> data)
        {
            Date = date;

            // Find first/last valid samples
            DateTime? first = data.FirstOrDefault(t => t.PowerW > 0)?.TimeStamp;
            DateTime? last = data.LastOrDefault(t => t.PowerW > 0)?.TimeStamp;

            if (first.HasValue && last.HasValue && !first.Value.Equals(last.Value))
            {
                // Have a sun range
                First = first.Value.TimeOfDay;
                Last = last.Value.TimeOfDay;
                Fault = data.Any(t => t.Fault != 0) ? 1 : 0;

                // Now take total power.
                // Typically this is stored in the EnergyTodayWh that is progressively stored, so ideally the last sample is OK
                // However we should cope with inverter reset in the middle of the day: in that case we have two separated power ramps (or more)
                // to sum up.
                // So find any ranges
                double lastSample = 0;
                double totalPower = 0;
                foreach (var sample in data)
                {
                    if (sample.EnergyTodayWh < lastSample)
                    {
                        // Interruption -> new range
                        totalPower += lastSample;
                    }
                    lastSample = sample.EnergyTodayWh;
                }
                totalPower += lastSample;

                PowerKWh = totalPower / 1000.0;

                // Calc max power
                var maxPowerSample = data.Aggregate((i1, i2) => i1.PowerW > i2.PowerW ? i1 : i2);
                PeakPowerW = maxPowerSample.PowerW;
                PeakTimestamp = maxPowerSample.TimeStamp.TimeOfDay;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
