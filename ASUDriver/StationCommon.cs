using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ProjectSummer.Repository;

namespace ASUDriver
{
    public class StationGate
    {
        public class XML_Serializator
        {
            public class Utf8StringWriter : StringWriter
            {
                public override Encoding Encoding => Encoding.UTF8;
            }
            public string ToXmlString()
            {
                var xmlSerializer = new XmlSerializer(GetType());

                using (var textWriter = new Utf8StringWriter())
                {
                    xmlSerializer.Serialize(textWriter, this);
                    return textWriter.ToString();
                }
            }
            public static ResultType FromXmlString<ResultType>(string XmlString)
            {
                var xmlSerializer = new XmlSerializer(typeof(ResultType));
                using (var stringReader = new StringReader(XmlString))
                {
                    return (ResultType)xmlSerializer.Deserialize(stringReader);
                }
            }
        }

        [XmlRoot("StationInformaton")]
        public class StationInformaton : XML_Serializator
        {
            [XmlElement("StationID")]
            public int StationID { get; set; }
            [XmlArray("Fuels")]
            [XmlArrayItem("Fuel", typeof(FuelInfo))]
            public List<FuelInfo> Fuels { get; set; }
            public class FuelInfo
            {
                [XmlElement("Code")]
                public int Code { get; set; }
                [XmlElement("Name")]
                public string Name { get; set; }
                [XmlElement("Price")]
                public decimal Price { get; set; }
            }
            [XmlArray("Pumps")]
            [XmlArrayItem("Pump", typeof(PumpInfo))]
            public List<PumpInfo> Pumps { get; set; }
            public class PumpInfo
            {
                [XmlElement("Number")]
                public int Number { get; set; }
                [XmlArray("Nozzles")]
                [XmlArrayItem("Nozzle", typeof(NozzleInfo))]
                public List<NozzleInfo> Nozzles { get; set; }
                [XmlElement("IsAvailable")]
                public bool IsAvailable { get; set; }
                [XmlElement("TransactionID")]
                public string TransactionID { get; set; }
                [XmlElement("MinOrder")]
                public int MinOrder { get; set; }
                public class NozzleInfo
                {
                    [XmlElement("FuelCode")]
                    public int FuelCode { get; set; }
                    [XmlElement("NozzleUp")]
                    public bool NozzleUp { get; set; }
                }
            }
        }
    }


    public class StationCommon
    {
        public interface IExcangeCallback
        {
            [OperationContract]
            int OnDebitPump(int PumpNum, int Fuel, string TransID, decimal Amount);
            [OperationContract]
            StationGate.StationInformaton GetStationInfo();
            [OperationContract]
            void OnPingOK();
            [OperationContract]
            bool CancelTransaction(string TransID);
        }

        [ServiceContract(CallbackContract = typeof(IExcangeCallback))]
        public interface IExcange
        {
            [OperationContract]
            bool Ping(string StationID, string HW_ID);
            [OperationContract]
            void FillingOver(string TransID, decimal Amount);
            [OperationContract]
            Update GetUpdate(string StationID, string CurrentVersion, string RunningVersion);
        }
        public struct Update
        {
            public string Version { get; set; }
            public string MD5 { get; set; }
            public byte[] Data { get; set; }
            public static string GetDataMd5Hash(byte[] Data) => BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(Data));
        }
        public class IExcangeClient : DuplexClientBase<IExcange>
        {
            public IExcangeClient(object callbackInstance, Binding binding, EndpointAddress remoteAddress)
                : base(callbackInstance, binding, remoteAddress) { }
        }

    }


    public class Excange : StationCommon.IExcangeCallback
    {

        enum ErrorCodes : int
        {
            NoError = 0,
            CommonError = 1,
            IncorrectRequest = 100,
            PumpOffline = 201,
            IncorrectFuel = 202,
            IncorrectPump = 203,
            PumpBusy = 204,
            PumpOverdose = 205
        }

        public static ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");

        public static Logger log = new Logger("Benzuber");
        static object pumpLocker = new object();
        static RemotePump_Driver.RemotePump pump = new RemotePump_Driver.RemotePump();
        static Excange()
        {
            pump.SetID("Benzuber");
            pump.FillingOverEvent += Pump_FillingOverEvent;
        }

        private static void Pump_FillingOverEvent(object sender, RemotePump_Driver.RemotePump.FillingOverEventArgs e)
        {
            log.Write($"Подтверждения налива: {e.TransactionID}, сумма: {e.Amount:0.00}р", 0, true);

            new Task(() =>
            {
                var data = pump.GetFillingOvers();
                lock (proxy)
                {
                    foreach (var d in data)
                    {
                        try
                        {
                            proxy.FillingOver(d.OrderRRN, d.OverAmount);
                        }
                        catch (Exception ex)
                        {
                            log.Write("Ошибка при выплнении подтверждения налива" + ex.ToString());
                            unReciverOrders.Add(d);
                            log.Write($"Не удалось подтвердить налив RNN: {d.OrderRRN}, Факт. Сумма: {d.OverAmount}");
                        }
                    }
                    data = unReciverOrders.ToArray();
                    foreach (var d in data)
                    {
                        try
                        {
                            log.Write($"Повторная передача подтверждения налива RNN: {d.OrderRRN}, Факт. Сумма: {d.OverAmount}");
                            proxy.FillingOver(d.OrderRRN, d.OverAmount);
                            unReciverOrders.Remove(d);
                        }
                        catch (Exception ex)
                        {
                            log.Write("Ошибка при выплнении подтверждения налива" + ex.ToString());
                        }
                    }
                }
            }).Start();
        }
        private static List<RemotePump_Driver.OrderInfo> unReciverOrders = new List<RemotePump_Driver.OrderInfo>();

        public int OnDebitPump(int PumpNum, int Fuel, string TransID, decimal Amount)
        {
            try
            {

                lock (pumpLocker)
                {
                    log.Write(string.Format("Запрос на установку заказа. PumpNum: {0}, Fuel: {1}, TransID: {2}, Amount: {3}",
                        PumpNum, Fuel, TransID, Amount));

                    var product = (from fuel in Driver.Fuels
                                   where fuel.Value.ID == get_int_code(Fuel)
                                   select fuel.Value).SingleOrDefault();
                    if (product.Name == null)
                    {
                        log.Write("Продукт не найден");
                        return 1;
                    }

                    log.Write("Выбран продукт: " + product.ToString());

                    log.Write("Транзакция сохранена в архив.");

                    var ret = pump.SetDose(
                        new RemotePump_Driver.OrderInfo
                        {
                            TID = "Benzuber",
                            Amount = Amount,
                            Quantity = Amount / product.Price,
                            PumpNo = PumpNum,
                            OrderRRN = TransID.PadLeft(20, '0'),

                            Price = product.Price,
                            BasePrice = product.Price,
                            ProductCode = product.ID,

                            CardNO = TransID.PadLeft(20, '0'),
                            DiscontCardNO = "",
                            DiscontCardType = "-2",
                            OverAmount = 0,
                            OverQuantity = 0,
                            PaymentCode = 99,
                            OrderMode = 1,
                        });
                    return (ret) ? 0 : 1;

                }

            }
            catch (Exception ex)
            {
                log.Write(ex.ToString());
            }
            return 1;
        }


        public void OnPingOK()
        {
        }
        public static string ComputeMD5Checksum(string Data)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] checkSum = md5.ComputeHash(Encoding.Default.GetBytes(Data));
            string result = BitConverter.ToString(checkSum).Replace("-", String.Empty);
            return result;
        }
        static StationCommon.IExcange proxy = null;
        public static bool enable = false;
        static bool _connection = false;
        /// <summary>
        /// Состояние подключения к сервису
        /// </summary>
        public static bool ConnectionState
        {
            get { return connection; }
        }
        public static string ID
        {
            get { return config["station_id"]; }
        }
        static bool connection { get { return _connection; } set { if (_connection != value) { _connection = value; log.Write((value ? "Сервер доступен" : "Сервер не доступен")); } } }
        static bool? _auth;
        static DateTime LastUpdate = DateTime.MinValue;

        public static string Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public static void StartClient()
        {
            if (enable)
                return;
            enable = true;
            new Task(() =>
            {
                string hw_id = "";
                var hw = ActivationClass.GetDiskDriveSerialNumbers();
                if (hw.Length <= 0)
                {
                    log.Write("Не удалось получить HW ID");
                    return;
                }
                else
                {
                    hw_id = ComputeMD5Checksum(hw[0].Name + hw[0].Serial);
                    log.Write($"Вычисляем HW ID: {hw_id} для: {hw[0].Name}:{hw[0].Serial}");
                }
                var str = string.Format("net.tcp://" + config["server"] + ":" + config["exchangeport"]);
                log.Write(str);
                var location = Assembly.GetExecutingAssembly().Location;

                while (enable)
                {
                    try
                    {
                        //var str = string.Format("net.tcp://localhost:1102");

                        var uri = new Uri(str);
                        var callback = new Excange();
                        //log.Write("Create callback");
                        var binding = new NetTcpBinding(SecurityMode.Transport);
                        //binding.Security.ToString().ClientCredentialType = MessageCredentialType.UserName;
                        //binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;

                        binding.Security.Message.ClientCredentialType = MessageCredentialType.None;
                        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;


                        // log.Write("Binding");
                        var client = new StationCommon.IExcangeClient(callback, binding, new EndpointAddress(uri));
                        //client.ClientCredentials.UserName.UserName = config["station_id"];               
                        //client.ClientCredentials.UserName.Password = hw_id;


                        System.Net.NetworkCredential credential = new System.Net.NetworkCredential();
                        //credential.Domain = "WORKGROUP";
                        credential.UserName = "BenzuberAZS";
                        credential.Password = "3740858";
                        client.ClientCredentials.Windows.ClientCredential = credential;

                        //log.Write("Excange object");
                        proxy = client.ChannelFactory.CreateChannel(new EndpointAddress(uri));

                        //   log.Write("Proxy");


                        try
                        {
                            //CommunicationState.
                            while (enable)
                            {
                                lock (proxy)
                                {
                                    var flag = (connection == false);
                                    if (!proxy.Ping(config["station_id"], hw_id))
                                    {
                                        connection = true;
                                        if ((_auth ?? true) || flag)
                                            log.Write($"Сервер отказал в авторизации: {config["station_id"]}, {hw_id}");
                                        _auth = false;
                                        System.Threading.Thread.Sleep(60000);
                                        break;
                                    }
                                    else
                                    {
                                        connection = true;
                                        if ((!(_auth ?? false)) || flag)
                                            log.Write($"Успешная авторизация на сервере: {config["station_id"]}, {hw_id}");
                                        _auth = true;

                                        if (LastUpdate.AddHours(6) < DateTime.Now)
                                        {
                                            try
                                            {
                                                LastUpdate = DateTime.Now;
                                                var updateResponse = proxy.GetUpdate(config["station_id"], FileVersionInfo.GetVersionInfo(location).FileVersion, Version);
                                                if (updateResponse.Data != null && updateResponse.MD5 != null
                                                    && updateResponse.MD5 == StationCommon.Update.GetDataMd5Hash(updateResponse.Data))
                                                {
                                                    if (File.Exists(location + ".back"))
                                                    {
                                                        try
                                                        {
                                                            File.Delete(location + ".back");
                                                        }
                                                        catch { }
                                                    }
                                                    if (!File.Exists(location + ".back"))
                                                    {
                                                        try
                                                        {
                                                            File.Move(location, location + ".back");
                                                        }
                                                        catch { }
                                                    }
                                                    if (!File.Exists(location))
                                                    {
                                                        try
                                                        {
                                                            File.WriteAllBytes(location, updateResponse.Data);
                                                            log.Write($"Загружено обновление драйвера, версия: {updateResponse.Version}\r\nОбновление будет установлено после перезапуска приложения.");
                                                        }
                                                        catch { }
                                                    }

                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                    //connection = true;
                                }
                                //log.Write("Ping");
                                System.Threading.Thread.Sleep(10000);
                                //Console.ReadLine();

                            }
                            connection = false;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(10000);
                        }
                        try
                        {
                            client.Close();
                        }
                        catch { }
                        connection = false;
                    }
                    catch (Exception ex)
                    {
                        log.Write(ex.ToString());
                    }
                }
            }
            ).Start();
            int cnt;
            lock (Driver.PumpsLocker)
            cnt = Driver.Pumps.Count;
            log.Write($"F:{ASUDriver.Driver.Fuels.Count} P:{cnt}", 0, true);
        }

        private int get_int_code(int ex_code) => int.Parse((from code in config.GetValueNames("fuel_code_") where config[code] == ex_code.ToString() select code.Replace("fuel_code_", ""))?.SingleOrDefault() ?? "-1");
        private int get_ex_code(int int_code) => int.Parse(config["fuel_code_" + int_code]);

        public StationGate.StationInformaton GetStationInfo()
        {
            lock (pumpLocker)
            {

                try
                {
                    int cnt;
                    lock (Driver.PumpsLocker)
                        cnt = Driver.Pumps.Count;
                    log.Write($"Получение информации о АЗС: F:{ASUDriver.Driver.Fuels.Count} P:{cnt}", 0, true);
                    var info = new StationGate.StationInformaton()
                    {
                        Fuels = new List<StationGate.StationInformaton.FuelInfo>(from fuel in ASUDriver.Driver.Fuels select new StationGate.StationInformaton.FuelInfo { Code = get_ex_code(fuel.Value.ID), Name = fuel.Value.Name, Price = fuel.Value.Price })
                    };

                    foreach (var fuel in info.Fuels) log.Write($"{fuel.Code}. {fuel.Name} = {fuel.Price:0.00}р");

                    List<StationGate.StationInformaton.PumpInfo> pumps = new List<StationGate.StationInformaton.PumpInfo>();
                    List<KeyValuePair<int, Driver.PumpInfo>> driverPumps;
                    lock (Driver.PumpsLocker)
                        driverPumps = Driver.Pumps.ToList();
                    foreach (var p in driverPumps)
                    {
                        var state = pump.GetPumpInformation(p.Value.Pump);
                        log.Write($"{state.ToString()}");
                        pumps.Add(new StationGate.StationInformaton.PumpInfo
                        {
                            IsAvailable = state.State == RemotePump_Driver.PumpState.Online,
                            MinOrder = 2,
                            Number = p.Value.Pump,
                            TransactionID = state.TransactionID,
                            Nozzles = new List<StationGate.StationInformaton.PumpInfo.NozzleInfo>(from n in state.ProductInformation select new StationGate.StationInformaton.PumpInfo.NozzleInfo { FuelCode = get_ex_code(n.Code), NozzleUp = (state.SelectedProduct == n.Code) })
                        });

                    }
                    info.Pumps = pumps;
                    info.StationID = int.Parse(config["station_id"]);
                    return info;
                }
                catch (Exception ex)
                {
                    if (ex is FormatException)
                    {
                        log.Write("Некорректно введена таблица соответствия видов топлива", 0, true);
                    }
                    else
                        log.Write("GetStationInfo()r\n" + ex.ToString(), 0, true);
                }
                return null;
            }
        }

        public bool CancelTransaction(string TransID)
        {
            log.Write($"Попытка отмены заказа {TransID}");
            return pump.CancelDose(TransID);
        }
    }

}