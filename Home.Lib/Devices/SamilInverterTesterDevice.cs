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
                        var cmd = cmdSink.ReadCommand();
                        switch (cmd)
                        {
                            case "Broadcast":
                                exec(BroadcastRequest, BroadcastResponse);
                                break;
                            case "Login":
                                exec(LoginMessage, LoginResponse);
                                break;
                            case "Logout":
                                exec(LogoutMessage, null);
                                break;
                            case "Unknown1":
                                exec(UnknownMessage1, UnknownResponse1);
                                break;
                            case "Unknown2":
                                exec(UnknownMessage2, UnknownResponse2);
                                break;
                            case "GetPvData":
                                exec(GetPvDataMessage, GetPvDataResponse);
                                break;
                            case "GetFwVersion":
                                exec(GetFwVersionMessage, GetFwVersionResponse);
                                break;
                            case "GetConfInfo":
                                exec(GetConfInfoMessage, GetConfInfoResponse);
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
            var resp = await CheckProtocolWRes(sink, request, expResponse, e => err = "ERR: " + e + ", exp " + expResponse.ToString());
            if (err == null)
            {
                err = "OK: " + string.Join(" ", resp.Item2.Payload.Select(b => b.ToString("x2")));
            }
            return err;
        }
    }
}
