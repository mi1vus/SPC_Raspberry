using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ProjectSummer.Repository;

namespace ASUDriver
{
    class Benzuber
    {
        public class ExcangeServer
        {
            public static bool enable = false;
            public static ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");

            //public static Logger logBenzuber = new Logger("Benzuber");

            /// <summary>
            /// Получение клиента по его идентификатору
            /// </summary>
            /// <param name="ClientID">Идентификатор клиента</param>
            /// <returns></returns>
            public TcpExcangeServerClient this[string ClientID]
            {
                get
                {
                    return clients.ContainsKey(ClientID) ? clients[ClientID] : null;
                }
            }

            /// <summary>
            /// Запуск сервера сообщений
            /// </summary>
            /// <param name="Port"></param>
            public void Start(X509Certificate2 Certificate, int Port = 5051)
            {
                this.Certificate = Certificate;
                //Запускаем отдельный поток для ожидания входящий сообщений               
                new Task(() =>
                {
                    //Запускаем прослущивание порта, для ожидания входящих сообщений 
                    TcpListener listener = new TcpListener(Port);
                    listener.Start();
                    while (true)
                    {
                        //Ожидаем подключние клиента
                        var client = listener.AcceptTcpClient();

                        //Запускаем обмен с клиентом в тодельном потоке
                        new Task<bool>(() => { return clientExhange(client); }).Start();
                    }
                }).Start();
            }

            /// <summary>
            /// Происходит при успешной регистрации клиента
            /// </summary>
            public event EventHandler<ClientInfoEventArgs> ClientRegistred;

            /// <summary>
            /// Информация о зарегистрированим клиенте
            /// </summary>
            public class ClientInfoEventArgs : EventArgs
            {
                public string ClientID { get; set; }
            }

            /// <summary>
            /// Кодировка по умолчанию для передачи сообщений
            /// </summary>
            public Encoding DefaultEncoding { get; set; } = Encoding.UTF8;

            public X509Certificate2 Certificate { get; set; }

            /// <summary>
            /// Словарь с данными о зарегистрированных ранее клиентах
            /// </summary>
            readonly Dictionary<string, TcpExcangeServerClient> clients = new Dictionary<string, TcpExcangeServerClient>();
            public delegate bool ValidateClientDelegate(string ID, string HW_ID);
            public ValidateClientDelegate ValidateClient;
            private bool clientExhange(TcpClient Client)
            {
                try
                {
                    Driver.log.Write("Bzer: Принято входящее подключения", 1, true);
                    try
                    {
                        var _Stream = TcpExcangeServerClient._GetNetStream(Client, Certificate);
                        TcpExcangeClientBase.sendMessage("GET_ID", _Stream, Encoding.UTF8);
                        var ClientID = TcpExcangeClientBase.waitMessage(_Stream, Encoding.UTF8);

                        if (string.IsNullOrWhiteSpace(ClientID))
                            throw new Exception("Некорректный ответ на запрос GET_ID");

                        TcpExcangeClientBase.sendMessage("GET_HW", _Stream, Encoding.UTF8);
                        var HW_ID = TcpExcangeClientBase.waitMessage(_Stream, Encoding.UTF8);
                        if (string.IsNullOrWhiteSpace(HW_ID))
                            throw new Exception("Некорректный ответ на запрос GET_HW");

                        Driver.log.Write($"Получены идентификаторы клиента. ClientID: '{ClientID}', HW_ID:{HW_ID}", 2, true);
                        if (!(ValidateClient?.Invoke(ClientID, HW_ID) ?? false))
                            return false;

                        if (string.IsNullOrWhiteSpace(ClientID))
                            return false;

                        lock (clients)
                        {
                            if (!clients.ContainsKey(ClientID))
                            {
                                clients.Add(ClientID, new TcpExcangeServerClient(Client, _Stream, ClientID, Certificate));
                                Driver.log.Write($"Клиент успешно зарегистрирован: " + ClientID, 0, true);
                                ClientRegistred?.Invoke(this, new ClientInfoEventArgs() { ClientID = ClientID });
                            }
                            else
                            {
                                Driver.log.Write($"Клиент переподключился: " + ClientID, 0, true);
                                lock (clients[ClientID])
                                {
                                    clients[ClientID].Client = Client;
                                    clients[ClientID].Stream = _Stream;
                                    clients[ClientID].HW_ID = HW_ID;
                                }
                            }
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Driver.log.Write(ex.ToString(), 0, true);
                    }
                    finally { }
                }
                catch (Exception ex) { Driver.log.Write(ex.ToString(), 0, true); }
                return false;
            }

            static TcpExcangeClient client;

            public static void StartClient()
            {
                if (enable)
                    return;
                enable = true;

                string hw_id = "";
                var hw = ActivationClass.GetDiskDriveSerialNumbers();
                if (hw.Length <= 0)
                {
                    Driver.log.Write("Bzer: Не удалось получить HW ID", 0, true);
                    return;
                }
                else
                {
                    hw_id = ComputeMD5Checksum(hw[0].Name + hw[0].Serial);
                    //В релизе id расчитывается от MAC адреса
#if DEBUG
                    hw_id = "7E6913831A8CF14E41943B3942C7E347";
#endif
                    Driver.log.Write($"Вычисляем HW ID: {hw_id} для: {hw[0].Name}:{hw[0].Serial}", 1, true);
                }
                //                var str = string.Format("net.tcp://" + config["server"] + ":" + config["exchangeport"]);
                var str = string.Format("serv: " + config["server"] + "; port: " + config["exchangeport"] + "; client: " + config["station_id"]);
                Driver.log.Write(str, 0, true);
                var location = Assembly.GetExecutingAssembly().Location;

                //ClientID должен задаваться в настройках
                //HW_ID должен генерироваться библиотекой на основании серийных номеров оборудования  
                client = new TcpExcangeClient(config["station_id"]/*"41065"*/, hw_id);

                //Указываем обработчик для запросов от сервера  
                client.HandleRequest = new TcpExcangeClient.HandleRequestDelegate(handler);

                int port;
                if (int.TryParse(config["exchangeport"], out port))
                {
                    //Запускаем клиента
                    client.Start(config["server"]/*"212.49.100.116 или testazsapi.benzuber.ru"*/, port/*5051 или 1102*/ );
                }
                else
                {
                    //Запускаем клиента
                    client.Start(config["server"]);
                }
            }
            public static string ComputeMD5Checksum(string Data)
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] checkSum = md5.ComputeHash(Encoding.Default.GetBytes(Data));
                string result = BitConverter.ToString(checkSum).Replace("-", String.Empty);
                return result;
            }
            static bool initialized = false;

            /// <summary>
            /// Обработчик запросов от сервера
            /// </summary>
            /// <param name="Str">Запрос</param>
            /// <returns></returns>
            static string handler(string Str)
            {
                try
                {
                    //Десериализуем запрос от сервера в объект класса Request   
                    var op = json_deser<Request>(Str);

                    switch (op?.Operation)
                    {
                        #region Ping

                            //Оперция проверки связи
                            //Интервал между запросами может варьироваться в диапазоне 1-30 сек в зависимости отнастроек сервера.
                            //По умолчанию каждые 5 сек.  
                            case "Ping":
                            {
                                //В случае, если это первый запрос после запуска - передаем номер версии установленного П.О.
                                if (!initialized)
                                {
                                    initialized = true;
                                    return json(new { Result = "OK", RunningVersion = "0.1.1 Linux", LoadedVersion = "0.1.1 Linux" });
                                }
                                //Проверяем есть ли непереданные завершенные транзакции.
                                //Если нет - возвращаем Result = "OK"
                                //var fillingOvers = Pump.GetFillingOvers();
                                if (TransOvers.Count == 0 /*&& fillingOvers.Length == 0*/)
                                    return json(new { Result = "OK" });
                                else
                                {
                                    //Если есть - передаем по одной. Обязательно начиная с последней.
                                    //На тот случай, если при передаче одной из транзакций происходит ошибка, 
                                    //чтобы эта ошбка не мешала передавать новые транзакции. 
                                    //Заранее блокируем доступ к хранилищу транзакций.
                                    lock (TransOversLocker)
                                    {
                                        if (TransOvers.Count == 0)
                                            return json(new { Result = "OK" });
                                        var last = TransOvers.Last();
                                        return json(new { Result = "OK", RequestOperation = "FillingOver", Amount = last.Value.Amount, TransactionID = last.Value.TransactionID });
                                    }
                                    //if (fillingOvers.Length == 0)
                                    //    return json(new { Result = "OK" });
                                    //var last = fillingOvers.Last();
                                    //return json(new { Result = "OK", RequestOperation = "FillingOver", Amount = last.Amount, TransactionID = last.OrderRRN });
                                }
                            }

                            #endregion

                        #region GetStationInfo

                            //Запрос оперативной информации о АЗС.
                            case "GetStationInfo":
                            {
                                List<int> inds;
                                lock (Driver.PumpsLocker)
                                    inds = Driver.Pumps.Keys.ToList();
                                foreach(var pmpInd in inds)
                                {
                                    Driver.log.Write("Запрос состояние ТРК: " + pmpInd + "\r\n", 3, true);

                                    //DispStatus:
                                    //	0 - ТРК онлайн(при этом TransID должен = -1, иначе данный статус воспринимается как 3)
                                    //	1 - ТРК заблокирована
                                    //	3 - Осуществляется отпуск топлива
                                    //	10 - ТРК занята
                                    object item;
                                    OnPumpStatusChange оnPumpStatusChanged = null;

                                    lock (XmlPumpClient.StatusesLocker)
                                        if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(pmpInd,
                                                MESSAGE_TYPES.OnPumpStatusChange), out item) && item != null)
                                            оnPumpStatusChanged = (OnPumpStatusChange) item;

                                    var е = XmlPumpClient.Fillings;
                                    if (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                                    {
                                        Thread myThread2 = new Thread(Driver.CollectOldOrderThread) { IsBackground = true };
                                        myThread2.Start(pmpInd); // запускаем поток
                                    }
                                    Driver.PumpInfo pmp;
                                    lock (Driver.PumpsLocker)
                                    {
                                        pmp = Driver.Pumps[pmpInd];
                                        pmp.DispStatus =
                                        (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_IDLE
                                         ||
                                         оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION)
                                            ? 0
                                            : (оnPumpStatusChanged?.StatusObj == PUMP_STATUS.PUMP_STATUS_AUTHORIZED
                                               ||
                                               оnPumpStatusChanged?.StatusObj ==
                                               PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                                                ? 3
                                                : 10;
                                        pmp.UpNozzle = (byte) ((оnPumpStatusChanged?.Grade ?? -2) + 1);
                                        Driver.Pumps[pmpInd] = pmp;
                                    }
                                    Driver.log.Write(
                                        $"Статус ТРК [{pmpInd}]: {(byte) pmp.DispStatus}; UpNozz: {(byte) ((оnPumpStatusChanged?.Grade ?? -2) + 1)}; Blocked:{pmp.Blocked}\r\n",
                                        3, true);
                                }
                                lock (Driver.PumpsLocker)
                                {
                                    var obj = new
                                    {
                                        //Код АЗС
                                        StationID = client.ClientID,
                                        //Список видов топлива
                                        Fuels = Driver.Fuels.Select(f =>
                                                new
                                                {
                                                    Code = f.Value.InternalCode,
                                                    Name = f.Value.Name,
                                                    Price = f.Value.Price
                                                }
                                        ).ToArray(),
                                        //Список ТРК
                                        Pumps = Driver.Pumps.Select(p => new
                                        {
                                            Number = p.Value.Pump,
                                            IsAvailable = p.Value.DispStatus == 0 && !p.Value.Blocked,
                                            MinOrder = 2,
                                            Nozzles = p.Value.Fuels.Select(f =>
                                                    new
                                                    {
                                                        FuelCode = f.Value.InternalCode,
                                                        NozzleUp = p.Value.UpNozzle == f.Value.ID
                                                    }
                                            )
                                        }).ToArray()
                                    };
                                return json(obj);
                                }
                            }

                            #endregion

                        #region OnDebitPump

                            case "OnDebitPump":
                            {
                                var fuel = Driver.Fuels.First(t => t.Value.ID == op.Fuel.Value);
                                var shift = XmlPumpClient.ReadAndUpdateCurrentShift();
                                
                                //TODO: Передача транзакции в систему управления
                                lock (Driver.TransCounterLocker)
                                {
                                    if (shift != null && Driver.TransCounter < shift.DocNum)
                                    {
                                        Driver.TransCounter = shift.DocNum;
                                    }

                                    Driver.TransCounter += 2;
                                    shift.DocNum = Driver.TransCounter - 1;
                                }
                                ++shift.ShiftDocNum;
                                XmlPumpClient.WriteOrReplaceToFile(1, shift.ShiftDocNum.ToString());
                                XmlPumpClient.WriteOrReplaceToFile(2, shift.DocNum.ToString());

                                if (!Transes.ContainsKey(op.TransactionID))
                                {
                                    //Добавляем новую транзакцию в хранилище
                                    lock (TransesLocker)
                                        Transes.Add(op.TransactionID, op);

                                    object item = null;
                                        Driver.log.Write("Bzer: Захват ТРК: " + op.Pump.Value + "\r\n", 0, true);

                                        lock (XmlPumpClient.StatusesLocker)
                                            if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                                MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                            {
                                                XmlPumpClient.InitData(Driver.terminal);
                                            }

                                    Driver.PumpInfo pump;
                                    lock (Driver.PumpsLocker)
                                    {
                                        pump = Driver.Pumps[op.Pump.Value];
                                        pump.Blocked = true;
                                        pump.BlockInitTime = DateTime.Now;
                                        Driver.Pumps[op.Pump.Value] = pump;
                                    }
                                    XmlPumpClient.Init(Driver.terminal, op.Pump.Value, op.Pump.Value,
                                            XmlPumpClient.WaitAnswerTimeout, 1);

                                    Driver.log.Write(
$@"Установка дозы на ТРК: {op.Pump.Value}, 
сгенерирован TransID: {shift.DocNum}
RRN: {op.TransactionID}\r\n", 1, true);

                                    //var prePaid = Order.Price*Order.Quantity;
                                    var discount = 0;//100;//(Order.BasePrice - Order.Price)*Order.Quantity;
                                    var fuel1 = Driver.Fuels.First(t => t.Value.ID == op.Fuel.Value);
                                    int allowed = 0;
                                    lock (Driver.PumpsLocker)
                                        foreach (var pumpFuel in Driver.Pumps[op.Pump.Value].Fuels)
                                        {
                                            allowed += 1 << (pumpFuel.Value.ID - 1);
                                        }

                                    Driver.log.Write(
$@"WaitCollectThread:SaleDataSale:\r\n
terminal: {Driver.terminal}\r\n
PumpNo: {op.Pump}\r\n
allowed: {allowed}\r\n
Amount: {op.Amount}\r\n
OverAmount: {op.Amount}\r\n
discount: {discount}\r\n
Quantity: {op.Amount / fuel.Value.Price}\r\n
OverQuantity: {op.Amount / fuel.Value.Price}\r\n
PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n
OrderRRN: {op.TransactionID.PadLeft(20, '0')}\r\n
ProductCode: {op.Fuel}\r\n
Key: {fuel.Key}\r\n
fuelPrice: {(int)(fuel.Value.Price * 100)}\r\n", 2, true
                                        );


                                    XmlPumpClient.SaleDataSale(Driver.terminal, op.Pump.Value, allowed,
                                                op.Amount.Value, op.Amount.Value, discount,
                                                op.Amount.Value / fuel.Value.Price, op.Amount.Value / fuel.Value.Price, PAYMENT_TYPE.Cash,
                                                op.TransactionID.PadLeft(20, '0'), op.Fuel.Value, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);

                                    item = null;
                                        Driver.log.Write("Bzer: Освобождение ТРК: " + op.Pump + "\r\n", 0, true);

                                        lock (XmlPumpClient.StatusesLocker)
                                            if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                            MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                            {
                                                XmlPumpClient.InitData(Driver.terminal);
                                            }

                                    lock (Driver.PumpsLocker)
                                    {
                                        pump = Driver.Pumps[op.Pump.Value];
                                        pump.Blocked = false;
                                        pump.BlockInitTime = null;
                                        Driver.Pumps[op.Pump.Value] = pump;
                                    }
                                    int pmp = op.Pump.Value;
                                        XmlPumpClient.Init(Driver.terminal, pmp, -pmp, XmlPumpClient.WaitAnswerTimeout, 1);

                                    var res2 = XmlPumpClient.Statuses;
                                    var res3 = XmlPumpClient.Fillings;

                                    XmlPumpClient.FiscalEventReceipt(Driver.terminal, op.Pump.Value/*order.PumpNo*/,
                                        shift.ShiftDocNum, shift.DocNum, shift.Number,
                                        op.Amount.Value/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, op.TransactionID.PadLeft(20, '0') /*order.OrderRRN*/, 1);

Driver.log.Write(
$@"Bzer: чек:\r\n
terminal: {Driver.terminal}\r\n
Pump: {op.Pump.Value}\r\n
GetShiftDocNum: {shift.ShiftDocNum}\r\n
GetDocNum: {shift.DocNum}\r\n
GetShiftNum: {shift.Number}\r\n
OverAmount: {op.Amount.Value}\r\n
Refund: {0}\r\n
PAYMENT_TYPE: {PAYMENT_TYPE.Cash.ToString()}\r\n
OrderRRN: {op.TransactionID.PadLeft(20, '0')/*order.OrderRRN*/}\r\n", 2, true);

                                    ++shift.DocNum;
                                    ++shift.ShiftDocNum;
                                    XmlPumpClient.WriteOrReplaceToFile(1, shift.ShiftDocNum.ToString());
                                    XmlPumpClient.WriteOrReplaceToFile(2, shift.DocNum.ToString());

                                    Driver.log.Write(
$@"Authorize:\r\n
терминал: {Driver.terminal}\r\n
колонка: {op.Pump.Value}\r\n
TransCounter: {shift.DocNum}\r\n
дост. пистолеты: {allowed}\r\n
Nazzle: {op.Fuel.Value}\r\n
RNN: {op.TransactionID.PadLeft(20, '0')}\r\n
Limit: {(int)((op.Amount.Value*100)/ fuel.Value.Price)}\r\n
Unit: {DELIVERY_UNIT.Volume.ToString()}\r\n
WaitAnswerTimeout: {XmlPumpClient.WaitAnswerTimeout}"
        , 2, true);

                                    //TODO Проба со скидками!!!!
                                    string errMsg = "";
                                        if (!XmlPumpClient.Authorize(Driver.terminal, op.Pump.Value, shift.DocNum,
                                            //TODO не передается
                                            allowed, op.Fuel.Value, op.TransactionID.PadLeft(20, '0'), (int)((op.Amount.Value*100)/ fuel.Value.Price), DELIVERY_UNIT.Volume,/*(int) (op.Amount.Value*100), DELIVERY_UNIT.Money,*/
                                            XmlPumpClient.WaitAnswerTimeout, out errMsg))
                                        {
                                            Driver.log.Write($"SetDoseCallback:Authorize: {errMsg}\r\n", 2, true);                                            return json(new { Result = "Error" });
                                        }

                                    Driver.log.Write(
$@"Presale:\r\n
терминал: {Driver.terminal}\r\n
колонка: {op.Pump.Value}\r\n
дост. пистолеты: {allowed}\r\n
сумма руб: {op.Amount.Value}\r\n
скидка: {discount}\r\n
кол-во литры: {(int)(op.Amount.Value / fuel.Value.Price)}\r\n
тип оплаты: {XmlPumpClient.PaymentCodeToType(1).ToString()}\r\n
рнн: {op.TransactionID.PadLeft(20, '0')}\r\n
продукт код: {op.Fuel.Value}\r\n
продукт: {fuel.Key}\r\n
продукт цена коп: {(int)(fuel.Value.Price * 100)}\r\n
WaitAnswerTimeout: {XmlPumpClient.WaitAnswerTimeout}"

        , 2, true);
                                    //TODO Проба со скидками!!!!
                                    if (!XmlPumpClient.Presale(Driver.terminal, op.Pump.Value, allowed, op.Amount.Value,
                                            //TODO не передается
                                            discount, (int)(op.Amount.Value / fuel.Value.Price), XmlPumpClient.PaymentCodeToType(1),
                                            op.TransactionID.PadLeft(20, '0'), op.Fuel.Value, fuel.Key,
                                            //TODO не передается
                                            (int)(fuel.Value.Price * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                                        {
                                            Driver.log.Write("Bzer: SetDoseCallback:Presale: нет о твета на Presale\r\n", 0, true);
                                            return json(new { Result = "Error" });
                                        }


                                        Driver.log.Write("Bzer: Hалив разрешен\r\n", 0, true);

                                        op.Shift = shift;

                                        //Поток ожидания окончания налива от АСУ
                                        Thread myThread2 = new Thread(Driver.WaitCollectBenzuber) { IsBackground = true };
                                        myThread2.Start(op.TransactionID);

                                        //Передаем "ОК" если заказ успешно передан в систему управления
                                        return json(new { Result = "OK" });
                                }
                                else
                                    return json(new { Result = "Error" });
                            }

                            #endregion

                        #region FillingOverCommit

                            case "FillingOverCommit":
                            {
                                //var r1 = Pump;
                                var r2 = Driver.TransMemory;

                                //Подключаемся к хранилищу транзакций и читаем нужную транзакцию
                                if (TransOvers.ContainsKey(op.TransactionID) && Transes.ContainsKey(op.TransactionID))
                                {
                                    //SYSTEMTIME time;
                                    //VariantTimeToSystemTime(_DateTime, &time);
                                    //TODO не передается
                                    //var summ = (DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;
                                    var op_in_storage = Transes[op.TransactionID];
                                    var over_op_in_storage = TransOvers[op.TransactionID];
                                    var fuel = Driver.Fuels.First(t => t.Value.ID == op_in_storage.Fuel.Value);
                                    decimal overSumm;
                                    decimal overQuantity;
                                    bool refund = false;
                                    if (op_in_storage.Amount.Value > over_op_in_storage.Amount.Value)
                                    {
                                        // Возврат
                                        refund = true;
                                        overSumm = - op_in_storage.Amount.Value + over_op_in_storage.Amount.Value;//(DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;
                                        overQuantity = op_in_storage.Amount.Value/fuel.Value.Price -
                                                       over_op_in_storage.Amount.Value/fuel.Value.Price;
                                    }
                                    else
                                    {
                                        overSumm = over_op_in_storage.Amount.Value;//(DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;
                                        overQuantity = op_in_storage.Amount.Value/fuel.Value.Price;
                                    }
Driver.log.Write("Bzer: Сохранение документа, TransID: " + op.TransactionID
+ "\r\nНомер документа:    " +op_in_storage.Shift.DocNum
+ "\r\nТип документа:      " +op_in_storage.Operation
+ "\r\nСумма:              " + overSumm
+ "\r\nКоличество:         " + overQuantity
+ "\r\nНомер продукта:     " +op_in_storage.Fuel
+ "\r\nID PumpNo:          " +op_in_storage.Pump
+ "\r\nID OrderRRN:        " + op.TransactionID.PadLeft(20, '0')
+ "\r\n", 2, true);

                                    //++transCounter;

                                    //if (DocType != 0 || PumpNo <= 0)
                                    //    return 1;

                                    var discount = 0;//100; //(order.BasePrice - order.Price) * order.Quantity;
                                        int allowed = 0;
                                    lock (Driver.PumpsLocker)
                                        foreach (var pumpFuel in Driver.Pumps[op_in_storage.Pump.Value].Fuels)
                                        {
                                            allowed += 1 << (pumpFuel.Value.ID - 1);
                                        }
                                    if (refund)
                                    {
Driver.log.Write(
$@"Bzer: WaitCollectThread:SaleDataSale:\r\n
terminal: {Driver.terminal}\r\n
PumpNo: {op_in_storage.Pump}\r\n
allowed: {allowed}\r\n
Amount: {op_in_storage.Amount}\r\n
OverAmount: {overSumm}\r\n
discount: {discount}\r\n
Quantity: {op_in_storage.Amount.Value/fuel.Value.Price}\r\n
OverQuantity: {overQuantity}\r\n
PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n
OrderRRN: {op.TransactionID.PadLeft(20, '0')}\r\n
ProductCode: {op_in_storage.Fuel.Value}\r\n
Key: {fuel.Key}\r\n
fuelPrice: {(int) (fuel.Value.Price*100)}\r\n", 2, true);

                                        //TODO Проба со скидками!!!!

                                        XmlPumpClient.SaleDataSale(Driver.terminal, op_in_storage.Pump.Value,
                                            allowed,
                                            op_in_storage.Amount.Value, overSumm, discount,
                                            op_in_storage.Amount.Value/fuel.Value.Price, overQuantity,
                                            PAYMENT_TYPE.Cash,
                                            op.TransactionID.PadLeft(20, '0'), op_in_storage.Fuel.Value,
                                            fuel.Key, (int) (fuel.Value.Price*100), "", 1);
                                    } //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
                                    //    order.Amount, order.OverAmount, discount,
                                        //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                                        //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                                        //Driver.log.Write(
                                        //    "WaitCollectThread:SaleDataSale:\r\n" +
                                        //    $"terminal: {Driver.terminal}\r\n" +
                                        //    $"PumpNo: {PumpNo}\r\n" +
                                        //    $"allowed: {allowed}\r\n" +
                                        //    $"Amount: {PreSum}\r\n" +
                                        //    $"OverAmount: {summ}\r\n" +
                                        //    $"discount: {discount}\r\n" +
                                        //    $"Quantity: {PreQuantity}\r\n" +
                                        //    $"OverQuantity: {Quantity}\r\n" +
                                        //    $"PAYMENT_TYPE: {PAYMENT_TYPE.Cash}\r\n" +
                                        //    $"OrderRRN: {OrderRRN.PadLeft(20, '0')}\r\n" +
                                        //    $"ProductCode: {BP_Product}\r\n" +
                                        //    $"Key: {fuel.Key}\r\n" +
                                        //    $"fuelPrice: {(int)(Price/*fuel.Value.Price*/ * 100)}\r\n", true
                                        //    );

                                        //XmlPumpClient.FiscalEventReceipt(Driver.terminal, order.PumpNo,
                                        //    GetShiftDocNum(), GetDocNum(), GetShiftNum(),
                                        //    (endMessage?.Money ?? 0) / 100m, 0, PAYMENT_TYPE.Cash, order.OrderRRN, 1);
                                        //Driver.log.Write.Write($"чек:\r\n" +
                                        //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                                        //    $"GetDocNum: {GetDocNum()}\r\n" +
                                        //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                                        //    $"OverAmount: {(endMessage?.Money ?? 0)/100.0}\r\n" +
                                        //    $"OrderRRN: {order.OrderRRN}\r\n");




                                        object item = null;
                                            Driver.log.Write("Bzer: Освобождение ТРК: " + op_in_storage.Pump.Value + "\r\n", 0, true);

                                        lock (XmlPumpClient.StatusesLocker)
                                            if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                                MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                                {
                                                    XmlPumpClient.InitData(Driver.terminal);
                                                }


                                    Driver.PumpInfo pump;
                                    lock (Driver.PumpsLocker)
                                    {
                                        pump = Driver.Pumps[op_in_storage.Pump.Value];
                                        pump.Blocked = false;
                                        pump.BlockInitTime = null;
                                        Driver.Pumps[op_in_storage.Pump.Value] = pump;
                                    }
                                    int pmp = op_in_storage.Pump.Value;
                                            XmlPumpClient.Init(Driver.terminal, pmp, -pmp, XmlPumpClient.WaitAnswerTimeout, 1);
                                            //Driver.Pumps[Pump] = pump;

                                            //label1.Text += "Освобождение ТРК: " + Pump + "\r\n";
                                    //Driver.log.Write("Bzer: изм. статуса\r\n", 0, true);

                                        //var res = XmlPumpClient.answers;
                                        var res2 = XmlPumpClient.Statuses;
                                        var res3 = XmlPumpClient.Fillings;

                                        XmlPumpClient.ClearAllTransactionAnswers(op_in_storage.Pump.Value, op.TransactionID.PadLeft(20, '0'));
                                    if (refund)
                                    {
                                        var last_trans_in_shift_src = XmlPumpClient.ReadFromFile(1);
                                        long last_trans_in_shift = 0;
                                        if (!long.TryParse(last_trans_in_shift_src, out last_trans_in_shift))
                                        {
                                            Driver.log.Write("ERROR при попытке чтения номера документа в смене из файла.", 0, true);
                                            last_trans_in_shift = 0;
                                        }
                                        var old_transCounter = op_in_storage.Shift.DocNum;
                                        lock (Driver.TransCounterLocker)
                                        {
                                            if (Driver.TransCounter < op_in_storage.Shift.DocNum)
                                            {
                                                Driver.TransCounter = op_in_storage.Shift.DocNum;
                                            }

                                            ++Driver.TransCounter;
                                            op_in_storage.Shift.DocNum = Driver.TransCounter;
                                        }

                                        XmlPumpClient.WriteOrReplaceToFile(1, (++last_trans_in_shift).ToString());
                                        XmlPumpClient.WriteOrReplaceToFile(2, op_in_storage.Shift.DocNum.ToString());

                                        XmlPumpClient.FiscalEventReceipt(Driver.terminal, op_in_storage.Pump.Value/*order.PumpNo*/,
                                            last_trans_in_shift, op_in_storage.Shift.DocNum, op_in_storage.Shift.Number,
                                           overSumm/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, op.TransactionID.PadLeft(20, '0') /*order.OrderRRN*/, 1);
                                        Driver.log.Write(
$@"Bzer: чек:\r\n
terminal: {Driver.terminal}\r\n
Pump: {op_in_storage.Pump}\r\n
GetShiftDocNum: {last_trans_in_shift}\r\n
GetDocNum: {op_in_storage.Shift.DocNum}\r\n
GetShiftNum: {op_in_storage.Shift}\r\n
OverAmount: {overSumm}\r\n
Refund: {0}\r\n
PAYMENT_TYPE: {PAYMENT_TYPE.Cash.ToString()}\r\n
OrderRRN: {op.TransactionID.PadLeft(20, '0') /*order.OrderRRN*/}\r\n", 2, true);
                                    }
                                    //Thread.Sleep(30000);
                                    //Удаляем удачно завершенную транзакцию
                                        lock (TransesLocker)
                                            Transes.Remove(op.TransactionID);
                                        lock (TransOversLocker)
                                            TransOvers.Remove(op.TransactionID);

                                    return json(new { Result = "OK" });
                                    }
                                    else
                                    return json(new { Result = "Error" });
                            }

                            #endregion

                        #region CancelTransaction

                            case "CancelTransaction":
                            {
                                //TODO: Отмена транзакций
                                var id = op.TransactionID;
                                //Пока обработка отмены лежит на сервере, или невозможна
                                return json(new { Result = "Error" });
                            }

                            #endregion

                        #region LoadUpdate

                        case "LoadUpdate":
                        {
                            //TODO: Загрузка обновлений с сервера
                            //Путь до обновления указывается в переменной op.UpdatePath
                            return json(new { Result = "OK" });
                        }

                    #endregion

                        default:
                            return json(new { Result = "Unsupported" });
                    }
                }
                catch (Exception ex)
                {
                    Driver.log.Write("ERROR " + ex, 0, true);
                    return json(new { Result = "Error" });
                }
            }
            public static object TransesLocker = new object();
            /// <summary>
            /// Коллекция установленных транзакций для хранения параметров транзакции
            /// </summary>
            public static Dictionary<string, Request> Transes = new Dictionary<string, Request>();
            public static object TransOversLocker = new object();
            /// <summary>
            /// Коллекция завершенных транзакций
            /// </summary>
            public static Dictionary<string, Request> TransOvers = new Dictionary<string, Request>();

            /// <summary>
            /// Десериализует объект в JSON
            /// </summary>
            /// <param name="obj">Строка JSON</param>
            /// <returns>Объект</returns>
            private static T json_deser<T>(string obj)
            {
                try
                {
                    return new JavaScriptSerializer().Deserialize<T>(obj);
                }
                catch
                {

                }
                return default(T);
            }

            /// <summary>
            /// Сериализует объект в JSON
            /// </summary>
            /// <param name="obj">Объект для сериализации</param>
            /// <returns>Строка JSON</returns>
            private static string json(object obj)
            {
                return new JavaScriptSerializer().Serialize(obj);
            }

            /// <summary>
            /// Описание входного запроса от сервера
            /// </summary>
            public class Request
            {
                /// <summary>
                /// Намиенование операции
                /// </summary>
                public string Operation { get; set; }
                /// <summary>
                /// Номер транзакции
                /// </summary>
                public string TransactionID { get; set; }
                /// <summary>
                /// Номер транзакции
                /// </summary>
                public Shift Shift { get; set; }
                /// <summary>
                /// Номер ТРК
                /// </summary>
                public int? Pump { get; set; }
                /// <summary>
                /// Код вида топлива
                /// </summary>
                public int? Fuel { get; set; }
                /// <summary>
                /// Сумма заказа
                /// </summary>
                public decimal? Amount { get; set; }
                /// <summary>
                /// Путь для загрузки обновления
                /// </summary>
                public string UpdatePath { get; set; }
            }

            /// <summary>
            /// Информация о АЗС
            /// </summary>
            public class StationInformaton
            {
                /// <summary>
                /// Код АЗС
                /// </summary>
                public int StationID { get; set; }

                /// <summary>
                /// Список видов топлива
                /// </summary>
                public List<FuelInfo> Fuels { get; set; }

                /// <summary>
                /// Описание видов топлива
                /// </summary>
                public class FuelInfo
                {
                    /// <summary>
                    /// Код
                    /// </summary>
                    public int Code { get; set; }
                    /// <summary>
                    /// Наименование
                    /// </summary>
                    public string Name { get; set; }
                    /// <summary>
                    /// Цена
                    /// </summary>
                    public decimal Price { get; set; }
                }

                /// <summary>
                /// Список ТРК
                /// </summary>
                public List<PumpInfo> Pumps { get; set; }

                /// <summary>
                /// Описание ТРК
                /// </summary>
                public class PumpInfo
                {
                    /// <summary>
                    /// Номер ТРК
                    /// </summary>
                    public int Number { get; set; }
                    /// <summary>
                    /// Список топливораздаточных рукавов на ТРК
                    /// </summary>
                    public List<NozzleInfo> Nozzles { get; set; }
                    /// <summary>
                    /// Описание топливораздаточного рукава на ТРК
                    /// </summary>
                    public class NozzleInfo
                    {
                        /// <summary>
                        /// Код вида топлива
                        /// </summary>
                        public int FuelCode { get; set; }
                        /// <summary>
                        /// Состояние рукава. 
                        /// Если false - в колонке, если true - снят (в баке автомобия). 
                        /// </summary>
                        public bool NozzleUp { get; set; }
                    }
                    /// <summary>
                    /// Доступность ТРК для онлайн заказа
                    /// </summary>
                    public bool IsAvailable { get; set; }
                    /// <summary>
                    /// Номер установленного на ТРК онлайн заказа
                    /// </summary>
                    public string TransactionID { get; set; }
                    /// <summary>
                    /// Минимальная допустимая доза, для установки заказа (в литрах)
                    /// </summary>
                    public int MinOrder { get; set; }
                }
            }
        }


        /// <summary>
        /// Клиент TCP сервера
        /// </summary>
        public class TcpExcangeServerClient : TcpExcangeClientBase
        {

            public X509Certificate2 Certificate { get; set; }

            public TcpExcangeServerClient(TcpClient Client, Stream Stream, string ClientID, X509Certificate2 Certificate) : base(null)
            {
                base.Client = Client;
                base.ClientID = ClientID;
                base.Stream = Stream;
                this.Certificate = Certificate;
            }

            /// <summary>
            /// Отправка сообщения клиенту с ожиданием ответного сообщения
            /// </summary>
            /// <param name="Message"></param>
            /// <returns></returns>
            public string SendReqest(string Message)
            {
                if (!this.Online)
                {
                    Console.WriteLine("Клиент отключен");
                    return null;
                }
                try
                {
                    lock (this)
                    {
                        var prefix = DateTime.Now.Ticks.ToString("X").PadLeft(16, '0');
                        if (prefix.Length > 16)
                            prefix = prefix.Substring(prefix.Length - 16, 16);
                        prefix = "TS: " + prefix;

                        sendMessage(prefix + Message);
                        var responce = waitMessage();
                        if (responce == "ERROR" || responce.Substring(0, 20) != prefix)
                            throw new Exception("Ошибка при выполнении запроса");

                        return responce;
                    }
                }
                catch (Exception ex)
                {
                    Driver.log.Write(ex.ToString(), 0, true);
                    disponse_client();
                }
                return null;
            }

            /// <summary>
            /// Деинициализация клиента
            /// </summary>
            private void disponse_client()
            {
                try
                {
                    Stream.Close();
                }
                catch { }
                try
                {
                    Client.Close();
                }
                catch { }
                try
                {
                    Client = null;
                }
                catch { }
            }
            public override Stream GetNetStream(TcpClient Client)
            {
                return _GetNetStream(Client, Certificate);
            }

            public static Stream _GetNetStream(TcpClient Client, X509Certificate2 Certificate)
            {
                SslStream sslStream = new SslStream(Client.GetStream(), false);
                // Authenticate the server but don't require the client to authenticate.
                try
                {
                    sslStream.AuthenticateAsServer(Certificate, false, SslProtocols.Tls12, false /*true*/);

                    // Set timeouts for the read and write to 5 seconds.
                    sslStream.ReadTimeout = 30000;
                    sslStream.WriteTimeout = 30000;

                    return sslStream as Stream;
                }
                catch (AuthenticationException e)
                {
                    Console.WriteLine("Exception: {0}", e.ToString());
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.ToString());
                    }
                    Console.WriteLine("Authentication failed - closing the connection.");
                    sslStream.Close();
                    Client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.ToString());
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.ToString());
                    }
                    Console.WriteLine("Authentication failed - closing the connection.");
                    sslStream.Close();
                    Client.Close();
                }
                finally
                {
                    // The client stream will be closed with the sslStream
                    // because we specified this behavior when creating
                    // the sslStream.

                }
                return null;
            }
        }

        /// <summary>
        /// Клиент TCP сервера
        /// </summary>
        public class TcpExcangeClientBase
        {
            /// <summary>
            /// Клиент TCP сервера
            /// </summary>
            /// <param name="ClientID">Идентификатор клиента</param>
            public TcpExcangeClientBase(string ClientID)
            {
                this.ClientID = ClientID;
            }

            /// <summary>
            /// Кодировка для передачи сообщений
            /// </summary>
            public Encoding Encoding { get; set; } = Encoding.UTF8;
            /// <summary>
            /// TCP Клиент
            /// </summary>
            public TcpClient Client { get; set; }
            /// <summary>
            /// Идентификатор клиента
            /// </summary>
            public string ClientID { get; set; }
            /// <summary>
            /// Идентификатор клиента
            /// </summary>
            public Stream Stream { get; set; }
            /// <summary>
            /// Аппаратный идентификатор клиента
            /// </summary>
            public string HW_ID { get; set; }

            /// <summary>
            /// Состояние подключения клиента к серверу
            /// </summary>
            public bool Online { get { return Client?.Connected ?? false; } }

            public virtual Stream GetNetStream(TcpClient Client) => Client.GetStream();

            protected void sendMessage(string Data)
            {
                sendMessage(Data, this.Stream, this.Encoding);
            }

            public static void sendMessage(string Data, Stream stream, Encoding Encoding)
            {
                //   if (stream == null) stream = Stream;
                if (string.IsNullOrWhiteSpace(Data))
                {
                    Driver.log.Write("Bzer: Не указаны данные для отправки", 0, true);
                    throw new Exception("Не указаны данные для отправки");
                }
                else Driver.log.Write($"Отправка сообщения: {Data}", 3, true);

                var str_arr = Encoding.GetBytes(Data);
                //var Stream = getNetStream(Client);

                stream.Write(str_arr, 0, str_arr.Length);
                stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
                stream.Flush();

                Driver.log.Write("Bzer: Данные успешно отправлены", 3, true);
            }
            // Максимальная длина пакета данных
            public const int max_data_len = 2048;

            protected string waitMessage(int Timeout = 30000) => waitMessage(this.Stream, this.Encoding, Timeout);

            public static string waitMessage(Stream stream, Encoding Encoding, int Timeout = 30000)
            {
                //   if (stream == null) stream = Stream;
                byte[] data = new byte[max_data_len];
                int len = 0;
                int ff_count = 0;
                int b = -1;
                var dt = DateTime.Now.AddMilliseconds(Timeout);
                // var stream = getNetStream(Client);
                while (dt > DateTime.Now)
                {
                    //Driver.log.Write($"In while: {1}");
                    try
                    {
                        if ((b = stream.ReadByte()) >= 0)
                        {
                            //Driver.log.Write($"In if1: {b}");
                            if ((byte) b == 0xFF)
                                ff_count++;
                            else
                            {
                                ff_count = 0;
                                data[len++] = (byte) b;
                            }
                            //Driver.log.Write($"In if2: {ff_count}");

                            if (ff_count >= 4)
                            {
                                var msg = Encoding.GetString(data, 0, len);
                                Driver.log.Write($"Получено сообщение: {msg}", 3, true);
                                return msg;
                            }
                        }
                        else
                        {
                            //Driver.log.Write($"No data", 0, true);
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        Driver.log.Write($"Benzuber waitMessage: {ex}", 0, true);
                    }
                }

                return null;
            }
        }

        public class TcpExcangeClient : TcpExcangeClientBase
        {

            public static bool ValidateServerCertificate(
                                                          object sender,
                                                          X509Certificate certificate,
                                                          X509Chain chain,
                                                          SslPolicyErrors sslPolicyErrors)
            {

                Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
                return true;
            }

            public override Stream GetNetStream(TcpClient Client)
            {
                //return Client.GetStream();
                SslStream sslStream = new SslStream(Client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate));
                Driver.log.Write($"SslStream {1}", 2, true);

                // Authenticate the server but don't require the client to authenticate.
                try
                {

                    sslStream.AuthenticateAsClient(this.Address, null, SslProtocols.Tls, true);
                    Driver.log.Write($"GetNetStream AuthenticateAsClient {SslProtocols.Tls.ToString()}", 2, true);


                    // Set timeouts for the read and write to 5 seconds.
                    sslStream.ReadTimeout = 30000;
                    sslStream.WriteTimeout = 30000;

                    return sslStream as Stream;
                }
                catch (AuthenticationException e)
                {
                    Driver.log.Write($"Error {e}", 0, true);
                    //Console.WriteLine("Exception: {0}", e.ToString());
                    //if (e.InnerException != null)
                    //{
                    //    Console.WriteLine("Inner exception: {0}", e.InnerException.ToString());
                    //}
                    Console.WriteLine("Authentication failed - closing the connection.");
                    sslStream.Close();
                    Client.Close();
                }
                return null;
            }

            public TcpExcangeClient(string ClientID, string HW_ID) : base(ClientID)
            {
                base.HW_ID = HW_ID;
            }
            Task background;
            public string Address { get; private set; }
            public int Port { get; private set; }
            public bool Start(string Address, int Port = 5051)
            {
                if (background != null)
                {
                    Driver.log.Write("Bzer: Error background != null", 0, true);
                    return false;
                }
                try
                {
                    this.Address = Address;
                    this.Port = Port;
                    Driver.log.Write($"Address {Address} Port {Port}", 2, true);

                    background = new Task(background_connect);
                    background.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    Driver.log.Write("Bzer: Error " + ex, 0, true);
                }
                return false;
            }
            public void Disconnect() => background = null;

            public delegate string HandleRequestDelegate(string Request);
            public HandleRequestDelegate HandleRequest { get; set; }

            protected virtual string handleRequest(string Request)
            {
                try
                {
                    var reqId = Request?.Substring(0, 20);
                    return reqId + (HandleRequest?.Invoke(Request?.Remove(0, 20)) ?? "Unsupported");
                }
                catch (Exception ex)
                {
                    Driver.log.Write(ex.ToString(), 0, true);
                    return "ERROR";
                }
            }

            private void background_connect()
            {
                while (background != null)
                {
                    try
                    {
                        Client = new TcpClient();
                        Client.Connect(Address, Port);
                        Driver.log.Write($"Connect", 2, true);
                        base.Stream = GetNetStream(Client);
                        Driver.log.Write($"GetNetStream", 2, true);
                        if (waitMessage() == "GET_ID")
                        {
                            sendMessage(ClientID);
                        }
                        else
                        {
                            Driver.log.Write("Bzer: Некорректный запрос от сервера. Ожидался запрос GET_ID.", 0, true);
                            continue;
                        }
                        if (waitMessage() == "GET_HW")
                        {
                            sendMessage(HW_ID);
                        }
                        else
                        {
                            Driver.log.Write("Bzer: Некорректный запрос от сервера. Ожидался запрос HW_ID.", 0, true);
                            continue;
                        }
                        while (background != null && Client != null)
                        {
                            sendMessage(handleRequest(waitMessage()));
                        }

                    }
                    catch (Exception ex)
                    {
                        Driver.log.Write($"Error {ex}", 0, true);
                        System.Threading.Thread.Sleep(10000);
                    }
                    finally
                    {
                        try
                        {
                            Stream.Close();
                        }
                        catch { }
                        try
                        {
                            Client.Close();
                        }
                        catch { }
                        try
                        {
                            Client = null;
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
