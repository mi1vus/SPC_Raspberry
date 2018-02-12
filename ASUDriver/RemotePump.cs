using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
//using ServioPump_2._34_Driver;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
//using System.Windows.Forms;
using ASUDriver;
using ProjectSummer.Repository;

namespace RemotePump_Driver
{
    [ServiceBehavior()]
    public class RemotePump : IRemotePump
    {
        public Version Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            }
        }
        static Logger log = new Logger("RemotePump");
        public RemotePump()
        {
            TID = DateTime.Now.ToString();
        }
        private static Dictionary<string, RemotePump> clients = new Dictionary<string, RemotePump>();
        public bool SetID(string _TID)
        {
            try
            {               
                log.WriteFormated("Установка TID = {0}", _TID);
                if (clients.ContainsKey(_TID))
                    clients[_TID] = this;
                else
                    clients.Add(_TID, this);

                log.WriteFormated("Установка TID = {0} ОК", _TID);
            }
            catch
            { }
            if (_TID == "Benzuber")
            {
                TID = _TID;
                return true;
            }
            try
            {
                OperationContext context = OperationContext.Current;
                MessageProperties prop = context.IncomingMessageProperties;
                RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
                //получаем IP клиента.
                var config = ConfigMemory.GetConfigMemory("ASUClient");
                string ipAddr = config["terminal_ip"];//endpoint.Address;
                TID = !string.IsNullOrWhiteSpace(_TID)?_TID:ipAddr;
                SmartPumpControlRemote.Shell.AddTerminal(TID, ipAddr);
                log.WriteFormated("Connected: {0}, from: {1}", TID, ipAddr);
                return true;
            }
            catch (Exception ex)
            {
                log.WriteFormated(ex.ToString());
            }
            return false;
        }
        private object busy_flag = new object();
 
        private static object fillingOversLocker = new object();
        private static Dictionary<string,List<OrderInfo>> fillingOvers = new Dictionary<string, List<OrderInfo>>();
        public static void AddFillingOver(long TransNum, OrderInfo Order)
        {
            log.Write("AddFillingOver: " + Order.OrderRRN.ToString()+", TID: "+Order.TID);
            lock (fillingOversLocker)
            {
                try
                {
                    if (!fillingOvers.ContainsKey(Order.TID))
                    {
                        fillingOvers.Add(Order.TID, new List<OrderInfo>());
                    }
                    fillingOvers[Order.TID].Add(Order);
                    try
                    {
                        long tid = 0;
                        int card_t = -2;
                        if (long.TryParse(Order.PumpRRN, out tid) && int.TryParse(Order.DiscontCardType, out card_t)
                            && Order.DiscontCardNO != null && Order.DiscontCardNO != "")
                            Driver.InsertCardInfo(DateTime.Now, Order.DiscontCardNO, card_t, tid);
                    }
                    catch { }

                    lock (Driver.TransMemoryLocker)
                        Driver.TransMemory.Remove(TransNum);
                    Driver.log.Write(
                        $"\t\tFillingOver:\r\n\t\tудаление заказа №{TransNum}\r\n", 2, true);

                }
                catch { }
            }
            try
            {
                log.Write($"Подтверждения налива: {Order.OrderRRN}, сумма: {Order.OverAmount:0.00}р");
                Driver.log.Write($"Подтверждения налива: {Order.OrderRRN}, сумма: {Order.OverAmount:0.00}р", 2, true);
                if (clients.ContainsKey(Order.TID))
                    clients[Order.TID].RaiseFillingOverEvent(Order.OrderRRN, Order.OverAmount);
                else
                {
                    log.Write($"Терминал \"{Order.TID}\" не найден.");
                    Driver.log.Write($"Терминал \"{Order.TID}\" не найден.", 2, true);
                }
            }
            catch (Exception ex)
            {
                log.Write("Ошибка при выплнении подтверждения налива" + ex.ToString());
            }
        }
        public void RaiseFillingOverEvent(string TransactionID, decimal Amount)
        {
            Driver.log.Write($@"RaiseFillingOverEvent: FillingOverEvent = {FillingOverEvent?.ToString() ?? "null"}", 2, true);
            FillingOverEvent?.Invoke(this, new FillingOverEventArgs() {TransactionID = TransactionID, Amount = Amount});
        }

        public class FillingOverEventArgs : EventArgs { public string TransactionID { get; set; } public decimal Amount { get; set; } }
        public event EventHandler<FillingOverEventArgs> FillingOverEvent;

        class UserNameValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
                if (userName != "user" ||
                    password != "password")
                {
                    throw new SecurityTokenException("Authentication failed");
                }
            }
        }
        static ServiceHost host = null;
        public static void StartServer(int port = 1111)
        {
            try
            {
                if (host == null)
                {
                    var uri = new Uri("net.tcp://localhost:"+port);
                    var binding = new NetTcpBinding(SecurityMode.None);
                    binding.Security.Message.ClientCredentialType = MessageCredentialType.None;
                    binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
                    host = new ServiceHost(typeof(RemotePump), uri);

                    host.AddServiceEndpoint(typeof(IRemotePump), binding, "");
                    host.Credentials.UserNameAuthentication.UserNamePasswordValidationMode = System.ServiceModel.Security.UserNamePasswordValidationMode.Custom;
                    host.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new UserNameValidator();

                    host.Open();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                log.Write(ex.ToString(),0,true);
            }
        }
        private string TID;
        public OrderInfo[] GetFillingOvers()
        {
            //Driver.log.Write($@"GetFillingOvers: start[{fillingOvers.Count}]", 2, true);
            var ret = new OrderInfo[0];
            lock (fillingOversLocker)
            {
                //if (fillingOvers.Count > 0)
                //{
                //foreach (var over in fillingOvers)
                //{
                //    Driver.log.Write($@"GetFillingOvers: if[{over.Key}]", 2, true);
                //    foreach (var ord in over.Value)
                //        Driver.log.Write($@"      GetFillingOvers: if[{ord}]", 2, true);
                //}
                try
                {
                    if (fillingOvers[TID]?.Any() ?? false)
                    {
                        // main.log.Write(TID+" fillingOvers.Count=" + fillingOvers[TID].Count);
                        Driver.log.Write(
                            $@"GetFillingOvers: {TID} ret[{fillingOvers[TID].Count}]", 2,
                            true);
                        foreach (var over in fillingOvers[TID])
                        {
                            Driver.log.Write($@"      GetFillingOvers: {TID} ret[{over}]", 2, true);
                        }
                        ret = fillingOvers[TID].ToArray();

                        fillingOvers[TID].Clear();

                    }
                }
                catch 
                {
                }
                //}
            }
            return ret;
        }

        private static Dictionary<int, string> pumpLocked_global = new Dictionary<int, string>();
        public bool LockPump(int PumpNo)
        {
            log.Write("LockPump TID:" + TID);

            if (pumpLocked_global.ContainsKey(PumpNo))
                return (pumpLocked_global[PumpNo] == TID);

           // bool result = false;
#warning Дописать обработку блокировки ТРК
            if (Driver.HoldPump(PumpNo))//main.callback_HoldPump(PumpNo, out result))
            {
                
                //try
                //{
                //    pumpLocked_global.Add(PumpNo, TID);
                //    new Task(() =>
                //        {
                //            System.Threading.Thread.Sleep(300000);
                //            UnlockPump(PumpNo);
                //        }).Start();
                //}
                //catch { }
                return true;//result;
            }
            return false;
        }
        public static string AppInfo { get; set; } = "N/A";
        public string SendCMD(string Cmd, string Data)
        {           
            try
            {
                log.Write("SendCMD TID:" + TID + " CMD:"+Cmd);
                switch (Cmd)
                {
                    case "get_version":
                        return Version.ToString();
                    case "get_app_info":
                        return AppInfo;
                    case "can_recive_filling_info":
                        return "yes";
                    case "get_datetime":
                        return DateTime.Now.ToString();

                    case "get_servio_discount":
                        {
                            //$"{order.Card.Emitent}\\{order.Card.CardNumber}\n{order.ProductCode}\n{order.Price}");
                            var data_array1 = Data?.Split('\n');
                            decimal price = 0;
                            if (data_array1?.Length == 4 && decimal.TryParse(data_array1[3], out price))
                                return price.ToString();
                            else
                                return "error";
                        }
                    case "save_fiscal_check":
                        /// <summary>
                        /// Сохранение напечатанного на ККМ чека
                        /// </summary>
                        /// <param name="BP_SerialNum">Серийный номер ККМ</param>
                        /// <param name="BP_DateTime">Дата и время</param>
                        /// <param name="BP_BillNumber">Номер чека</param>
                        /// <param name="BP_BillType">Тип чека (6 - продажа, 4 - возврат)</param>
                        /// <param name="BP_PayKind">Вид платежа (0 - Наличный расчет, 1 - Плат. картой, 2 - Кредитом)</param>
                        /// <param name="BP_Section">Секция ФР</param>
                        /// <param name="BP_Product">Код продукта продукт</param>
                        /// <param name="BP_Price">Цена</param>
                        /// <param name="BP_Quantity">Кол-во</param>
                        /// <param name="BP_Sum">Сумма</param>
                        /// <param name="PaymentCode">Код основания</param>
                        /// <returns></returns>
                        //public static bool callback_BILL_PRINTED(
                        //    string BP_DeviceName,
                        //    string BP_SerialNum,
                        //    DateTime BP_DateTime,
                        //    int BP_BillNumber,
                        //    short BP_BillType,
                        //    short BP_PayKind,
                        //    short BP_Section,
                        //    int BP_Product,
                        //    double BP_Price,
                        //    double BP_Quantity,
                        //    double BP_Sum,
                        //    int PaymentCode)
                        log.Write($"save_fiscal_check\n**************************************\n{Data}\n");
                        var data_array = Data.Split('\n');
                        //if (main.callback_BILL_PRINTED(TID, data_array[0], DateTime.Parse(data_array[1]), int.Parse(data_array[2]), short.Parse(data_array[3]), short.Parse(data_array[4]),
                        //    short.Parse(data_array[5]), int.Parse(data_array[6]), double.Parse(data_array[7]), double.Parse(data_array[8]), double.Parse(data_array[9]), int.Parse(data_array[10])))
                        //{
                        //    log.Write("save_fiscal_check ок");
                        //    return "ok";
                        //}

                        try
                        {
                            //  string BP_DeviceName = TID;
                            /*
                             *                       
                             *var ret = CoreRef.Pump_Driver.SendCmd("save_fiscal_check",
                                  reciept.CashRegisterSerial + "\n"
                                  + reciept.DateTime.ToString() + "\n"
                                  + reciept.Id + "\n"
                                  + ((reciept.DocType == DocType.Sale) ? 6 : 4).ToString() + "\n"
                                  + ((int)pay_type) + "\n1\n"
                                  + Order.ProductCode + "\n"
                                  + reciept.Price + "\n"
                                  + reciept.Quantity + "\n"
                                  + reciept.Amount + "\n"
                                  + Order.PaymentControllerExCode + "\n"
                                  + reciept.Text.Replace("\r","\\r").Replace("\n","\\n") + "\n"
                                  + reciept.TypeEx);
                             */
                            //reciept.CashRegisterSerial
                            string BP_SerialNum = data_array[0];
                            //reciept.DateTime.ToString() 
                            DateTime BP_DateTime = DateTime.Parse(data_array[1]);
                            //reciept.Id 
                            int BP_BillNumber = int.Parse(data_array[2]);
                            //((reciept.DocType == DocType.Sale) ? 6 : 4).ToString() + "\n"
                            short BP_BillType = short.Parse(data_array[3]);
                            //((int)pay_type) Код типа оплаты (нал платежная карта кредитн карта)
                            short BP_PayKind = short.Parse(data_array[4]);
                            //1 
                            short BP_Section = short.Parse(data_array[5]);
                            //код продукта
                            int BP_Product = int.Parse(data_array[6]);

                            int OS = Environment.OSVersion.Version.Major;
                            if (OS <= 4)
                            {
                                data_array[7] = data_array[7].Replace(',', '.');
                                data_array[8] = data_array[8].Replace(',', '.');
                                data_array[9] = data_array[9].Replace(',', '.');
                            }

                            decimal BP_Price = decimal.Parse(data_array[7]);
                            decimal BP_Quantity = decimal.Parse(data_array[8]);
                            decimal BP_Sum = decimal.Parse(data_array[9]);
                            log.Write($"save_fiscal_check\nBP_Price\n{BP_Price}\nBP_Quantity\n{BP_Quantity}\nBP_Sum\n{BP_Sum}\n");
                            // код типа основания
                            int PaymentCode = int.Parse(data_array[10]);
                            // тип документа
                            string BP_BillTypeText = (BP_BillType == 6) ? "Продажа" : "Возврат";
                            // текст чека
                            var text = data_array[11].Replace("\\r","\r").Replace("\\n","\n");

                            long tran_id = 0;

                            if (data_array.Length >= 13 && data_array[12] != null && data_array[12] != "")
                                BP_BillTypeText = data_array[12];

                            if (data_array.Length >= 14 && !string.IsNullOrWhiteSpace(data_array[13]))
                            {
                                if (!long.TryParse(data_array[13], out tran_id))
                                    tran_id = 0;
                            }

                            int BP_Pump = 0;
                            int BP_ShiftDocNum = 0;
                            int BP_ShiftNum = 0;
                            string BP_RNN = "";
                            if (data_array.Length >= 18)
                            {
                                BP_Pump = int.Parse(data_array[14]);
                                BP_ShiftDocNum = int.Parse(data_array[15]);
                                BP_ShiftNum = int.Parse(data_array[16]);
                                if (!string.IsNullOrWhiteSpace(data_array[17]))
                                    BP_RNN = data_array[17];
                            }

                            decimal BP_PrePrice = 0;
                            decimal BP_PreQuantity = 0;
                            decimal BP_PreSum = 0;

                            if (data_array.Length >= 21)
                            {
                                if (OS <= 4)
                                {
                                    data_array[18] = data_array[18].Replace(',', '.');
                                    data_array[19] = data_array[19].Replace(',', '.');
                                    data_array[20] = data_array[20].Replace(',', '.');
                                }

                                BP_PrePrice = decimal.Parse(data_array[18]);
                                BP_PreQuantity = decimal.Parse(data_array[19]);
                                BP_PreSum = decimal.Parse(data_array[20]);
                                log.Write($"save_fiscal_check\nBP_PrePrice\n{BP_PrePrice}\nBP_PreQuantity\n{BP_PreQuantity}\nBP_PreSum\n{BP_PreSum}\n");
                            }

                            if (BP_BillTypeText == "Аванс")
                                BP_Product = 0;



                            if (Driver.SaveReciept(text, BP_DateTime, TID, BP_SerialNum, BP_BillNumber, 0, BP_Sum, BP_PrePrice, BP_PreQuantity, BP_PreSum,
                                BP_Price, BP_Quantity, (BP_Product == 0), BP_BillTypeText, BP_BillType,
                                BP_PayKind, tran_id, BP_Pump, BP_ShiftDocNum, BP_ShiftNum, BP_RNN, BP_Product: BP_Product))
                            {
                                log.Write("save_fiscal_check ок");
                                return "ok";
                            }
                        }
                        catch { }
                        break;
                    case "save_reciept":
                        //public static bool callback_DOC_PRINTED(
                        //string DP_DeviceName,
                        //DateTime DP_DateTime,
                        //int DP_DocType,
                        //string DP_DocKind,
                        //string DP_DockImage)
                        data_array = Data.Split('\n');
                        //if (main.callback_DOC_PRINTED(TID, DateTime.Parse(data_array[1]), int.Parse(data_array[2]), data_array[3], data_array[0].Replace("\\n","\r\n")))
                        //{
                        //    log.Write("save_reciept ок");
                        //    return "ok";
                        //}
                        int BP_BillNumber1 = int.Parse(data_array[4]);
                        string BP_SerialNum1 = data_array[5];
                        DateTime DP_DateTime = DateTime.Parse(data_array[1]);
                        int DP_DocType = int.Parse(data_array[2]);
                        string DP_DocKind = data_array[3];
                        string DP_DockImage = data_array[0].Replace("\\n", "\r\n");
    
                        if (Driver.SaveReciept(DP_DockImage, DP_DateTime, TID, BP_SerialNum1, BP_BillNumber1, DP_DocType, DocKind: DP_DocKind))
                        {
                            return "ok";
                        }
                        break;
                    case "show_error":
                        new System.Threading.Thread(delegate ()
                        {
                            try
                            {
                                log.Write($"{TID} _ {Data}");
                                //MessageBox.Show(Data, TID, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                            }
                            catch { }
                        }).Start();
                        return "ok";
                    case "recalc_filling_over":
                        if (Data != null)
                        {
                            var data_lines = Data.Split('\n');
                            if (data_lines.Length > 2)
                            {
                              
                                
                                try
                                {
                                    bool result = false;
                                    var num_pump = int.Parse(data_lines[0]);
                                    var trans_id = long.Parse(data_lines[1]);
                                    if (data_lines.Length >= 4)
                                    {
                                        try
                                        {
                                            var FillingOverAmount = decimal.Parse(data_lines[2]);
                                            var Price = decimal.Parse(data_lines[3]);
                                            var Discont = (data_lines.Length > 4) ? decimal.Parse(data_lines[4]) : 0;
                                            result = (Driver.UpdateFillingOver(FillingOverAmount, Price, trans_id, Discont));
                                        }
                                        catch (Exception ex)
                                        {
                                            return ex.ToString();
                                        }
                                    }
                                    else
                                        result = true;
                                    return result ? "ok" : "UpdateFillingOver error";
                                }
                                catch (Exception ex)
                                {
                                    return ex.ToString();
                                }                               

                            }
                            else
                                return "data_lines.Length<4";
                        }
                        else
                            return "Data is null";
                    case "insert_card":
                        try
                        {
                            int counter = 0;
                            var data_lines = Data.Split('\n');
                            long tid = 0;
                            if (data_lines.Length > 1 && long.TryParse(data_lines[0].Trim(), out tid))
                            {
                                for (int z = 1; z < data_lines.Length; z++)
                                {
                                    int em = 0;
                                    int index = data_lines[z].IndexOf("=");
                                    if (index > 0 && int.TryParse(data_lines[z].Substring(0, index), out em))
                                    {
                                        Driver.InsertCardInfo(DateTime.Now, data_lines[z].Substring(index + 1, data_lines[z].Length - index - 1), em, tid);
                                        counter++;
                                    }
                                }
                                return counter.ToString()+" cards loaded";
                            }
                            else
                                return "tid error";
                        }
                        catch { }                           
                        break;
                   
                        //
                }
            }
            catch (Exception ex)
            {
                try
                {
                    log.Write("SendCMD: " + Cmd + ", Data: " + Data + ", error: " + ex.ToString());
                }
                catch { }
            }
            return "";
        }
        public ProductInformation GetProduct(int code)
        {
            var prods = GetProducts();
            foreach (var p in prods)
                if (p.Code == code)
                    return p;
            return default(ProductInformation);
        }

        public bool UnlockPump(int PumpNo)
        {
            if (pumpLocked_global.ContainsKey(PumpNo) && pumpLocked_global[PumpNo] != TID)
                return false;

       //     bool result = false;
#warning Дописать обработку разблокировки ТРК
            if (Driver.HoldPump(PumpNo, true))//true)//main.callback_ReleasePump(PumpNo))
            {
                try
                {
                    pumpLocked_global.Remove(PumpNo);
                }
                catch { }
                return true;
            }
            return false;
        }
        public OrderInfo GetDoseInfo(string OrderRRN)
        {
            
            lock (Driver.TransMemoryLocker)
            {
                var trans = (from t in Driver.TransMemory where t.Value.OrderRRN == OrderRRN && t.Value.TID == TID select t.Value).ToArray();
                if (trans.Length > 0)
                    return trans[0];
            }
            lock (fillingOversLocker)
            {
                var tmp = (from t in fillingOvers[TID] where t.OrderRRN == OrderRRN select t).SingleOrDefault();
                Driver.log.Write(
$"\t\rGetDoseInfo:\r\n\t\tOrderRRN: {OrderRRN} OverAmount: {tmp.OverAmount}\r\n", 2, true);

                return tmp;
            }
        }
        public bool SetDose(OrderInfo Order)
        {
            if (pumpLocked_global.ContainsKey(Order.PumpNo) && pumpLocked_global[Order.PumpNo] != TID)
                return false;
            return Driver.SetDose(Order)>0;
        }
        public bool CancelDose(OrderInfo Order)
        {
            return Driver.CancelDose(Order.OrderRRN);// main.callback_CANCEL_TRANS(Order.PumpNo);
        }
        public bool CancelDose(string OrderRRN)
        {
            return Driver.CancelDose(OrderRRN);// main.callback_CANCEL_TRANS(Order.PumpNo);
        }
        public ProductInformation[] GetProducts()
        {            
            List<ProductInformation> ret = new List<ProductInformation>();
            var prods =  XmlPumpClient.Fuels;
            foreach (var prod in prods)
            {
                ret.Add(new ProductInformation() { Name = prod.Value.Name, BasePrice = prod.Value.Price, Code = prod.Value.Id});
            }
            return ret.ToArray();
        }

        
        struct PumpMemory
        {
            public PumpInformation Value;
            public DateTime LastUpdate;
        }
        private static Dictionary<int, PumpMemory> pumpInformationMem = new Dictionary<int, PumpMemory>();

        public PumpInformation GetPumpInformation(int No)
        {
            lock (pumpInformationMem)
            {
                if (pumpInformationMem.ContainsKey(No) && pumpInformationMem[No].LastUpdate.AddMilliseconds(1000) > DateTime.Now)
                    return pumpInformationMem[No].Value;
            }
            //#warning Дописать обработку получения активного топлива
            lock ( XmlPumpClient.PumpsLocker)
                if (! XmlPumpClient.Pumps.ContainsKey(No))
                    return new PumpInformation();
          //  log.Write("GetPumpInformation" + No.ToString());

            Dictionary<string, FuelInfo> fuels;
            lock ( XmlPumpClient.PumpsLocker)
                fuels =  XmlPumpClient.Pumps[No].Fuels;
            

          //  main.FuelListItem[] fuels;
            PumpInformation ret = new PumpInformation();
            List<ProductInformation> prodInfo = new List<ProductInformation>();
            var pump_status = Driver.GetDose(No);
            //log.Write("callback_GetPumpStatus: " + pump_status);
           
        //    log.WriteFormated("Status: {0}, ActiveFuel: {1}", Status, ActiveFuel);
            //log.Write("callback_GetEnumFuelsOnPump: " + main.callback_GetEnumFuelsOnPump(No, out fuels));
            //log.WriteFormated("fuels.Length: {0}", fuels.Count);
          //  var products = GetProducts();
          //  log.WriteFormated("GetProducts.Length: {0}", products.Length);
            foreach (var fuel in fuels.Where(fa => fa.Value.Active))
            {
                prodInfo.Add(new ProductInformation() { Name = fuel.Value.Name, BasePrice = fuel.Value.Price, Code = fuel.Value.Id });
                log.Write("Products:"+ fuel.Value.ToString());
            }
            log.Write($"pump_status.DispStatus: {pump_status.DispStatus}");

            ret.No = No;
            ret.SelectedProduct = pump_status.UpFuel;
            switch(pump_status.DispStatus)
            {
                
                
                case 0:
                case 4:
                    if (pump_status.TransID == -1)
                        ret.State = PumpState.Online;
                    else
                        ret.State = PumpState.Filling;
                    break;
                case 3:
                    ret.State = PumpState.Filling;
                    if (pump_status.TransID > 0)
                    {
                        lock (Driver.TransMemoryLocker)
                        {
                            ret.TransactionID = Driver.TransMemory.SingleOrDefault(i=>i.Key==pump_status.TransID).Value.OrderRRN??"";
                        }

                      //  log.Write($"{ret.PreselQuantity:0.00}/{ret.FillingQuantity:0.00}/{ret.FillingPrice:0.00}");
                        //if (!string.IsNullOrWhiteSpace(ret.TransactionID))
                        //{
                      
                        //}

                    }
                    try
                    {
                        ret.FillingQuantity = (decimal)pump_status.FillingVolume;
                        ret.FillingAmount = (decimal)pump_status.FillingSum;
                        ret.FillingPrice = (decimal)pump_status.FillingPrice;
                        ret.PreselQuantity = (pump_status.PreselMode == 0) ? (decimal)pump_status.PreselDose : (((decimal)pump_status.PreselDose) / ret.FillingPrice);
                    }
                    catch { }
                    break;
                default:
                case 1: //ТРК заблокирована
                case 10: //Доза установлена
                    ret.State = PumpState.Busy; break;
            }

            
            ret.ProductInformation = prodInfo.ToArray();
            lock (pumpInformationMem)
            {
                pumpInformationMem[No] = new PumpMemory() { Value = ret, LastUpdate = DateTime.Now };
            }
            log.WriteFormated(" ret.ProductInformation.Length: {0}", ret.ProductInformation.Length);
            return ret;
        }
    }
}
