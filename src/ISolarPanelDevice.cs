using Lucky.Db;
using Lucky.Home.Power;
using System.Threading.Tasks;

namespace Lucky.Home.Devices.Solar
{
    interface ISolarPanelDevice : IDevice
    {
        /// <summary>
        /// Friendly name for db, etc..
        /// </summary>
        string Name { get; }

        PowerData ImmediateData { get; }

        Task StartLoop(ITimeSeries<PowerData, DayPowerData> database);
    }
}
