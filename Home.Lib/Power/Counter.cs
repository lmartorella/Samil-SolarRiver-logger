using System;

namespace Lucky.Home.Power
{
    struct Counter
    {
        double AveragePower { get; set; }
        double PeakPower { get; set; }
        DateTime PeakPowerDate { get; set; }
    }
}
