﻿using Lucky.Home.Sinks;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lucky.Home.Devices
{
    [Device("Samil Tester Message")]
    [Requires(typeof(HalfDuplexLineSink))]
    [Requires(typeof(MockCommandSink))]
    class SamilInverterTesterDevice : SamilInverterDeviceBase
    {
        private Timer _timer;
        private string _autocmd;
        private DateTime _lastautocmd;
        private TimeSpan _autocmdPeriod;

        public SamilInverterTesterDevice()
            :base("TESTER")
        {
            _timer = new Timer(o => 
            {
                if (IsFullOnline)
                {
                    // Read command...
                    var cmdSink = Sinks.OfType<MockCommandSink>().FirstOrDefault();
                    var samilSink= Sinks.OfType<HalfDuplexLineSink>().FirstOrDefault();
                    if (cmdSink != null && samilSink != null)
                    {
                        string resp = null;
                        bool echo = false;

                        var cmd = cmdSink.ReadCommand()?.ToLower();
                        Action<SamilMsg, SamilMsg> exec = (req, expResp) =>
                        {
                            resp = Exec(samilSink, cmd, req, expResp, echo);
                        };

                        int secs;
                        if (cmd != null && cmd.Length > 2 && cmd[0] == '^' && int.TryParse(cmd.Substring(1, 1), out secs))
                        {
                            cmd = _autocmd = cmd.Substring(2);
                            _lastautocmd = DateTime.Now;
                            _autocmdPeriod = TimeSpan.FromSeconds(secs);
                        }
                        if (cmd == "" && _lastautocmd != null && DateTime.Now > (_lastautocmd + _autocmdPeriod))
                        {
                            cmd = _autocmd;
                            _lastautocmd = DateTime.Now;
                        }

                        if (cmd != null && cmd.Length > 1 && cmd[0] == '*')
                        {
                            echo = true;
                            cmd = cmd.Substring(1);
                        }

                        switch (cmd)
                        {
                            case "auth":
                                // 3 logout first
                                resp = "auth flow: S";
                                for (int i = 0; i < 3; i++)
                                {
                                    resp += ", logout:" + Exec(samilSink, "logout", LogoutMessage, null, echo);
                                    Thread.Sleep(250);
                                }
                                resp += ", bcast:" + Exec(samilSink, "bcast", BroadcastRequest, BroadcastResponse, echo);
                                Thread.Sleep(250);

                                byte[] id = new byte[] { 0x41, 0x53, 0x35, 0x31, 0x34, 0x42, 0x58, 0x30, 0x33, 0x39 };
                                var loginMsg = LoginMessage.Clone(id.Concat(new byte[] { AddressToAllocate }).ToArray());
                                resp += ", login:" + Exec(samilSink, "login", loginMsg, LoginResponse, echo);
                                break;
                            case "broadcast":
                                exec(BroadcastRequest, BroadcastResponse);
                                break;
                            case "login":
                                exec(LoginMessage, LoginResponse);
                                break;
                            case "logout":
                                exec(LogoutMessage, null);
                                break;
                            case "unknown1":
                                exec(UnknownMessage1, UnknownResponse1);
                                break;
                            case "unknown2":
                                exec(UnknownMessage2, UnknownResponse2);
                                break;
                            case "getpvdata":
                                exec(GetPvDataMessage, GetPvDataResponse);
                                break;
                            case "getfwversion":
                                exec(GetFwVersionMessage, GetFwVersionResponse);
                                break;
                            case "getconfinfo":
                                exec(GetConfInfoMessage, GetConfInfoResponse);
                                break;
                            case "mini":
                                resp = ToString(samilSink.SendReceive(new byte[] { 0x1, 0xaa }, echo, cmd) ?? new byte[0]);
                                break;
                            case "zero":
                                resp = ToString(samilSink.SendReceive(new byte[] { 0 }, echo, cmd) ?? new byte[0]);
                                break;
                            case "ascii":
                                resp = ToString(samilSink.SendReceive(new byte[] { 0x2, 0x40, 0x41 }, echo, cmd) ?? new byte[0]);
                                break;
                            case "long":
                                resp = ToString(samilSink.SendReceive(Encoding.ASCII.GetBytes("0123456789abcdefghijklmnopqrstuwxyz$"), echo, cmd) ?? new byte[0]);
                                break;
                            case null:
                            case "":
                                break;
                            default:
                                resp = "Unknown command";
                                break;
                        }
                        if (resp != null)
                        {
                            cmdSink.WriteResponse(resp);
                        }
                    }
                }
            }, null, 0, 500);
        }

        private string Exec(HalfDuplexLineSink sink, string opName, SamilMsg request, SamilMsg expResponse, bool echo)
        {
            string err = null;
            var resp = CheckProtocolWRes(sink, opName, request, expResponse, (data, msg) => err = "ERR: rcvd " + ToString(data), null, echo);
            if (resp != null)
            {
                err = "OK: " + ToString(resp.Payload);
            }
            return err;
        }
    }
}
