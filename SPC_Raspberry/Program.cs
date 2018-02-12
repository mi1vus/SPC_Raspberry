using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ASUDriver;
using ProjectSummer.Repository;
using RemotePump_Driver;

namespace SPC_Raspberry
{
    static class Program
    {
        public static void OpenDriver()
        {
            try
            {
                var enable = ConfigMemory.GetConfigMemory("ASUClient");
                foreach (var r in enable.GetValueNames())
                {
                    Driver.log.Write($@"[{r}]:{enable[r]}", 3, true);
                }
                if (string.Compare(enable["terminal_enable"], "1", StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    Driver.log.Write("Client ТРК отключен", 0, true);
                    return;
                }

                if (Driver.Open(
                    //Установка дозы на ТРК
                    (RemotePump_Driver.OrderInfo Order, IntPtr ctx) =>
                    {
                        long transCounter;
                        lock (Driver.TransCounterLocker)
                        {
                            transCounter = Driver.TransCounter;
                            ++Driver.TransCounter;
                        }
                        Driver.log.Write("Установка дозы на ТРК: " + Order.PumpNo + " , сгенерирован TransID: " +
                                         transCounter + "\r\n", 0, true);
                        Order.PumpRRN = transCounter.ToString();
                        ++transCounter;

                        Thread myThread2 = new Thread(Driver.WaitCollectProxy) { IsBackground = true };
                        myThread2.Start(transCounter); // запускаем поток

                        return transCounter;
                    },
                    //Запрос состояние ТРК
                    (long Pump, IntPtr ctx) =>
                    {
                        Driver.log.Write("Запрос состояние ТРК: " + Pump + "\r\n", 2, true);

                        //DispStatus:
                        //	0 - ТРК онлайн(при этом TransID должен = -1, иначе данный статус воспринимается как 3)
                        //	1 - ТРК заблокирована
                        //	3 - Осуществляется отпуск топлива
                        //	10 - ТРК занята
                        object item;
                        OnPumpStatusChange оnPumpStatusChanged = null;

                        //XmlPumpClient.PumpGetStatus(XmlPumpClient.terminal, (int)Pump, 1);
                        //lock (XmlPumpClient.answers)
                        //    оnPumpStatusChanged = (OnPumpStatusChange)XmlPumpClient.answers.LastOrDefault(t => t is OnPumpStatusChange && (t as OnPumpStatusChange).PumpNo == Pump);
                        lock (XmlPumpClient.StatusesLocker)
                        {
                            //Driver.log.Write("", 0, true);
                            if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>((int)Pump,
                                    MESSAGE_TYPES.OnPumpStatusChange), out item) && item != null)
                                оnPumpStatusChanged = (OnPumpStatusChange)item;
                            //Driver.log.Write("", 0, true);
                        }

                        var е = XmlPumpClient.Fillings;
                        if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                        {
                            Thread myThread2 = new Thread(Driver.CollectOldOrderThread) { IsBackground = true };
                            myThread2.Start((int)Pump); // запускаем поток


                            //    Driver.log.Write("\tGetPumpState статус WAITING_COLLECTING\r\n");
                            //    XmlPumpClient.Collect(XmlPumpClient.terminal, (int)Pump, transCounter, "", XmlPumpClient.WaitAnswerTimeout);
                            //    //Thread.Sleep(500);
                            //    XmlPumpClient.PumpGetStatus(XmlPumpClient.terminal, (int)Pump, 1);
                            //    оnPumpStatusChanged = (OnPumpStatusChange)XmlPumpClient.Statuses[new Tuple<int, MESSAGE_TYPES>
                            //                ((int)Pump, MESSAGE_TYPES.OnPumpStatusChange)];
                        }

                        PumpInfo pmp;
                        lock ( XmlPumpClient.PumpsLocker)
                        {
                            //Driver.log.Write("", 0, true);
                            pmp =  XmlPumpClient.Pumps[(int)Pump];
                            pmp.DispStatus =
                            (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_IDLE
                            || оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION) ? 0 :
                            (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_AUTHORIZED
                            || оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING) ? 3 : 10;
                             XmlPumpClient.Pumps[(int)Pump] = pmp;
                            //Driver.log.Write("", 0, true);
                        }
                        Driver.log.Write($"Статус ТРК [{Pump}]: { (byte)pmp.DispStatus}; UpNozz: {(byte)((оnPumpStatusChanged?.Grade ?? -2) + 1)}; Blocked:{pmp.Blocked}\r\n", 2, true);

                        Driver.GetPumpStateResponce resp;

                        resp.DispStatus = (byte)pmp.DispStatus;
                        // StateFlags - всегда 0
                        resp.StateFlags = 0;
                        // ErrorCode - код ошибки
                        resp.ErrorCode = 0;
                        // DispMode - всегда 0
                        resp.DispMode = 0;
                        // UpNozz - номер снятого пистолета, не обязательный параметр для заполнения
                        // допускается 0
                        resp.UpNozz = (byte)((оnPumpStatusChanged?.Grade ?? -2) + 1);
                        // UpFuel - продукт снятого пистолета
                        resp.UpFuel = 0;
                        // UpTank - Номер емкости, к которой привязан снятый пистолет
                        //не обязательный параметр для заполнения допускается 0
                        resp.UpTank = 0;
                        // TransID - Номер транзакции
                        // в случае, если на ТРК отсутствует заказ: '-1'
                        resp.TransID = -1;
                        //PreselMode - режим заказа установленного на ТРК
                        //0 - литровый заказ
                        //1 - денежный заказ
                        resp.PreselMode = 0;
                        //PreselDose - сумма заказа установленного на ТРК,
                        //  в случае 'PreselMode = 0' - кол-во литров
                        //  в случае 'PreselMode = 1' - сумма в рублях
                        resp.PreselDose = 0;
                        //PreselDose - цена за лит топлива для заказа установленного на ТРК
                        resp.PreselPice = 0;
                        //PreselFuel - продукт заказа установленного на ТРК,
                        resp.PreselFuel = 0;
                        //PreselFullTank - если True, на ТРК установлен заказ до полного бака
                        resp.PreselFullTank = 0;
                        //FillingVolume - данные дисплея ТРК кол-во.
                        resp.FillingVolume = 0;
                        //FillingVolume - данные дисплея ТРК цена.
                        resp.FillingPrice = 0;
                        //FillingVolume - данные дисплея ТРК сумма.
                        resp.FillingSum = 0;

                        //var t = Marshal.SizeOf(Driver.GetPumpStateResponce);
                        //IntPtr respPtr = Marshal.AllocCoTaskMem(t);
                        //Marshal.StructureToPtr(resp, respPtr, false);

                        //Driver.log.Write("return", 0, false);

                        return resp;

                        //return new Driver.GetPumpStateResponce();
                    },
                    //Отмена транзакции
                    (long TransID, IntPtr ctx) =>
                    {
                        Driver.log.Write("Отмена транзакции: TransactID: " + TransID + "\r\n", 0, true);

                        //Driver.VolumeMem = 0;
                        //Driver.AmountMem = 0;
                        //EndFilling.Enabled = false;
                        //FillingUp.Enabled = false;
                        //FillingDown.Enabled = false;
                        OrderInfo order = new OrderInfo();

                        int cnt = 2;
                        while (cnt > 0)
                        {
                            lock (Driver.TransMemoryLocker)
                            {
                                //Driver.log.Write("", 0, true);
                                if (Driver.TransMemory.TryGetValue((long)TransID, out order))
                                {
                                    break;
                                }
                                else
                                    --cnt;
                                //Driver.log.Write("", 0, true);
                            }
                            if (cnt > 0)
                                Thread.Sleep(500);
                        }

                        if (cnt <= 0)
                        {
                            Driver.log.Write(
                                        $"\t\tERROR!!! CancelDoseDelegate:\r\n\t\tне найден заказ №{(long)TransID}\r\n", 0, true);
                            return -1;
                        }

                        XmlPumpClient.PumpGetStatus(XmlPumpClient.terminal, order.PumpNo, 1);

                        object item = null;
                        OnPumpStatusChange оnPumpStatusChanged = null;

                        lock (XmlPumpClient.StatusesLocker)
                        {
                            //Driver.log.Write("", 0, true);
                            if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(order.PumpNo,
                                MESSAGE_TYPES.OnPumpStatusChange), out item) && item != null)
                                оnPumpStatusChanged = (OnPumpStatusChange)item;
                            //Driver.log.Write("", 0, true);
                        }

                        //var discount = (order.BasePrice - order.Price) * order.Quantity;
                        //var fuel =  XmlPumpClient.Fuels.First(t => t.Value.Id == order.ProductCode);
                        //int allowed = 0;
                        //foreach (var pumpFuel in  XmlPumpClient.Pumps[order.PumpNo].Fuels)
                        //{
                        //    allowed += 1 << (pumpFuel.Value.Id - 1);
                        //}
                        //TODO Не работает!
                        if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_AUTHORIZED)
                        {
                            Driver.log.Write(
                                       $"\t\tCancelDoseDelegate:\r\n\t\tPumpStop №{(long)TransID} pmp:{order.PumpNo}\r\n", 0, true);
                            XmlPumpClient.PumpStop(XmlPumpClient.terminal, order.PumpNo, 1);

                            //Thread.Sleep(1000);

                            //var res2 = XmlPumpClient.statuses;
                            //var res3 = XmlPumpClient.fillings;


                            //XmlPumpClient.SaleDataSale(XmlPumpClient.terminal, order.PumpNo, allowed,
                            //    order.Amount, order.OverAmount, discount,
                            //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                            //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                            //Driver.log.Write.Write(
                            //    "CancelDoseDelegate:SaleDataSale:\r\n" +
                            //    $"terminal: {XmlPumpClient.terminal}\r\n" +
                            //    $"PumpNo: {order.PumpNo}\r\n" +
                            //    $"allowed: {allowed}\r\n" +
                            //    $"Amount: {order.Amount}\r\n" +
                            //    $"OverAmount: {order.OverAmount}\r\n" +
                            //    $"discount: {discount}\r\n" +
                            //    $"Quantity: {order.Quantity}\r\n" +
                            //    $"OverQuantity: {order.OverQuantity}\r\n" +
                            //    $"PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n" +
                            //    $"OrderRRN: {order.OrderRRN}\r\n" +
                            //    $"ProductCode: {order.ProductCode}\r\n" +
                            //    $"Key: {fuel.Key}\r\n" +
                            //    $"fuelPrice: {(int)(fuel.Value.Price * 100)}\r\n"
                            //    );
                        }
                        //else
                        //    Driver.FillingOver(TransID, 0, 0);

                        //if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                        //{
                        //    Driver.log.Write.Write(
                        //        "CancelDoseDelegate:Collect:\r\n" +
                        //        $"terminal: {XmlPumpClient.terminal}\r\n" +
                        //        $"PumpNo: {order.PumpNo}\r\n" +
                        //        $"RequestId: {TransID}\r\n" +
                        //        $"OrderRRN: {order.OrderRRN}\r\n"
                        //        );
                        //    if (!XmlPumpClient.Collect(XmlPumpClient.terminal, order.PumpNo, TransID, order.OrderRRN, 3000))
                        //    {
                        //        Driver.log.Write.Write(
                        //            $"\t\tERROR!!! CancelDoseDelegate:\r\n\t\tнет события ответа на Collect заказа RNN{order.OrderRRN}\r\n");
                        //        return -1;
                        //    }

                        //}

                        Driver.log.Write("Налив успешно отменен" + "\r\n", 0, true);

                        //Driver.PriceMem = 0;
                        return 1;
                    },
                    //Захват/Освобождение ТРК
                    (int PumpId, byte ReleasePump, IntPtr ctx) =>
                    {
                        object item = null;
                        if (ReleasePump == 0)
                        {
                            Driver.log.Write("Захват ТРК: " + PumpId + "\r\n", 0, true);

                            lock (XmlPumpClient.StatusesLocker)
                            {
                                //Driver.log.Write("", 0, true);
                                if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                {
                                    XmlPumpClient.InitData(XmlPumpClient.terminal);
                                }
                                //Driver.log.Write("", 0, true);
                            }

                            PumpInfo pump;
                            lock ( XmlPumpClient.PumpsLocker)
                            {
                                //Driver.log.Write("", 0, true);
                                pump =  XmlPumpClient.Pumps[PumpId];
                                pump.Blocked = true;
                                pump.BlockInitTime = DateTime.Now;
                                 XmlPumpClient.Pumps[PumpId] = pump;
                                //Driver.log.Write("", 0, true);
                            }
                            XmlPumpClient.Init(XmlPumpClient.terminal, PumpId, PumpId, XmlPumpClient.WaitAnswerTimeout, 1);
                            // XmlPumpClient.Pumps[Pump] = pump;

                            //label1.Text += "Захват ТРК: " + Pump + "\r\n";
                            return 1;
                        }
                        else
                        {
                            Driver.log.Write("Освобождение ТРК: " + PumpId + "\r\n", 0, true);

                            lock (XmlPumpClient.StatusesLocker)
                            {
                                //Driver.log.Write("", 0, true);
                                if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                {
                                    XmlPumpClient.InitData(XmlPumpClient.terminal);
                                }
                                //Driver.log.Write("", 0, true);
                            }

                            PumpInfo pump;
                            lock ( XmlPumpClient.PumpsLocker)
                            {
                                //Driver.log.Write("", 0, true);
                                pump =  XmlPumpClient.Pumps[PumpId];
                                pump.Blocked = false;
                                pump.BlockInitTime = null;
                                 XmlPumpClient.Pumps[PumpId] = pump;
                                //Driver.log.Write("", 0, true);
                            }
                            XmlPumpClient.Init(XmlPumpClient.terminal, PumpId, -1, XmlPumpClient.WaitAnswerTimeout, 1);
                            // XmlPumpClient.Pumps[Pump] = pump;

                            //label1.Text += "Освобождение ТРК: " + Pump + "\r\n";
                            return 1;
                        }
                    },
                    //Пересчет заказа UpdateFillingOver
                    (int Amount, int Price, long Trans_ID, int DiscountMoney, IntPtr ctx) =>
                    {
                        Driver.log.Write(
$@"Пересчет заказа, TransID: {Trans_ID}\r\n
Новая цена:   { (float)Price / 100}\r\n
Новая сумма:  {(float)Amount / 100}\r\n
Новая скидка: {(float)DiscountMoney / 100}\r\n
\r\n", 2, true);

                        //++Driver.TransCounter;

                        //XmlPumpClient.FiscalEventReceipt(XmlPumpClient.terminal, 1/*order.PumpNo*/,
                        //    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
                        //    (decimal)Amount / 100m/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, "123123123123" /*order.OrderRRN*/, 1);
                        //Driver.log.Write($"чек:\r\n" +
                        //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                        //    $"GetDocNum: {GetDocNum()}\r\n" +
                        //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                        //    $"OverAmount: {(decimal)Amount / 100m}\r\n" +
                        //    $"OrderRRN: {123123123123/*order.OrderRRN*/}\r\n");

                        //DebithThread.SetTransID(Driver.TransCounter);

                        //var order = Driver.TransMemory[Trans_ID];
                        //var discount = (order.BasePrice - order.Price) * order.Quantity;
                        //var fuel =  XmlPumpClient.Fuels.First(t => t.Value.Id == order.ProductCode);
                        //int allowed = 0;
                        //foreach (var pumpFuel in  XmlPumpClient.Pumps[order.PumpNo].Fuels)
                        //{
                        //    allowed += 1 << (pumpFuel.Value.Id - 1);
                        //}

                        //if (!XmlPumpClient.Collect(XmlPumpClient.terminal, order.PumpNo, allowed, order.OrderRRN, 3000))
                        //    return -1;
                        //Driver.log.Write("освобождение колонки\r\n");
                        //XmlPumpClient.SaleDataSale(XmlPumpClient.terminal, order.PumpNo, allowed,
                        //    order.Amount, order.OverAmount, discount,
                        //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                        //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                        //Driver.log.Write("фактические данные заправки\r\n");
                        //XmlPumpClient.FiscalEventReceipt(XmlPumpClient.terminal, order.PumpNo, 1, 1, 1,
                        //    order.OverAmount, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
                        //Driver.log.Write("чек\r\n");
                        //XmlPumpClient.Init(XmlPumpClient.terminal, order.PumpNo, -order.PumpNo, 1, 3000);
                        //Driver.log.Write("изм. статуса\r\n");

                        //var res2 = XmlPumpClient.statuses;
                        //var res3 = XmlPumpClient.fillings;

                        //XmlPumpClient.ClearAllTransactionAnswers(order.PumpNo, order.OrderRRN);

                        return 1;
                    },
                    //Сохранение информации о доп карте InsertCardInfo_Delegate
                    (long _DateTime, string CardNo, int CardType, long Trans_ID, IntPtr ctx) =>
                    {
                        //SYSTEMTIME time;
                        //VariantTimeToSystemTime(_DateTime, &time);

                        Driver.log.Write(
//+ "Дата/Время транзакции: " + time.wYear + "-" + time.wMonth + "-" + time.wDay + " " + time.wHour + ":" + time.wMinute + ":" + time.wSecond + "\r\n"
$@"Сохранение информации о доп карте, TransID: {Trans_ID}\r\n
Дата/Время транзакции: { _DateTime}\r\n
Номер карты:           { CardNo}\r\n
Тип карты:             { CardType}\r\n
\r\n", 2, true);

                        //label1.Text += "Сохранение информации о доп карте, TransID: " + Trans_ID + "\r\n"
                        //+ "Дата/Время транзакции: " + time.wYear + "-" + time.wMonth + "-" + time.wDay + " " + time.wHour + ":" + time.wMinute + ":" + time.wSecond + "\r\n"
                        //+ "Дата/Время транзакции: " + _DateTime + "\r\n"
                        // + "Номер карты:           " + CardNo + "\r\n"
                        //+ "Тип карты:             " + CardType + "\r\n"
                        //+ "\r\n";
                        return 1;
                    },
                    //Сохранение документа SaveReciept_Delegate
                    ///  DocType 0 - фискальные 1 - нефискальные 2 - отчет
                    (string RecieptText, long _DateTime, string DeviceName, string DeviceSerial,
                            int DocNo, int DocType, int Amount, decimal PrePrice,
                            decimal PreQuantity, decimal PreSum, decimal Price, decimal Quantity,
                            int VarCheck, string DocKind, int DocKindCode,
                            int PayType, int FactDoc, int BP_Product, long Trans_ID,
                            int PumpNo, int ShiftDocNum, int ShiftNum, string OrderRRN, IntPtr ctx) =>
                    {
                        //SYSTEMTIME time;
                        //VariantTimeToSystemTime(_DateTime, &time);
                        var summ = (DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;

                        long transCounter;
                        lock (Driver.TransCounterLocker)
                        {
                            //Driver.log.Write("", 0, true);
                            ++Driver.TransCounter;
                            transCounter = Driver.TransCounter;
                            //Driver.log.Write("", 0, true);
                        }

                        Driver.log.Write(
//{time.wYear}-{time.wMonth}-{time.wDay} {time.wHour}:{time.wMinute}:{time.wSecond}
$@"Сохранение документа, TransID: {Trans_ID}
\r\nДата/время:         {_DateTime}
\r\nИмя устройства:     {DeviceName}
\r\nСерийный номер:     {DeviceSerial}
\r\nНомер документа:    {DocNo}
\r\nТип документа:      {DocType}
\r\nСумма:              {summ}
\r\nПроизвольный чек:   {VarCheck}
\r\nВид документа:      {DocKind}
\r\nКод вида документа: {DocKindCode}
\r\nТип оплаты:         {PayType}
\r\nЧек по факту:       {FactDoc}
\r\nНомер продукта:     {BP_Product}
\r\nID Транзакции:      {Trans_ID}
\r\nID PumpNo:          {PumpNo}
\r\nID ShiftDocNum:     {ShiftDocNum}
\r\nID ShiftNum:        {ShiftNum}
\r\nID OrderRRN:        {OrderRRN.PadLeft(20, '0')}
\r\n------------------------------------------------------
\r\nОбраз Чека:
\r\n{RecieptText}
\r\n------------------------------------------------------
\r\n", 2, true);

                        //++Driver.TransCounter;

                        if (DocType != 0 || PumpNo <= 0)
                        {
                            Driver.log.Write("Не фискальные чеки не обрабатываются!", 1, true);

                            return 1;
                        }

                        var discount = 0;//100; //(order.BasePrice - order.Price) * order.Quantity;
                        var fuel =  XmlPumpClient.Fuels.First(t => t.Value.Id == BP_Product);
                        int allowed = 0;
                        Dictionary<string, FuelInfo> fuels;
                        lock ( XmlPumpClient.PumpsLocker)
                            fuels =  XmlPumpClient.Pumps[PumpNo].Fuels;
                        foreach (var pumpFuel in fuels.Where(fa => fa.Value.Active))
                        {
                            allowed += 1 << (pumpFuel.Value.Id - 1);
                        }

                        //TODO Проба со скидками!!!!
                        XmlPumpClient.SaleDataSale(XmlPumpClient.terminal, PumpNo, allowed,
                                PreSum, summ, discount,
                                PreQuantity, Quantity, PAYMENT_TYPE.Cash,
                                OrderRRN.PadLeft(20, '0'), BP_Product, fuel.Key, (int)(Price/*fuel.Value.Price*/ * 100), "", 1);
                        //XmlPumpClient.SaleDataSale(XmlPumpClient.terminal, order.PumpNo, allowed,
                        //    order.Amount, order.OverAmount, discount,
                        //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                        //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                        Driver.log.Write(
$@"WaitCollectThread:SaleDataSale:\r\n
terminal: {XmlPumpClient.terminal}\r\n
PumpNo: {PumpNo}\r\n
allowed: {allowed}\r\n
Amount: {PreSum}\r\n
OverAmount: {summ}\r\n
discount: {discount}\r\n
Quantity: {PreQuantity}\r\n
OverQuantity: {Quantity}\r\n
PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n
OrderRRN: {OrderRRN.PadLeft(20, '0')}\r\n
ProductCode: {BP_Product}\r\n
Key: {fuel.Key}\r\n
fuelPrice: {(int)(Price/*fuel.Value.Price*/ * 100)}\r\n", 2, true
                            );

                        //XmlPumpClient.FiscalEventReceipt(XmlPumpClient.terminal, order.PumpNo,
                        //    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
                        //    (endMessage?.Money ?? 0) / 100m, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
                        //Driver.log.Write.Write($"чек:\r\n" +
                        //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                        //    $"GetDocNum: {GetDocNum()}\r\n" +
                        //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                        //    $"OverAmount: {(endMessage?.Money ?? 0)/100.0}\r\n" +
                        //    $"OrderRRN: {order.OrderRRN}\r\n");

                        XmlPumpClient.Init(XmlPumpClient.terminal, PumpNo, -1, XmlPumpClient.WaitAnswerTimeout, 1);
                        Driver.log.Write("Освобождение колонки\r\n", 0, true);

                        //var res = XmlPumpClient.answers;
                        var res2 = XmlPumpClient.Statuses;
                        var res3 = XmlPumpClient.Fillings;

                        XmlPumpClient.ClearAllTransactionAnswers(PumpNo, OrderRRN.PadLeft(20, '0'));

                        XmlPumpClient.FiscalEventReceipt(XmlPumpClient.terminal, PumpNo/*order.PumpNo*/,
                            ShiftDocNum, DocNo, ShiftNum,
                            summ/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, OrderRRN.PadLeft(20, '0') /*order.OrderRRN*/, 1);
                        Driver.log.Write(
$@"чек:\r\n
GetShiftDocNum: {ShiftDocNum}\r\n
GetDocNum: {DocNo}\r\n
GetShiftNum: {ShiftNum}\r\n
OverAmount: {summ}\r\n
OrderRRN: {OrderRRN.PadLeft(20, '0')/*order.OrderRRN*/}\r\n", 2, true);

                        //DebithThread.SetTransID(Driver.TransCounter);

                        return 1;
                    },
                    "Sample Control", IntPtr.Zero/*ctxSrc*/) != 1
                )
                {
                    Driver.log.Write("Ошибка подключения драйвера\r\n", 0, true);
                    return;
                }
                RemotePump_Driver.RemotePump.StartServer();
            }
            catch (Exception ex)
            {
                Driver.log.Write($"Ошибка инициализации библиотеки {Driver.Description()}:{ex}\r\n", 0, true);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Console.WriteLine("Opening...");
            //start ASU - xml client
            Driver.InitXmlClient();
            //start Terminal client
            OpenDriver();
            //start Benzuber client
            Driver.StartBenzuber();
            //for background child process
            Console.WriteLine("Press [ENTER] to exit");
            Console.ReadLine();
        }
    }
}
