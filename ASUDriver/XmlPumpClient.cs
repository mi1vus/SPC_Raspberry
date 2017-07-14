using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace ASUDriver
{
    public class TransactionInfo
    {
        public int Pump;
        public int PaymentCode;
        public int Fuel;
        public int OrderInMoney;
        public int Quantity;
        public int Price;
        public int Amount;
        public string CardNum;
        public string RRN;
        public string BillImage;
    };

    public enum PUMP_STATUS
    {
        PUMP_STATUS_NOT_PROGRAMMED = 32,
        PUMP_STATUS_IDLE = 33,
        PUMP_STATUS_WAITING_AUTHORIZATION = 34,
        PUMP_STATUS_AUTHORIZED = 35,
        PUMP_STATUS_WAITING_COLLECTING = 37,
        PUMP_STATUS_FUELLING_READY_ERROR = 38,
        PUMP_STATUS_NOT_COMMUNICATING = 39,
        PUMP_STATUS_OCCUPIED = 40,
    }

    public enum PAYMENT_TYPE
    {
         Cash = 0,
         Card = 1,
         FuelCard = 2
    }

    public enum DELIVERY_UNIT
    {
        Volume = 0,
        Money = 1,
    }

    public enum MESSAGE_TYPES
    {
        PumpResponse        = 1,
        OnDataInit          = 2,
        OnPumpStatusChange  = 3,
        OnPumpStatusChangeFilling = 4,
        OnSetGradePrices = 5,
        OnPumpError         = 6,
    }

    [XmlRoot("PumpResponse")]
    public class PumpResponse
    {
        [XmlAttribute]
        public string RequestType {get;set;}
        [XmlAttribute]
        public int OptId {get;set;}
        [XmlAttribute]
        public string OverAllResult {get;set;}
        [XmlElement("MessageHeader")]
        public MessageHeader MessageHeader { get; set; }
        [XmlElement("Pump")]
        public PumpObj PumpObj { get; set; }
    }
    public class MessageHeader
    {
        [XmlAttribute]
        public string RequestID { get; set; }
        [XmlElement("Timestamp")]
        public Timestamp Timestamp { get; set; }
    }
    public class Timestamp
    {
        [XmlAttribute]
        public string Date { get; set; }
        [XmlAttribute]
        public string Time { get; set; }
    }
    public class PumpObj
    {
        [XmlAttribute]
        public int PumpNumber { get; set; }
        [XmlElement("PumpStatus")]
        public PumpStatus PumpStatus { get; set; }
    }
    public class PumpStatus
    {
        [XmlAttribute]
        public string Status { get; set; }
        [XmlAttribute]
        public int Litres { get; set; }
        [XmlAttribute]
        public int Grade { get; set; }
        [XmlAttribute]
        public int Money { get; set; }
        [XmlAttribute]
        public string Error { get; set; }
    }

    [XmlRoot("OnDataInit")]
    public class OnDataInit
    {
        [XmlElement("OptId")]
        public int OptId { get; set; }
        [XmlElement("Date")]
        public string Date { get; set; }
        [XmlElement("Time")]
        public string Time { get; set; }
        [XmlElement("Pumps")]
        public List<Pump> Pumps { get; set; }
    }
    public class Pump
    {
        [XmlElement("Pump")]
        public int PumpId { get; set; }
        [XmlElement("Nozzles")]
        public List<Nozzle> Nozzles { get; set; }
    }
    public class Nozzle
    {
        [XmlElement("Nozzle")]
        public int NozzleId { get; set; }
        [XmlElement("Grade")]
        public int GradeId { get; set; }
        [XmlElement("Approval")]
        public string Approval { get; set; }
    }

    [XmlRoot("OnPumpStatusChange")]
    public class OnPumpStatusChange
    {
        [XmlElement("OptId")]
        public int OptId { get; set; }
        [XmlElement("PumpNo")]
        public int PumpNo { get; set; }
        [XmlElement("Date")]
        public string Date { get; set; }
        [XmlElement("Time")]
        public string Time { get; set; }
        [XmlElement("Status")]
        public int Status { get; set; }
        public PUMP_STATUS StatusObj => (PUMP_STATUS) Status;
        [XmlElement("Grade")]
        public int Grade { get; set; }
        [XmlElement("Liters")]
        public int Liters { get; set; }
        [XmlElement("Money")]
        public int Money { get; set; }
        [XmlElement("UnitPrice")]
        public int UnitPrice { get; set; }
        [XmlElement("OrderUID")]
        public string OrderUID { get; set; }
        [XmlElement("Nozzles")]
        public List<Nozzle> Nozzles { get; set; }
    }

    [XmlRoot("OnSetGradePrices")]
    public class OnSetGradePrices
    {
        [XmlElement("OptId")]
        public int OptId { get; set; }
        [XmlElement("Date")]
        public string Date { get; set; }
        [XmlElement("Time")]
        public string Time { get; set; }
        [XmlElement("GradePrices")]
        public List<GradePrice> GradePrices { get; set; }
    }
    public class GradePrice
    {
        [XmlElement("GradeId")]
        public int GradeId { get; set; }
        [XmlElement("GradeName")]
        public string GradeName { get; set; }
        [XmlElement("Price")]
        public int Price { get; set; }
        [XmlElement("GradePrice")]
        public int GradePriceVal { get; set; }
    }

    [XmlRoot("OnPumpError")]
    public class OnPumpError
    {
        [XmlElement("OptId")]
        public int OptId { get; set; }
        [XmlElement("PumpNo")]
        public int PumpNo { get; set; }
        [XmlElement("Date")]
        public string Date { get; set; }
        [XmlElement("Time")]
        public string Time { get; set; }
        [XmlElement("ErrorCode")]
        public int ErrorCode { get; set; }
    }

    public class XmlPumpClient
    {
        public const int BuffSize = 65536;
        public static Socket socket; 
        public static TcpClient client;
        public static NetworkStream stream;
        private static byte[] sockHost;
        private static int sockPort;
        private static int sockTerminal;

        public static int SendTimeout;
        public static int WaitAnswerTimeout;

        public static Encoding Enc = Encoding.UTF8;

        //public static List<object> answers;
        public static Dictionary<Tuple<int, MESSAGE_TYPES>, object> Statuses;
        public static Dictionary<Tuple<int, MESSAGE_TYPES>, List<object>> Fillings;

        public static void StartClient(string bHostB2, int iPort)
        {
            client = new TcpClient();
            try
            {
                client.Connect(bHostB2, iPort); //подключение клиента
                stream = client.GetStream(); // получаем поток

                //string message = "-------";
                //SendMessage(message);
                //byte[] data = Encoding.Unicode.GetBytes(message);
                //stream.Write(data, 0, data.Length);

                //запускаем новый поток для получения данных
                Thread receiveThread = new Thread(new ThreadStart(ReceiveMessageClient));
                receiveThread.Start(); //старт потока
                //Console.WriteLine("Добро пожаловать, {0}", userName);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                //Disconnect();
            }
        }

        public static void StartSocket(byte[] bHostB2, int iPort, int terminal)
        {
            if (socket != null)
                return;

            IPHostEntry hostEntry = null;

            IPEndPoint ipe = new IPEndPoint(new IPAddress(bHostB2), iPort);
            Socket tempSocket =
                new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            tempSocket.Connect(ipe);

            if (tempSocket.Connected)
            {
                socket = tempSocket;
                //socket.Blocking = false;
                //answers = new List<object>();
                Statuses = new Dictionary<Tuple<int, MESSAGE_TYPES>, object>();
                Fillings = new Dictionary<Tuple<int, MESSAGE_TYPES>, List<object>>();
                Thread receiveThread = new Thread(new ThreadStart(ReceiveMessageSocket));
                sockHost = bHostB2;
                sockPort = iPort;
                sockTerminal = terminal;
                receiveThread.Start(); //старт потока
            }
        }

        public static void RestatrSocketIfNotAlive(byte[] bHostB2, int iPort)
        {
            bool blockingState = socket.Blocking;
            try
            {
                byte[] tmp = new byte[1];

                socket.Blocking = false;
                socket.Send(tmp, 0, 0);
                //Console.WriteLine("Connected!");
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (!e.NativeErrorCode.Equals(10035))
                {
                    //MessageBox.Show($"Disconnected: error code {e.NativeErrorCode}!");
                    //throw new Exception("ERROR Разрыв соединения с попыткой восстановления!");

                    File.AppendAllText("exchange2.log", $"\r\n********ERROR*********\r\nРазрыв соединения с попыткой восстановления!");

                    IPHostEntry hostEntry = null;

                    IPEndPoint ipe = new IPEndPoint(new IPAddress(bHostB2), iPort);
                    Socket tempSocket =
                        new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

                    tempSocket.Connect(ipe);

                    if (tempSocket.Connected)
                    {
                        socket = tempSocket;
                        //socket.Blocking = false;
                        //lock (answers)
                        //{
                        //    answers.RemoveAll(t =>t is OnDataInit);
                        //    answers.Add("Замена сокета!!!1!!");
                        //}
                        InitData(sockTerminal);
                    }
                }
                //else
                //    Console.WriteLine("Still Connected, but the Send would block");
            }
            finally
            {
                socket.Blocking = blockingState;
            }
        }

        private static void SendMessageClient(string message)
        {
            //Console.WriteLine("Введите сообщение: ");

            //while (true)
            //{
            //string message = Console.ReadLine();
            byte[] data = Encoding.ASCII.GetBytes(message);
            stream?.Write(data, 0, data.Length);
            //}
        }

        private static void SendMessageSocket(string message, int timeout)
        {
            byte[] bytesToSent = Enc.GetBytes(message);

            if (socket == null) return;

            lock (socket)
                RestatrSocketIfNotAlive(sockHost, sockPort);

            // Send request to the server.
            lock (socket)
                socket.Send(bytesToSent, bytesToSent.Length, 0);

            File.AppendAllText("exchange2.log", $"\r\n********send*********\r\n{message}");

            Thread.Sleep(timeout);
        }

        public static void SendMessage(string message, int timeout)
        {
            if (!isValidXml(message))
                throw new XmlException();

            SendMessageSocket(message, timeout);
        }

        public static void ReceiveMessageClient()
        {
            while (true)
            {
                try
                {
                    byte[] data = new byte[64]; // буфер для получаемых данных
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = stream.Read(data, 0, data.Length);
                        if (bytes != 0)
                            builder.Append(Encoding.ASCII.GetString(data, 0, bytes));
                    } while (stream != null && stream.DataAvailable);

                    if (builder.Length != 0)
                    {
                        string[] message = builder.ToString().Split(new [] {"<?xml version=\"1.0\"?>"},StringSplitOptions.RemoveEmptyEntries );
                        XmlSerializer serializer = new XmlSerializer(typeof(OnDataInit));
                        using (TextReader reader = new StringReader(message[0]))
                        {
                            OnDataInit result = (OnDataInit)serializer.Deserialize(reader);
                        }
                    }
                    //Console.WriteLine(message);//вывод сообщения
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Подключение прервано!"); //соединение было прервано
                    Console.ReadLine();
                    Disconnect();
                }
            }
        }
        public static void ReceiveMessageSocket()
        {
            StringBuilder builder = new StringBuilder();
            if (File.Exists("exchange2.log"))
            {
                try
                {
                    File.Delete("exchange2.log");
                }
                catch { }
            }
            //File.WriteAllText("exchange2.log", string.Empty);
            //File.Delete("exchange2.log");
            while (true)
            {
                try
                {
                    // Receive the server content.
                    Byte[] bytesReceived = new Byte[65536];
                    int bytes = 0;

                    lock (socket)
                        RestatrSocketIfNotAlive(sockHost, sockPort);

                    //Console.WriteLine("Connected: {0}", client.Connected);

                    // The following will block until te page is transmitted.
                    lock (socket)
                        while (socket.Available != 0)
                        {
                            bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                            if (bytes != 0)
                                builder.Append(Enc.GetString(bytesReceived, 0, bytes));
                        }

                    if (builder.Length != 0)
                    {
                        var tmp = builder.ToString();
                        File.AppendAllText("exchange2.log", $"\r\n********recv*********\r\n{tmp}");
                        string[] ansverStrings = builder.ToString()
                            .Split(new[] {"<?xml version=\"1.0\"?>"}, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var xml in ansverStrings)
                        {
                            if (isValidXml(xml))
                            {
                                builder.Remove(0, xml.Length + 21);
                                File.AppendAllText("exchange2.log", "\r\n*****************\r\n<? xml version =\"1.0\"?>" + xml);
                            }
                            else
                            {
                                File.AppendAllText("exchange2.log", $"\r\n********ERROR*********\r\nnot valid xml\r\n{xml}");
                                continue;
                            }

                            List<object> msgs;
                            if (xml.Contains("OnDataInit"))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(OnDataInit));
                                using (TextReader reader = new StringReader(xml))
                                {
                                    OnDataInit result = (OnDataInit) serializer.Deserialize(reader);
                                    //lock(answers)
                                    //    answers.Add(result);
                                    lock (Statuses)
                                    {
                                        Statuses[new Tuple<int, MESSAGE_TYPES>
                                            (-1, MESSAGE_TYPES.OnDataInit)] =  result;
                                    }
                                }
                            }
                            if (xml.Contains("OnPumpStatusChange"))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(OnPumpStatusChange));
                                using (TextReader reader = new StringReader(xml))
                                {
                                    OnPumpStatusChange result = (OnPumpStatusChange) serializer.Deserialize(reader);
                                    //lock (answers)
                                    //    answers.Add(result);
                                    //if (result.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                                    //PumpRequestCollect(result.OptId, result.PumpNo);
                                    lock (Statuses)
                                    {
                                        Statuses[new Tuple<int, MESSAGE_TYPES>
                                            (result.PumpNo, MESSAGE_TYPES.OnPumpStatusChange)] =  result;


                                        if (!String.IsNullOrEmpty(result.OrderUID))
                                        {
                                            if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>
                                                (result.PumpNo, MESSAGE_TYPES.OnPumpStatusChangeFilling), out msgs))
                                            {
                                                msgs.Add(result);
                                            }
                                            else
                                                Fillings[new Tuple<int, MESSAGE_TYPES>
                                                (result.PumpNo, MESSAGE_TYPES.OnPumpStatusChangeFilling)] = new List<object>{ result };
                                        }
                                    }
                                }
                            }
                            if (xml.Contains("OnSetGradePrices"))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(OnSetGradePrices));
                                using (TextReader reader = new StringReader(xml))
                                {
                                    OnSetGradePrices result = (OnSetGradePrices) serializer.Deserialize(reader);
                                    //lock (answers)
                                    //    answers.Add(result);
                                    lock (Statuses)
                                    {
                                        Statuses[new Tuple<int, MESSAGE_TYPES>
                                            (-1, MESSAGE_TYPES.OnSetGradePrices)] =  result ;
                                    }
                                }
                            }
                            if (xml.Contains("PumpResponse"))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(PumpResponse));
                                using (TextReader reader = new StringReader(xml))
                                {
                                    PumpResponse result = (PumpResponse) serializer.Deserialize(reader);
                                    //lock (answers)
                                    //    answers.Add(result);
                                    lock (Statuses)
                                    {
                                        if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>
                                            (result.PumpObj.PumpNumber, MESSAGE_TYPES.PumpResponse), out msgs))
                                        {
                                            //msgs.Clear();
                                            msgs.Add(result);
                                        }
                                        else
                                            Fillings[new Tuple<int, MESSAGE_TYPES>
                                            (result.PumpObj.PumpNumber, MESSAGE_TYPES.PumpResponse)] = new List<object>() { result };
                                    }
                                }
                            }
                            if (xml.Contains("OnPumpError"))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(OnPumpError));
                                using (TextReader reader = new StringReader(xml))
                                {
                                    OnPumpError result = (OnPumpError)serializer.Deserialize(reader);
                                    //lock (answers)
                                    //    answers.Add(result);
                                    lock (Statuses)
                                    {
                                        Statuses[new Tuple<int, MESSAGE_TYPES>
                                            (result.PumpNo, MESSAGE_TYPES.OnPumpError)] = result;
                                    }
                                }
                            }
                            
                        }
                    }
                    Thread.Sleep(250);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Подключение прервано!"); //соединение было прервано
                    Console.ReadLine();
                    Disconnect();
                }
            }
        }

        public static void Disconnect()
        {
            stream?.Close();//отключение потока
            client?.Close();//отключение клиента

            stream = null;
            client = null;
//            Environment.Exit(0); //завершение процесса
        }

        public static void InitData(int TerminalId, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string initData1 =
"<?xml version=\"1.0\"?>" +
$@"
<InitData>
  <OptId> {TerminalId} </OptId>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <Cashier> {Cashier} </Cashier>
</InitData>";

            SendMessage(initData1, SendTimeout);
        }

        public static void GetGradePrices(int TerminalId, int PumpId, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string GetGrade1 =
"<?xml version=\"1.0\"?>" +
$@"
<GetGradePrices>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <Cashier> {Cashier} </Cashier>
</GetGradePrices>
";

            SendMessage(GetGrade1, SendTimeout);
        }

        private static void InitPump(int TerminalId, int PumpId, int TerminalBlocked, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string initPump1 =
"<?xml version=\"1.0\"?>" +
$@"
<InitPump>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <SetOccupied>
    <OptId> {TerminalBlocked} </OptId>
  </SetOccupied>
  <Cashier> {Cashier} </Cashier>
</InitPump>";

            SendMessage(initPump1, SendTimeout);
        }

        public static bool Init(int TerminalId, int PumpId, int TerminalBlocked, int timeout, int Cashier = 1)
        {
            //answers.RemoveAll(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId);

            InitPump(TerminalId, PumpId, TerminalBlocked, Cashier);

            //while (timeout > 0 && !answers.Any(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId
            //    && ((OnPumpStatusChange)t).Grade == PumpId - 1
            //    ))
            //{
            //    Thread.Sleep(250);
            //    timeout -= 250;
            //}

            //if (timeout <= 0)
            //    return false;

            //var authorizeResponse = (OnPumpStatusChange)answers.Last(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId
            //    && ((OnPumpStatusChange)t).Grade == PumpId - 1
            //    );

            return true;
        }

        private static void SaleDataPresale(int TerminalId, int PumpId, int AllowedNozzles,
            decimal PrePaid, decimal Discount, decimal Volume, PAYMENT_TYPE PaymentType,
            string RNN, int GradeId, string GradeName, int GradePrice,
            string CardNumber, int Cashier)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");

            string saleData1 =
"<?xml version=\"1.0\"?>" +
$@"
<SaleData>
    <OptId>{TerminalId}</OptId>
    <PumpNo>{PumpId}</PumpNo>
    <Date>{date}</Date>
    <Time>{time}</Time>
    <AllowedNozzles>{AllowedNozzles}</AllowedNozzles>
    <PrePaid>{DecimalToRoundF2String(PrePaid)}</PrePaid>" +
//@"    <Notes>
//      <NotesCount>1</NotesCount>
//      <NotesID>100</NotesID>
//    </Notes>" +
$@"
    <PaymentType>{(int)PaymentType}</PaymentType>
    <SaleTotal>{DecimalToRoundF2String(PrePaid + Discount)}</SaleTotal>
    <Volume>{DecimalToRoundF2String(Volume)}</Volume>
    <SaleDiscount>{DecimalToRoundF2String(Discount)}
      <Generic>{DecimalToRoundF2String(Discount)}</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </SaleDiscount>
    <CardNumber>{CardNumber}</CardNumber>
    <PreSale>1</PreSale>
    <Sale>0</Sale>
    <GradeId>{GradeId}</GradeId>
    <GradeName>{GradeName}</GradeName>
    <GradePrice>{GradePrice}</GradePrice>
    <OrderUid>{RNN}</OrderUid>
    <Cashier>{Cashier}</Cashier>
" +
//    "<PrepaymentBarCode BarCodeType=\"CODE128\">2804524148019514262803</PrepaymentBarCode>" +
$@"
</SaleData>
";

            SendMessage(saleData1, SendTimeout);
        }

        public static bool Presale(int TerminalId, int PumpId, int AllowedNozzles,
            decimal PrePaid, decimal Discount, decimal Volume, PAYMENT_TYPE PaymentType,
            string RNN, int GradeId, string GradeName, int GradePrice,
            string CardNumber, int timeout, int Cashier = 1)
        {
            //answers.RemoveAll(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId);

            SaleDataPresale(TerminalId, PumpId, AllowedNozzles, PrePaid, Discount, Volume, 
                PaymentType, RNN, GradeId, GradeName, GradePrice, CardNumber, Cashier);

            Thread.Sleep(1000);

            var r2 = Statuses;
            var r3 = Fillings;
            //while (timeout > 0 && !answers.Any(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId
            //    && ((OnPumpStatusChange)t).Grade == PumpId - 1
            //    ))
            //{
            //    Thread.Sleep(250);
            //    timeout -= 250;
            //}

            //if (timeout <= 0)
            //    return false;

            //var authorizeResponse = (OnPumpStatusChange)answers.Last(t =>
            //    t is OnPumpStatusChange
            //    && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION
            //    && ((OnPumpStatusChange)t).PumpNo == PumpId
            //    && ((OnPumpStatusChange)t).Grade == PumpId - 1
            //    );

            //return authorizeResponse.PumpObj.PumpStatus.Error == null;

            return true;
        }

        public static void SaleDataSale(int TerminalId, int PumpId, int AllowedNozzles,
            decimal PrePaid, decimal Sold, decimal Discount, decimal Volume, decimal SaleLiters, PAYMENT_TYPE PaymentType,
            string RNN, int GradeId, string GradeName, int GradePrice,
            string CardNumber, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string saleData1 =
"<?xml version=\"1.0\"?>" +
$@"
<SaleData>
    <OptId>{TerminalId}</OptId>
    <PumpNo>{PumpId}</PumpNo>
    <Date>{date}</Date>
    <Time>{time}</Time>
    <AllowedNozzles>{AllowedNozzles}</AllowedNozzles>
    <PrePaid>{DecimalToRoundF2String(PrePaid)}</PrePaid>" +
//@"    <Notes>
//      <NotesCount>1</NotesCount>
//      <NotesID>100</NotesID>
//    </Notes>" +
$@"
    <Sold>{DecimalToRoundF2String(Sold)}</Sold>
    <PaymentType>{(int)PaymentType}</PaymentType>
    <SaleTotal>{DecimalToRoundF2String(PrePaid + Discount)}</SaleTotal>
    <Volume>{DecimalToRoundF2String(Volume)}</Volume>
    <SaleDiscount>{DecimalToRoundF2String(Discount)}
      <Generic>{DecimalToRoundF2String(Discount)}</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </SaleDiscount>
    <CardNumber>{CardNumber}</CardNumber>
    <PreSale>0</PreSale>
    <Sale>1</Sale>
    <SaleLiters>{DecimalToRoundF2String(SaleLiters)}</SaleLiters>
    <GradeId>{GradeId}</GradeId>
    <GradeName>{GradeName}</GradeName>
    <GradePrice>{GradePrice}</GradePrice>
    <OrderUid>{RNN}</OrderUid>
    <PostSaleDiscount>{DecimalToRoundF2String(Discount)}
      <Generic>{DecimalToRoundF2String(Discount)}</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </PostSaleDiscount>
" +
//@"    < RTA_RefundAmount>0 </RTA_RefundAmount>
//    <RTA_TelephoneNumber>91612345678 </RTA_TelephoneNumber>
//    <RTA_RefundExport>91612345678</RTA_RefundExport>
//    <RTA_ProviderId>91612345678</RTA_ProviderId>
//    <RTA_ProviderName>MTS</RTA_ProviderName>
//    <RTA_RefundIdNumber>12345678</RTA_RefundIdNumber>
//    <Cashier>1</Cashier>" +
//    "<PrepaymentBarCode BarCodeType=\"CODE128\">2804524148019514262803</PrepaymentBarCode>" +
$@"
</SaleData>
";

            SendMessage(saleData1, SendTimeout);
        }

        private static void PumpRequestAuthorize(int TerminalId, int PumpId, long RequestID, int NozzleAllowed, int NozzleNumber, string RNN, int DeliveryLimit, DELIVERY_UNIT DeliveryUnit)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string pumpRequest1 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Authorize\" OptId=\"{TerminalId}\">\r\n" +
$"     <MessageHeader RequestID=\"{RequestID}\">\r\n" +
$"       <Timestamp Date=\"{date}\" Time=\"{time}\"/>\r\n" +
"     </MessageHeader>\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\">\r\n" +
$"       <Nozzle IsNozzleAllowed=\"{NozzleAllowed}\" NozzleNumber=\"{NozzleNumber}\" />\r\n" +
"     </Pump>\r\n" +
"     <DeliveryData >\r\n" +
$"       <AuthorizationData DeliveryLimit=\"{DeliveryLimit}\" DeliveryUnit=\"{(int)DeliveryUnit}\"/>\r\n" +
$"       <OptTransactionInfo OrderUid=\"{RNN}\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";

            SendMessage(pumpRequest1, SendTimeout);
        }

        public static bool Authorize(int TerminalId, int PumpId, long RequestID, int NozzleAllowed, int NozzleNumber, string RNN, int DeliveryLimit, DELIVERY_UNIT DeliveryUnit, int timeout)
        {
            bool next = false;
            List<object> item  = null;
            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    item?.RemoveAll(t =>
                    string.Compare(((PumpResponse)t).RequestType, "Authorize", StringComparison.Ordinal) == 0);

            PumpRequestAuthorize(TerminalId, PumpId, RequestID, NozzleAllowed, NozzleNumber, RNN, DeliveryLimit, DeliveryUnit);

            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    next = !item.Any(t => 
                    string.Compare(((PumpResponse) t).RequestType, "Authorize", StringComparison.Ordinal) == 0);
            while (timeout > 0 && next)
            {
                Thread.Sleep(250);
                timeout -= 250;
                lock (Statuses)
                    if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                        next = !item.Any(t =>
                        string.Compare(((PumpResponse)t).RequestType, "Authorize", StringComparison.Ordinal) == 0);
            }

            if (timeout <= 0)
                return false;

            PumpResponse authorizeResponse = null;
            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    authorizeResponse = (PumpResponse)item.Last(t =>
                    string.Compare(((PumpResponse)t).RequestType, "Authorize", StringComparison.Ordinal) == 0);

            return authorizeResponse?.PumpObj.PumpStatus.Error == null;
        }

        public static OnPumpStatusChange EndFillingEventWait(int PumpId, string rnn)
        {
            bool next = false;
            List<object> item2 = null;
            object item1 = null;
            //lock (statuses)
            //    if (fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChange), out item))
            //        item.RemoveAll(t =>t is OnPumpStatusChange);

            OnPumpStatusChange оnPumpStatusChanged = null;

            //lock (Statuses)
            //{
            //    if (Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChange), out item1) && item1 != null)
            //        оnPumpStatusChanged = (OnPumpStatusChange)item1;

            //    if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChangeFilling), out item2) && item2 != null)
            //    next = !item2.Any(t => string.CompareOrdinal(((OnPumpStatusChange)t).OrderUID, rnn) == 0
            //                        && ((OnPumpStatusChange) t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
            //    && оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_IDLE
            //    && оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION;
            //}

            do
            {
                Thread.Sleep(250);
                //timeout -= 250;
                lock (Statuses)
                {
                    if (
                        Statuses.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChange),
                            out item1) && item1 != null)
                        оnPumpStatusChanged = (OnPumpStatusChange) item1;

                    if (
                        Fillings.TryGetValue(
                            new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChangeFilling), out item2) &&
                        item2 != null)
                        next = !item2.Any(t => String.CompareOrdinal(((OnPumpStatusChange) t).OrderUID, rnn) == 0
                                               &&
                                               ((OnPumpStatusChange) t).StatusObj ==
                                               PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                               && оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_IDLE
                               && оnPumpStatusChanged?.StatusObj != PUMP_STATUS.PUMP_STATUS_WAITING_AUTHORIZATION;
                }
            } while (next);
            //if (timeout <= 0)
            //    return null;
            ASUDriver.Driver.log.Write($"\t EndFillingEventWait: stat: {оnPumpStatusChanged?.StatusObj} item2.Any: {!item2?.Any(t => String.CompareOrdinal(((OnPumpStatusChange)t).OrderUID, rnn) == 0 && ((OnPumpStatusChange)t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)}");
            OnPumpStatusChange result = null;
            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChangeFilling), out item2) && item2 != null)
                    result = (OnPumpStatusChange)item2.LastOrDefault(t => String.CompareOrdinal(((OnPumpStatusChange)t).OrderUID, rnn) == 0
                        && ((OnPumpStatusChange) t).StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING);
            ASUDriver.Driver.log.Write($"\t EndFillingEventWait: result: {result}");
            return result;
        }

        public static bool Collect(int TerminalId, int PumpId, long RequestID, string RNN, int timeout)
        {
            bool next = false;
            List<object> item = null;
            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    item?.RemoveAll(t => 
                    string.Compare(((PumpResponse)t).RequestType, "Collect", StringComparison.Ordinal) == 0);

            PumpRequestCollect(TerminalId, PumpId, RequestID, RNN);

            //lock (Statuses)
            //    if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
            //        next = !item.Any(t =>
            //        string.Compare(((PumpResponse)t).RequestType, "Collect", StringComparison.Ordinal) == 0);
            do
            {
                Thread.Sleep(250);
                timeout -= 250;
                lock (Statuses)
                    if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                        next = !item.Any(t =>
                                string.Compare(((PumpResponse) t).RequestType, "Collect", StringComparison.Ordinal) == 0);
            }
            while (timeout > 0 && next);

            if (timeout <= 0)
                return false;

            PumpResponse collectResponse = null;
            lock (Statuses)
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    collectResponse = (PumpResponse)item.Last(t =>
                    string.Compare(((PumpResponse)t).RequestType, "Collect", StringComparison.Ordinal) == 0);

            return collectResponse?.PumpObj.PumpStatus.Error == null;
        }

        public static void ClearAllTransactionAnswers(int PumpId, string RNN)
        {
            List<object> item = null;
            lock (Statuses)
            {
                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.PumpResponse), out item))
                    item.Clear();

                if (Fillings.TryGetValue(new Tuple<int, MESSAGE_TYPES>(PumpId, MESSAGE_TYPES.OnPumpStatusChangeFilling), out item))
                    item.RemoveAll(t => String.CompareOrdinal(((OnPumpStatusChange)t).OrderUID, RNN) == 0);
            }
    }

        private static void PumpRequestCollect(int TerminalId, int PumpId, long RequestID, string RNN)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string pumpRequest1 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Collect\" OptId=\"{TerminalId}\">\r\n" +
$"     <MessageHeader RequestID=\"{RequestID}\">\r\n" +
$"       <Timestamp Date=\"{date}\" Time=\"{time}\"/>\r\n" +
"     </MessageHeader>\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\"/>\r\n" +
"     <DeliveryData >\r\n" +
$"       <OptTransactionInfo OrderUid=\"{RNN}\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";

            SendMessage(pumpRequest1, SendTimeout);
        }

        public static void FiscalEventReceipt(int TerminalId, int PumpId, int DocNumberInShift, int DocNumber, int ShiftNumber, decimal SaleTotal, decimal RefundAmount, PAYMENT_TYPE PaymentType, string RNN, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string FiscalEventReceipt1 =
"<?xml version=\"1.0\"?>" +
$@"
<FiscalEventReceipt>
  <OptId>{TerminalId}</OptId>
  <PumpNo>{PumpId}</PumpNo>
  <Date>{date}</Date>
  <Time>{time}</Time>
  <DocNumberInShift>{DocNumberInShift}</DocNumberInShift>
  <DocNumber>{DocNumber}</DocNumber>
  <ShiftNumber>{ShiftNumber}</ShiftNumber>
  <Cashier>{Cashier}</Cashier>
  <SaleTotal>{DecimalToRoundF2String(SaleTotal)}</SaleTotal>
  <PaymentType>{(int)PaymentType}</PaymentType>
  <OrderUid>{RNN}</OrderUid>
" +
//"  <RefundBarCode BarCodeType=\"CODE128\">2804524148019514262803</RefundBarCode>\r\n"+
$@"  <RefundAmount>{RefundAmount}</RefundAmount>
</FiscalEventReceipt>
";

            SendMessage(FiscalEventReceipt1, SendTimeout);
        }

        public static void PumpGetStatus(int TerminalId, int PumpId, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string initData1 =
"<?xml version=\"1.0\"?>" +
$@"
<PumpGetStatus>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <Cashier> {Cashier} </Cashier>
</PumpGetStatus>";

            SendMessage(initData1, SendTimeout);
        }

        public static void PumpStop(int TerminalId, int PumpId, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string initData1 =
"<?xml version=\"1.0\"?>" +
$@"
<PumpStop>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <Cashier> {Cashier} </Cashier>
</PumpStop>";

            SendMessage(initData1, SendTimeout);
        }

        private static bool isValidXml(string candidate)
        {
            try
            {
                XElement.Parse(candidate);
            }
            catch (XmlException) { return false; }
            return true;
        }

        public static PAYMENT_TYPE PaymentCodeToType(int code)
        {
            return code/10 == 1 ? PAYMENT_TYPE.Cash : code/10 == 2 ? PAYMENT_TYPE.FuelCard : PAYMENT_TYPE.Card;
        }

        public static string DecimalToRoundF2String(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.ToEven).ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
