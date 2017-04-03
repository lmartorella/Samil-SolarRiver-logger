using System;
using System.Linq;
using Lucky.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lucky.Home.Sinks;

namespace Lucky.Home.Devices
{
    /// <summary>
    /// Base device class for solar logger
    /// </summary>
    class SamilInverterDeviceBase : DeviceBase
    {
        protected ILogger _logger;

        protected const int AddressToAllocate = 1;
        protected static readonly SamilMsg BroadcastRequest = new SamilMsg(0, 0, 0, 0);
        protected static readonly SamilMsg BroadcastResponse = new SamilMsg(0, 0, 0, 0x80);
        protected static readonly byte[] UnknownResponse1Data = new byte[] { 0x00, 0x01, 0x04, 0x09, 0x0a, 0x0c, 0x11, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36 };

        protected static readonly SamilMsg LoginMessage = new SamilMsg(0, 0, 0, 1);
        protected static readonly SamilMsg LoginResponse = new SamilMsg(AddressToAllocate, 0, 0, 0x81, new byte[] { 0x6 });
        protected static readonly SamilMsg LogoutMessage = new SamilMsg(0, 0, 0, 4);

        protected static readonly SamilMsg UnknownMessage1 = new SamilMsg(0, AddressToAllocate, 1, 0);
        protected static readonly SamilMsg UnknownResponse1 = new SamilMsg(AddressToAllocate, 0, 1, 0x80, UnknownResponse1Data);
        protected static readonly SamilMsg UnknownMessage2 = new SamilMsg(0, AddressToAllocate, 1, 1);
        protected static readonly SamilMsg UnknownResponse2 = new SamilMsg(AddressToAllocate, 0, 1, 0x81);
        protected static readonly SamilMsg GetPvDataMessage = new SamilMsg(0, AddressToAllocate, 1, 2);
        protected static readonly SamilMsg GetPvDataResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x82);
        protected static readonly SamilMsg GetFwVersionMessage = new SamilMsg(0, AddressToAllocate, 1, 3);
        protected static readonly SamilMsg GetFwVersionResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x83);
        protected static readonly SamilMsg GetConfInfoMessage = new SamilMsg(0, AddressToAllocate, 1, 4);
        protected static readonly SamilMsg GetConfInfoResponse = new SamilMsg(AddressToAllocate, 0, 1, 0x84);

        protected class SamilMsg
        {
            private List<byte> _bytes;

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
                    return null;
                }
                var checksum = data.Take(data.Length - 2).Aggregate((ushort)0, (b1, b2) => (ushort)(b1 + b2));
                int l = data[8];
                if (WordAt(0, data) != 0x55aa || WordAt(data.Length - 2, data) != checksum || l != data.Length - 11)
                {
                    return null;
                }
                return new SamilMsg(WordAt(2, data), WordAt(4, data), data[6], data[7], payload ?? data.Skip(9).Take(l).ToArray());
            }

            internal SamilMsg Clone(byte[] payload)
            {
                return FromBytes(ToBytes(), payload);
            }

            public override string ToString()
            {
                return _bytes.ToString();
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

        protected static string ToString(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "<nodata>";
            }
            else
            {
                return string.Join(" ", bytes.Select(b => b.ToString("x2")));
            }
        }

        protected static ushort WordAt(int pos, byte[] data)
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

        public string Name { get; private set; }

        public SamilInverterDeviceBase(string name)
        {
            Name = name;
            _logger = Manager.GetService<LoggerFactory>().Create("Samil_" + Name);
        }

        protected SamilMsg CheckProtocolWRes(HalfDuplexLineSink line, SamilMsg request, SamilMsg expResponse, Action<byte[], SamilMsg> reportFault, Action<SamilMsg> reportWarning = null)
        {
            // Broadcast hello message
            var rcvBytes = (line.SendReceive(request.ToBytes())) ?? new byte[0];
            var res = SamilMsg.FromBytes(rcvBytes);
            // Check correct response
            if (res == null && expResponse != null)
            {
                reportFault(rcvBytes, null);
                return null;
            }
            if (res != null && expResponse == null)
            {
                reportFault(rcvBytes, res);
                return null;
            }
            if (res != null && expResponse != null && !res.CheckStructure(expResponse))
            {
                reportFault(rcvBytes, res);
                return null;
            }
            if (reportWarning != null && !res.CheckPayload(expResponse))
            {
                reportWarning(res);
            }
            return res;
        }
    }
}
