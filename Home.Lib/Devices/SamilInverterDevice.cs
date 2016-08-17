using System;
using Lucky.Home.Db;
using Lucky.Home.Sinks;
using System.Threading;
using System.Linq;
using Lucky.Services;
using Lucky.Home.Power;

namespace Lucky.Home.Devices
{
    [Device("Samil Inverter")]
    [Requires(typeof(DuplexLineSink))]
    class SamilInverterDevice : DeviceBase, ISolarPanelDevice
    {
        private Timer _timer;
        private ILogger _logger;
        private bool _noSink;

        public SamilInverterDevice(string name)
        {
            Name = name;
            _logger = Manager.GetService<LoggerFactory>().Create("Samil_" + Name);

            // Poll SAMIL each 5 secs
            _timer = new Timer(o =>
            {
                Poll();
            }, null, 0, 5000);
        }

        public string Name { get; private set; }

        public ITimeSeries<PowerData> Database { get; set; }

        public double ImmediatePower { get; private set; }

        private void Poll()
        {
            // Poll the line
            var line = Sinks.OfType<DuplexLineSink>().FirstOrDefault();
            if (line == null)
            {
                // No sink
                if (!_noSink)
                {
                    _logger.Log("NoSink");
                    _noSink = true;
                }
                return;
            }

            _noSink = false;

            // Establish connection (Samil protocol)
            //...
            // Update ImmediatePower
        }
    }
}
