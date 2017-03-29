using Lucky.Home.Sinks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lucky.Home.Devices
{
    [Device("Samil Tester Logger")]
    [Requires(typeof(HalfDuplexLineSink))]
    [Requires(typeof(MockCommandSink))]
    class SamilInverterTesterDevice : SamilInverterDeviceBase
    {
        private Timer _timer;

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
                        Action<SamilMsg, SamilMsg> exec = async (req, expResp) =>
                        {
                            resp = await Exec(samilSink, req, expResp);
                        };
                        var cmd = cmdSink.ReadCommand()?.ToLower();
                        switch (cmd)
                        {
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

        private async Task<string> Exec(HalfDuplexLineSink sink, SamilMsg request, SamilMsg expResponse)
        {
            string err = null;
            var resp = await CheckProtocolWRes(sink, request, expResponse, (data, msg) => err = "ERR: rcvd " + ToString(data));
            if (resp != null)
            {
                err = "OK: " + ToString(resp.Payload);
            }
            return err;
        }
    }
}
