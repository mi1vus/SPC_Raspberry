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

            public static Logger logBenzuber = new Logger("Benzuber");

            public static void Log(string msg, bool console = true)
            {
                logBenzuber.Write(msg, 0, console);
            }


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
                    Log("Принято входящее подключения");
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

                        Log($"Получены идентификаторы клиента. ClientID: '{ClientID}', HW_ID:{HW_ID}");
                        if (!(ValidateClient?.Invoke(ClientID, HW_ID) ?? false))
                            return false;

                        if (string.IsNullOrWhiteSpace(ClientID))
                            return false;

                        lock (clients)
                        {
                            if (!clients.ContainsKey(ClientID))
                            {
                                clients.Add(ClientID, new TcpExcangeServerClient(Client, _Stream, ClientID, Certificate));
                                Log($"Клиент успешно зарегистрирован: " + ClientID);
                                ClientRegistred?.Invoke(this, new ClientInfoEventArgs() { ClientID = ClientID });
                            }
                            else
                            {
                                Log($"Клиент переподключился: " + ClientID);
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
                        Log(ex.Message);
                    }
                    finally { }



                }
                catch (Exception ex) { Log(ex.Message); }
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
                    Log("Не удалось получить HW ID");
                    return;
                }
                else
                {
                    hw_id = ComputeMD5Checksum(hw[0].Name + hw[0].Serial);
                    Log($"Вычисляем HW ID: {hw_id} для: {hw[0].Name}:{hw[0].Serial}", true);
                }
                var str = string.Format("net.tcp://" + config["server"] + ":" + config["exchangeport"]);
                Log(str);
                var location = Assembly.GetExecutingAssembly().Location;

                //ClientID должен задаваться в настройках
                //HW_ID должен генерироваться библиотекой на основании серийных номеров оборудования  
                client = new TcpExcangeClient("41065", hw_id);

                //Указываем обработчик для запросов от сервера  
                client.HandleRequest = new TcpExcangeClient.HandleRequestDelegate(handler);

                try
                {

                    try
                    {
                        Driver.Params =
                            Serialization.Deserialize<Serialization.SerializableDictionary<string, string>>(
                                Path.Combine("config", "SmartPumpControlParams.xml"));
                    }
                    catch
                    {
                    }
                    if (Driver.Params == null)
                        Driver.Params = new Serialization.SerializableDictionary<string, string>();
                    //Log("ctx " + (((ctx == null) || (ctx == IntPtr.Zero)) ? "is null" : "success set"));
                    //Driver.ctx = ctx;
                    if (!Driver.isInit)
                    {
                        try
                        {
                            Driver.isInit = true;
                            int pt;
                            if (!Driver.Params.ContainsKey("port") || !int.TryParse(Driver.Params["port"], out pt))
                                pt = Driver.port;
                            int t;
                            if (Driver.Params.ContainsKey("SendTimeout") && int.TryParse(Driver.Params["SendTimeout"], out t))
                                XmlPumpClient.SendTimeout = t;
                            else
                                XmlPumpClient.SendTimeout = 1500;

                            if (Driver.Params.ContainsKey("WaitAnswerTimeout") &&
                                int.TryParse(Driver.Params["WaitAnswerTimeout"], out t))
                                XmlPumpClient.WaitAnswerTimeout = t;
                            else
                                XmlPumpClient.WaitAnswerTimeout = 3500;

                            //RemotePump_Driver.RemotePump.StartServer();
                            XmlPumpClient.StartSocket(Driver.hostB2, pt, Driver.terminal);
                            XmlPumpClient.InitData(Driver.terminal);
                            Log(
                                $"Open port: {pt} SendTimeout: {XmlPumpClient.SendTimeout} WaitAnswerTimeout: {XmlPumpClient.WaitAnswerTimeout}");
                        }
                        catch
                        {
                            Driver.isInit = false;
                        }


                        //else
                        //{
                        //    Log(Driver.Description() + "\r\n");
                        //}

                        //int OS = Environment.OSVersion.Version.Major;
                        //Log("OS Ver - " + Environment.OSVersion.Version + "\r\n");
                        //if (OS > 4)
                        Driver.FuelPrices();
                        //else
                        //    Driver.FuelPrices();

                        //if (OS > 4)
                        Driver.PumpFuels();
                        //else
                        //    Driver.PumpFuels("1=95.92.80;2=95.92.80;3=95.92;4=95.92");

                        //Driver.StartBenzuber();

                        object res;
                        if (XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1, MESSAGE_TYPES.OnDataInit),
                            out res))
                        {
                            Log(
                                Driver.Description() + " успешно открыта!\r\n");
                        }
                        else
                        {
                            Log(
                                Driver.Description() + " нет ответа от АСУ!\r\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Ошибка инициализации библиотеки {Driver.Description()}:{ex}\r\n");
                }

                //Запускаем клиента
                client.Start("testazsapi.benzuber.ru");
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
                            if (transOver.Count == 0)
                                return json(new { Result = "OK" });
                            else
                            {
                                //Если есть - передаем по одной. Обязательно начиная с последней.
                                //На тот случай, если при передаче одной из транзакций происходит ошибка, 
                                //чтобы эта ошбка не мешала передавать новые транзакции. 
                                //Заранее блокируем доступ к хранилищу транзакций.
                                lock (transOver)
                                {
                                    if (transOver.Count == 0)
                                        return json(new { Result = "OK" });
                                    var last = transOver.Last();
                                    return json(new { Result = "OK", RequestOperation = "FillingOver", Amount = last.Value.Amount, TransactionID = last.Value.TransactionID });
                                }
                            }
                        }

                        #endregion

                    #region GetStationInfo

                        //Запрос оперативной информации о АЗС.
                        case "GetStationInfo":
                        {
                            return json(
                                new
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
                                    Pumps = Driver.Pumps.Select(p=>new
                                    {
                                        Number = p.Value.Pump,
                                        IsAvailable = !p.Value.Blocked,
                                        MinOrder = 2,
                                        Nozzles = p.Value.Fuels.Select(f=> 
                                                new
                                                {
                                                    FuelCode = f.Value.InternalCode,
                                                    NozzleUp = false
                                                }
                                        )
                                    }).ToArray()
                                });


                                 // Для JSON равносильно:
/*                                return json(
                                    new StationInformaton()
                                    {
                                        StationID = int.Parse(client.ClientID),
                                        Fuels = new List<StationInformaton.FuelInfo>()
                                        {
                                            new StationInformaton.FuelInfo() { Code = 95, Name = "АИ-95", Price = 34.99M },
                                            new StationInformaton.FuelInfo() { Code = 92, Name = "АИ-92", Price = 38.88M }
                                        },
                                        Pumps = new List<StationInformaton.PumpInfo>()
                                        {
                                           new StationInformaton.PumpInfo()
                                           {
                                               Number = 1,
                                               IsAvailable = true,
                                               MinOrder = 2,
                                               Nozzles = new List<StationInformaton.PumpInfo.NozzleInfo>()
                                               {
                                                   new StationInformaton.PumpInfo.NozzleInfo() { FuelCode = 92, NozzleUp = false },
                                                   new StationInformaton.PumpInfo.NozzleInfo() { FuelCode = 95, NozzleUp = false }
                                               },
                                           },
                                           new StationInformaton.PumpInfo()
                                           {
                                               Number = 2,
                                               IsAvailable = true,
                                               MinOrder = 2,
                                               Nozzles = new List<StationInformaton.PumpInfo.NozzleInfo>()
                                               {
                                                   new StationInformaton.PumpInfo.NozzleInfo() { FuelCode = 92, NozzleUp = false },
                                                   new StationInformaton.PumpInfo.NozzleInfo() { FuelCode = 95, NozzleUp = false }
                                               },
                                           }
                                        }
                                    });
  */                              
                        }

                        #endregion

                    #region OnDebitPump

                        case "OnDebitPump":
                        {
                            //TODO: Передача транзакции в систему управления

                            //Эмуляция налива и завершения заказа. 
                            if (!trans.ContainsKey(op.TransactionID))
                            {
                                //new Task(() =>
                                //{
                                    //Thread.Sleep(10000);
                                    lock (trans)
                                        trans.Add(op.TransactionID, op);
                                //}).Start();

                                object item = null;
                                lock (XmlPumpClient.Statuses)
                                {
                                    Log("Захват ТРК: " + op.Pump.Value + "\r\n", true);


                                    if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                            MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                    {
                                        XmlPumpClient.InitData(Driver.terminal);
                                    }

                                    var pump = Driver.Pumps[op.Pump.Value];
                                    pump.Blocked = true;

                                    XmlPumpClient.Init(Driver.terminal, op.Pump.Value, op.Pump.Value,
                                        XmlPumpClient.WaitAnswerTimeout, 1);
                                }



                                ++Driver.TransCounter;
                                    Log("Установка дозы на ТРК: " + op.Pump.Value + " , сгенерирован TransID: " +
                                        Driver.TransCounter + "\r\n", true);

                                    //var prePaid = Order.Price*Order.Quantity;
                                    var discount = 0;//100;//(Order.BasePrice - Order.Price)*Order.Quantity;
                                    var fuel = Driver.Fuels.First(t => t.Value.ID == op.Fuel.Value);
                                    int allowed = 0;
                                    foreach (var pumpFuel in Driver.Pumps[op.Pump.Value].Fuels)
                                    {
                                        allowed += 1 << (pumpFuel.Value.ID - 1);
                                    }
                                    //decimal price = 156;
                                    //decimal vol = 100;
                                    //if (!XmlPumpClient.Presale(
                                    //    Driver.terminal, op.Pump.Value, allowed, op.Amount.Value + discount,
                                    //    discount, Order.Quantity, XmlPumpClient.PaymentCodeToType(Order.PaymentCode), 
                                    //    op.TransactionID, 1, "АИ-92", 2000, "1234567890123456", 1, 3000))
                                    //    return -1;
                                    //log("предоплата\r\n");

                                    //if (!XmlPumpClient.Authorize(Driver.terminal, op.Pump.Value, 10, allowed, op.Pump.Value, op.TransactionID, (int)(price * 100), DELIVERY_UNIT.Money, 3000))
                                    //    return -1;
                                    //log("разрешить налив\r\n");

                                    Log("Presale:\r\n" +
                                    $"терминал:{Driver.terminal}\r\n" +
                                    $"колонка:{op.Pump.Value}\r\n" +
                                    $"дост. пистолеты:{allowed}\r\n" +
                                    $"сумма руб:{op.Amount.Value*100}\r\n" +
                                    $"скидка:{discount}\r\n" +
                                    //$"кол-во литры:{Order.Quantity}\r\n" +
                                    //$"тип оплаты:{XmlPumpClient.PaymentCodeToType(Int32.Parse(op.TransactionID))}\r\n" +
                                    $"рнн:{op.TransactionID}\r\n" +
                                    $"продукт код:{op.Fuel.Value}\r\n" +
                                    $"продукт{fuel.Key}\r\n" +
                                    //TODO не передается
                                    $"продукт цена коп:{(int)(fuel.Value.Price/*fuel.Value.Price*/ * 100)}\r\n", true);

                                    //TODO Проба со скидками!!!!
                                    string errMsg = "";
                                    if (!XmlPumpClient.Authorize(Driver.terminal, op.Pump.Value, Driver.TransCounter,
                                        //TODO не передается
                                        allowed, op.Fuel.Value, op.TransactionID, (int)((op.Amount.Value*100)/ fuel.Value.Price), DELIVERY_UNIT.Volume,/*(int) (op.Amount.Value*100), DELIVERY_UNIT.Money,*/
                                        XmlPumpClient.WaitAnswerTimeout, out errMsg))
                                    //if (!XmlPumpClient.Authorize(Driver.terminal, op.Pump.Value, Driver.TransCounter,Order.ProductCode
                                    //    allowed, op.Fuel.Value, op.TransactionID, (int) (op.Amount.Value*100), DELIVERY_UNIT.Money,
                                    //    XmlPumpClient.WaitAnswerTimeout))
                                    {
                                        Log($"SetDoseCallback:Authorize: {errMsg}\r\n", true);
                                        return json(new { Result = "Error" });
                                    }


                                    //TODO Проба со скидками!!!!
                                    if (!XmlPumpClient.Presale(Driver.terminal, op.Pump.Value, allowed, op.Amount.Value,
                                        //TODO не передается
                                        discount, (int)(op.Amount.Value / fuel.Value.Price), XmlPumpClient.PaymentCodeToType(0),
                                        op.TransactionID, op.Fuel.Value, fuel.Key,
                                        //TODO не передается
                                        (int)(fuel.Value.Price * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                                    //if (!XmlPumpClient.Presale(Driver.terminal, op.Pump.Value, allowed, op.Amount.Value,
                                    //    discount, Order.Quantity, XmlPumpClient.PaymentCodeToType(Order.PaymentCode),
                                    //    op.TransactionID, op.Fuel.Value, fuel.Key,
                                    //    (int)(fuel.Value.Price * 100), "", XmlPumpClient.WaitAnswerTimeout, 1))
                                    {
                                        Log("SetDoseCallback:Presale: нет о твета на Presale\r\n", true);
                                        return json(new { Result = "Error" });
                                    }


                                    Log("налив разрешен\r\n", true);

                                    //lock (Driver.TransMemory)
                                    //{
                                    //    Driver.TransMemory[(long) Driver.TransCounter] = Order;
                                    //}

                                    Thread myThread2 = new Thread(Driver.WaitCollectBenzuber);
                                    myThread2.Start(op.TransactionID); // запускаем поток

                                    //XmlPumpClient.PumpRequestAuthorize(Driver.terminal, op.Pump.Value, Driver.TransCounter,
                                    //    allowed, 1, op.TransactionID, (int)(op.Amount.Value * 100), DELIVERY_UNIT.Money);

                                    //Thread.Sleep(2000);


                                    //while (XmlPumpClient.answers.Count != 0)
                                    //    XmlPumpClient.PumpRequestCollect(Driver.terminal, op.Pump.Value, Driver.TransCounter,
                                    //    op.TransactionID);

                                    //Thread.Sleep(2000);

                                    //    Driver.FillingOver(Driver.TransCounter, (int)(Order.Quantity * 100), (int)(op.Amount.Value*100));

                                    //DebithThread.SetTransID(Driver.TransCounter);
                                    //return Driver.TransCounter;




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
                            //Подключаемся к хранилищу транзакций и удаляем из него завершенную транзакцию.
                            //Либо помечаем её, как завершенную в случае использоания БД 
                            if (transOver.ContainsKey(op.TransactionID) && trans.ContainsKey(op.TransactionID))
                            {
                                //SYSTEMTIME time;
                                //VariantTimeToSystemTime(_DateTime, &time);
                                //TODO не передается
                                var summ = transOver[op.TransactionID].Amount.Value;//(DocKindCode != 4) ? (decimal)Amount / 100 : -(decimal)Amount / 100;

                                    //Log("Сохранение документа, TransID: " + op.TransactionID
                                    //+ "\r\nДата/время:         " + _DateTime//time.wYear + "-" + time.wMonth + "-" + time.wDay + " " + time.wHour + ":" + time.wMinute + ":" + time.wSecond
                                    //+ "\r\nИмя устройства:     " + DeviceName
                                    //+ "\r\nСерийный номер:     " + DeviceSerial
                                    //+ "\r\nНомер документа:    " + DocNo
                                    //+ "\r\nТип документа:      " + DocType
                                    //+ "\r\nСумма:              " + summ
                                    //+ "\r\nПроизвольный чек:   " + VarCheck
                                    //+ "\r\nВид документа:      " + DocKind
                                    //+ "\r\nКод вида документа: " + DocKindCode
                                    //+ "\r\nТип оплаты:         " + PayType
                                    //+ "\r\nЧек по факту:       " + FactDoc
                                    //+ "\r\nНомер продукта:     " + BP_Product
                                    //+ "\r\nID Транзакции:      " + Trans_ID
                                    //+ "\r\nID PumpNo:          " + PumpNo
                                    //+ "\r\nID ShiftDocNum:     " + ShiftDocNum
                                    //+ "\r\nID ShiftNum:        " + ShiftNum
                                    //+ "\r\nID OrderRRN:        " + OrderRRN.PadLeft(20, '0')
                                    //+ "\r\n------------------------------------------------------"
                                    //+ "\r\nОбраз Чека:         "
                                    //+ "\r\n" + RecieptText
                                    //+ "\r\n------------------------------------------------------"
                                    //+ "\r\n", true);

                                    //++Driver.TransCounter;

                                    //if (DocType != 0 || PumpNo <= 0)
                                    //    return 1;

                                    var discount = 0;//100; //(order.BasePrice - order.Price) * order.Quantity;
                                    var fuel = Driver.Fuels.First(t => t.Value.ID == trans[op.TransactionID].Fuel.Value);
                                    int allowed = 0;
                                    foreach (var pumpFuel in Driver.Pumps[trans[op.TransactionID].Pump.Value].Fuels)
                                    {
                                        allowed += 1 << (pumpFuel.Value.ID - 1);
                                    }

                                    //TODO Проба со скидками!!!!
                                    XmlPumpClient.SaleDataSale(Driver.terminal, trans[op.TransactionID].Pump.Value, allowed,
                                            trans[op.TransactionID].Amount.Value, summ, discount,
                                            trans[op.TransactionID].Amount.Value / fuel.Value.Price, summ / fuel.Value.Price, PAYMENT_TYPE.Cash,
                                            op.TransactionID.PadLeft(20, '0'), trans[op.TransactionID].Fuel.Value, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                                    //XmlPumpClient.SaleDataSale(Driver.terminal, order.PumpNo, allowed,
                                    //    order.Amount, order.OverAmount, discount,
                                    //    order.Quantity, order.OverQuantity, PAYMENT_TYPE.Cash,
                                    //    order.OrderRRN, order.ProductCode, fuel.Key, (int)(fuel.Value.Price * 100), "", 1);
                                    //log(
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
                                    //log.Write($"чек:\r\n" +
                                    //    $"GetShiftDocNum: {GetShiftDocNum()}\r\n" +
                                    //    $"GetDocNum: {GetDocNum()}\r\n" +
                                    //    $"GetShiftNum: {GetShiftNum()}\r\n" +
                                    //    $"OverAmount: {(endMessage?.Money ?? 0)/100.0}\r\n" +
                                    //    $"OrderRRN: {order.OrderRRN}\r\n");




                                    object item = null;
                                    lock (XmlPumpClient.Statuses)
                                    {
                                        Log("Освобождение ТРК: " + trans[op.TransactionID].Pump.Value + "\r\n", true);

                                        if (!XmlPumpClient.Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(-1,
                                            MESSAGE_TYPES.OnDataInit), out item) || item == null)
                                        {
                                            XmlPumpClient.InitData(Driver.terminal);
                                        }

                                        var pump = Driver.Pumps[trans[op.TransactionID].Pump.Value];
                                        pump.Blocked = false;

                                        XmlPumpClient.Init(Driver.terminal, trans[op.TransactionID].Pump.Value, -trans[op.TransactionID].Pump.Value, XmlPumpClient.WaitAnswerTimeout, 1);
                                        //Driver.Pumps[Pump] = pump;

                                        //label1.Text += "Освобождение ТРК: " + Pump + "\r\n";
                                    }



                                    Log("изм. статуса\r\n", true);

                                    //var res = XmlPumpClient.answers;
                                    var res2 = XmlPumpClient.Statuses;
                                    var res3 = XmlPumpClient.Fillings;

                                    XmlPumpClient.ClearAllTransactionAnswers(trans[op.TransactionID].Pump.Value, op.TransactionID.PadLeft(20, '0'));

                                    XmlPumpClient.FiscalEventReceipt(Driver.terminal, trans[op.TransactionID].Pump.Value/*order.PumpNo*/,
                                        2, 5, 34,
                                        summ/*(endMessage?.Money ?? 0) / 100m*/, 0, PAYMENT_TYPE.Cash, op.TransactionID.PadLeft(20, '0') /*order.OrderRRN*/, 1);
                                    Log($"чек:\r\n" +
                                        //$"GetShiftDocNum: {ShiftDocNum}\r\n" +
                                        //$"GetDocNum: {DocNo}\r\n" +
                                        //$"GetShiftNum: {ShiftNum}\r\n" +
                                        $"OverAmount: {summ}\r\n" +
                                        $"OrderRRN: {op.TransactionID.PadLeft(20, '0')/*order.OrderRRN*/}\r\n", true);

                                    //DebithThread.SetTransID(Driver.TransCounter);




                                    //Thread.Sleep(30000);
                                    lock (trans)
                                        trans.Remove(op.TransactionID);
                                    lock (transOver)
                                        transOver.Remove(op.TransactionID);





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
                            //Путь до обновления указывается в переменной op.UpdatePath
                            //return json(new { Result = "OK" });
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
                catch
                {
                    return json(new { Result = "Error" });
                }
                return json(new { Result = "Unsupported" });
            }


            public static Dictionary<string, Request> trans = new Dictionary<string, Request>();
            public static Dictionary<string, Request> transOver = new Dictionary<string, Request>();

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
                //sendMessage("GET_ID");
                //ClientID = waitMessage(stream: _Stream);
                //if (string.IsNullOrWhiteSpace(ClientID))
                //    throw new Exception("Некорректный ответ на запрос GET_ID");
                //sendMessage("GET_HW", stream: _Stream);
                //HW_ID = waitMessage(stream: _Stream);
                //  if (string.IsNullOrWhiteSpace(HW_ID))
                //       throw new Exception("Некорректный ответ на запрос GET_HW");

                // Log($"Получены идентификаторы клиента. ClientID: '{ClientID}', HW_ID:{HW_ID}");

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
                    ExcangeServer.Log(ex.Message);
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
                //return Client.GetStream();
                SslStream sslStream = new SslStream(Client.GetStream(), false);
                // Authenticate the server but don't require the client to authenticate.
                try
                {
                    /*    var thumbprint = Regex.Replace("‎‎60 34 79 97 9f 8b 76 19 de e7 17 ea 01 ca ea 22 5d a2 69 33", @"[^\da-zA-z]", string.Empty).ToUpper();
                        // var thumbprint = Regex.Replace("‎‎c6 66 57 3b 37 c7 ca 25 1d f1 5b 4d 4c 75 88 59 3e cd 4f 9a", @"[^\da-zA-z]", string.Empty).ToUpper();
                        var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                        store.Open(OpenFlags.ReadOnly);
                        var certificate = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false)[0];
                         store.Close();
                                                            */

                    sslStream.AuthenticateAsServer(Certificate, false, SslProtocols.Tls12, false /*true*/);


                    // Set timeouts for the read and write to 5 seconds.
                    sslStream.ReadTimeout = 30000;
                    sslStream.WriteTimeout = 30000;

                    return sslStream as Stream;
                }
                catch (AuthenticationException e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                    }
                    Console.WriteLine("Authentication failed - closing the connection.");
                    sslStream.Close();
                    Client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
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
                    ExcangeServer.Log("Не указаны данные для отправки");
                    throw new Exception("Не указаны данные для отправки");
                }
                else ExcangeServer.Log($"Отправка сообщения: {Data}");



                var str_arr = Encoding.GetBytes(Data);
                //var Stream = getNetStream(Client);


                stream.Write(str_arr, 0, str_arr.Length);
                stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
                stream.Flush();

                ExcangeServer.Log("Данные успешно отправлены");
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
                    if ((b = stream.ReadByte()) >= 0)
                    {
                        if ((byte)b == 0xFF)
                            ff_count++;
                        else
                        {
                            ff_count = 0;
                            data[len++] = (byte)b;
                        }

                        if (ff_count >= 4)
                        {
                            var msg = Encoding.GetString(data, 0, len);
                            ExcangeServer.Log("Получено сообщение: " + msg);
                            return msg;
                        }

                    }
                    else System.Threading.Thread.Sleep(100);
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

                // Authenticate the server but don't require the client to authenticate.
                try
                {

                    sslStream.AuthenticateAsClient(this.Address, null, SslProtocols.Tls12, true);


                    // Set timeouts for the read and write to 5 seconds.
                    sslStream.ReadTimeout = 30000;
                    sslStream.WriteTimeout = 30000;

                    return sslStream as Stream;
                }
                catch (AuthenticationException e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                    }
                    Console.WriteLine("Authentication failed - closing the connection.");
                    sslStream.Close();
                    Client.Close();
                }
                finally
                {
                    // The client stream will be closed with the sslStream
                    // because we specified this behavior when creating
                    //// the sslStream.
                    //sslStream.Close();
                    //Client.Close();
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
                    ExcangeServer.Log("Error background != null");
                    return false;
                }
                try
                {
                    this.Address = Address;
                    this.Port = Port;
                    ExcangeServer.Log($"Address {Address} Port {Port}");

                    background = new Task(background_connect);
                    background.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    ExcangeServer.Log("Error " + ex);
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
                    var req_id = Request.Substring(0, 20);
                    return req_id + (HandleRequest?.Invoke(Request.Remove(0, 20)) ?? "Unsupported");
                }
                catch
                {
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
                        base.Stream = GetNetStream(Client);
                        if (waitMessage() == "GET_ID")
                        {
                            sendMessage(ClientID);
                        }
                        else
                        {
                            ExcangeServer.Log("Некорректный запрос от сервера. Ожидался запрос GET_ID.");
                            continue;
                        }
                        if (waitMessage() == "GET_HW")
                        {
                            sendMessage(HW_ID);
                        }
                        else
                        {
                            ExcangeServer.Log("Некорректный запрос от сервера. Ожидался запрос HW_ID.");
                            continue;
                        }
                        while (background != null && Client != null)
                        {
                            sendMessage(handleRequest(waitMessage()));
                        }

                    }
                    catch (Exception ex)
                    {
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

        //public class Logger
        //{


        //    public static void Write(string Message, bool IsError = false,
        //        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        //        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        //        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        //    {
        //        if (IsError) Console.WriteLine($"Error in {memberName}: {Message}");
        //        else Console.WriteLine(Message);
        //    }
        //}
    }
}
