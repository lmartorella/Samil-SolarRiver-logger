using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace SerialEcho
{
    class FakeSamil
    {
        private SerialPort _port;
        private List<byte> _string = new List<byte>();

        public string TimeStamp
        {
            get
            {
                return DateTime.Now.ToString("ss:ffffff");
            }
        }

        public FakeSamil(SerialPort port)
        {
            _port = port;
            _port.ReceivedBytesThreshold = 1;
            _port.ReadTimeout = 1000;
            _port.WriteTimeout = SerialPort.InfiniteTimeout;
        }

        public void Run()
        {
            while (true)
            {
                int b;
                try
                {
                    b = _port.ReadByte();
                }
                catch (TimeoutException)
                {
                    b = -1;
                }
                if (b == -1)
                {
                    ResetString();
                }
                else
                {
                    _string.Add((byte)b);
                    if (CheckString())
                    {
                        SendResponse();
                        ResetString();
                    }
                }
            }
        }

        private void SendResponse()
        {
            // Invert rx/tx
            Swap(2, 4);
            Swap(3, 5);
            _string[7] += 0x80;
            var l = _string[8];
            WordAt(l + 9, (ushort)(WordAt(l + 9) + 0x80));

            Console.WriteLine("{0}  TX <- {1}", TimeStamp, string.Join(" ", _string.Select(b => b.ToString("X2"))));
            _port.Write(_string.ToArray(), 0, _string.Count);
        }

        private ushort WordAt(int pos)
        {
            return (ushort)((_string[pos] << 8) + _string[pos + 1]);
        }

        private void WordAt(int pos, ushort v)
        {
            var b = BitConverter.GetBytes(v);
            _string[pos] = b[1];
            _string[pos + 1] = b[0];
        }

        private void Swap(int i1, int i2)
        {
            var b = _string[i1];
            _string[i1] = _string[i2];
            _string[i2] = b;
        }

        private bool CheckString()
        {
            if (_string.Count < 2)
            {
                return false;
            }
            if (_string[0] != 0x55 && _string[1] != 0xaa)
            {
                ResetString("HEAD");
                return false;
            }
            if (_string.Count < 9)
            {
                return false;
            }
            var l = _string[8];
            if (_string.Count < 9 + l + 2)
            {
                return false;
            }
            // Check CRC
            var crc = _string.Take(l + 9).Aggregate((ushort)0, (b1, b2) => (ushort)(b1 + b2));
            if (crc != WordAt(l + 9))
            {
                ResetString("CRC");
                return false;
            }
            // Ok complete string
            Console.WriteLine("{1} RX  -> {0}", string.Join(" ", _string.Select(b => b.ToString("X2"))), TimeStamp);
            return true;
        }

        private void ResetString(string err = null)
        {
            if (err != null)
            {
                Console.WriteLine("RESET: {0}: {1}", err, string.Join(" ", _string.Select(b => b.ToString("X2"))));
            }
            _string.Clear();
        }
    }
}
