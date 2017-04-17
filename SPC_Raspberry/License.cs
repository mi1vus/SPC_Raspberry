using System;
using System.Collections.Generic;

namespace ProjectSummer.Repository
{
    public class ActivationClass
    {

        #region Получение серийных номеров HDD
        public struct DiskDriveInfoStruct
        {
            public DiskDriveInfoStruct(string _Name, string _Serial)
            {
                Name = _Name;
                Serial = _Serial;
            }
            public string Name;
            public string Serial;

            public override string ToString()
            {
                return string.Format("{0}: {1}", Name, Serial);
            }
        }
        private static DiskDriveInfoStruct[] DiskDriveInfo;
        public static DiskDriveInfoStruct[] DiskDrivesInfo => GetDiskDriveSerialNumbers();

        /// <summary>
        /// Получение серийных номеров HDD
        /// </summary>
        /// <returns></returns>
        public static DiskDriveInfoStruct[] GetDiskDriveSerialNumbers()
        {

            if (DiskDriveInfo != null)
                return DiskDriveInfo;
            Console.WriteLine("Получение списка установленных накопителей:");
            List<DiskDriveInfoStruct> arr = new List<DiskDriveInfoStruct>();


            //ManagementObjectSearcher searcher =
            //    new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            //foreach (ManagementObject wmi_HD in searcher.Get())
            //{
            //    try
            //    {
            //        DiskDriveInfoStruct inf = new DiskDriveInfoStruct("None", "None");
            //        try
            //        {
            //            inf.Name = wmi_HD["Model"].ToString();
            //        }
            //        catch { }
            //        try
            //        {
            //            inf.Serial = wmi_HD["SerialNumber"].ToString().Trim();
            //        }
            //        catch { }
            //        arr.Add(inf);
            //    }
            //    catch { }

            //}c
            //TODO серийник диска
            if (arr.Count < 1)
                arr.Add(new DiskDriveInfoStruct("None", "None"));

            DiskDriveInfo = arr.ToArray();

            return DiskDriveInfo;
        }
        #endregion

        #region Методы проверки имени и серийного номера HDD
        /// <summary>
        /// Проверка серийного номера и модели диска
        /// </summary>
        /// <param name="Name">Модель диска</param>
        /// <param name="SerialNumber">Серийный номер диска</param>
        /// <returns></returns>
        private static bool CheckHDDSerial(string Name, string SerialNumber)
        {
            bool retCode = false;

            if (DiskDriveInfo == null)
                GetDiskDriveSerialNumbers();
            foreach (var item in DiskDriveInfo)
            {
//#if DEBUG
//                log.ClientLog(string.Format("Serial \"{0}\", Name \"{1}\"\r\nDevice: Serial \"{2}\", Device \"{3}\"", SerialNumber, Name, item.Serial, item.Name));
//#endif
                if ((SerialNumber == item.Serial) && (Name == item.Name))
                {
                    retCode = true;
                    break;
                }
            }
            return retCode;
        }

        /// <summary>
        /// Проверка серийного номера и модели диска
        /// </summary>
        /// <param name="_DiskDriveInfo">Информация о HDD</param>
        /// <returns></returns>
        public static bool CheckHDDSerial(DiskDriveInfoStruct _DiskDriveInfo)
        {
            return CheckHDDSerial(_DiskDriveInfo.Name, _DiskDriveInfo.Serial);
        }
        #endregion

    }
}
