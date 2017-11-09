using System;
using System.Linq;
using System.Threading;
using ASUDriver;
using RemotePump_Driver;

//using ASUDriver;
//using RemotePump_Driver;

namespace SPC_Raspberry
{
    static class Program
    {
        public static void log(string txt, bool toConsole)
        {
                //Logger.Text += txt;
                //Driver.log.Write(txt);
                Driver.log.Write(txt,0, toConsole);
            //Console.WriteLine($"{DateTime.Now.ToString("u")} - {txt}");
            //return;
            //string path = @"app_log.log";
            //// This text is added only once to the file.
            //if (!File.Exists(path))
            //{
            //    // Create a file to write to.
            //    using (StreamWriter sw = File.CreateText(path))
            //    {
            //        sw.WriteLine(txt);
            //    }
            //}
            //else using (StreamWriter sw = File.AppendText(path))
            //    {
            //        sw.WriteLine(txt);
            //    }
        }

        public static void OpenDriver()
        {
            try
            {
                if (Driver.Open(
                    //Установка дозы на ТРК
                    (RemotePump_Driver.OrderInfo Order, IntPtr ctx) =>
                    {
                        ++Driver.TransCounter;
                        log("Установка дозы на ТРК: " + Order.PumpNo + " , сгенерирован TransID: " +
                            Driver.TransCounter + "\r\n", true);

                            //var prePaid = Order.Price*Order.Quantity;
                            var discount = 0;//100;//(Order.BasePrice - Order.Price)*Order.Quantity;
                            var fuel = Driver.Fuels.First(t => t.Value.ID == Order.ProductCode);
                            int allowed = 0;
                            foreach (var pumpFuel in Driver.Pumps[Order.PumpNo].Fuels)
                            {
                                allowed += 1 << (pumpFuel.Value.ID - 1);
                            }
                            //decimal price = 156;
                            //decimal vol = 100;
                            //if (!XmlPumpClient.Presale(
                            //    Driver.terminal, Order.PumpNo, allowed, Order.Amount + discount,
                            //    discount, Order.Quantity, XmlPumpClient.PaymentCodeToType(Order.PaymentCode), 
                            //    Order.OrderRRN, 1, "АИ-92", 2000, "1234567890123456", 1, 3000))
                            //    return -1;
                            //log("предоплата\r\n");

                            //if (!XmlPumpClient.Authorize(Driver.terminal, Order.PumpNo, 10, allowed, Order.PumpNo, Order.OrderRRN, (int)(price * 100), DELIVERY_UNIT.Money, 3000))
                            //    return -1;
                            //log("разрешить налив\r\n");

                            log("Presale:\r\n" +
                            $"терминал:{Driver.terminal}\r\n" +
                            $"колонка:{Order.PumpNo}\r\n" +
                            $"дост. пистолеты:{allowed}\r\n" +
                            $"сумма руб:{Order.Amount}\r\n" +
                            $"скидка:{discount}\r\n" +
                            $"кол-во литры:{Order.Quantity}\r\n" +
                            $"тип оплаты:{XmlPumpClient.PaymentCodeToType(Order.PaymentCode)}\r\n" +
                            $"рнн:{Order.OrderRRN}\r\n" +
                            $"продукт код:{Order.ProductCode}\r\n" +
                            $"продукт{fuel.Key}\r\n" +
                            $"продукт цена коп:{(int)(Order.Price/*fuel.Value.Price*/ * 100)}\r\n", true);

                        //TODO Проба со скидками!!!!
                        string errMsg = "";
                        if (!XmlPumpClient.Authorize(Driver.terminal, Order.PumpNo, Driver.TransCounter,
                            allowed, Order.ProductCode, Order.OrderRRN, (int)(Order.Quantity * 100), DELIVERY_UNIT.Volume,/*(int) (Order.Amount*100), DELIVERY_UNIT.Money,*/
                            XmlPumpClient.WaitAnswerTimeout, out errMsg))
                        //if (!XmlPumpClient.Authorize(Driver.terminal, Order.PumpNo, Driver.TransCounter,
                        //    allowed, Order.ProductCode, Order.OrderRRN, (int) (Order.Amount*100), DELIVERY_UNIT.Money,
                        //    XmlPumpClient.WaitAnswerTimeout))
                        {
                            log($"SetDoseCallback:Authorize: {errMsg}\r\n", true);
                            return -1;
                        }


                        //TODO Проба со скидками!!!!
                        if (!XmlPumpClient.Presale(Driver.terminal, Order.PumpNo, allowed, Order.Amount,
                            discount, Order.Quantity, XmlPumpClient.PaymentCodeToType(Order.PaymentCode),
                            Order.OrderRRN, Order.ProductCode, fuel.Key,
                            (int)(Order.Price/*fuel.Value.Price*/ * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                        //if (!XmlPumpClient.Presale(Driver.terminal, Order.PumpNo, allowed, Order.Amount,
                        //    discount, Order.Quantity, XmlPumpClient.PaymentCodeToType(Order.PaymentCode),
                        //    Order.OrderRRN, Order.ProductCode, fuel.Key,
                        //    (int)(fuel.Value.Price * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                        {
                            log("SetDoseCallback:Presale: нет о твета на Presale\r\n", true);
                            return -1;
                        }


                        log("налив разрешен\r\n", true);

                            //lock (Driver.TransMemory)
                            //{
                            //    Driver.TransMemory[(long) Driver.TransCounter] = Order;
                            //}

                        Thread myThread2 = new Thread(Driver.WaitCollect);
                        myThread2.Start(Driver.TransCounter); // запускаем поток

                            //XmlPumpClient.PumpRequestAuthorize(Driver.terminal, Order.PumpNo, Driver.TransCounter,
                            //    allowed, 1, Order.OrderRRN, (int)(Order.Amount * 100), DELIVERY_UNIT.Money);

                            //Thread.Sleep(2000);


                            //while (XmlPumpClient.answers.Count != 0)
                            //    XmlPumpClient.PumpRequestCollect(Driver.terminal, Order.PumpNo, Driver.TransCounter,
                            //    Order.OrderRRN);

                            //Thread.Sleep(2000);

                            //    Driver.FillingOver(Driver.TransCounter, (int)(Order.Quantity * 100), (int)(Order.Amount*100));

                            //DebithThread.SetTransID(Driver.TransCounter);
                            return Driver.TransCounter;
                    },
                    //Запрос состояние ТРК
                    (long Pump, IntPtr ctx) =>
                    {
                        log("Запрос состояние ТРК: " + Pump + "\r\n", false);

                            //DispStatus:
                            //	0 - ТРК онлайн(при этом TransID должен = -1, иначе данный статус воспринимается как 3)
                            //	1 - ТРК заблокирована
                            //	3 - Осуществляется отпуск топлива
                            //	10 - ТРК занята
                            object item;
                        OnPumpStatusChange оnPumpStatusChanged = null;

                        XmlPumpClient.PumpGetStatus(Driver.terminal, (int)Pump, 1);
                            //lock (XmlPumpClient.answers)
                            //    оnPumpStatusChanged = (OnPumpStatusChange)XmlPumpClient.answers.LastOrDefault(t => t is OnPumpStatusChange && (t as OnPumpStatusChange).PumpNo == Pump);
                        lock (XmlPumpClient.Statuses)
                            if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>((int)Pump,
                                MESSAGE_TYPES.OnPumpStatusChange), out item) && item != null)
                                оnPumpStatusChanged = (OnPumpStatusChange)item;

                        var е = XmlPumpClient.Fillings;
                        if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                        {
                            Thread myThread2 = new Thread(Driver.CollectOldOrderThread);
                            myThread2.Start((int)Pump); // запускаем поток


                                //    log("\tGetPumpState статус WAITING_COLLECTING\r\n");
                                //    XmlPumpClient.Collect(Driver.terminal, (int)Pump, Driver.TransCounter, "", XmlPumpClient.WaitAnswerTimeout);
                                //    //Thread.Sleep(500);
                                //    XmlPumpClient.PumpGetStatus(Driver.terminal, (int)Pump, 1);
                                //    оnPumpStatusChanged = (OnPumpStatusChange)XmlPumpClient.Statuses[new Tuple<int, MESSAGE_TYPES>
                                //                ((int)Pump, MESSAGE_TYPES.OnPumpStatusChange)];
                            }

                        var pmp = Driver.Pumps[(int)Pump];
                        pmp.DispStatus =
                        (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_IDLE
                        || оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION) ? 0 :
                        (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_AUTHORIZED
                        || оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING) ? 3 : 10;
                        Driver.Pumps[(int)Pump] = pmp;

                        log($"Статус ТРК [{Pump}]: { (byte)pmp.DispStatus}; UpNozz: {(byte)((оnPumpStatusChanged?.Grade ?? -2) + 1)}\r\n", true);

                        /*                        Driver.GetPumpStateResponce resp;

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

                                                IntPtr respPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(resp));
                                                Marshal.StructureToPtr(resp, respPtr, false);

                                                log("return", false);

                                                return resp;
                        */
                        return new Driver.GetPumpStateResponce();
                    },
                    //Отмена транзакции
                    (long TransID, IntPtr ctx) =>
                    {
                        log("Отмена транзакции: TransactID: " + TransID + "\r\n", true);

                            //Driver.VolumeMem = 0;
                            //Driver.AmountMem = 0;
                            //EndFilling.Enabled = false;
                            //FillingUp.Enabled = false;
                            //FillingDown.Enabled = false;
                            OrderInfo order = new OrderInfo();

                        int cnt = 2;
                        while (cnt > 0)
                        {
                            lock (Driver.TransMemory)
                            {
                                if (Driver.TransMemory.TryGetValue((long)TransID, out order))
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
                            log(
                                        $"\t\tERROR!!! CancelDoseDelegate:\r\n\t\tне найден заказ №{(long)TransID}\r\n", true);
                            return -1;
                        }

                        XmlPumpClient.PumpGetStatus(Driver.terminal, order.PumpNo, 1);

                        object item = null;
                        OnPumpStatusChange оnPumpStatusChanged = null;

                        lock (XmlPumpClient.Statuses)
                            if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(order.PumpNo,
                                MESSAGE_TYPES.OnPumpStatusChange), out item) && item != null)
                                оnPumpStatusChanged = (OnPumpStatusChange)item;

                            //var discount = (order.BasePrice - order.Price) * order.Quantity;
                            //var fuel = Driver.Fuels.First(t => t.Value.ID == order.ProductCode);
                            //int allowed = 0;
                            //foreach (var pumpFuel in Driver.Pumps[order.PumpNo].Fuels)
                            //{
                            //    allowed += 1 << (pumpFuel.Value.ID - 1);
                            //}
                            //TODO Не работает!
                            if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_AUTHORIZED)
                        {
                            log(
                                       $"\t\tCancelDoseDelegate:\r\n\t\tPumpStop №{(long)TransID} pmp:{order.PumpNo}\r\n", true);
                            XmlPumpClient.PumpStop(Driver.terminal, order.PumpNo, 1);

                                //Thread.Sleep(1000);

                                //var res2 = XmlPumpClient.statuses;
                                //var res3 = XmlPumpClient.fillings;


                                //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
                                //    order.Amount, order.OverAmount, discount,
                                //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                                //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                                //log.Write(
                                //    "CancelDoseDelegate:SaleDataSale:\r\n" +
                                //    $"terminal: {Driver.terminal}\r\n" +
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
                            //    log.Write(
                            //        "CancelDoseDelegate:Collect:\r\n" +
                            //        $"terminal: {Driver.terminal}\r\n" +
                            //        $"PumpNo: {order.PumpNo}\r\n" +
                            //        $"RequestId: {TransID}\r\n" +
                            //        $"OrderRRN: {order.OrderRRN}\r\n"
                            //        );
                            //    if (!XmlPumpClient.Collect(Driver.terminal, order.PumpNo, TransID, order.OrderRRN, 3000))
                            //    {
                            //        log.Write(
                            //            $"\t\tERROR!!! CancelDoseDelegate:\r\n\t\tнет события ответа на Collect заказа RNN{order.OrderRRN}\r\n");
                            //        return -1;
                            //    }

                            //}

                            log("Налив успешно завершен" + "\r\n", true);

                            //Driver.PriceMem = 0;
                            return 1;
                    },
                    //Захват/Освобождение ТРК
                    (int PumpId, byte ReleasePump, IntPtr ctx) =>
                    {
                        object item = null;
                        lock (XmlPumpClient.Statuses)
                        {
                            if (ReleasePump == 0)
                            {
                                log("Захват ТРК: " + PumpId + "\r\n", true);


                                if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                    MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                {
                                    XmlPumpClient.InitData(Driver.terminal);
                                }

                                var pump = Driver.Pumps[PumpId];
                                pump.Blocked = true;

                                XmlPumpClient.Init(Driver.terminal, PumpId, PumpId, XmlPumpClient.WaitAnswerTimeout, 1);
                                    //Driver.Pumps[Pump] = pump;

                                    //label1.Text += "Захват ТРК: " + Pump + "\r\n";
                                    return 1;
                            }
                            else
                            {
                                log("Освобождение ТРК: " + PumpId + "\r\n", true);

                                if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                    MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                {
                                    XmlPumpClient.InitData(Driver.terminal);
                                }

                                var pump = Driver.Pumps[PumpId];
                                pump.Blocked = false;

                                XmlPumpClient.Init(Driver.terminal, PumpId, -PumpId, XmlPumpClient.WaitAnswerTimeout, 1);
                                    //Driver.Pumps[Pump] = pump;

                                    //label1.Text += "Освобождение ТРК: " + Pump + "\r\n";
                                    return 1;
                            }
                        }
                    },
                    //Пересчет заказа UpdateFillingOver
                    (int Amount, int Price, long Trans_ID, int DiscountMoney, IntPtr ctx) =>
                    {
                        log("Пересчет заказа, TransID: " + Trans_ID + "\r\n"
                        + "Новая цена:   " + (float)Price / 100 + "\r\n"
                        + "Новая сумма:  " + (float)Amount / 100 + "\r\n"
                        + "Новая скидка: " + (float)DiscountMoney / 100 + "\r\n"
                        + "\r\n", true);

                            //++Driver.TransCounter;

                            //XmlPumpClient.FiscalEventReceipt(Driver.terminal, 1/*order.PumpNo*/,
                            //    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
                            //    (decimal)Amount / 100m/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, "123123123123" /*order.OrderRRN*/, 1);
                            //log($"чек:\r\n" +
                            //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                            //    $"GetDocNum: {GetDocNum()}\r\n" +
                            //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                            //    $"OverAmount: {(decimal)Amount / 100m}\r\n" +
                            //    $"OrderRRN: {123123123123/*order.OrderRRN*/}\r\n");

                            //DebithThread.SetTransID(Driver.TransCounter);

                            //var order = Driver.TransMemory[Trans_ID];
                            //var discount = (order.BasePrice - order.Price) * order.Quantity;
                            //var fuel = Driver.Fuels.First(t => t.Value.ID == order.ProductCode);
                            //int allowed = 0;
                            //foreach (var pumpFuel in Driver.Pumps[order.PumpNo].Fuels)
                            //{
                            //    allowed += 1 << (pumpFuel.Value.ID - 1);
                            //}

                            //if (!XmlPumpClient.Collect(Driver.terminal, order.PumpNo, allowed, order.OrderRRN, 3000))
                            //    return -1;
                            //log("освобождение колонки\r\n");
                            //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
                            //    order.Amount, order.OverAmount, discount,
                            //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                            //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                            //log("фактические данные заправки\r\n");
                            //XmlPumpClient.FiscalEventReceipt(Driver.terminal, order.PumpNo, 1, 1, 1,
                            //    order.OverAmount, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
                            //log("чек\r\n");
                            //XmlPumpClient.Init(Driver.terminal, order.PumpNo, -order.PumpNo, 1, 3000);
                            //log("изм. статуса\r\n");

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

                            log("Сохранение информации о доп карте, TransID: " + Trans_ID + "\r\n"
                        //+ "Дата/Время транзакции: " + time.wYear + "-" + time.wMonth + "-" + time.wDay + " " + time.wHour + ":" + time.wMinute + ":" + time.wSecond + "\r\n"
                        + "Дата/Время транзакции: " + _DateTime + "\r\n"
                        + "Номер карты:           " + CardNo + "\r\n"
                        + "Тип карты:             " + CardType + "\r\n"
                        + "\r\n", true);

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
                            decimal PreQuantity,decimal PreSum,decimal Price,decimal Quantity,
                            int VarCheck, string DocKind, int DocKindCode,
                            int PayType, int FactDoc, int BP_Product, long Trans_ID,
                            int PumpNo, int ShiftDocNum, int ShiftNum, string OrderRRN, IntPtr ctx) =>
                    {
                        //SYSTEMTIME time;
                        //VariantTimeToSystemTime(_DateTime, &time);
                        var summ = (DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;

                        log("Сохранение документа, TransID: " + Trans_ID
                        + "\r\nДата/время:         " + _DateTime//time.wYear + "-" + time.wMonth + "-" + time.wDay + " " + time.wHour + ":" + time.wMinute + ":" + time.wSecond
                        + "\r\nИмя устройства:     " + DeviceName
                        + "\r\nСерийный номер:     " + DeviceSerial
                        + "\r\nНомер документа:    " + DocNo
                        + "\r\nТип документа:      " + DocType
                        + "\r\nСумма:              " + summ
                        + "\r\nПроизвольный чек:   " + VarCheck
                        + "\r\nВид документа:      " + DocKind
                        + "\r\nКод вида документа: " + DocKindCode
                        + "\r\nТип оплаты:         " + PayType
                        + "\r\nЧек по факту:       " + FactDoc
                        + "\r\nНомер продукта:     " + BP_Product
                        + "\r\nID Транзакции:      " + Trans_ID
                        + "\r\nID PumpNo:          " + PumpNo
                        + "\r\nID ShiftDocNum:     " + ShiftDocNum
                        + "\r\nID ShiftNum:        " + ShiftNum
                        + "\r\nID OrderRRN:        " + OrderRRN.PadLeft(20, '0')
                        + "\r\n------------------------------------------------------"
                        + "\r\nОбраз Чека:         "
                        + "\r\n" + RecieptText
                        + "\r\n------------------------------------------------------"
                        + "\r\n", true);

                        ++Driver.TransCounter;

                        if (DocType != 0 || PumpNo <= 0)
                            return 1;

                        var discount = 0;//100; //(order.BasePrice - order.Price) * order.Quantity;
                        var fuel = Driver.Fuels.First(t => t.Value.ID == BP_Product);
                        int allowed = 0;
                        foreach (var pumpFuel in Driver.Pumps[PumpNo].Fuels)
                        {
                            allowed += 1 << (pumpFuel.Value.ID - 1);
                        }

                        //TODO Проба со скидками!!!!
                        XmlPumpClient.SaleDataSale(Driver.terminal, PumpNo, allowed,
                                PreSum, summ, discount,
                                PreQuantity, Quantity, PAYMENT_TYPE.Cash,
                                OrderRRN.PadLeft(20, '0'), BP_Product, fuel.Key, (int)(Price/*fuel.Value.Price*/ * 100), "", 1);
                        //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
                        //    order.Amount, order.OverAmount, discount,
                        //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                        //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                        log(
                            "WaitCollectThread:SaleDataSale:\r\n" +
                            $"terminal: {Driver.terminal}\r\n" +
                            $"PumpNo: {PumpNo}\r\n" +
                            $"allowed: {allowed}\r\n" +
                            $"Amount: {PreSum}\r\n" +
                            $"OverAmount: {summ}\r\n" +
                            $"discount: {discount}\r\n" +
                            $"Quantity: {PreQuantity}\r\n" +
                            $"OverQuantity: {Quantity}\r\n" +
                            $"PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n" +
                            $"OrderRRN: {OrderRRN.PadLeft(20, '0')}\r\n" +
                            $"ProductCode: {BP_Product}\r\n" +
                            $"Key: {fuel.Key}\r\n" +
                            $"fuelPrice: {(int)(Price/*fuel.Value.Price*/ * 100)}\r\n", true
                            );

                        //XmlPumpClient.FiscalEventReceipt(Driver.terminal, order.PumpNo,
                        //    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
                        //    (endMessage?.Money ?? 0) / 100m, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
                        //log.Write($"чек:\r\n" +
                        //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                        //    $"GetDocNum: {GetDocNum()}\r\n" +
                        //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                        //    $"OverAmount: {(endMessage?.Money ?? 0)/100.0}\r\n" +
                        //    $"OrderRRN: {order.OrderRRN}\r\n");

                        XmlPumpClient.Init(Driver.terminal, PumpNo, -PumpNo, XmlPumpClient.WaitAnswerTimeout, 1);
                        log("изм. статуса\r\n", true);

                        //var res = XmlPumpClient.answers;
                        var res2 = XmlPumpClient.Statuses;
                        var res3 = XmlPumpClient.Fillings;

                        XmlPumpClient.ClearAllTransactionAnswers(PumpNo, OrderRRN.PadLeft(20, '0'));

                        XmlPumpClient.FiscalEventReceipt(Driver.terminal, PumpNo/*order.PumpNo*/,
                            ShiftDocNum, DocNo, ShiftNum,
                            summ/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, OrderRRN.PadLeft(20, '0') /*order.OrderRRN*/, 1);
                        log($"чек:\r\n" +
                            $"GetShiftDocNum: {ShiftDocNum}\r\n" +
                            $"GetDocNum: {DocNo}\r\n" +
                            $"GetShiftNum: {ShiftNum}\r\n" +
                            $"OverAmount: {summ}\r\n" +
                            $"OrderRRN: {OrderRRN.PadLeft(20, '0')/*order.OrderRRN*/}\r\n", true);

                        //DebithThread.SetTransID(Driver.TransCounter);

                        return 1;
                    },
                    "Sample Control", IntPtr.Zero/*ctxSrc*/) != 1
                )
                {
                    log("Ошибка подключения драйвера" + "\r\n", true);
                    return;
                }
                //else
                //{
                //    log(Driver.Description() + "\r\n");
                //}

                //int OS = Environment.OSVersion.Version.Major;
                //log("OS Ver - " + Environment.OSVersion.Version + "\r\n");
                //if (OS > 4)
                Driver.FuelPrices();
                //else
                //    Driver.FuelPrices();

                //if (OS > 4)
                Driver.PumpFuels();
                //else
                //    Driver.PumpFuels("1=95.92.80;2=95.92.80;3=95.92;4=95.92");

                Driver.StartBenzuber();

                object res;
                if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1, MESSAGE_TYPES.OnDataInit), out res))
                {
                    log(
                        Driver.Description() + " успешно открыта!\r\n", true);
                }
                else
                {
                    log(
                        Driver.Description() + " нет ответа от АСУ!\r\n", true);
                }
            }
            catch (Exception ex)
            {
                log($"Ошибка инициализации библиотеки {Driver.Description()}:{ex}\r\n", true);
            }
        }

        public static void TestTrans(int terminal = 1, int pump = 1,//int.Parse(comboBox1.SelectedItem.ToString());
        int nozzle = 1, int product = 1, decimal price = 558, int initNum = 0)
        {
            var discount = 0;//100;//(Order.BasePrice - Order.Price)*Order.Quantity;
            var fuel = Driver.Fuels.First(t => t.Value.ID == product);
            int allowed = 0;
            foreach (var pumpFuel in Driver.Pumps[pump].Fuels)
            {
                allowed += 1 << (pumpFuel.Value.ID - 1);
            }
            ASUDriver.XmlPumpClient.Init(terminal, pump, pump, XmlPumpClient.WaitAnswerTimeout, 1);
            log("занять колонку\r\n", true);
            if (!XmlPumpClient.Presale(terminal, pump, allowed, price, 0, price / fuel.Value.Price, PAYMENT_TYPE.Cash, "qwertyuiop" + initNum, product, "АИ-92", (int)(fuel.Value.Price * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                return;
            log("предоплата\r\n", true);

            string errMsg = "";
            if (!XmlPumpClient.Authorize(terminal, pump, 10, allowed, nozzle, "qwertyuiop" + initNum, (int)(price * 100), DELIVERY_UNIT.Money, XmlPumpClient.WaitAnswerTimeout, out errMsg))
                return;
            log("разрешить налив\r\n", true);
            
            var endMessage = XmlPumpClient.EndFillingEventWait(pump, "qwertyuiop" + initNum);
            
            if (!XmlPumpClient.Collect(terminal, pump, 10, "qwertyuiop" + initNum, XmlPumpClient.WaitAnswerTimeout))
                return;
            log("освобождение колонки\r\n", true);
            XmlPumpClient.SaleDataSale(terminal, pump, allowed, price, (endMessage == null ? 0 : (decimal)endMessage.Money) / 100, 0, price / fuel.Value.Price, (endMessage == null ? 0 : (decimal)endMessage.Liters) / 100, PAYMENT_TYPE.Cash, "qwertyuiop" + initNum, product, "АИ-92", (int)(fuel.Value.Price * 100), "", 1);
            Thread.Sleep(3000);
            log("фактические данные заправки\r\n", true);
            XmlPumpClient.FiscalEventReceipt(Driver.terminal, pump,
            1350 + initNum, 65 + initNum, 6,//GetShiftDocNum(), GetDocNum(), GetShiftNum(),
            price, 0, PAYMENT_TYPE.Cash, "qwertyuiop" + initNum, 1);
            log($"чек:\r\n" +
            $"GetShiftDocNum: {1350 + initNum}\r\n" +
            $"GetDocNum: {65 + initNum}\r\n" +
            $"GetShiftNum: {6}\r\n" +
            $"OverAmount: {price}\r\n", true);
            XmlPumpClient.Init(terminal, pump, -pump, XmlPumpClient.WaitAnswerTimeout, 1);
            log("изм. статуса\r\n", true);
            
            var res2 = XmlPumpClient.Statuses;
            var res3 = XmlPumpClient.Fillings;
            
            XmlPumpClient.ClearAllTransactionAnswers(pump, "qwertyuiop" + initNum);
            log("\r\n", true);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            Console.WriteLine("Opening...");
            Driver.StartBenzuber();
            //OpenDriver();

            //TestTrans(1, 2, 1, 1, 542, 3);
        }
    }
}
