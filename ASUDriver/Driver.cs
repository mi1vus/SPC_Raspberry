using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ProjectSummer.Repository;
using RemotePump_Driver;
//using ServioPumpGAS_Driver;

namespace ASUDriver
{
        public class Driver
        {
            //public static const string host = /*"127.0.0.1"*/  "85.12.218.7";
            //public static byte[] hostB = new byte[] {0x55, 0xC, 0xDA, 0x7};
            public static string host2 = /*"127.0.0.1"*/  "85.12.204.135";
            public static byte[] hostB2 = new byte[] { 0x55, 0xC, 0xCC, 0x87 };
            public const int port = /*80*/  3505;

            public const int terminal = 1;

            #region Глобальные переменные
            public static Logger log = new Logger("SmartPumpControl_Driver");
            public static object locker = new object();

            public static long TransCounter;
            public static object TransCounterLocker = new object();

            private static long callback_SetDose(OrderInfo Order)
            {
                lock (locker)
                {
                    try
                    {
                        long result = -1;
                        for (int z = 0; z < 5; z++)
                        {
                            result = callback_SetDose_callback?.Invoke(Order, ctx) ?? -1;
                            if (result != -1)
                                break;
                            else
                                Thread.Sleep(1000);
                        }
                        return result;
                    }
                    catch (Exception e)
                    {
                        var r = e;
                    }
                    return -1;
                }
            }
            private static GetPumpStateResponce callback_GetDose(long Pump)
            {
                lock (locker)
                {
                    try
                    {
                        return callback_GetDose_callback?.Invoke(Pump, ctx) ?? default(GetPumpStateResponce);
                    }
                    catch { }
                    return default(GetPumpStateResponce);
                }
            }
            private static int callback_CancelDose(long TransID)
            {
                lock (locker)
                {
                    try
                    {
                        return callback_CancelDose_callback?.Invoke(TransID, ctx) ?? -1;
                    }
                    catch { }
                    return -1;
                }
            }

            private static int callback_SQL_Write(string SQL_Request, int retry_count = 5)
            {
                log.Write(
"\r\nЗапрос на выполнение скрипта SQL скрипта: " + SQL_Request, 2, true);
                lock (locker)
                {
                    try
                    {
                        log.Write(
"\r\nВыполнение SQL скрипта: " + SQL_Request);
                        int result = -1;
                        for (int z = 0; z < retry_count; z++)
                        {
                            result = callback_SQL_Write_callback?.Invoke(SQL_Request, ctx) ?? -1;
                            log.Write(
$"Попытка \"{z + 1}\" результат: {result}");

                            if (result == 1)
                                break;
                            else
                                Thread.Sleep(1000);
                        }

                        return result;
                    }
                    catch { }
                    return -1;
                }
            }
            private static string[] callback_SQL_Read(string SQL_Request, int retry_count = 5)
            {
                try
                {
                    log.Write(
"\r\nЗапрос на выполнение скрипта SQL скрипта: " + SQL_Request, 2, true);
                    string result = "";
                    lock (locker)
                    {

                        log.Write(
"\r\nВыполнение SQL скрипта: " + SQL_Request, 2, true);
                        for (int z = 0; z < retry_count; z++)
                        {
                            var ptr = callback_SQL_Read_callback?.Invoke(SQL_Request, ctx) ?? IntPtr.Zero;
                            if (ptr != IntPtr.Zero)
                                result = Marshal.PtrToStringAnsi(ptr);
                            log.Write(
$"Попытка \"{z + 1}\" результат: {result}", 2, true);

                            if (result != "")
                                break;
                            else
                                Thread.Sleep(1000);
                        }
                    }
                    var tmpres = new List<string>();
                    if (result != null && result != "empty" && result != "")
                    {
                        var tmp = result.Split(new string[] { ";empty;" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in tmp)
                        {
                            tmpres.Add(t);
                        }
                    }
                    return tmpres.ToArray();
                }
                catch { }
                return new string[0];
            }

            private static int callback_HoldPump(int Pump, byte ReleasePump)
            {
                lock (locker)
                {
                    try
                    {
                        return callback_HoldPump_callback?.Invoke(Pump, ReleasePump, ctx) ?? -1;
                    }
                    catch { }
                    return -1;
                }
            }

            private static SetDose_Delegate callback_SetDose_callback;
            private static GetDose_Delegate callback_GetDose_callback;
            private static CancelDose_Delegate callback_CancelDose_callback;
            private static HoldPump_Delegate callback_HoldPump_callback;
            #region интеграция с ServioPump
            private static SQL_Write_Delegate callback_SQL_Write_callback;
            private static SQL_Read_Delegate callback_SQL_Read_callback;
            #endregion

            #region Интеграция с другими АСУ

            private static UpdateFillingOver_Delegate UpdateFillingOver_callback;
            private static InsertCardInfo_Delegate InsertCardInfo_callback;
            private static SaveReciept_Delegate SaveReciept_callback;
            #endregion

            private static IntPtr ctx;
            public static object TransMemoryLocker = new object();
            public static Dictionary<long, OrderInfo> TransMemory = new Dictionary<long, OrderInfo>();
            #endregion

            #region Структуры
            public struct GetPumpStateResponce
            {
                public byte DispStatus;//0
                public ushort StateFlags;//1
                public int ErrorCode;//3
                public byte DispMode;//7
                public byte UpNozz;//8
                public byte UpFuel;//9
                public byte UpTank;//10
                public Int64 TransID;//11
                public byte PreselMode;//19
                public double PreselDose;//20
                public double PreselPice;//28
                public byte PreselFuel;//36
                public byte PreselFullTank;//37
                public double FillingVolume;//38
                public double FillingPrice;//46
                public double FillingSum;//54

                public static GetPumpStateResponce ReadFromPtr(IntPtr Ptr)
                {
                    var result = new GetPumpStateResponce()
                    {
                        DispStatus = Marshal.ReadByte(Ptr, 0),
                        StateFlags = (ushort)Marshal.ReadInt16(Ptr, 1),
                        ErrorCode = Marshal.ReadInt32(Ptr, 3),
                        DispMode = Marshal.ReadByte(Ptr, 7),
                        UpNozz = Marshal.ReadByte(Ptr, 8),
                        UpFuel = Marshal.ReadByte(Ptr, 9),
                        UpTank = Marshal.ReadByte(Ptr, 10),
                        TransID = Marshal.ReadInt64(Ptr, 11),
                        PreselMode = Marshal.ReadByte(Ptr, 19),
                        PreselDose = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 20)),
                        PreselPice = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 28)),
                        PreselFuel = Marshal.ReadByte(Ptr, 36),
                        PreselFullTank = Marshal.ReadByte(Ptr, 37),
                        FillingVolume = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 38)),
                        FillingPrice = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 46)),
                        FillingSum = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 54)),
                    };
                    //for (int z = 0; z < 62; z++)
                    //    Marshal.WriteByte(Ptr, z, 0);
                    return result;
                }
                public override string ToString()
                {
                    return string.Format("DispStatus:" + DispStatus.ToString()
                                        + "\r\nStateFlags:" + StateFlags.ToString()
                                        + "\r\nErrorCode:" + ErrorCode.ToString()
                                        + "\r\nDispMode:" + DispMode.ToString()
                                        + "\r\nUpNozz:" + UpNozz.ToString()
                                        + "\r\nUpFuel:" + UpFuel.ToString()
                                        + "\r\nUpTank:" + UpTank.ToString()
                                        + "\r\nTransID:" + TransID.ToString()
                                        + "\r\nPreselMode:" + PreselMode.ToString()
                                        + "\r\nPreselDose:" + PreselDose.ToString()
                                        + "\r\nPreselPice:" + PreselPice.ToString()
                                        + "\r\nPreselFuel:" + PreselFuel.ToString()
                                        + "\r\nPreselFullTank:" + PreselFullTank.ToString()
                                        + "\r\nFillingVolume:" + FillingVolume.ToString()
                                        + "\r\nFillingPrice:" + FillingPrice.ToString()
                                        + "\r\nFillingSum:" + FillingSum.ToString());
                }
            }
#warning Удален SetDoseResponse
            /*public struct SetDoseResponse
            {
                //Offset: 0
                public Int64 TransNum;
                //Offset: 8
                public DateTime _DateTime;
                //Offset: 16
                public Int32 RetCode;

                public static SetDoseResponse ReadFromIntPtr(IntPtr Ptr)
                {
                    try
                    {
                        var transNum = Marshal.ReadInt64(Ptr);
                        var dateTime = DateTime.FromOADate(BitConverter.Int64BitsToDouble(Marshal.ReadInt64(Ptr, 8)));
                        var retCode = Marshal.ReadInt32(Ptr, 16);

                        return new SetDoseResponse() { TransNum = transNum, _DateTime = dateTime, RetCode = retCode };
                    }
                    catch (Exception ex)
                    {
                                            log.Write(ex.ToString());
                    }
                    return new SetDoseResponse();
                }

                public override string ToString()
                {
                    return string.Format("TransNum = {0}, DateTime = {1}, RetCode = {2}", TransNum, _DateTime, RetCode);
                }
            }*/
            //  [StructLayout(LayoutKind.Sequential)]
            //  public struct TransactionInfo
            // {
            //   public int Pump;
            //public int PaymentCode;
            //public int Fuel;
            //public int OrderInMoney;
            //public int Quantity;
            //public int Price;
            //public int Amount;
            //[MarshalAs(UnmanagedType.LPStr)]
            //public string CardNum;
            //[MarshalAs(UnmanagedType.LPStr)]
            //public string RRN;
            //    public RemotePump_Driver.OrderInfo Order;

            //public static TransactionInfo ReadFromIntPtr(IntPtr ptr)
            //{
            //    return new TransactionInfo()
            //    {
            //        Pump = Marshal.ReadInt32(ptr, 0),
            //        PaymentCode = Marshal.ReadInt32(ptr, 4),
            //        Fuel = Marshal.ReadInt32(ptr, 8),
            //        OrderInMoney = Marshal.ReadInt32(ptr, 12),
            //        Quantity = Marshal.ReadInt32(ptr, 16),
            //        Price = Marshal.ReadInt32(ptr, 20),
            //        Amount = Marshal.ReadInt32(ptr, 24),
            //        CardNum = Marshal.PtrToStringAnsi(ptr, 28),
            //        RRN = Marshal.PtrToStringAnsi(ptr, 32),
            //    };
            //}

            // }

            public static void WriteOrderToIntPtr(OrderInfo Order, IntPtr ptr)
            {
                Marshal.WriteInt32(ptr, 0, Order.PumpNo);
                Marshal.WriteInt32(ptr, 4, Order.PaymentCode);
                Marshal.WriteInt32(ptr, 8, Order.ProductCode);
                Marshal.WriteInt32(ptr, 12, (byte)Order.OrderMode);
                Marshal.WriteInt32(ptr, 16, (int)(Order.Quantity * 1000));
                Marshal.WriteInt32(ptr, 20, (int)(Order.Price * 100));
                Marshal.WriteInt32(ptr, 24, (int)(Order.Amount * 100));
                Marshal.WriteIntPtr(ptr, 28, Marshal.StringToHGlobalAnsi(Order.CardNO));
                Marshal.WriteIntPtr(ptr, 32, Marshal.StringToHGlobalAnsi(Order.OrderRRN));
            }
            public static void ReadOrderFromIntPtr(OrderInfo Order, IntPtr ptr)
            {
                Marshal.WriteInt32(ptr, 0, Order.PumpNo);
                Marshal.WriteInt32(ptr, 4, Order.PaymentCode);
                Marshal.WriteInt32(ptr, 8, Order.ProductCode);
                Marshal.WriteInt32(ptr, 12, (byte)Order.OrderMode);
                Marshal.WriteInt32(ptr, 16, (int)(Order.Quantity * 1000));
                Marshal.WriteInt32(ptr, 20, (int)(Order.Price * 100));
                Marshal.WriteInt32(ptr, 24, (int)(Order.Amount * 100));
                Marshal.WriteIntPtr(ptr, 28, Marshal.StringToHGlobalAnsi(Order.CardNO));
                Marshal.WriteIntPtr(ptr, 32, Marshal.StringToHGlobalAnsi(Order.OrderRRN));
            }

            public struct FuelInfo
            {
                public int ID;
                public int InternalCode;
                public string Name;
                public decimal Price;
                public override string ToString() => $"ID = {ID:00}, InternalCode = {InternalCode:00}, Name = {Name}, Price = {Price:0.00}р";

            }

            public struct PumpInfo
            {
                public int Pump;
                public bool Blocked;
                public int DispStatus;
                public int UpNozzle;
                #warning В случае если на ТРК два продукта с одинаковым внешним кодом будет полная хрень!!!
                public Dictionary<string, FuelInfo> Fuels;
                public DateTime? BlockInitTime;
                //public void OnBlockTimerEvent(object source, ElapsedEventArgs e)
                //{
                //    Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime);
                //    this.BlockTimer.Enabled = false;
                //    this.BlockTimer.Stop();
                //    this.Blocked = false;
                //}
            }

            public static void CollectOldOrderThread(object Pump)
            {
                object item = null;
                var pumpId = (int)Pump;
                log.Write($"\tCollectOldOrderThread Pump:{Pump}\r\n", 1, true);
                Thread.Sleep(XmlPumpClient.WaitAnswerTimeout);

                bool res;

                lock (XmlPumpClient.StatusesLocker)
                    res = XmlPumpClient.Statuses.TryGetValue(
                              new Tuple<int, MESSAGE_TYPES>(pumpId, MESSAGE_TYPES.OnPumpStatusChange), out item)
                          && item != null &&
                          ((OnPumpStatusChange)item)?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING;

                if (res)
                {
                    log.Write(XmlPumpClient.Collect(terminal, pumpId, 0, "", XmlPumpClient.WaitAnswerTimeout)
                        ? $"\tCollectOldOrderThread статус WAITING_COLLECTING Pump:{Pump} - Sucess\r\n"
                        : $"\tCollectOldOrderThread статус WAITING_COLLECTING Pump:{Pump} - Fail\r\n", 1, true);
                    //Thread.Sleep(500);
                    //XmlPumpClient.PumpGetStatus(terminal, pumpId, 1);
                    //XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(pumpId,
                    //    MESSAGE_TYPES.OnPumpStatusChange), out item);
                }
            }

            public static void WaitCollect(object TransCounter)
            {
                OrderInfo order = new OrderInfo();

                int cnt = 2;
                while (cnt > 0)
                {
                    lock (TransMemoryLocker)
                    {
                        if (TransMemory.TryGetValue((long)TransCounter, out order))
                        {
                            break;
                        }
                        else
                            --cnt;
                    }
                    if (cnt > 0)
                        Thread.Sleep(500);
                }

                if (cnt <= 0)
                {
                    log.Write(
                                $"\t\tERROR!!! WaitCollectThread:\r\n\t\tне найден заказ №{(long)TransCounter}\r\n", 0, true);
                    return;
                }
                WaitCollectThread(order);
            }
            public static void WaitCollectBenzuber(object TransactionID)
            {
                OrderInfo order = new OrderInfo();
                Benzuber.ExcangeServer.Request request = new Benzuber.ExcangeServer.Request();

                int cnt = 2;
                while (cnt > 0)
                {
                    lock (Benzuber.ExcangeServer.TransesLocker)
                    {
                        if (Benzuber.ExcangeServer.Transes.TryGetValue((string)TransactionID, out request))
                        {
                            order.PumpNo = request.Pump.Value;
                            order.OrderRRN = request.TransactionID;
                            order.PumpRRN = request.Shift.DocNum.ToString();
                            order.ProductCode = request.Fuel.Value;
                            order.PaymentCode = 99;
                            order.Amount = request.Amount.Value;
                            order.Price = Fuels.First(t => t.Value.ID == request.Fuel.Value).Value.Price;
                            order.Quantity = order.Amount/order.Price;

                            break;
                        }
                        else
                            --cnt;
                    }
                    if (cnt > 0)
                        Thread.Sleep(500);
                }

                if (cnt <= 0)
                {
                    log.Write(
$@"
        \t\tERROR!!! WaitCollectThreadBenzuber:
        \r\n\t\tне найден транзакция №{(string)TransactionID}\r\n", 0, true);
                    return;
                }
                WaitCollectThread(order);
            }

            private static void WaitCollectThread(OrderInfo order)
            {
                var endMessage = XmlPumpClient.EndFillingEventWait(order.PumpNo, order.OrderRRN);
                long transCounter;
                if (endMessage == null)
                {
                    log.Write(
$@"
        \t\tERROR!!! WaitCollectThread: нет события окончания налива \r\n
        \t\tPump: {order.PumpNo}\r\n
        \t\tRNN: {order.OrderRRN}\r\n", 0, true);
                    
                }
                if (!long.TryParse(order.PumpRRN, out transCounter) )
                {
                log.Write(
$@"
        \t\tERROR!!! \r\n PumpRRN неверный
        PumpRRN {order.PumpRRN}\r\n", 0, true);
                    return;
                }

                log.Write(
$@"WaitCollectThread:FillingOver:\r\n
TransCounter: {transCounter}\r\n
Liters: {(endMessage?.Liters ?? 0) * 10}\r\n
Money: {endMessage?.Money ?? 0}\r\n", 2, true);

                int amount = (int) ((endMessage?.Liters ?? 0)*order.Price);
                int quantity = (endMessage?.Liters ?? 0)*10;

                //TODO Проба со скидками!!!!
                FillingOver(transCounter, quantity, amount);
                if (order.PaymentCode == 99)
                {
                    Benzuber.ExcangeServer.TransOvers.Add(order.OrderRRN, new Benzuber.ExcangeServer.Request
                    {
                        TransactionID = order.OrderRRN,
                        Amount = amount/100M,
                        Fuel = order.ProductCode,
                        Pump = endMessage?.PumpNo
                    });
                }
                //FillingOver((long)TransCounter, (endMessage?.Liters ?? 0) * 10, endMessage?.Money ?? 0);

                var discount = 0;//100; //(order.BasePrice - order.Price) * order.Quantity;
                var fuel = Fuels.First(t => t.Value.ID == order.ProductCode);
                int allowed = 0;
                lock (PumpsLocker)
                    foreach (var pumpFuel in Pumps[order.PumpNo].Fuels)
                    {
                        allowed += 1 << (pumpFuel.Value.ID - 1);
                    }
                log.Write(
$@"WaitCollectThread:Collect:\r\n
terminal: {terminal}\r\n
transCounter: {transCounter}\r\n
allowed: {allowed}\r\n
PumpNo: {order.PumpNo}\r\n
OrderRRN: {order.OrderRRN}\r\n"
                    , 2, true);


                if (!XmlPumpClient.Collect(terminal, order.PumpNo, transCounter, order.OrderRRN, XmlPumpClient.WaitAnswerTimeout))
                {
                    log.Write(
$@"
        \t\tERROR!!! WaitCollectThread:
        \r\n\t\tнет события ответа на Collect заказа RNN{order.OrderRRN}\r\n", 0, true);
                    return;
                }

                //TODO Проба со скидками!!!!
    //                if (order.PaymentCode == 99)
    //                {
    //                    XmlPumpClient.SaleDataSale(terminal, order.PumpNo, allowed,
    //                        order.Amount, ((decimal)amount) / 100, discount,
    //                        order.Quantity, ((decimal)quantity) / 1000, PAYMENT_TYPE.FuelCard,
    //                        order.OrderRRN, order.ProductCode, fuel.Key, (int)(order.Price/*fuel.Value.Price*/ * 100), "", 1);
    //                    //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
    //                    //    order.Amount, order.OverAmount, discount,
    //                    //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
    //                    //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
    //                    log.Write(
    //$@"WaitCollectThread:SaleDataSale: Benzuber\r\n
    //terminal: {terminal}\r\n
    //PumpNo: {order.PumpNo}\r\n
    //allowed: {allowed}\r\n
    //Amount: {order.Amount}\r\n
    //OverAmount: {((decimal)amount) / 100}\r\n
    //discount: {discount}\r\n
    //Quantity: {((decimal)quantity) / 1000}\r\n
    //OverQuantity: {order.OverQuantity}\r\n
    //PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n
    //OrderRRN: {order.OrderRRN}\r\n
    //ProductCode: {order.ProductCode}\r\n
    //Key: {fuel.Key}\r\n
    //fuelPrice: {(int)(order.Price/*fuel.Value.Price*/ * 100)}\r\n", 2, true
    //                    );

    //                ////XmlPumpClient.FiscalEventReceipt(Driver.terminal, order.PumpNo,
    //                ////    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
    //                ////    (endMessage?.Money ?? 0) / 100m, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
    //                ////log.Write($"чек:\r\n" +
    //                ////    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
    //                ////    $"GetDocNum: {GetDocNum()}\r\n" +
    //                ////    $"GetShiftNum: {GetShiftNum()}\r\n" +
    //                ////    $"OverAmount: {(endMessage?.Money ?? 0)/100.0}\r\n" +
    //                ////    $"OrderRRN: {order.OrderRRN}\r\n");

    //                //XmlPumpClient.Init(Driver.terminal, order.PumpNo, -order.PumpNo, XmlPumpClient.WaitAnswerTimeout, 1);
    //                //log.Write("изм. статуса\r\n");

    //                ////var res = XmlPumpClient.answers;
    //                //var res2 = XmlPumpClient.Statuses;
    //                //var res3 = XmlPumpClient.Fillings;

    //                XmlPumpClient.ClearAllTransactionAnswers(order.PumpNo, order.OrderRRN);
    //            }
            }

            public static Dictionary<string, FuelInfo> Fuels = new Dictionary<string, FuelInfo>();

            public static object PumpsLocker = new object();
            public static Dictionary<int, PumpInfo> Pumps = new Dictionary<int, PumpInfo>();
            public static bool isInit = false;
            #endregion
            public static string SystemName { get; private set; } = "Unknown";

            #region Экспортируемые драйвером функции
            /// <summary>Функция  загрузки драйвера для АСУ Сервио</summary>
            /// <param name="callback">Ссылка на функцию обратного вызова для установки заказа на ТРК</param>
            /// <param name="ctx"></param>
            /// <returns>1 - при успешной загрузке, </returns>
            [Obfuscation()]
            public static byte Open_Servio(SetDose_Delegate _callback_SetDose,
                GetDose_Delegate _callback_GetDose,
                SQL_Write_Delegate _callback_SQL_Write,
                SQL_Read_Delegate _callback_SQL_Read,
                CancelDose_Delegate _callback_CancelDose,
                HoldPump_Delegate _callback_HoldPump,
                IntPtr ctx)
            => OpenBase(_callback_SetDose, _callback_GetDose, _callback_SQL_Write, _callback_SQL_Read, _callback_CancelDose, _callback_HoldPump, null, null, null, "Servio Pump GAS 2.67+", ctx);

            public static byte OpenBase(SetDose_Delegate _callback_SetDose,
                                        GetDose_Delegate _callback_GetDose,
                                        SQL_Write_Delegate _callback_SQL_Write,
                                        SQL_Read_Delegate _callback_SQL_Read,
                                        CancelDose_Delegate _callback_CancelDose,
                                        HoldPump_Delegate _callback_HoldPump,

                                        UpdateFillingOver_Delegate _callback_UpdateFillingOver,
                                        InsertCardInfo_Delegate _callback_InsertCardInfo,
                                        SaveReciept_Delegate _callback_SaveReciept,
                                        string _SystemName,
                                        IntPtr ctx)
            {
                try
                {

                    //Application.EnableVisualStyles();
                    //Application.SetCompatibleTextRenderingDefault(false);
                }
                catch { }
                try
                {
                    log.Write("", 1, true);
                    SystemName = _SystemName;
                    log.Write($"Драйвер открыт. Система управления: \"{SystemName ?? "Нет данных"}\"", 1, true);

                    log.Write("callback_SQL_Write ".PadLeft(37) + ((_callback_SQL_Write == null) ? "is null" : "success set"), 1, true);
                    if (_callback_SQL_Write != null) callback_SQL_Write_callback = _callback_SQL_Write;

                    log.Write("callback_SQL_Read ".PadLeft(37) + ((_callback_SQL_Read == null) ? "is null" : "success set"), 1, true);
                    if (_callback_SQL_Read != null) callback_SQL_Read_callback = _callback_SQL_Read;


                    log.Write("callback_UpdateFillingOver ".PadLeft(37) + ((_callback_UpdateFillingOver == null) ? "is null" : "success set"), 1, true);
                    if (_callback_UpdateFillingOver != null) UpdateFillingOver_callback = _callback_UpdateFillingOver;

                    log.Write("callback_InsertCardInfo ".PadLeft(37) + ((_callback_InsertCardInfo == null) ? "is null" : "success set"), 1, true);
                    if (_callback_InsertCardInfo != null) InsertCardInfo_callback = _callback_InsertCardInfo;

                    log.Write("callback_SaveReciept ".PadLeft(37) + ((_callback_SaveReciept == null) ? "is null" : "success set"), 1, true);
                    if (_callback_SaveReciept != null) SaveReciept_callback = _callback_SaveReciept;

                    log.Write("callback_SetDose ".PadLeft(37) + ((_callback_SetDose == null) ? "is null" : "success set"), 1, true);
                    if (_callback_SetDose != null) callback_SetDose_callback = _callback_SetDose;

                    log.Write("callback_GetDose ".PadLeft(37) + ((_callback_GetDose == null) ? "is null" : "success set"), 1, true);
                    if (_callback_GetDose != null) callback_GetDose_callback = _callback_GetDose;



                    log.Write("callback_CancelDose ".PadLeft(37) + ((_callback_CancelDose == null) ? "is null" : "success set"), 1, true);
                    if (_callback_CancelDose != null) callback_CancelDose_callback = _callback_CancelDose;

                    log.Write("callback_HoldPump ".PadLeft(37) + ((_callback_HoldPump == null) ? "is null" : "success set"), 1, true);
                    if (_callback_HoldPump != null) callback_HoldPump_callback = _callback_HoldPump;

                    return 1;
                }
                catch (Exception ex)
                {
                    log.Write("Ошибка при открытии драйвера. " + ex.ToString(), 0, true);
                    return 0;
                }

            }
            //static TestWindow window;
            public static void InitXmlClient()
            {
                try
                {
                    Params =
                        Serialization.Deserialize<Serialization.SerializableDictionary<string, string>>(
                            Path.Combine("config", "SmartPumpControlParams.xml"));
                }
                catch (Exception ex)
                {
                    log.Write($"Файл конфигурации SmartPumpControlParams.xml не найден: {ex}", 0, true);
                }

                if (Params == null)
                        Params = new Serialization.SerializableDictionary<string, string>();

                log.Write("ctx point in driver " + (ctx == IntPtr.Zero ? "is null" : "success set"), 2, true);
                if (!isInit)
                {
                    try
                    {
                        isInit = true;
                        int pt;
                        if (!Params.ContainsKey("port") || !int.TryParse(Params["port"], out pt))
                            pt = port;
                        int t;
                        if (Params.ContainsKey("SendTimeout") && int.TryParse(Params["SendTimeout"], out t))
                            XmlPumpClient.SendTimeout = t;
                        else
                            XmlPumpClient.SendTimeout = 1500;

                        if (Params.ContainsKey("WaitAnswerTimeout") && int.TryParse(Params["WaitAnswerTimeout"], out t))
                            XmlPumpClient.WaitAnswerTimeout = t;
                        else
                            XmlPumpClient.WaitAnswerTimeout = 3500;

                        if (Params.ContainsKey("LogLevel") && int.TryParse(Params["LogLevel"], out t))
                        {
                            log.LogLevel = t;
                            XmlPumpClient.ExchangeLogLevel = t;
                            //Benzuber.ExcangeServer.logBenzuber.LogLevel = t;
                        }

                        if (Params.ContainsKey("UnblockingTimeoutMin") && int.TryParse(Params["UnblockingTimeoutMin"], out t))
                        {
                            XmlPumpClient.UnblockingTimeoutMin = t;
                            //Benzuber.ExcangeServer.logBenzuber.LogLevel = t;
                        }

                    //RemotePump_Driver.RemotePump.StartServer();
                    XmlPumpClient.StartSocket(hostB2, pt, terminal);
                        XmlPumpClient.InitData(terminal);
                        log.Write(
$@"XmlPumpClient Open port: {pt}{Environment.NewLine}
SendTimeout: {XmlPumpClient.SendTimeout} {Environment.NewLine}
WaitAnswerTimeout: {XmlPumpClient.WaitAnswerTimeout}{Environment.NewLine}
LogLevel: {log.LogLevel}{Environment.NewLine}
UnblockingTimeoutMin: {XmlPumpClient.UnblockingTimeoutMin}{Environment.NewLine}", 0, true);

                        //else
                        //{
                        //    Driver.log.Write(Driver.Description() + "\r\n");
                        //}

                        //int OS = Environment.OSVersion.Version.Major;
                        //Driver.log.Write("OS Ver - " + Environment.OSVersion.Version + "\r\n");
                        //if (OS > 4)
                        FuelPrices();
                        //else
                        //    Driver.FuelPrices();

                        //if (OS > 4)
                        PumpFuels();
                        //else
                        //    Driver.PumpFuels("1=95.92.80;2=95.92.80;3=95.92;4=95.92");


                        object res;
                        if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1, MESSAGE_TYPES.OnDataInit), out res))
                        {
                            log.Write(
                                Description() + " успешно открыта!\r\n", 0, true);

                            XmlPumpClient.pump_status_update_background_th.Start();
                        }
                        else
                        {
                            log.Write(
                                Description() + " нет ответа!\r\n", 0, true);
                        }

                }
                    catch (Exception ex)
                    {
                        log.Write($"Ошибка инициализации библиотеки {Description()}:{ex}\r\n", 0, true);
                    }
            }
        }

            public static void StartBenzuber()
            {
                var enable = ConfigMemory.GetConfigMemory("Benzuber");
                foreach (var r in enable.GetValueNames())
                {
                    Driver.log.Write($@"[{r}]:{enable[r]}", 3, true);
                }
                if (string.Compare(enable["enable"], "1", StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    log.Write("BenzuberServer отключен", 0, true);
                    return;
                }
                log.Write("Стартуем BenzuberServer:", 0, true);
                Benzuber.ExcangeServer.StartClient();
            }

        /// <summary>
        /// Инициализация драйвера
        /// </summary>
        /// <param name="_callback_SetDose">Адрес функции установки дозы на ТРК</param>
        /// <param name="_callback_GetDose">Адрес функции получения информации о ТРК</param>
        /// <param name="_callback_CancelDose">Адрес функции сброса с ТРК</param>
        /// <param name="_callback_HoldPump">Адрес функции проверки доступности ТРК</param>
        /// <param name="_callback_UpdateFillingOver">Адрес функции сохранения информации после пересчата завершенного заказа</param>
        /// <param name="_callback_InsertCardInfo">Адрес функции сохранения информации о доп. картах клиена</param>
        /// <param name="_callback_SaveReciept">Адрес функции сохранения информации о напечатанном документе</param>
        /// <param name="_SystemName">Информация о системе управления АЗС</param>
        /// <param name="ctx">Произвольныый объект, который будет возвращаться при каждом вызове callback функций</param>
        /// <returns></returns>
        [Obfuscation()]
            public static byte Open(SetDose_Delegate _callback_SetDose,
                                      GetDose_Delegate _callback_GetDose,
                                      CancelDose_Delegate _callback_CancelDose,
                                      HoldPump_Delegate _callback_HoldPump,
                                      UpdateFillingOver_Delegate _callback_UpdateFillingOver,
                                      InsertCardInfo_Delegate _callback_InsertCardInfo,
                                      SaveReciept_Delegate _callback_SaveReciept,
                                      string _SystemName,
                                      IntPtr ctx)
                => OpenBase(_callback_SetDose, _callback_GetDose, null, null, _callback_CancelDose, _callback_HoldPump, _callback_UpdateFillingOver, _callback_InsertCardInfo, _callback_SaveReciept, _SystemName, ctx);

            /// <summary>Функция выгрузки драйвера</summary>
            [Obfuscation()]
            public static void Close()
            {
                XmlPumpClient.Disconnect();
                
                log.Write("Driver close", 0, true);
                //try
                //{
                //    if (window != null && window.Visible)
                //        window.Close();
                //}
                //catch { }
            }

            /// <summary>
            /// Получение строки описания драйвера
            /// </summary>
            /// <returns>Строка описания драйвера</returns>
            [Obfuscation()]
            public static string Description()
            {
                //log.Write("");
                //log.Write("Description");
                return "SmartPumpControl ASU";
            }

            /// <summary>
            /// Завершение транзакции.
            /// </summary>
            /// <param name="TransNum">номер транзакции</param>
            /// <param name="Quantity">Фактическое кол-во литров в миллилитрах</param>
            /// <param name="Amount">Фактическая сумма заказа в копейках</param>
            [Obfuscation()]
            public static void FillingOver(long TransNum, int Quantity, int Amount)
            {
                try
                {
                    lock (TransMemoryLocker)
                    {
                        
                        log.Write(
                            $"Налив окончен: Номер транзакции АСУ = {TransNum}, Кол-во = {Quantity}, Amount = {Amount}", 1, true);
                        if (TransMemory.ContainsKey(TransNum))
                        {
                            log.Write("Транзакция найдена", 2, true);
                            var order = TransMemory[TransNum];
                            order.OverAmount = ((decimal)Amount) / 100;
                            order.OverQuantity = ((decimal)Quantity) / 1000;
                            order.PumpRRN = TransNum.ToString();

                            log.Write(
                                $"Транзакция отправлена: Номер транзакции АСУ = {order.PumpRRN}, Кол-во = {order.OverQuantity}, Amount = {order.OverAmount}", 2, true);

                            RemotePump.AddFillingOver(order);

#warning Если возникнут проблеммы убрать
                            TransMemory.Remove(TransNum);
                            log.Write(
                                $"\t\tFillingOver:\r\n\t\tудаление заказа №{TransNum}\r\n", 2, true);
                        }
                    }

                    //   var tmp = TelFuelCommon.Excange.TransMemory.GetTransByStationTag(TransNum);
                    //   tmp.OverAmount = (((decimal)Amount) / 100);
                    //   tmp.Quantity = (((decimal)Quantity) / 100);
                    //   tmp.State = TelFuelCommon.Excange.TransMemory.StateEnum.Commited;
                }
                catch (Exception ex)
                {
                    log.Write("Ошибка подтверждения налива топлива. " + ex.ToString(), 0, true);
                }
            }

            /// <summary>
            /// Получить список видов оплаты
            /// </summary>
            /// <returns></returns>
            [Obfuscation]
            public static string GetCardTypes()
            {
                if (!Params.ContainsKey("CardType"))
                {
                    Params.Add("CardType", ";10=Наличные;20=Топливные карты;30=Банковские карты;99=Benzuber");
                    SaveParams();
                }
                return Params["CardType"];//"; 1 = Наличные; 2 = Топливные карты; 3 = Банковские карты; 4 = Дисконтные карты";// Params["CardType"];
            }

            public static int ServiceOperationTimeout
            {
                get
                {
                    int result = 0;
                    if (Params.ContainsKey("service_operation_timeout") && int.TryParse(Params["service_operation_timeout"], out result))
                        return result;
                    else
                        return 300;

                }

                set
                {

                    Params["service_operation_timeout"] = value.ToString();
                    SaveParams();
                }
            }

            public static Serialization.SerializableDictionary<string, string> Params = new Serialization.SerializableDictionary<string, string>();
            public static void SaveParams()
            {
                try
                {
                    Serialization.Serialize(Params, "SmartPumpControlParams.xml");
                }
                catch (Exception ex) {log.Write(ex.ToString(), 0, true); }
            }

            /// <summary>
            /// Закрытие смены АСУ
            /// </summary>
            [Obfuscation()]
            public static void CloseShift()
            {
                
                log.Write("Закрытие смены", 0, true);
                var tids = SmartPumpControlRemote.Shell.GetTIDS();
                foreach (var tid in tids)
                {
                    if (SmartPumpControlRemote.Shell.GetActions(tid).Contains("Закрытие смены"))
                    {
                        //var message = "Выполнить закрытие смены на\r\nтерминале: \"" + tid + "\"?";
                        //if (MessageBox.Show(message, "Закрытие смены на терминале", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            SmartPumpControlRemote.Shell.RunAction("Закрытие смены", tid);
                    }
                }
            }

            /// <summary>
            /// Открытие окна "обслуживание драйвера"
            /// </summary>
            [Obfuscation()]
            public static void Service()
            {
                
                log.Write("Service", 0, true);
                //var dialog = new SmartPumpControlRemote.QuickLaunch();
                //if (dialog.tid != "") dialog.ShowDialog();
            }

            /// <summary>
            /// Открытие окна "Настроек драйвера"
            /// </summary>
            [Obfuscation()]
            public static void Settings()
            {
                //                    log.Write(System.Reflection.Assembly.GetExecutingAssembly().Location);
                
                log.Write("Settings", 0, true);
                //new SmartPumpControlRemote.Settings().ShowDialog();
            }

            /// <summary>
            /// Установка информации по видам топлива
            /// </summary>
            /// <param name="Fuels">
            /// Строка с информацией по видам топлива в формате
            /// </param>
            [Obfuscation()]
            public static void FuelPrices()
            {
                
                log.Write("Установка цен: " + Fuels, 0, true);
                //lock(XmlPumpClient.answers)
                try
                {
                    log.Write("Очистка памяти цен.", 2, true);
                    Fuels.Clear();
                    log.Write("Формирование списка доступных продуктов", 2, true);

                    object item;

                    if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                        MESSAGE_TYPES.OnDataInit), out item) || item == null)
                    {
                        XmlPumpClient.InitData(terminal);
                    }

                    XmlPumpClient.GetGradePrices(terminal, 1);
                    Thread.Sleep(500);
                    //XmlPumpClient.PumpGetStatus(terminal, 1);

                    if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1, MESSAGE_TYPES.OnSetGradePrices), out item) && item != null)
                    {
                        foreach (var price in ((OnSetGradePrices)item).GradePrices)
                        {
                            var tmp = new FuelInfo() { ID = price.GradeId, Name = price.GradeName, Price = (decimal)price.Price / 100, InternalCode = price.GradeId };
                            log.Write("Add Fuel: " + tmp.ToString(), 0, true);
                            Fuels.Add(price.GradeName, tmp);
                        }
                    }
                }
                catch { }
            }

            /// <summary>
            /// Получить информацию о транзакции
            /// </summary>
            /// <param name="ID">ID транзакции</param>
            /// <param name="Result">Структура содержащая информацию о транзакции:
            ///         
            //struct GetTransactionResult
            //{
            //    public int PumpNo;
            //    public int PaymentCode;
            //    public int ProductCode;
            //    public int OrderMode;
            //    public int Quantity;
            //    public int Price;
            //    public int Amount;
            //    [MarshalAs(UnmanagedType.AnsiBStr)]
            //    public string CardNO;
            //    [MarshalAs(UnmanagedType.AnsiBStr)]
            //    public string OrderRRN;
            //}
            /// </param>
            /// <returns></returns>
            [Obfuscation]
            public static int GetTransaction(long ID, IntPtr Result)
            {
                try
                {
                    log.Write($"GetTransaction(long ID = {ID},  IntPtr Result):\r\n", 0, true);
                    lock (TransMemoryLocker)
                    {
                        if (TransMemory.ContainsKey(ID))
                        {
                            log.Write("Транзакция найдена", 2, true);
                            //   Marshal.StructureToPtr(TransMemory[ID], Result, true);
                            WriteOrderToIntPtr(TransMemory[ID], Result);
                            //TransMemory[ID].WriteToIntPtr(Result);
                            return 1;
                        }
                        else
                        {
                            log.Write("Транзакция не найдена", 2, true);
                            foreach (var key in TransMemory.Keys)
                                log.Write(key.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Write($"GetTransaction(long ID = {ID},  IntPtr Result):\r\n" + ex, 0, true);
                }
                return 0;

            }
            public static TransactionInfo GetTransactionInfo(long ID, IntPtr Result)
            {
                TransactionInfo result = new TransactionInfo();
                try
                {
                    log.Write($"GetTransactionInfo(long ID = {ID}):\r\n", 0, true);
                    lock (TransMemoryLocker)
                    {
                        if (TransMemory.ContainsKey(ID))
                        {
                            log.Write("Транзакция найдена", 2, true);
                            //   Marshal.StructureToPtr(TransMemory[ID], Result, true);
                            //ReadOrderFromIntPtr(TransMemory[ID], Result);

                            result.Pump = Marshal.ReadInt32(Result, 0);
                            result.PaymentCode = Marshal.ReadInt32(Result, 4);
                            result.Fuel = Marshal.ReadInt32(Result, 8);
                            result.OrderInMoney = Marshal.ReadInt32(Result, 12);
                            result.Quantity = Marshal.ReadInt32(Result, 16);
                            result.Price = Marshal.ReadInt32(Result, 20);
                            result.Amount = Marshal.ReadInt32(Result, 24);
                            result.CardNum = Marshal.PtrToStringAnsi(Result, 28);
                            result.RRN = Marshal.PtrToStringAnsi(Result, 32);
                            //result.BillImage = Marshal.WriteIntPtr(ptr, 32;
                            //TransMemory[ID].WriteToIntPtr(Result);
                            return result;
                        }
                        else
                        {
                            log.Write("Транзакция не найдена", 2, true);
                            foreach (var key in TransMemory.Keys)
                                log.Write(key.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Write($"GetTransaction(long ID = {ID},  IntPtr Result):\r\n" + ex, 0, true);
                }
                return null;

            }

            /// <summary>
            /// Установка информации по видам топлива доступным на ТРК
            /// </summary>
            /// <param name="PumpsInfo">
            /// Строка с информацией по видам топлива доступным на ТРК в формате:
            /// Pump=FuelCode,FuelCode,FuelCode;
            /// Пример:
            /// 1=1,2,3; 2=1,2,3; 3=2,3; 4=2,3
            /// </param>
            [Obfuscation()]
            public static void PumpFuels()
            {
                //"1=95.92.80;2=95.92.80;3=95.92;4=95.92"
                
                log.Write("Установка продуктов, доступных на ТРК.", 0, true);
                //lock (XmlPumpClient.answers)
                try
                {
                    object item;
                    lock (PumpsLocker)
                        Pumps.Clear();
                    if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                        MESSAGE_TYPES.OnDataInit), out item) || item == null)
                    {
                        XmlPumpClient.InitData(terminal);
                    }

                    if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1, MESSAGE_TYPES.OnDataInit), out item) && item != null)
                    {
                        OnDataInit onDataInit = (OnDataInit)item;
                        foreach (var pump in onDataInit.Pumps)
                        {
                            log.Write($"ТРК[{pump.PumpId}]\r\n", 2, true);

                            XmlPumpClient.PumpGetStatus(terminal, pump.PumpId, 1);

                            OnPumpStatusChange оnPumpStatusChanged = null;

                            if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(pump.PumpId, MESSAGE_TYPES.OnPumpStatusChange), out item)
                                    && item != null && ((OnPumpStatusChange)item)?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                            {
                                log.Write("\tстатус WAITING_COLLECTING\r\n", 2, true);
                                XmlPumpClient.Collect(terminal, pump.PumpId, 0, "", XmlPumpClient.WaitAnswerTimeout);
                                //Thread.Sleep(500);
                                XmlPumpClient.PumpGetStatus(terminal, pump.PumpId, 1);
                                XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(pump.PumpId,
                                    MESSAGE_TYPES.OnPumpStatusChange), out item);
                            }

                            оnPumpStatusChanged = (OnPumpStatusChange)item;

                            var pmp = new PumpInfo()
                            {
                                Pump = pump.PumpId,
                                Blocked =
                                (оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_IDLE &&
                                 оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION),
                                Fuels = new Dictionary<string, FuelInfo>(),
                                //BlockTimer = new System.Timers.Timer(15000 /*900000*/)
                                BlockInitTime = null
                            };
                            //pmp.BlockTimer.Elapsed += pmp.OnBlockTimerEvent;
                            //pmp. BlockTimer.AutoReset = false;
                            //pmp.BlockTimer. = pmp;
                            lock (PumpsLocker)
                                Pumps.Add(pump.PumpId, pmp);
                            foreach (var nozzle in pump.Nozzles)
                            {
                                if (оnPumpStatusChanged == null || (оnPumpStatusChanged.Nozzles.First(t => t.NozzleId == nozzle.NozzleId).Approval.Contains("Forbidden")))
                                    continue;

                                var fuel = Fuels.First(t => t.Value.ID == nozzle.GradeId);
                                log.Write($"\tПродукт: {fuel.Key}\r\n", 0, true);
                                lock (PumpsLocker)
                                    Pumps[pump.PumpId].Fuels.Add(fuel.Key, fuel.Value);
                            }
                        }
                    }
                }
                catch { }
            }

            private static bool init_cr([MarshalAs(UnmanagedType.BStr)]string Name, bool retry = false)
            {
                var config = ConfigMemory.GetConfigMemory(Name);
                try
                {
                    var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                    config["name"] = proxy.RequestData("cashregister", "devicename");
                    config["serial"] = proxy.RequestData("cashregister", "serialnumber");
                    config.Save();
                    return config["serial"] != null && config["serial"] != "";
                }
                catch { }
                return false;
            }

            [Obfuscation()]
            static public byte CRCommTest([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                return (byte)(init_cr(Name, true) ? 1 : 0);
            }
            [Obfuscation()]
            static public byte CRService([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                //MessageBox.Show("Используйте функцию \"Сервис\" терминала.", Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
            [Obfuscation()]
            static public byte CRSetup([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                //var dialog = new IP_Request();
                //var config = ConfigMemory.GetConfigMemory(Name);
                //dialog.Value = config["ip"];
                //dialog.Text = "Параметры \"" + Name + "\"";
                //if (dialog.ShowDialog() == DialogResult.OK)
                //{
                //    //System.Windows.Forms.ToString()Box.Show(Name + " Setup");
                //    if (!dialog.Value.Contains("://"))
                //        dialog.Value = "net.tcp://" + dialog.Value + ":1120";
                //    config["ip"] = dialog.Value;
                //    config.Save();
                //}
                return 1;
            }

            [Obfuscation()]
            static public byte XReport([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                var config = ConfigMemory.GetConfigMemory(Name);
                if (init_cr(Name))
                {
                    new Task(() =>
                    {
                        try
                        {
                            var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                            proxy.RunCommand(config["name"], "X-отчет (без печати)", null);
                        }
                        catch { }
                    }).Start();
                }
                return 1;
            }
            [Obfuscation()]
            static public int GetCheckNumber([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                var config = ConfigMemory.GetConfigMemory(Name);
                try
                {
                    init_cr(Name);
                    var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                    return int.Parse(proxy.RequestData("cashregister", "checkno"));
                }
                catch { }
                return 0;
            }
            [Obfuscation()]
            static public byte CRCloseShift([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                var config = ConfigMemory.GetConfigMemory(Name);
                if (init_cr(Name))
                {
                    byte result = 0;
                    do
                    {
                        try
                        {
                            var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                            result = (byte)(proxy.RunCommand(config["name"], "Z - отчет (Без печати)", null) ? 1 : 0);
                        }
                        catch { }
                        var message = 
                        $@"Не удалось закрыть смену закрыть смену на фискальном
                        \r\nрегистраторе: { Name }
                        \r\nПовторить попытку закрытия смены?";
                        log.Write(message, 1, true);
                    if (result == 0 /*&& MessageBox.Show(message, "Закрытие смены на ККМ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No*/)
                            break;
                    }
                    while (result == 0);
                    return result;
                }
                else
                    return 0;
            }
            [Obfuscation()]
            static public string GetSerialNumber([MarshalAs(UnmanagedType.BStr)]string Name)
            {
                init_cr(Name);
                var config = ConfigMemory.GetConfigMemory(Name);
                return config["serial"];
            }
            [Obfuscation()]
            static public byte CRMoneyIn([MarshalAs(UnmanagedType.BStr)]string Name, double Sum)
            {
                var result = 0;
                var operation = "Внесение";
                var config = ConfigMemory.GetConfigMemory(Name);
                try
                {
                    var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                    var param = new Dictionary<string, string>();
                    param.Add("Выбор операции", operation);
                    param.Add("Введите сумму в копейках", ((int)(Math.Round(Sum * 100))).ToString());
                    result = (byte)(proxy.RunCommand(config["name"], "Внесение/выплата денег", param) ? 1 : 0);
                }
                catch { }

                if (result == 0)
                log.Write($"Не удалось выполнить операцию {operation}", 0, true);

                return (byte)result;
            }

            [Obfuscation()]
            static public byte CRMoneyOut([MarshalAs(UnmanagedType.BStr)]string Name, double Sum)
            {
                var result = 0;
                var operation = "Выплата";
                var config = ConfigMemory.GetConfigMemory(Name);
                try
                {
                    var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                    var param = new Dictionary<string, string>();
                    param.Add("Выбор операции", operation);
                    param.Add("Введите сумму в копейках", ((int)(Math.Round(Sum * 100))).ToString());
                    result = (byte)(proxy.RunCommand(config["name"], "Внесение/выплата денег", param) ? 1 : 0);
                }
                catch { }

                if (result == 0)
                log.Write($"Не удалось выполнить операцию {operation}", 0, true);

            return (byte)result;
            }
            [Obfuscation()]
            static public byte PrintCheck([MarshalAs(UnmanagedType.BStr)]string Name, [MarshalAs(UnmanagedType.BStr)]string Text, int CheckKind, int PayKind, double Amount)
            {
                log.Write(
                    $"Печать чека. {Name}, {CheckKind}, {PayKind}, {Amount}", 0, true);
                var config = ConfigMemory.GetConfigMemory(Name);
                var result = 0;
                try
                {
                    init_cr(Name);
                    if (CheckKind == 6 || CheckKind == 4 || CheckKind == 3)
                    {
                        var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(config["ip"]);
                        var param = new Dictionary<string, string>();
                        if (CheckKind == 6)
                            param.Add("Выберите тип чека", "Продажа");
                        else if (CheckKind == 4)
                            param.Add("Выберите тип чека", "Возврат");
                        else if (CheckKind == 3)
                            param.Add("Выберите тип чека", "Возврат");


                        if (PayKind == 0)
                            param.Add("Выберите тип оплаты", "Наличные");
                        else if (PayKind == 1)
                            param.Add("Выберите тип оплаты", "Платежной картой");
                        else if (PayKind == 2)
                            param.Add("Выберите тип оплаты", "Кредитной картой");

                        param.Add("Введите сумму заказа", (Amount).ToString());
                        param.Add("Комментарий", Text.ToString());
                        result = (byte)(proxy.RunCommand(config["name"], "Произвольный чек", param) ? 1 : 0);
                    }
                }
                catch
                {
                }
                if (result == 0)
                    log.Write("Не удалось напечатать чек на ККМ: " + config["serial"] + "Печать чека", 0, true);
                return (byte)result;
            }

            /// <summary>Функция обратного вызова установки заказа на ТРК</summary>
            /// <param name="Pump">Номер ТРК</param>
            /// <param name="Fuel">Код топлива</param>
            /// <param name="OrderInMoney">Режим заказа: 0 - литры, 1 - деньги</param>
            /// <param name="Quantity">Сумма заказа в миллилитрах</param>
            /// <param name="Amount">Сумма заказа в копейках</param>
            /// <param name="CardNum">Номер карты</param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns>
            /// Номер транзакции в системе управления (TransID), в случае ошибки -1;
            /// </returns>
            public delegate long SetDose_Delegate(OrderInfo Order, IntPtr ctx);

            /// <summary>
            /// Получить состояние ТРК
            /// </summary>
            /// <param name="Pump">Номер ТРК</param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns>
            /// Ссылка на структуру содержащую состояние ТРК:
            /// 
            /// public struct GetPumpStateResponce
            /// {
            ///     public byte DispStatus;//0
            ///     public ushort StateFlags;//1
            ///     public int ErrorCode;//3
            ///     public byte DispMode;//7
            ///     public byte UpNozz;//8
            ///     public byte UpFuel;//9
            ///     public byte UpTank;//10
            ///     public Int64 TransID;//11
            ///     public byte PreselMode;//19
            ///     public double PreselDose;//20
            ///     public double PreselPice;//28
            ///     public byte PreselFuel;//36
            ///     public byte PreselFullTank;//37
            ///     public double FillingVolume;//38
            ///     public double FillingPrice;//46
            ///     public double FillingSum;//54
            /// }
            /// </returns>
            public delegate GetPumpStateResponce GetDose_Delegate(long Pump, IntPtr ctx);
            /// <summary>
            /// Остановка ТРК/Сброс заказа с ТРК
            /// При успешном выполнении данной функции необходимо выполнить попытку сброса заказа с ТРК, и в случае успеха  
            /// Выполнить функцию "FillingOver" с передачей в неё фактически отпущенной дозы.
            /// </summary>
            /// <param name="TransID">Номер транзакции</param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns></returns>
            public delegate int CancelDose_Delegate(long TransID, IntPtr ctx);

            public delegate int SQL_Write_Delegate([MarshalAs(UnmanagedType.LPWStr)]string SQL_Request, IntPtr ctx);
            public delegate IntPtr SQL_Read_Delegate([MarshalAs(UnmanagedType.LPWStr)]string SQL_Request, IntPtr ctx);

            /// <summary>
            /// Проверка доступности ТРК.
            /// Данная функция может использоваться для предварительного захвата ТРК терминалом.(не рекомендуется)
            /// </summary>
            /// <param name="Pump">Номер ТРК</param>
            /// <param name="ReleasePump">Если true - терминал долже</param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns></returns>
            public delegate int HoldPump_Delegate(int Pump, byte ReleasePump, IntPtr ctx);

            /// <summary>
            /// Обновление суммы заказа.
            /// После получения терминалом информации о завершении отпуска топлива, может возникнуть необходимость пересчета заказа
            /// например при использовании пороговых скидок или бонусных систем.
            /// В таком случае драйвер передает информацию о пересчитаном заказе в данную функцию
            /// </summary>
            /// <param name="Amount">Сумма заказа в копейках</param>
            /// <param name="Price">Цена в копейках</param>
            /// <param name="Trans_ID">Номер транзакции</param>
            /// <param name="DiscountMoney">
            /// Сумма скидки  в копейках 
            /// (в зависимости от требований используемой системы лояльности может передаваться либо сумма скидки с суммы, 
            /// либо сумма скидки с единицы товара (с литра))
            /// </param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns></returns>
            public delegate int UpdateFillingOver_Delegate(int Amount, int Price, long Trans_ID, int DiscountMoney, IntPtr ctx);

            /// <summary>
            /// Передача дполнительного номера карты, используемого при транзакции.
            /// В случае, если при операции с терминалом использовалась не одна карта (например при оплате топлива по банковской карте с использованием
            /// карты лояльности) терминал передает информацию по данным картам в данную функцию.
            /// </summary>
            /// <param name="_DateTime">Дата/Время предъявления карты</param>
            /// <param name="CardNo">Номер карты</param>
            /// <param name="CardType">Тип карты</param>
            /// <param name="Trans_ID">Номер транзакции</param>
            /// <param name="ctx">Ссылка полученная при инициализации библиотеки</param>
            /// <returns></returns>
            public delegate int InsertCardInfo_Delegate(long _DateTime, [MarshalAs(UnmanagedType.AnsiBStr)]string CardNo, int CardType, long Trans_ID, IntPtr ctx);

            /*
             *                     //Тип документа (0 - ФД, 1 - Не ФД, 2 - Отчет)
                                    + DocType.ToString() + ", "
                                    //Сумма                                                 
                                    + Amount.ToString().Replace(",", ".") + ", "
                                    //Произвольный чек                                                  
                                    + (VarCheck ? "1" : "0") + ", "
                                    //Текст чека
                                    + "\'" + RecieptText + "\', "
                                    //Тип документа
                                    + "\'" + DocKind + "\', "
                                    //Тип документа (6 - Продажа, 4 - Возврат)
                                    + DocKindCode.ToString() + ", "
                                    //Вид платежа (0 - Нал, 1 - Плат. картой, 2 - Безнал)
             */
            /// <summary>
            /// Сохранение информации о напечатанном документе
            /// </summary>
            /// <param name="RecieptText">
            /// Образ документа
            /// </param>
            /// <param name="_DateTime">Дата/Время печати документа</param>
            /// <param name="DeviceName">Имя терминала, на котором был напечатан чек</param>
            /// <param name="DeviceSerial">Серийный номер фискального регистратора</param>
            /// <param name="DocNo">Номер документа</param>
            /// <param name="DocType">
            /// Тип документа:
            /// 0 - ФД, 
            /// 1 - Не ФД, 
            /// 2 - Отчет
            /// </param>
            /// <param name="Amount">Сумма чека</param>
            /// <param name="VarCheck">Если 1 - произвольный чек</param>
            /// <param name="DocKind">Вид документа</param>
            /// <param name="DocKindCode"> Код вида документа
            /// 6 - Продажа, 
            /// 4 - Возврат
            /// </param>
            /// <param name="PayType"> Тип оплаты
            /// 0 - Нал, 1 - Плат. картой, 2 - Безнал
            /// </param>
            /// <param name="FactDoc">Если 1 - чек по факту</param>
            /// <param name="BP_Product">Код продукта</param>
            /// <param name="TransID">Номер транзакции [В данный момент не используется]</param>
            /// <param name="ctx"></param>
            /// <returns></returns>
            public delegate int SaveReciept_Delegate([MarshalAs(UnmanagedType.AnsiBStr)]string RecieptText,
                                                                long _DateTime,
                                                                [MarshalAs(UnmanagedType.AnsiBStr)]string DeviceName,
                                                                [MarshalAs(UnmanagedType.AnsiBStr)]string DeviceSerial,
                                                                int DocNo,
                                                                int DocType,
                                                                int Amount,
                                                                decimal PrePrice,
                                                                decimal PreQuantity,
                                                                decimal PreSum,
                                                                decimal Price,
                                                                decimal Quantity,
                                                                int VarCheck,
                                                                [MarshalAs(UnmanagedType.AnsiBStr)]string DocKind,
                                                                int DocKindCode,
                                                                int PayType,
                                                                int FactDoc,
                                                                int BP_Product,
                                                                long TransID,
                                                                int PumpNo,
                                                                int ShiftDocNum,
                                                                int ShiftNum,
                                                                string OrderRRN,
                                                                IntPtr ctx);

            #endregion

            #region Функции оболочки

            public static bool CancelDose(string RRN)
            {
                try
                {
                    log.Write(
                        $"Сброс дозы. RRN: {RRN}", 0, true);

                    long TransID = -1;
                    var tm = TransMemory.ToArray();
                    foreach (var t in tm)
                        if (t.Value.OrderRRN == RRN)
                            TransID = t.Key;

                    if (TransID > 0)
                    {
                        log.Write(
                            $"Найдена транзакция. RRN: {RRN}, TransID: {TransID}", 2, true);

                        return callback_CancelDose(TransID) == 1;
                    }
                    else
                        log.Write(
                            $"Транзакция не найдена. RRN: {RRN}", 0, true);
                }
                catch { log.Write("error: CancelDose" + RRN, 0, true); }

                return false;
            }

            public static long SetDose(OrderInfo Order)
            {
            //int Pump, int Fuel, bool OrderInMoney, decimal Quantity, decimal Price, decimal Amount, int CardType, string CardNum, string RRN
            log.Write(
$@"Установка дозы на ТРК: {Order.PumpNo}", 0, true);
            log.Write(
$@"продукт: {Order.ProductCode}, 
заказ в деньгах: {Order.OrderMode}, 
кол-во: {Order.Quantity}, 
сумма: {Order.Amount}, 
номер карты: {Order.CardNO}", 2, true);

                var trans_id = callback_SetDose(Order);
                log.Write("Ответ АСУ: trans_id = " + trans_id, 2, true);
                log.Write(
                    $"\t\tSetDose:\r\n\t\tОтвет АСУ: trans_id = {trans_id}\r\n", 2, true);

                if (trans_id > 0)
                {
                    lock (TransMemoryLocker)
                    {
                        if (TransMemory.ContainsKey(trans_id))
                        {
                            TransMemory.Remove(trans_id);
                            log.Write(
                                $"\t\tSetDose:\r\n\t\tудаление заказа №{trans_id}\r\n", 2, true);
                        }
#warning Если будут сбойные ситуации - убрать
                        Order.PumpRRN = trans_id.ToString();
                        /*********************************************/
                        TransMemory.Add(trans_id, Order);
                        log.Write(
                            $"\t\tSetDose:\r\n\t\tдобавление заказа №{trans_id}\r\n", 2, true);
                    }
                }
                else
                    log.Write("Ошибка при задании дозы на ТРК. callback_SetDose = null", 0, true);
                return trans_id;
            }
            public static GetPumpStateResponce GetDose(long Pump)
            {
                var result = callback_GetDose(Pump);
                if (!result.Equals(default(GetPumpStateResponce)))
                {
                    //var res = GetPumpStateResponce.ReadFromPtr(result);
                    //                    log.Write("Ответ АСУ: DispStatus = " + res.DispStatus.ToString());
                    return result;
                }
                else
                    //                    log.Write("Ошибка при получении дозы на ТРК. callback_SetDose = null");

                    return new GetPumpStateResponce();
                //var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(transact));
                //Marshal.WriteInt32(ptr, 5);
                //Marshal.StructureToPtr(transact, ptr, false);
                //var ret_str = SetDoseResponse.ReadFromIntPtr(callback_SetDose.Invoke(Pump, CardType, ctx)); // (SetDoseResponse)Marshal.PtrToStructure(Marshal.ReadIntPtr(ret), typeof(SetDoseResponse));
                //if (XmlPumpClient.answers.Count == 0)
                //{
                //    XmlPumpClient.InitData(terminal);
                //}
                //if (Pump % 2 == 1)
                //    XmlPumpClient.InitPump(terminal, (int)Pump, (int)Pump);
                //else
                //    XmlPumpClient.InitPump(terminal, (int)Pump, -1);

                //XmlPumpClient.PumpGetStatus(terminal, (int)Pump);
            }
            public static bool HoldPump(int Pump, bool Release = false)
            {
                return callback_HoldPump(Pump, (byte)((Release) ? 1 : 0)) == 1;
            }

            public static bool UpdateFillingOver(decimal Amount, decimal Price, long Trans_ID, decimal DiscountMoney)
            {
                if (UpdateFillingOver_callback != null)
                {
                    try
                    {
                        lock (locker)
                            return UpdateFillingOver_callback.Invoke((int)(Amount * 100), (int)(Price * 100), Trans_ID, (int)(DiscountMoney * 100), ctx) == 1;
                    }
                    catch { }
                    return false;
                }
                else if (callback_SQL_Read_callback != null && callback_SQL_Write_callback != null)
                    return UpdateFillingOver("TRANS_FUEL_ORDER", Amount, Price, Trans_ID, DiscountMoney)
                        && UpdateFillingOver("GSMARCHIVE", Amount, Price, Trans_ID, DiscountMoney);
                return false;
            }

            private static bool UpdateFillingOver(string Table, decimal Amount, decimal Price, long Trans_ID, decimal DiscountMoney)
            {
                for (int z = 0; z < 5; z++)
                {
                    try
                    {
                        if (callback_SQL_Write($"update {Table} set FACTPRICE = {Price.ToString().Replace(",", ".")}, FACTSUMMA = {Amount.ToString().Replace(",", ".")}, DISCOUNTMONEY={DiscountMoney.ToString().Replace(",", ".")} where TRANS_ID = {Trans_ID}") == 1)
                        {
                            var result = callback_SQL_Read($"select FACTPRICE, FACTSUMMA, DISCOUNTMONEY from {Table}  where TRANS_ID = {Trans_ID}");
                            if (result?.Length == 1 && result?[0].Split('/').Length == 3)
                            {
                                var values = result?[0].Replace(".", ",").Split('/');
                                decimal db_price, db_amount, db_dicsount;
                                if (decimal.TryParse(values[0], out db_price) && decimal.TryParse(values[1], out db_amount) && decimal.TryParse(values[2], out db_dicsount))
                                {
                                    if (test_values(Price, db_price, 2) && test_values(Amount, db_amount, 2) && test_values(DiscountMoney, db_dicsount, 2))
                                    {
                                        log.Write($"Таблица {Table} обновленна успешно", 2, true);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write($"Ошибка при выполнении пересчета заказа: {ex}", 0, true);
                    }
                    Thread.Sleep(1000);
                }
                log.Write($"Не удалось обновить таблицу {Table}.", 0, true);
                return false;
            }
            private static bool test_values(decimal v1, decimal v2, int decimals) => Math.Round(v1, decimals) == Math.Round(v2, decimals);
            public static bool InsertCardInfo(DateTime _DateTime, string CardNo, int CardType, long Trans_ID)
            {
                if (InsertCardInfo_callback != null)
                {
                    try
                    {
                        log.Write($"Сохранение информации о карте: {_DateTime.ToString()}, {CardNo}, {CardType}, {Trans_ID}", 2, true);
                        lock (locker)
                            InsertCardInfo_callback.Invoke(BitConverter.DoubleToInt64Bits(_DateTime.ToOADate()), CardNo, CardType, Trans_ID, ctx);
                    }
                    catch { }
                    return false;
                }
                else if (callback_SQL_Read_callback != null && callback_SQL_Write_callback != null)
                    return (callback_SQL_Write(
                         "insert into personalarchive (TRANS_ID, ISSUER_ID, CARD_ID, DATETIME) "
                                + "VALUES("
                                 //Номер транзакции
                                 + Trans_ID.ToString() + ", "
                                 //Код эмитента
                                 + CardType.ToString() + ", "
                                 //Номер карты
                                 + "\'" + CardNo + "\', "
                                 //Дата/время
                                 + "\'" + _DateTime.ToString("dd.MM.yyyy HH:mm:ss") + "\')") == 1);
                return false;
            }

            public static bool SaveReciept(string RecieptText, DateTime _DateTime, string DeviceName, string DeviceSerial, int DocNo = 0,
                int DocType = 1, decimal Amount = 0, decimal PrePrice = 0, decimal PreQuantity = 0, decimal PreSum = 0, decimal Price = 0, decimal Quantity = 0,
                bool VarCheck = false, string DocKind = "", int DocKindCode = 0, int PayType = 0, long TransID = 0,
                int PumpNo = 0, int ShiftDocNum = 0, int ShiftNum = 0, string OrderRRN = "",
                bool FactDoc = false, int BP_Product = 0)
            {
                if (SaveReciept_callback != null)
                {
                    try
                    {
                        lock (locker)
                            return SaveReciept_callback.Invoke(RecieptText, BitConverter.DoubleToInt64Bits(_DateTime.ToOADate()), DeviceName, DeviceSerial, DocNo, DocType, (int)(Amount * 100), 
                                PrePrice,PreQuantity,PreSum,Price,Quantity, VarCheck ? 1 : 0, DocKind, DocKindCode, PayType, FactDoc ? 1 : 0, BP_Product, TransID, PumpNo, ShiftDocNum, ShiftNum, OrderRRN, ctx) == 1;
                    }
                    catch { }
                    return true;
                }
                else if (callback_SQL_Read_callback != null && callback_SQL_Write_callback != null)
                {
                    #region Проверка на наличие записи в базе
                    var result = false;
                    var tmp = callback_SQL_Read($"select * from docs where host = '{DeviceName}' and datetime = '{_DateTime.ToString("dd.MM.yyyy HH:mm:ss")}' and serno = '{DeviceSerial}' and docnum = { DocNo.ToString()} and doctype = {DocType.ToString()}");
                    if (tmp?.Count() > 0)
                    {
                        log.Write("Данный чек бы сохранен ранее:" + tmp[0] ?? " нет", 2, true);
                        result = true;
                    }
                    #endregion
                    else
                    {
                        result = (callback_SQL_Write(
                            "INSERT INTO DOCS (DATETIME, HOST, SERNO, DOCNUM, DOCTYPE, SUMM, VARCHECK, DOCIMAGE, DOCKIND, DOCKINDCODE, PAYMENTKIND, DEVICENAME, TRANS_ID, FACTDOC) "
                                   + "VALUES("
                                    //Дата/время
                                    + "\'" + _DateTime.ToString("dd.MM.yyyy HH:mm:ss") + "\', "
                                    //Рабочее место
                                    + "\'" + DeviceName + "\', "
                                    //Серийный номер
                                    + "\'" + DeviceSerial + "\', "
                                    //Номер документа
                                    + "\'" + DocNo.ToString() + "\', "
                                    //Тип документа (0 - ФД, 1 - Не ФД, 2 - Отчет)
                                    + DocType.ToString() + ", "
                                    //Сумма                                                 
                                    + Amount.ToString().Replace(",", ".") + ", "
                                    //Произвольный чек                                                  
                                    + (VarCheck ? "1" : "0") + ", "
                                    //Текст чека
                                    + "\'" + RecieptText + "\', "
                                    //Тип документа
                                    + "\'" + DocKind + "\', "
                                    //Тип документа (6 - Продажа, 4 - Возврат)
                                    + DocKindCode.ToString() + ", "
                                    //Вид платежа (0 - Нал, 1 - Плат. картой, 2 - Безнал)
                                    + PayType.ToString() + ", "
                                    //Имя устройства, на котором был напечатан чек
                                    + "\'" + DeviceName + "\', "
                                    //Номер транзакции
                                    + TransID.ToString() + ", "
                                    //Фактический документ
                                    + (FactDoc ? "1" : "0") + ")") == 1);
                    }

                    int internal_code = 0;
                    if (DocType == 0 && result && (internal_code = TranslateProdCode(BP_Product)) > 0)
                    {
                        try
                        {
                            for (int z = 0; z < 5; z++)
                            {
                                log.Write("Сохранение товарной позиции. Попытка: " + (z + 1).ToString(), 2, true);
                                Thread.Sleep(1000);
                                tmp = callback_SQL_Read($"select * from doc_items where host = '{DeviceName}' and datetime = '{_DateTime.ToString("dd.MM.yyyy HH:mm:ss")}' and serno = '{DeviceSerial}' and docnum = { DocNo.ToString()} and ITEM = {internal_code} and itemkind = 2 and itemno = 1");
                                if (tmp?.Count() > 0)
                                {
                                    log.Write("Данная позиция чека была сохранена ранее:" + tmp[0] ?? "нет", 2, true);
                                    result = true;
                                    //break;
                                }
                                else
                                {
                                    if (callback_SQL_Write("INSERT INTO DOC_ITEMS (DATETIME, HOST, SERNO, DOCNUM, ITEM, ITEMKIND, ITEMNO, SECTION,  SUMM) "
                                               + "VALUES("
                                                //Дата/время
                                                + "\'" + _DateTime.ToString("dd.MM.yyyy HH:mm:ss") + "\', "
                                                //Рабочее место
                                                + "\'" + DeviceName + "\', "
                                                //Серийный номер
                                                + "\'" + DeviceSerial + "\', "
                                                //Номер документа
                                                + "\'" + DocNo.ToString() + "\', "
                                                //Продукт
                                                + internal_code.ToString()
                                                // Тип записи (2 = топливо), Номер записи, Секция 
                                                + ", 2, 1, 1,"
                                                //Сумма                                                 
                                                + Amount.ToString().Replace(",", ".") + ")", 1) == 0)
                                    {
                                        result = true;
                                        break;
                                    }
                                    else
                                        result = false;

                                }
                            }
                        }
                        catch { }
                    }

                    return result;
                }
                return false;
            }
            public static int TranslateProdCode(int ID)
            {
                try
                {
                    foreach (var Prod in Fuels)
                    {
                        if (Prod.Value.ID == ID)
                            return Prod.Value.InternalCode;
                    }
                }
                catch { }
                return -1;
            }
            #endregion
        }
}

