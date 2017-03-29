using Lucky.Home.Db;
using Lucky.Home.Power;
using Lucky.Home.Sinks;
using Lucky.Services;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lucky.Home.Devices
{
    [Device("Samil Inverter")]
    [Requires(typeof(HalfDuplexLineSink))]
    class SamilInverterLoggerDevice : SamilInverterDeviceBase, ISolarPanelDevice
    {
        private Timer _timer;
        private bool _noSink;

        private static readonly TimeSpan CheckConnectionPeriod = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PollDataPeriod = TimeSpan.FromSeconds(5);

        public SamilInverterLoggerDevice(string name)
            : base(name)
        {
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
            Action<SamilMsg> warn = checkPayload ? (Action<SamilMsg>)(w => ReportWarning("Strange payload " + phase, w)) : null;
            return (await CheckProtocolWRes(line, request, expResponse, (bytes, msg) => ReportFault("Unexpected " + phase, bytes, msg), warn)) != null;
        }

        private async void LoginInverter(HalfDuplexLineSink line)
        {
            // Send 3 logout messages
            for (int i = 0; i < 3; i++)
            {
                await line.SendReceive(LogoutMessage.ToBytes());
            }
            var res = await CheckProtocolWRes(line, BroadcastRequest, BroadcastResponse, (bytes, msg) => ReportFault("Unexpected broadcast response", bytes, msg));
            if (res == null)
            {
                // Still continue to try login
                return;
            }

            // Correct response!
            var id = res.Payload;
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
            var res = await CheckProtocolWRes(line, GetPvDataMessage, GetPvDataResponse, (bytes, msg) => ReportFault("Unexpected PV data", bytes, msg));
            if (res == null)
            {
                // Relogin!
                StartConnectionTimer();
                return;
            }
            // Decode and record data
            if (!DecodePvData(res.Payload))
            {
                // Report invalid msg
                ReportWarning("Invalid/strange PV data", res);
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

        private void ReportFault(string reason, byte[] msg, SamilMsg message)
        {
            if (message != null)
            {
                _logger.Log(reason, "Msg", message.ToString());
            }
            else
            {
                _logger.Log(reason, "Rcv", ToString(msg));
            }
        }

        private void ReportWarning(string reason, SamilMsg message)
        {
            _logger.Log(reason, "Warn. Msg", message.ToString());
        }
    }
}
