using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SPC_Raspberry
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

    public class XmlPumpClient
    {
        public const int BuffSize = 65536;
        public static Socket socket; 
        public static TcpClient client;
        public static NetworkStream stream;

        public static Encoding Enc = Encoding.UTF8;

        public static List<object> answers;

        public static void StartClient(string b_hostB2, int i_port)
        {
            client = new TcpClient();
            try
            {
                client.Connect(b_hostB2, i_port); //подключение клиента
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

        public static void StartSocket(byte[] b_hostB2, int i_port)
        {
            if (socket != null)
                return;

            IPHostEntry hostEntry = null;

            IPEndPoint ipe = new IPEndPoint(new IPAddress(b_hostB2), i_port);
            Socket tempSocket =
                new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            tempSocket.Connect(ipe);

            if (tempSocket.Connected)
            {
                socket = tempSocket;
                answers = new List<object>();
                //Thread receiveThread = new Thread(new ThreadStart(ReceiveMessageSocket));
                //receiveThread.Start(); //старт потока
            }
        }

        private static void SendMessageClient(string message)
        {
            //Console.WriteLine("Введите сообщение: ");

            //while (true)
            //{
            //string message = Console.ReadLine();
            byte[] data = Encoding.ASCII.GetBytes(message);
            if (stream != null)
                stream.Write(data, 0, data.Length);
            //}
        }

        private static void SendMessageSocket(string message, int timeout)
        {
            //Console.WriteLine("Введите сообщение: ");

            //while (true)
            //{
            //string message = Console.ReadLine();
            byte[] bytesToSent = Enc.GetBytes(message);

            if (socket != null)
            {
                // Send request to the server.
                socket.Send(bytesToSent, bytesToSent.Length, 0);
                Thread.Sleep(timeout);
                // Receive the server home page content.
                Byte[] bytesReceived = new Byte[65536];
                int bytes = 0;
                StringBuilder builder = new StringBuilder();

                // The following will block until te page is transmitted.
                while (socket.Available != 0)
                {
                    bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                    if (bytes != 0)
                        builder.Append(Enc.GetString(bytesReceived, 0, bytes));
                } 

                if (builder.Length != 0)
                {
                    string[] ansverStrings = builder.ToString().Split(new[] { "<?xml version=\"1.0\"?>" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var xml in ansverStrings)
                    {
                        if (!isValidXml(xml))
                            continue;

                        if (xml.Contains("OnDataInit"))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(OnDataInit));
                            using (TextReader reader = new StringReader(xml))
                            {
                                OnDataInit result = (OnDataInit) serializer.Deserialize(reader);
                                answers.Add(result);
                            }
                        }
                        if (xml.Contains("OnPumpStatusChange"))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(OnPumpStatusChange));
                            using (TextReader reader = new StringReader(xml))
                            {
                                OnPumpStatusChange result = (OnPumpStatusChange)serializer.Deserialize(reader);
                                answers.Add(result);
                                //if (result.StatusObj == PUMP_STATUS.PUMP_STATUS_WAITING_COLLECTING)
                                    //PumpRequestCollect(result.OptId, result.PumpNo);

                            }
                        }
                        if (xml.Contains("OnSetGradePrices"))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(OnSetGradePrices));
                            using (TextReader reader = new StringReader(xml))
                            {
                                OnSetGradePrices result = (OnSetGradePrices)serializer.Deserialize(reader);
                                answers.Add(result);
                            }
                        }
                        if (xml.Contains("PumpResponse"))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(PumpResponse));
                            using (TextReader reader = new StringReader(xml))
                            {
                                PumpResponse result = (PumpResponse)serializer.Deserialize(reader);
                                answers.Add(result);
                            }
                        }
                    }
                }
            }
        }

        public static void SendMessage(string message, int timeout = 1000)
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
            while (true)
            {
                try
                {
                    // Receive the server home page content.
                    Byte[] bytesReceived = new Byte[BuffSize];
                    int bytes = 0;
                    StringBuilder builder = new StringBuilder();

                    // The following will block until te page is transmitted.
                    do
                    {
                        if (socket.Available != 0)
                        {
                            bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                            if (bytes != 0)
                                builder.Append(Encoding.ASCII.GetString(bytesReceived, 0, bytes));
                        }
                        //else
                        //    bytes = 0;

                    } while (socket.Available != 0);

                    if (builder.Length != 0)
                    {
                        string[] ansverStrings = builder.ToString().Split(new[] { "<?xml version=\"1.0\"?>" }, StringSplitOptions.RemoveEmptyEntries);
                        XmlSerializer serializer = new XmlSerializer(typeof(OnDataInit));
                        using (TextReader reader = new StringReader(ansverStrings[0]))
                        {
                            OnDataInit result = (OnDataInit)serializer.Deserialize(reader);
                        }
                    }
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
            if (stream != null)
                stream.Close();//отключение потока
            if (client != null)
                client.Close();//отключение клиента

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
            string initData2 =
"<?xml version=\"1.0\"?>" +
$@"
<InitData>
  <OptId> {TerminalId} </OptId>
</InitData>";
            SendMessage(initData1);
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
            string GetGrade2 =
"<?xml version=\"1.0\"?>" +
$@"
<GetGradePrices>
  <OptId> {TerminalId} </OptId>
</GetGradePrices>
";
            SendMessage(GetGrade1);
        }

        public static void InitPump(int TerminalId, int PumpId, int TerminalBlocked, int Cashier = 1)
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
            string initPump2 =
"<?xml version=\"1.0\"?>" +
$@"
<InitPump>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <SetOccupied>
    <OptId> {TerminalBlocked} </OptId>
  </SetOccupied>
</InitPump>";
            var doc = 
                new XDocument(new XDeclaration("1.0", "UTF-8", null), 
                    new XElement("InitPump",
                            new XElement("OptId", TerminalId),
                            new XElement("PumpNo", PumpId),
                            new XElement("SetOccupied",
                                new XElement("OptId", TerminalBlocked)
                        )));

            var initPump3 = doc.Declaration.ToString() + doc.ToString();

            SendMessage(initPump3, 2000);
        }

        public static void SaleDataPresale(int TerminalId, int PumpId)
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
    <AllowedNozzles>15</AllowedNozzles>
    <PrePaid>100</PrePaid>
    <Notes>
      <NotesCount>1</NotesCount>
      <NotesID>100</NotesID>
    </Notes>
    <PaymentType>1</PaymentType>
    <SaleTotal>100</SaleTotal>
    <Volume>3.23</Volume>
    <SaleDiscount>0
      <Generic>0</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </SaleDiscount>
    <CardNumber>1234567890123456</CardNumber>
    <PreSale>1</PreSale>
    <Sale>0</Sale>
    <GradeId>1</GradeId>
    <GradeName>А-92</GradeName>
    <GradePrice>3100</GradePrice>
    <OrderUid>1b85d34d-93ca-45e2-bde3-3290a664fb14</OrderUid>
    <Cashier>1</Cashier>
" +
    "<PrepaymentBarCode BarCodeType=\"CODE128\">2804524148019514262803</PrepaymentBarCode>" +
$@"
</SaleData>
";

            string saleData2 =
"<?xml version=\"1.0\"?>" +
$@"
";
            SendMessage(saleData1);
        }

        public static void SaleDataSale(int TerminalId, int PumpId)
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
    <AllowedNozzles>15</AllowedNozzles>
    <PrePaid>100</PrePaid>
    <Notes>
      <NotesCount>1</NotesCount>
      <NotesID>100</NotesID>
    </Notes>
    <Sold>100</Sold>
    <PaymentType>0</PaymentType>
    <SaleTotal>100</SaleTotal>
    <Volume>3.23</Volume>
    <SaleDiscount>0
      <Generic>0</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </SaleDiscount>
    <CardNumber>1234567890123456</CardNumber>
    <PreSale>0</PreSale>
    <Sale>1</Sale>
    <SaleLiters>3.23</SaleLiters>
    <GradeId>1</GradeId>
    <GradeName>А-92</GradeName>
    <GradePrice>3100</GradePrice>
    <OrderUid>c6d4e4d0-a048-416d-a998-eb69c7587a44</OrderUid>
    <PostSaleDiscount>0
      <Generic>0</Generic>
      <Loyalty>0</Loyalty>
      <Rounding>0</Rounding>
    </PostSaleDiscount>
    <RTA_RefundAmount>0 </RTA_RefundAmount>
    <RTA_TelephoneNumber>91612345678 </RTA_TelephoneNumber>
    <RTA_RefundExport>91612345678</RTA_RefundExport>
    <RTA_ProviderId>91612345678</RTA_ProviderId>
    <RTA_ProviderName>MTS</RTA_ProviderName>
    <RTA_RefundIdNumber>12345678</RTA_RefundIdNumber>
    <Cashier>1</Cashier>
" +
    "<PrepaymentBarCode BarCodeType=\"CODE128\">2804524148019514262803</PrepaymentBarCode>" +
$@"
</SaleData>
";

            string saleData2 =
"<?xml version=\"1.0\"?>" +
$@"
";
            SendMessage(saleData1);
        }

        public static void PumpRequestAuthorize(int TerminalId, int PumpId)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string pumpRequest1 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Authorize\" OptId=\"{TerminalId}\">\r\n" +
"     <MessageHeader RequestID=\"101304\">\r\n" +
$"       <Timestamp Date=\"{date}\" Time=\"{time}\"/>\r\n" +
"     </MessageHeader>\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\">\r\n" +
"       <Nozzle IsNozzleAllowed=\"15\" NozzleNumber=\"1\" />\r\n" +
"     </Pump>\r\n" +
"     <DeliveryData >\r\n" +
"       <AuthorizationData DeliveryLimit=\"10000\" DeliveryUnit=\"1\"/>\r\n" +
"       <OptTransactionInfo OrderUid=\"c6d4e4d0-a048-416d-a998-eb69c7587a44\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";


            string pumpRequest2 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Authorize\" OptId=\"{TerminalId}\">\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\">\r\n" +
"       <Nozzle IsNozzleAllowed=\"15\" NozzleNumber=\"1\" />\r\n" +
"     </Pump>\r\n" +
"     <DeliveryData >\r\n" +
"       <AuthorizationData DeliveryLimit=\"10000\" DeliveryUnit=\"1\"/>\r\n" +
"       <OptTransactionInfo OrderUid=\"c6d4e4d0-a048-416d-a998-eb69c7587a44\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";
            SendMessage(pumpRequest2);
        }

        public static void PumpRequestCollect(int TerminalId, int PumpId)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string pumpRequest1 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Collect\" OptId=\"{TerminalId}\">\r\n" +
"     <MessageHeader RequestID=\"113683\">\r\n" +
$"       <Timestamp Date=\"{date}\" Time=\"{time}\"/>\r\n" +
"     </MessageHeader>\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\"/>\r\n" +
"     <DeliveryData >\r\n" +
"       <OptTransactionInfo OrderUid=\"c6d4e4d0-a048-416d-a998-eb69c7587a44\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";


            string pumpRequest2 =
"<?xml version=\"1.0\"?>\r\n" +
$"<PumpRequest RequestType=\"Collect\" OptId=\"{TerminalId}\">\r\n" +
"     <MessageHeader RequestID=\"113683\">\r\n" +
$"       <Timestamp Date=\"{date}\" Time=\"{time}\"/>\r\n" +
"     </MessageHeader>\r\n" +
$"    <Pump PumpNumber=\"{PumpId}\"/>\r\n" +
"     <DeliveryData >\r\n" +
"       <OptTransactionInfo OrderUid=\"c6d4e4d0-a048-416d-a998-eb69c7587a44\"/>\r\n" +
"     </DeliveryData>\r\n" +
"</PumpRequest>\r\n";
            SendMessage(pumpRequest2);
        }

        public static void FiscalEventReceipt(int TerminalId, int PumpId, int Cashier = 1)
        {
            var date = DateTime.Now.ToString("yyyy.MM.dd");
            var time = DateTime.Now.ToString("hh:mm:ss");
            string FiscalEventReceipt1 =
"<?xml version=\"1.0\"?>" +
$@"
<FiscalEventReceipt>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <DocNumberInShift>3</DocNumberInShift>
  <DocNumber>3</DocNumber>
  <ShiftNumber>1</ShiftNumber>
  <Cashier>{Cashier}</Cashier>
  <SaleTotal>100</SaleTotal>
  <PaymentType>0</PaymentType>
  <OrderUid>c6d4e4d0-a048-416d-a998-eb69c7587a44</OrderUid>
" +
"  <RefundBarCode BarCodeType=\"CODE128\">2804524148019514262803</RefundBarCode>\r\n"+
@"  <RefundAmount>0<RefundAmount>
</FiscalEventReceipt>
";
            string FiscalEventReceipt2 =
"<?xml version=\"1.0\"?>" +
$@"
<FiscalEventReceipt>
  <OptId> {TerminalId} </OptId>
  <PumpNo> {PumpId} </PumpNo>
  <Date> {date} </Date>
  <Time> {time} </Time>
  <DocNumberInShift>3</DocNumberInShift>
  <DocNumber>3</DocNumber>
  <ShiftNumber>1</ShiftNumber>
  <SaleTotal>100</SaleTotal>
  <PaymentType>0</PaymentType>
  <OrderUid>c6d4e4d0-a048-416d-a998-eb69c7587a44</OrderUid>
</FiscalEventReceipt>
";
            SendMessage(FiscalEventReceipt2);
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

            SendMessage(initData1);
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
    }
}
