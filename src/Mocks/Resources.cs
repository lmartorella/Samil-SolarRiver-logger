using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]

namespace Lucky.Home
{
    internal static class Resources
    {
        internal static string solar_daily_summary_title = "Daily power: {0}";
        internal static string solar_daily_summary = "PowerKWh: {PowerKWh}\r\nPeak: ${PeakPowerW} at {PeakTimestamp}\r\nDay length: ${SunTime}";
        internal static string solar_daylight_format = "t";
    }
}
