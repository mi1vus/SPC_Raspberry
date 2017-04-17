using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace LifeStyleDiscounter
{
    class TLV
    {

        public struct TLV_Pack
        {
            public int tag;
            public int length;
            public byte[] data;
        }
        ArrayList tlvArray = new ArrayList();
        private bool error = false;

        /// <summary>
        /// В распознаных или упакованных данных имеются ошибки.
        /// </summary>
        public bool Error
        {
            get
            {
                return error;
            }
        }

        public TLV_Pack this[int i]
        {
            get
            {
                return (TLV_Pack)tlvArray[i];
            }
        }

        /// <summary>
        /// Колличество TLV последовательностей находящееся в памяти.
        /// </summary>
        public int Count
        {
            get
            {
                return tlvArray.Count;
            }
        }

        /// <summary>
        /// Удаление всех TVL пакетов хранящихся в памяти.
        /// </summary>
        public void Clear()
        {
            tlvArray.Clear();
        }
        /// <summary>
        /// Добавление TLV пакета.
        /// </summary>
        /// <param name="TLV_Tag">Тег</param>
        /// <param name="data">Данные</param>
        public void AddTag(int TLV_Tag, byte[] data)
        {
            TLV_Pack tlv = new TLV_Pack();
            tlv.tag = TLV_Tag;
            tlv.data = data;
            tlv.length = data.Length;
            tlvArray.Add(tlv);
        }

        /// <summary>
        /// Упаковка набора TLV последовательностей в массив байт.
        /// </summary>
        /// <param name="tmp">Упакованные данные</param>
        /// <returns>Error</returns>
        public bool PackData(ref byte[] tmp)
        {
            try
            {
                ArrayList tmpArray = new ArrayList();
                int tmplen = 0;
                for (int z = 0; z < tlvArray.Count; z++)
                {
                    tmpArray.Add((byte)((TLV_Pack)tlvArray[z]).tag);
                    tmplen = ((TLV_Pack)tlvArray[z]).length;
                    if (tmplen < 128) tmpArray.Add((byte)tmplen);
                    else
                    {
                        tmp = BitConverter.GetBytes(tmplen);
                        tmpArray.Add(((byte)(tmp.Length + 128)));
                        for (int y = 0; y < tmp.Length; y++)
                        {
                            tmpArray.Add(tmp[y]);
                        }
                    }
                    tmp = ((TLV_Pack)tlvArray[z]).data;
                    for (int y = 0; y < tmp.Length; y++)
                    {
                        tmpArray.Add(tmp[y]);
                    }
                }
                tmp = new byte[tmpArray.Count];
                for (int y = 0; y < tmpArray.Count; y++)
                {
                    tmp[y] = (byte)tmpArray[y];
                }
                error = false;
            }
            catch
            {
                error = true;
            }
            return error;

        }



        /// <summary>
        /// Распознавание данных упакованных в фотмат TLV
        /// </summary>
        /// <param name="data">Массив упакованных данных</param>
        public bool UnpackData(byte[] data)
        {
            tlvArray.Clear();

            TLV_Pack tlv = new TLV_Pack();

            int end = 0;
            byte lenlen = 0;

            for (int z = 0; z < data.Length; )
            {
                try
                {
                    tlv.tag = data[z];
                    z++;
                    if (data[z] < 128)
                    {
                        tlv.length = data[z];
                    }
                    else
                    {
                        lenlen = (byte)(data[1] - 128);
                    }
                    z++;
                    end = z + lenlen;
                    for (; z < end; z++)
                    {
                        tlv.length = tlv.length * 256 + data[z];
                    }
                    end = z + tlv.length;
                    int point = z;
                    tlv.data = new byte[tlv.length];
                    for (; z < end; z++)
                    {
                        tlv.data[z - point] = data[z];
                    }
                    tlvArray.Add(tlv);
                    error = false;
                }
                catch
                {
                    error = true;
                }
            }
            return error;

        }
        public byte[] GetTagData(int tag)
        {
            byte[] retdata = new byte[0];
            foreach (TLV_Pack tmp in tlvArray)
            {
                if (tmp.tag == tag)
                {
                    retdata = tmp.data;
                    break;
                }
            }
            if (retdata.Length < 1)
                retdata = new byte[] { 0 };
            return retdata;
        }

    }
}
