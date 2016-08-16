
using Lucky.Home.Db;

namespace Lucky.Home.Devices
{
    interface ISolarPanelDevice : IDevice
    {
        double ImmediatePower { get; }
        ITimeSeries Database { get; set; }
    }
}
