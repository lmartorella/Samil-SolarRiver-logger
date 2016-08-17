
using Lucky.Home.Db;

namespace Lucky.Home.Devices
{
    interface ISolarPanelDevice : IDevice
    {
        /// <summary>
        /// Friendly name for db, etc..
        /// </summary>
        string Name { get; }

        /// <summary>
        /// In kW
        /// </summary>
        double ImmediatePower { get; }

        ITimeSeries Database { get; set; }
    }
}
