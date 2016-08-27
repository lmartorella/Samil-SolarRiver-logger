using System;
using Lucky.Home.Db;
using Lucky.Home.Sinks;
using System.Threading;
using System.Linq;
using Lucky.Services;
using Lucky.Home.Power;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Lucky.Home.Devices
{
    [Device("Samil Inverter")]
    [Requires(typeof(HalfDuplexLineSink))]
    class SamilInverterDevice : DeviceBase, ISolarPanelDevice
    {
        private Timer _timer;
        private ILogger _logger;
        private bool _noSink;

        private static readonly TimeSpan CheckConnectionPeriod = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PollDataPeriod = TimeSpan.FromSeconds(5);
        private const int AddressToAllocate = 1;

        private static readonly SamilMsg BroadcastRequest = new SamilMsg(0, 0, 0, 0);
        private static readonly SamilMsg BroadcastResponse = new SamilMsg(0, 0, 0, 0x80);
        private static readonly byte[] UnknownResponse1Data = new byte[] { 0x00, 0x01, 0x04, 0x09, 0x0a, 0x0c, 0x11, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36 };

        private static readonly SamilMsg LoginMessage = new SamilMsg(0, 0, 0, 1);
        private static readonly SamilMsg LoginResponse = new SamilMsg(AddressToAllocate, 0, 0, 0x81, new byte[] { 0x6 });
        private static readonly SamilMsg LogoutMessage = new SamilMsg(0, 0, 0, 4);

        private static readonly SamilMsg UnknownMessage1 = new SamilMsg(0, AddressToAllocate, 1, 0);
        private static readonly SamilMsg UnknownResponse1 = new SamilMsg(AddressToAllocate, 0, 1, 0x80, UnknownResponse1Data);
        private static readonly SamilMsg UnknownMessage2 = new SamilMsg(0, AddressToAllocate, 1, 1);
        private static readonly SamilMsg UnknownResponse2 = new SamilMsg(AddressToAllocate, 0, 1, 0x81);
        private static readonly SamilMsg GetPvDataMessage = new SamilMsg(0, AddressToAllocate, 1, 2);
        private static readonly SamilMsg GetPvDataResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x82);
        private static readonly SamilMsg GetFwVersionMessage = new SamilMsg(0, AddressToAllocate, 1, 3);
        private static readonly SamilMsg GetFwVersionResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x83);
        private static readonly SamilMsg GetConfInfoMessage = new SamilMsg(0, AddressToAllocate, 1, 4);
        private static readonly SamilMsg GetConfInfoResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x84);

        private class SamilMsg
        {
            private List<byte> _bytes;
            private static readonly SamilMsg Invalid = new SamilMsg(0xFFFF, 0xFFFF, 0xFF, 0xFF);

            public SamilMsg(ushort from, ushort to, byte cmd, byte subcmd, byte[] payload = null)
            {
                From = from;
                To = to;
                Cmd = (ushort)((cmd << 8) + subcmd);
                Payload = payload ?? new byte[0];

                _bytes = new List<byte>(11 + Payload.Length);
                PushW(0x55AA);
                PushW(From);
                PushW(To);
                PushW(Cmd);
                _bytes.Add((byte)Payload.Length);
                _bytes.AddRange(Payload);
                PushW(_bytes.Aggregate((ushort)0, (b1, b2) => (ushort)(b1 + b2)));
            }

            private void PushW(ushort w)
            {
                byte[] v = BitConverter.GetBytes(w);
                if (BitConverter.IsLittleEndian)
                {
                    _bytes.Add(v[1]);
                    _bytes.Add(v[0]);
                }
                else
                {
                    _bytes.AddRange(v);
                }
            }

            public ushort From;
            public ushort To;
            public ushort Cmd;
            public byte[] Payload;

            public byte[] ToBytes()
            {
                return _bytes.ToArray();
            }

            public static SamilMsg FromBytes(byte[] data, byte[] payload = null)
            {
                // Transform null in invalid message
                if (data == null || data.Length < 11)
                {
                    return Invalid;
                }
                var checksum = data.Take(data.Length - 2).Aggregate((ushort)0, (b1, b2) => (ushort)(b1 + b2));
                int l = data[8];
                if (WordAt(0, data) != 0x55aa || WordAt(data.Length - 2, data) != checksum || l != data.Length - 11)
                {
                    return Invalid;
                }
                return new SamilMsg(WordAt(2, data), WordAt(4, data), data[6], data[7], payload ?? data.Skip(9).Take(l).ToArray());
            }

            internal SamilMsg Clone(byte[] payload)
            {
                return FromBytes(ToBytes(), payload);
            }

            public override string ToString()
            {
                return string.Join(" ", _bytes.Select(b => b.ToString("x2")));
            }

            internal bool CheckStructure(SamilMsg msg)
            {
                return From == msg.From && To == msg.To && Cmd == msg.Cmd;
            }

            internal bool CheckPayload(SamilMsg msg)
            {
                return Payload.SequenceEqual(msg.Payload);
            }
        }

        private static ushort WordAt(int pos, byte[] data)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (ushort)((data[pos] << 8) + data[pos + 1]);
            }
            else
            {
                return BitConverter.ToUInt16(data, pos);
            }
        }

        public SamilInverterDevice(string name)
        {
            Name = name;
            _logger = Manager.GetService<LoggerFactory>().Create("Samil_" + Name);
            StartConnectionTimer();
        }

        private void StartConnectionTimer()
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

        ITimeSeries<PowerData> ISolarPanelDevice.Database
        {
            get
            {
                return (ITimeSeries<PowerData>)Database;
            }
            set
            {
                Database = (ITimeSeries<SamilPowerData>)value;
            }
        }

        public ITimeSeries<SamilPowerData> Database { get; set; }
        
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

        private async Task<bool> CheckProtocol(HalfDuplexLineSink line, SamilMsg request, SamilMsg expResponse, string phase, bool checkPayload)
        {
            return (await CheckProtocolWRes(line, request, expResponse, phase, checkPayload)).Item1;
        }

        private async Task<Tuple<bool, SamilMsg>> CheckProtocolWRes(HalfDuplexLineSink line, SamilMsg request, SamilMsg expResponse, string phase, bool checkPayload)
        {
            // Broadcast hello message
            var rcvBytes = await line.SendReceive(request.ToBytes());
            var res = SamilMsg.FromBytes(rcvBytes);
            // Check correct response
            if (!res.CheckStructure(expResponse))
            {
                ReportFault("Unexpected " + phase, rcvBytes != null ? string.Join(" ", rcvBytes.Select(b => b.ToString("x2"))) : "<null>");
                return Tuple.Create<bool, SamilMsg>(false, null);
            }
            if (checkPayload && !res.CheckPayload(expResponse))
            {
                ReportWarning("Strange payload " + phase, res);
            }
            return Tuple.Create(true, res);
        }

        private async void LoginInverter(HalfDuplexLineSink line)
        {
            // Send 3 logout messages
            for (int i = 0; i < 3; i++)
            {
                await line.SendReceive(LogoutMessage.ToBytes());
            }
            var res = await CheckProtocolWRes(line, BroadcastRequest, BroadcastResponse, "broadcast response", false);
            if (!res.Item1)
            {
                // Still continue to try login
                return;
            }

            // Correct response!
            var id = res.Item2.Payload;
            _logger.Log("Found", "ID", Encoding.ASCII.GetString(id));

            // Now try to login as address 1
            var loginMsg = LoginMessage.Clone(id.Concat(new byte[] { AddressToAllocate }).ToArray());

            if (!await CheckProtocol(line, loginMsg, LoginResponse, "login response", true))
            { 
                // Still continue to try login
                return;
            }

            // Now I'm logged in!
            // Go with msg 1
            if (!await CheckProtocol(line, UnknownMessage1, UnknownResponse1, "unknown message 1", true))
            {
                // Still continue to try login
                return;
            }
            // Go with msg 2
            if (!await CheckProtocol(line, UnknownMessage2, UnknownResponse2, "unknown message 2", true))
            {
                // Still continue to try login
                return;
            }
            // Go with get firmware
            if (!await CheckProtocol(line, GetFwVersionMessage, GetFwVersionResponse, "get firmware response", false))
            {
                // Still continue to try login
                return;
            }
            // Go with get conf info
            if (!await CheckProtocol(line, GetConfInfoMessage, GetConfInfoResponse, "get configuration", true))
            {
                // Still continue to try login
                return;
            }

            // OK!
            // Start data timer
            StartDataTimer();
        }

        private async void PollData()
        {
            var line = Sinks.OfType<HalfDuplexLineSink>().FirstOrDefault();
            if (line == null)
            {
                // disconnect
                StartConnectionTimer();
                return;
            }
            var res = await CheckProtocolWRes(line, GetPvDataMessage, GetPvDataResponse, "get PV data", false);
            if (!res.Item1)
            {
                // Relogin!
                StartConnectionTimer();
                return;
            }
            // Decode and record data
            if (!DecodePvData(res.Item2.Payload))
            {
                // Report invalid msg
                ReportWarning("Invalid/strange PV data", res.Item2);
            }
        }

        private bool DecodePvData(byte[] payload)
        {
            if (payload.Length != 50)
            {
                return false;
            }

            int panelVoltage = ExtractW(payload, 1);
            int panelCurrent = ExtractW(payload, 2);
            int mode = ExtractW(payload, 5);
            int energyToday = ExtractW(payload, 6);
            int gridCurrent = ExtractW(payload, 19);
            int gridVoltage = ExtractW(payload, 20);
            int gridFrequency = ExtractW(payload, 21);
            int gridPower = ExtractW(payload, 22);
            int totalPower = (ExtractW(payload, 23) << 16) + ExtractW(payload, 24);

            var db = Database;
            if (db != null)
            {
                var data = new SamilPowerData
                {
                    PowerW = gridPower,
                    PanelVoltageV = panelVoltage / 10.0,
                    GridVoltageV = gridVoltage / 10.0,
                    PanelCurrentA = panelCurrent / 10.0,
                    GridCurrentA = gridCurrent / 10.0,
                    Mode = mode,
                    EnergyTodayW = energyToday * 10.0,
                    GridFrequencyHz = gridFrequency / 100.0,
                    TotalPowerKW = totalPower / 10.0
                };
                db.AddNewSample(data, DateTime.Now);
            }

            return payload.All(b => b == 0);
        }

        private ushort ExtractW(byte[] payload, int pos)
        {
            pos *= 2;
            var ret = WordAt(pos, payload);
            payload[pos++] = 0;
            payload[pos++] = 0;
            return ret;
        }

        private void ReportFault(string reason, string bytes)
        {
            _logger.Log(reason, "Msg", bytes);
        }

        private void ReportWarning(string reason, SamilMsg message)
        {
            _logger.Log(reason, "Msg", message);
        }
    }
}
