﻿using Lucky.Db;
using Lucky.Home.Power;
using Lucky.Home.Sinks;
using Lucky.Home.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lucky.Home.Devices
{
    /// <summary>
    /// Device that logs solar power immediate readings and stats
    /// </summary>
    [Device("Samil Inverter")]
    [Requires(typeof(HalfDuplexLineSink))]
    class SamilInverterLoggerDevice : SamilInverterDeviceBase, ISolarPanelDevice
    {
        private bool _noSink;
        private bool _inNightMode = false;
        private bool _isSummarySent = true;
        private DateTime _lastValidData = DateTime.Now;
        private ITimeSeries<PowerData, DayPowerData> Database { get; set; }

        /// <summary>
        /// After this time of no samples, enter night mode
        /// </summary>
        private static readonly TimeSpan EnterNightModeAfter = TimeSpan.FromMinutes(2);

        /// <summary>
        /// During day (e.g. when samples are working), retry every 10 seconds
        /// </summary>
        private static readonly TimeSpan CheckConnectionPeriodDay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// During noght (e.g. when last sample is older that 2 minutes), retry every 2 minutes
        /// </summary>
        private static readonly TimeSpan CheckConnectionPeriodNight = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Get a solar PV sample every 15 seconds
        /// </summary>
        private static readonly TimeSpan PollDataPeriod = TimeSpan.FromSeconds(15);

        private ushort _lastFault = 0;
        private IStatusUpdate _lastFaultMessage;
        private bool _inConnectionMode;
        private CancellationTokenSource _terminating = new CancellationTokenSource();
        private TaskCompletionSource<object> _terminated = new TaskCompletionSource<object>();

        public SamilInverterLoggerDevice()
            : base("SAMIL")
        {
        }

        public async Task StartLoop(ITimeSeries<PowerData, DayPowerData> database)
        {
            Database = database;
            _inConnectionMode = true;

            // Start wait loop
            try
            {
                while (!_terminating.IsCancellationRequested)
                {
                    if (_inConnectionMode)
                    {
                        await Task.Delay(InNightMode ? CheckConnectionPeriodNight : CheckConnectionPeriodDay, _terminating.Token);
                        await CheckConnection();
                    }
                    else
                    {
                        // Poll SAMIL for data
                        await Task.Delay(PollDataPeriod, _terminating.Token);
                        await PollData();
                    }
                }
            }
            catch (TaskCanceledException)
            {

            }

            // Do a clean logout
            await LogoutInverter();
            _terminated.SetResult(null);
        }

        protected override async Task OnTerminate()
        {
            _terminating.Cancel();
            await _terminated.Task;
            await base.OnTerminate();
        }

        private void StartConnectionTimer()
        {
            _inConnectionMode = true;
        }

        private void StartDataTimer()
        {
            _inConnectionMode = false;
        }

        public PowerData ImmediateData { get; private set; }

        private HalfDuplexLineSink Sink
        {
            get
            {
                var line = Sinks.OfType<HalfDuplexLineSink>().FirstOrDefault();
                if (line != null && !line.IsOnline)
                {
                    return null;
                }
                return line;
            }
        }

        private async Task CheckConnection()
        {
            // Poll the line
            var line = Sink;
            if (line == null)
            {
                // No sink
                if (!_noSink)
                {
                    _logger.Log("NoSink");
                    _noSink = true;
                }
            }
            else
            {
                if (_noSink)
                {
                    _logger.Log("Sink OK");
                    _noSink = false;
                }
                if (!await LoginInverter(line))
                {
                    if (DateTime.Now - _lastValidData > EnterNightModeAfter)
                    {
                        InNightMode = true;
                    }
                }
            }
        }

        private bool InNightMode
        {
            get
            {
                return _inNightMode;
            }
            set
            {
                if (_inNightMode != value)
                {
                    _inNightMode = value;
                    _logger.Log("NightMode: " + value);
                    if (value)
                    {
                        // From day to night -> connect
                        StartConnectionTimer();

                        // Send summary
                        var summary = Database.GetAggregatedData();
                        // Skip the first migration from day to night at startup during night
                        if (summary != null && !_isSummarySent)
                        {
                            SendSummaryMail(summary);
                            _isSummarySent = true;
                        }
                    }
                }
            }
        }

        private bool CheckProtocol(HalfDuplexLineSink line, SamilMsg request, SamilMsg expResponse, string phase, bool checkPayload)
        {
            Action<SamilMsg> warn = checkPayload ? (Action<SamilMsg>)(w => ReportWarning("Strange payload " + phase, w)) : null;
            return (CheckProtocolWRes(line, phase, request, expResponse, (err, bytes, msg) => ReportFault("Unexpected " + phase, bytes, msg, err), warn)) != null;
        }

        private async Task LogoutInverter(HalfDuplexLineSink line = null)
        {
            if (line == null)
            {
                line = Sink;
            }
            if (line != null)
            {
                // Send 3 logout messages
                for (int i = 0; i < 3; i++)
                {
                    var r = await line.SendReceive(LogoutMessage.ToBytes(), false, false, "logout");
                    // Ignore errors
                    await Task.Delay(500);
                }
            }
        }

        private async Task<bool> LoginInverter(HalfDuplexLineSink line)
        {
            await LogoutInverter(line);
            var res = await CheckProtocolWRes(line, "bcast", BroadcastRequest, BroadcastResponse, (er, bytes, msg) =>
                {
                    if (!InNightMode)
                    {
                        ReportFault("Unexpected broadcast response", bytes, msg, er);
                    }
                });
            if (res == null)
            {
                // Still continue to try login
                return false;
            }

            // Correct response!
            var id = res.Payload;
            _logger.Log("Found", "ID", Encoding.ASCII.GetString(id));

            // Now try to login as address 1
            var loginMsg = LoginMessage.Clone(id.Concat(new byte[] { AddressToAllocate }).ToArray());

            Thread.Sleep(500);
            if (!CheckProtocol(line, loginMsg, LoginResponse, "login response", true))
            {
                // Still continue to try login
                return false;
            }

            // Now I'm logged in!
            // Go with msg 1
            Thread.Sleep(500);
            if (!CheckProtocol(line, UnknownMessage1, UnknownResponse1, "unknown message 1", true))
            {
                // Still continue to try login
                return false;
            }
            // Go with msg 2
            Thread.Sleep(500);
            if (!CheckProtocol(line, UnknownMessage2, UnknownResponse2, "unknown message 2", true))
            {
                // Still continue to try login
                return false;
            }
            // Go with get firmware
            Thread.Sleep(500);
            if (!CheckProtocol(line, GetFwVersionMessage, GetFwVersionResponse, "get firmware response", false))
            {
                // Still continue to try login
                return false;
            }
            // Go with get conf info
            Thread.Sleep(500);
            if (!CheckProtocol(line, GetConfInfoMessage, GetConfInfoResponse, "get configuration", true))
            {
                // Still continue to try login
                return false;
            }

            // OK!
            // Start data timer
            InNightMode = false;
            StartDataTimer();
            return true;
        }

        private async Task PollData()
        {
            var line = Sink;
            if (line == null)
            {
                // disconnect
                StartConnectionTimer();
                return;
            }
            var res = await CheckProtocolWRes(line, "pv", GetPvDataMessage, GetPvDataResponse, (err, bytes, msg) => ReportFault("Unexpected PV data", bytes, msg, err));
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
            else
            {
                // Store last timestamp
                _lastValidData = DateTime.Now;
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
            ushort fault = ExtractW(payload, 7);
            int gridCurrent = ExtractW(payload, 19);
            int gridVoltage = ExtractW(payload, 20);
            int gridFrequency = ExtractW(payload, 21);
            int gridPower = ExtractW(payload, 22);
            int totalPower = (ExtractW(payload, 23) << 16) + ExtractW(payload, 24);

            var db = Database;
            if (db != null)
            {
                var data = new PowerData
                {
                    TimeStamp = DateTime.Now,
                    PowerW = gridPower,
                    PanelVoltageV = panelVoltage / 10.0,
                    GridVoltageV = gridVoltage / 10.0,
                    PanelCurrentA = panelCurrent / 10.0,
                    GridCurrentA = gridCurrent / 10.0,
                    Mode = mode,
                    Fault = fault,
                    EnergyTodayWh = energyToday * 10.0,
                    GridFrequencyHz = gridFrequency / 100.0,
                    TotalEnergyKWh = totalPower / 10.0
                };
                db.AddNewSample(data);
                if (gridPower > 0)
                {
                    // New data, unlock next mail
                    _isSummarySent = false;
                }

                CheckFault(data.Fault);
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

        private void ReportFault(string reason, byte[] msg, SamilMsg message, HalfDuplexLineSink.Error err)
        {
            if (err != HalfDuplexLineSink.Error.Ok)
            {
                _logger.Log(reason, "Err", err);
            }
            else if (message != null)
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
            _logger.Log(reason, "Warn. Payload:", ToString(message.Payload));
        }

        private void CheckFault(ushort fault)
        {
            if (_lastFault != fault)
            {
                var notification = Manager.GetService<INotificationService>();
                DateTime ts = DateTime.Now;
                if (fault != 0)
                {
                    _lastFaultMessage = notification.EnqueueStatusUpdate("Errori Inverter", "Errore: " + ToFaultDescription(fault));
                }
                else
                {
                    // Try to recover last message update
                    bool notify = true;
                    if (_lastFaultMessage != null)
                    {
                        if (_lastFaultMessage.Update(() =>
                        {
                            _lastFaultMessage.Text += ", risolto dopo " + (int)(DateTime.Now - _lastFaultMessage.TimeStamp).TotalSeconds + " secondi.";
                        }))
                        {
                            notify = false;
                        }
                    }
                    if (notify)
                    {
                        notification.EnqueueStatusUpdate("Errori Inverter", "Normale");
                    }
                }
                _lastFault = fault;
            }
        }

        private string ToFaultDescription(ushort fault)
        {
            switch (fault)
            {
                case 0x800:
                    return "No grid connection";
                case 0x1000:
                    return "Grid frequency too low";
                case 0x2000:
                    return "Grid frequency too high";
            }
            return "0x" + fault.ToString("X4");
        }

        private void SendSummaryMail(DayPowerData day)
        {
            var title = Resources.solar_daily_summary_title.Replace("{0}", day.PowerKWh.ToString("0.0"));
            var body = Resources.solar_daily_summary.Replace("{0}", day.PowerKWh.ToString("0.00"));
            //var chart = lastPeriod.ToChart().ToPng(250, 250);
            var attachments = new Tuple<Stream, ContentType, string>[]
            {
                //Tuple.Create(chart, new ContentType("image/png"), "summary")
            };
            Manager.GetService<INotificationService>().SendHtmlMail(title, body, false, attachments);
            Logger.Log("DailyMailSent", "Power", day.PowerKWh);
        }
    }
}
