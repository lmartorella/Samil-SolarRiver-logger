using Lucky.HomeMock.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Lucky.HomeMock.Sinks
{
    class SamilPanelMock : SinkMockBase
    {
        private enum State
        {
            Logout,
            Login
        }

        private State _state = State.Logout;
        private byte[] _response = new byte[0];

        public SamilPanelMock()
            :base("SLIN")
        { }

        public override void Read(BinaryReader reader)
        {
            Mode mode;
            var msg = ReadMessage(reader, out mode);
            switch (msg)
            {
                case "55 aa 00 00 00 00 00 04 00 01 03":
                    // Logout
                    _state = State.Logout;
                    Log("logout");
                    break;

                case "55 aa 00 00 00 00 00 00 00 00 ff":  // broadcast
                    if (_state == State.Logout)
                    {
                        // Only respond if logged out
                        RespondWith("55 aa 00 00 00 00 00 80 0a 41 53 35 31 34 42 58 30 33 39 03 ed");
                        Log("bcast");
                    }
                    else
                    {
                        Log("bcast but not logged in");
                    }
                    break;

                case "55 aa 00 00 00 00 00 01 0b 41 53 35 31 34 42 58 30 33 39 01 03 70": // login
                    if (_state == State.Logout)
                    {
                        _state = State.Login;
                        RespondWith("55 aa 00 01 00 00 00 81 01 06 01 88");
                        Log("login");
                    }
                    else
                    {
                        Log("bcast but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 00 00 01 01":
                    // Unknown message 1:
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 80 19 00 01 04 09 0a 0c 11 17 18 19 1a 1b 1c 1d 1e 1f 20 21 22 31 32 33 34 35 36 04 5a");
                        Log("unknown1");
                    }
                    else
                    {
                        Log("unknown1 but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 01 00 01 02":
                    // Unknown message 2:
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 81 00 01 82");
                        Log("unknown2");
                    }
                    else
                    {
                        Log("unknown2 but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 03 00 01 04":
                    // Fw version:
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 83 3c 31 20 20 31 31 30 30 56 31 2e 33 30 20 20 20 53 52 20 31 31 30 30 54 4c 2d 53 00 20 53 61 6d 69 6c 50 6f 77 65 72 00 20 20 20 20 20 41 53 35 31 34 42 58 30 33 39 00 00 00 00 00 00 0e 39");
                        Log("fw ver");
                    }
                    else
                    {
                        Log("fw ver but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 04 00 01 05":
                    // Conf info:
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 84 00 01 85");
                        Log("getConf");
                    }
                    else
                    {
                        Log("getConf but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 02 00 01 03":
                    // PV data
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 82 32 00 00 04 8a 00 26 00 00 00 00 00 01 00 50 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 14 08 9e 13 87 01 c2 00 00 00 fa 05 cb");
                        Log("pv data");
                    }
                    else
                    {
                        Log("pv data but not logged out");
                    }
                    break;

                case "55 aa 00 00 00 01 01 11 00 01 12":
                    // PV data
                    if (_state == State.Login)
                    {
                        RespondWith("55 aa 00 01 00 00 01 91 01 15 01 a8");
                        Log("pv data2");
                    }
                    else
                    {
                        Log("pv data2 but not logged out");
                    }
                    break;

                default:
                    throw new InvalidOperationException("Unknown message: " + msg);
            }
        }

        private void RespondWith(string bytes)
        {
            _response = bytes.Split(' ').Select(s => byte.Parse(s, NumberStyles.HexNumber)).ToArray();
        }

        private enum Mode : byte
        {
            Normal = 0,
            NoResponse = 0xfe,
            Echo = 0xff
        }

        private string ReadMessage(BinaryReader reader, out Mode mode)
        {
            mode = (Mode)reader.ReadByte();
            if (mode != Mode.Normal && mode != Mode.NoResponse)
            {
                throw new InvalidOperationException("UNSUPPORTED MODE: 0x" + mode.ToString("x2"));
            }
            int msgLen = reader.ReadByte() + (reader.ReadByte() << 8);
            List<byte> ret = new List<byte>();
            for (int i = 0; i < msgLen; i++)
            {
                ret.Add(reader.ReadByte());
            }
            return string.Join(" ", ret.Select(b => b.ToString("x2")));
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((ushort)_response.Length);
            writer.Write(_response);
            _response = new byte[0];
        }

        private void Log(string command)
        {
            LogLine?.Invoke(this, new ItemEventArgs<string>("Solar: " + command));
        }

        /// <summary>
        /// For logging
        /// </summary>
        public event EventHandler<ItemEventArgs<string>> LogLine;
    }
}
