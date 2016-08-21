using System;
using Lucky.Home.Db;
using Lucky.Home.Sinks;
using System.Threading;
using System.Linq;
using Lucky.Services;
using Lucky.Home.Power;
using System.Text;

namespace Lucky.Home.Devices
{
    [Device("Samil Inverter")]
    [Requires(typeof(DuplexLineSink))]
    class SamilInverterDevice : DeviceBase, ISolarPanelDevice
    {
        private Timer _timer;
        private ILogger _logger;
        private bool _noSink;

        private static readonly TimeSpan CheckConnectionPeriod = TimeSpan.FromSeconds(10);
        private const int AddressToAllocate = 1;

        private static SamilMsg BroadcastRequest = new SamilMsg(0, 0, 0, 0);
        private static SamilMsg LoginMessage = new SamilMsg(0, 0, 0, 1);
        private static SamilMsg LogoutMessage = new SamilMsg(0, 0, 0, 4);
        private static SamilMsg UnknownMessage1 = new SamilMsg(0, AddressToAllocate, 1, 0);
        private static SamilMsg UnknownMessage2 = new SamilMsg(0, AddressToAllocate, 1, 1);
        private static SamilMsg GetPvDataMessage = new SamilMsg(0, AddressToAllocate, 1, 2);
        private static SamilMsg GetFwVersionMessage = new SamilMsg(0, AddressToAllocate, 1, 3);
        private static SamilMsg GetConfInfoMessage = new SamilMsg(0, AddressToAllocate, 1, 4);

        private class SamilMsg
        {
            public SamilMsg(ushort from, ushort to, byte cmd, byte subcmd)
            {
                From = from;
                To = to;
                Cmd = cmd;
                SubCmd = subcmd;
                Payload = new byte[0];
            }

            public ushort From;
            public ushort To;
            public byte Cmd;
            public byte SubCmd;
            public byte[] Payload;

            public byte[] ToBytes()
            {
                throw new NotImplementedException();
            }

            public static SamilMsg FromBytes(byte[] data)
            {
                throw new NotImplementedException();
            }

            internal SamilMsg Clone()
            {
                return FromBytes(ToBytes());
            }
        }

        public SamilInverterDevice(string name)
        {
            Name = name;
            _logger = Manager.GetService<LoggerFactory>().Create("Samil_" + Name);
            Start();
        }

        private void Start()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }

            // Poll SAMIL each 5 secs
            _timer = new Timer(o =>
            {
                CheckConnection();
            }, null, TimeSpan.Zero, CheckConnectionPeriod);
        }

        private void StartDataTimer()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }

            // Poll SAMIL each 5 secs
            _timer = new Timer(o =>
            {
                PollData();
            }, null, TimeSpan.Zero, PollDataPeriod);
        }

        public string Name { get; private set; }

        public ITimeSeries<PowerData> Database { get; set; }

        public PowerData ImmediateData { get; private set; }

        private void CheckConnection()
        {
            // Poll the line
            var line = Sinks.OfType<HalfDuplexLineSink>().FirstOrDefault();
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
            else
            {
                _noSink = false;
                LoginInverter(line);
            }
        }

        private async void LoginInverter(HalfDuplexLineSink line)
        {
            // Send 3 logout messages
            for (int i = 0; i < 3; i++)
            {
                await line.SendReceive(LogoutMessage.ToBytes());
            }
            // Broadcast hello message
            var res = SamilMsg.FromBytes(await line.SendReceive(BroadcastRequest.ToBytes()));
            if (res == null)
            {
                // Still continue to try login
                return;
            }

            // Check correct response
            if (res.From != 0 || res.To != 0 || res.Cmd != 0 || res.SubCmd != 0x80 || res.Payload.Length == 0)
            {
                ReportFault("Unexpected broadcast response", res);
                // Still continue to try login
                return;
            }

            // Correct response!
            var id = res.Payload;
            _logger.Log("Found", "ID", Encoding.ASCII.GetString(id));

            // Now try to login as address 1
            var loginMsg = LoginMessage.Clone();
            loginMsg.Payload = id.Concat(new byte[] { (byte)AddressToAllocate }).ToArray();
            res = SamilMsg.FromBytes(await line.SendReceive(loginMsg.ToBytes()));

            // Check correct response
            if (res.From != AddressToAllocate || res.To != 0 || res.Cmd != 0 || res.SubCmd != 0x81 || res.Payload.Length != 1)
            {
                ReportFault("Unexpected login response", res);
                // Still continue to try login
                return;
            }

            // Now I'm logged in!
            // Go with msg 1
            res = SamilMsg.FromBytes(await line.SendReceive(UnknownMessage1.ToBytes()));
            // Check correct response
            if (res.From != AddressToAllocate || res.To != 0 || res.Cmd != 1 || res.SubCmd != 0x80 || res.Payload.Length == 0)
            {
                ReportFault("Unexpected 'unknown message 1' response", res);
                // Still continue to try login
                return;
            }
            // Go with msg 2
            res = SamilMsg.FromBytes(await line.SendReceive(UnknownMessage2.ToBytes()));
            // Check correct response
            if (res.From != AddressToAllocate || res.To != 0 || res.Cmd != 1 || res.SubCmd != 0x81 || res.Payload.Length != 0)
            {
                ReportFault("Unexpected 'unknown message 2' response", res);
                // Still continue to try login
                return;
            }
            // Go with get firmware
            res = SamilMsg.FromBytes(await line.SendReceive(GetFwVersionMessage.ToBytes()));
            // Check correct response
            if (res.From != AddressToAllocate || res.To != 0 || res.Cmd != 1 || res.SubCmd != 0x83 || res.Payload.Length == 0)
            {
                ReportFault("Unexpected GetFirmware response", res);
                // Still continue to try login
                return;
            }
            // Go with get conf info
            res = SamilMsg.FromBytes(await line.SendReceive(GetConfInfoMessage.ToBytes()));
            // Check correct response
            if (res.From != AddressToAllocate || res.To != 0 || res.Cmd != 1 || res.SubCmd != 0x84 || res.Payload.Length != 0)
            {
                ReportFault("Unexpected GetConfiguration response", res);
                // Still continue to try login
                return;
            }

            // OK!
            // Start data timer
            StartDataTimer();
        }
    }
}
