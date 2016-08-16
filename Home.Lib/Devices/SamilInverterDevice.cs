using System;
using Lucky.Home.Db;
using Lucky.Home.Sinks;

namespace Lucky.Home.Devices
{
    [Device("Samil Inverter")]
    [Requires(typeof(DuplexLineSink))]
    class SamilInverterDevice : DeviceBase, ISolarPanelDevice
    {
        public ITimeSeries Database { get; set; }

        public double ImmediatePower { get; private set; }
    }
}
