using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeStyleDiscounter
{
    class ECDLC
    {
        private byte[] data = new byte[0];
        private byte[] packdata = new byte[0];

        public byte[] Pack
        {
            get { return packdata; }
        }

        public byte[] Data
        {
            get { return data; }
        }

        public int status = 0;

        public int PackData(byte[] in_data)
        {
            packdata = new byte[in_data.Length + 6];
            packdata[0] = 0x02;
            packdata[1] = (byte)((in_data.Length > 255) ? (in_data.Length / 256) : 0);
            packdata[2] = (byte)Math.IEEERemainder((byte)in_data.Length, 256);
            for (int z = 0; z < in_data.Length; z++)
            {
                packdata[z + 3] = in_data[z];
            }
            packdata[in_data.Length + 3] = 0x03;

            byte[] tmpdata = new byte[packdata.Length - 3];
            for (int z = 0; z < tmpdata.Length; z++)
            {
                tmpdata[z] = packdata[z + 1];
            }
            int crc16 = CRC16_Calculate(tmpdata);
            packdata[in_data.Length + 4] = (byte)((crc16 > 255) ? (crc16 / 256) : 0);
            packdata[in_data.Length + 5] = (byte)Math.IEEERemainder((byte)crc16, 256);
            return 0;
        }

        public int AddData(byte[] pack)
        {
            if (status != 0x17)
            {
                data = new byte[0];
            }
            int ret = 0;
            if (pack[0] == 0x02)
            {
                int len = (pack[1] * 256) + pack[2];
                byte[] tmpdata = new byte[data.Length + len];
                if (!(CRC16_Compare(pack))) return 1;
                data.CopyTo(tmpdata, 0);
                try
                {
                    for (int z = 0; z < tmpdata.Length; z++)
                    {
                        tmpdata[z + data.Length] = pack[3 + z];
                    }
                    status = pack[3 + len];
                    data = tmpdata;
                }
                catch
                { }

            }
            return ret;
        }

        private bool CRC16_Compare(byte[] cdata)
        {
            bool ret = false;
            if (cdata[0] == 0x02)
            {
                int datalen = (cdata[1] * 256) + cdata[2];
                byte[] tmpdata = new byte[cdata.Length - 3];
                for (int z = 0; z < tmpdata.Length; z++)
                {
                    tmpdata[z] = cdata[z + 1];
                }
                if ((cdata[datalen + 3] != 0x03) && (cdata[datalen + 3] != 0x17)) return false;
                ushort crc16 = (ushort)((cdata[cdata.Length - 2] * 256) + cdata[cdata.Length - 1]);
                ret = (crc16 == CRC16_Calculate(tmpdata));
            }
            return ret;
        }

        ushort CRC16_Calculate(byte[] p)
        {
            ushort c = 0;
            ushort a3;
            for (int z = 0; z < p.Length; z++)
            {
                a3 = (ushort)((ushort)(p[z] ^ c) ^ (ushort)((p[z] ^ c) << 4));
                c = (ushort)((c >> 8) ^ (a3 >> 12) ^ (a3 >> 5) ^ a3);

            }
            return c;
        }

    }
}
