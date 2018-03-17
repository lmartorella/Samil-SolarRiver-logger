using Lucky.Db;
using Lucky.Home.Power;

namespace Lucky.Home.Devices
{
    interface ISolarPanelDevice : IDevice
    {
        /// <summary>
        /// Friendly name for db, etc..
        /// </summary>
        string Name { get; }

        PowerData ImmediateData { get; }

        void Init(ITimeSeries<PowerData, DayPowerData> database);
    }
}
