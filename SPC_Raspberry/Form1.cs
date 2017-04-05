using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace SPC_Raspberry
{
    public partial class Form1 : Form
    {

        /// <summary>
        /// Класс для работы с терминалом или эмулятором терминала 
        /// системы LifeStyleMarketing
        /// </summary>
        public class LifeStyle
        {
            ProjectSummer.Repository.ConfigMemory conf = ProjectSummer.Repository.ConfigMemory.GetConfigMemory("LifeStyleAPI");

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            /// <summary>
            /// Конструктор объекта обмена с терминалом.
            /// </summary>
            /// <param name="logPath">Путь до папки, в которую необходимо писать лог файлы</param>
            /// <param name="logEnable">Если "true" - логирование включено.</param>
            /// <param name="debug">Если "true" - ведение отладочной информации.</param>
            /// <param name="lowLevelLog">Если "true" - логировать низкоуровневый обмен.</param>
            public LifeStyle(string logPath, bool logEnable, bool debug, bool lowLevelLog)
            {
                log = new ProjectSummer.Repository.Logger(logPath);
                log.LogEnable = logEnable;
                log.LogLevel = (debug ? 100 : 0);
                obmen = new ProjectSummer.Repository.Logger(logPath);
                obmen.LogEnable = lowLevelLog;

            }

            private int connectionTimeout = 10;
            private bool goldCard = false;
            private int goldCardDisc = 600;

            private bool silverCard = false;
            private int silverCardDisc = 500;

            private bool platinumCard = false;
            private int platinumCardDisc = 500;
            string cardNum = "0";

            /// <summary>
            /// Таймаут ожидания подключения терминала в секундах. 
            /// По умолчанию = 5 сек.
            /// </summary>
            public int ConnectionTimeout
            {
                get
                {
                    return connectionTimeout / 2;
                }
                set
                {
                    connectionTimeout = value * 2;
                }
            }


            #region Перечисления
            /// <summary>
            /// Тип операции
            /// </summary>
            public enum OpType
            {
                /// <summary>
                /// Продажа
                /// </summary>
                Debit = 1,
                /// <summary>
                /// Возврат
                /// </summary>
                Return = 2,
            };

            /// <summary>
            /// Служебные символы протокола.
            /// </summary>
            private enum STX : byte
            {
                STX = 0x02,
                ETX = 0x03,
                EOT = 0x04,
                ENQ = 0x05,
                ACK = 0x06,
                BEL = 0x07,
                NAK = 0x15,
                ETB = 0x17,
            };
            #endregion

            #region Глобальные переменные
            private byte[] memReciveData = new byte[0];
            private int timeout = 10000;
            private int LocalPort;
            private ProjectSummer.Repository.Logger log;
            private ProjectSummer.Repository.Logger obmen;
            TcpListener Listener;
            Socket ClientSock;
            /// <summary>
            /// Проводить операцию, даже если скидка равно 0. 
            /// </summary>
            public bool ZeroDiscontEnable = false;
            /// <summary>
            /// Принудительное завершение обмена с терминалом.
            /// </summary>
            public bool cancel = false;
            #endregion

            /// <summary>
            /// Закрытиие сервера обмена с терминалом
            /// </summary>
            public void Close()
            {
                log.Write("Закрытиие сервера обмена с терминалом", 100);
                try
                {
                    Listener.Stop();
                }
                catch { }
                try
                {
                    ClientSock.Close();
                }
                catch { }
                return;
            }

            /// <summary>
            /// Принудительное завершение обмена с терминалом.
            /// </summary>
            public void CancelDebit()
            {
                cancel = true;
            }

            /// <summary>
            /// Функция выполнения дебита клиента.
            /// </summary>
            /// <param name="port">Порт через который происходит обмен с терминалом</param>
            /// <param name="Price">Цена</param>
            /// <param name="Amount">Сумма</param>
            /// <param name="Quantity">Кол-во</param>
            /// <param name="GoodCode">Код продукта в системе BonusPlus</param>
            /// <param name="GoodName">Наименование продукта. 
            /// Для получения скидки, без проведения операции дебита, необходимо передать "discount".</param>
            /// <param name="withDiscount">Фиксирование суммы заказа</param>
            /// <param name="BpRRN">Возвращаемый параметр. Номер транзакции.</param>
            /// <param name="Discount">Возвращаемый параметр. Скидка.</param>
            /// <param name="RespCode">Возвращаемый параметр.Код ответа от терминала.</param>
            /// <param name="ScreenMsg">Возвращаемый параметр.Текст экранного сообщения терминала.</param>
            /// <returns>Результат выполнения функции</returns>
            public bool StartDebit(int port, uint Price, uint Amount, uint Quantity,
                string GoodCode, string GoodName, bool withDiscount, out byte[] BpRRN, out int Discount,
                out int RespCode, out string ScreenMsg)
            {

                if ((GoodName == "discount") || (GoodCode == "discount"))
                {
                    GoodName = "discount";
                    GoodCode = "discount";
                }
                else
                {
                    GoodName = "1";
                    GoodCode = "1";
                }

                /////////////////////////////////////////////
                withDiscount = true;/////////////////////////
                                    /////////////////////////////////////////////

                cancel = false;
                RespCode = 0;
                ScreenMsg = ".";
                Discount = 0;
                if (goldCard)
                {
                    Discount = goldCardDisc;
                    string disc_str = Discount.ToString();
                    if (disc_str.Length > 2)
                        disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                    ScreenMsg = string.Format("\r\nERGO LOYALTY GOLD\r\nТекущий % скидки: {0}", disc_str);
                }
                if (silverCard)
                {
                    Discount = silverCardDisc;
                    string disc_str = Discount.ToString();
                    if (disc_str.Length > 2)
                        disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                    ScreenMsg = string.Format("\r\nERGO LOYALTY SILVER\r\nТекущий % скидки: {0}", disc_str);
                }
                if (platinumCard)
                {
                    Discount = platinumCardDisc;
                    string disc_str = Discount.ToString();
                    if (disc_str.Length > 2)
                        disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                    ScreenMsg = string.Format("\r\nERGO LOYALTY PLATINUM\r\nТекущий % скидки: {0}", disc_str);
                }

                log.Write("Запуск сервера обмена с терминалом для выполнения дебита", 100);

                LocalPort = port;
                Listener = new TcpListener(LocalPort);
                Listener.Start();
                log.Write(string.Format("Открыт порт: {0}", port), 100);
                BpRRN = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

                try
                {
                    int z = 0;
                    for (z = 0; (!Listener.Pending()); z++)
                    {
                        Thread.Sleep(500);
                        if (z == connectionTimeout || cancel)
                        {
                            log.Write("Очередь запросов на подключение к серверу пуста.", 100);
                            return false;
                        }
                    }
                    log.Write("Ожидание входящего подключения", 100);
                    ClientSock = Listener.AcceptSocket();

                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    log.Write("Ошибка ожидания входящего подключения", 100);
                    return false;
                }

                if (ClientSock.Connected)
                {
                    log.Write("Подключен клиент", 100);
                    try
                    {
                        log.Write("Выполнение дебита клиента.");
                        log.Write(string.Format("Код продукта: {0}, Наименование продукта: {1}.",
                            GoodCode, GoodName));
                        log.Write(string.Format("Кол-во: {0}, Цена: {1}, Сумма: {2}.",
                            Quantity, Price, Amount));

                        bool res = Debit(Price, Amount, Quantity, GoodCode, GoodName, withDiscount,
                            out BpRRN, out Discount, out RespCode, out ScreenMsg);
                        if (goldCard)
                        {
                            Discount = goldCardDisc;
                            string disc_str = Discount.ToString();
                            if (disc_str.Length > 2)
                                disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                            ScreenMsg = string.Format("\r\nERGO LOYALTY GOLD\r\nТекущий % скидки: {0}", disc_str);

                        }
                        if (silverCard)
                        {
                            Discount = silverCardDisc;
                            string disc_str = Discount.ToString();
                            if (disc_str.Length > 2)
                                disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                            ScreenMsg = string.Format("\r\nERGO LOYALTY SILVER\r\nТекущий % скидки: {0}", disc_str);
                        }
                        if (platinumCard)
                        {
                            Discount = platinumCardDisc;
                            string disc_str = Discount.ToString();
                            if (disc_str.Length > 2)
                                disc_str = disc_str.Insert(disc_str.Length - 2, ".");
                            ScreenMsg = string.Format("\r\nERGO LOYALTY PLATINUM\r\nТекущий % скидки: {0}", disc_str);
                        }


                        try
                        {
                            ClientSock.Close();
                            Listener.Stop();
                        }
                        catch { }

                        if (conf["InfoAfterPayment"] == "true" && GoodCode != "discount" && !goldCard && !silverCard && !platinumCard)
                        {
                            Thread.Sleep(2000);
                            byte[] BpRRN_Post;
                            int Discount_Post = 0;
                            int RespCode_Post = 0;
                            string ScreenMsg_Post = "";
                            SetCardNum(cardNum);
                            if (StartDebit(port, Price, Amount, Quantity, "discount", "discount", withDiscount, out BpRRN_Post,
                                out Discount_Post, out RespCode_Post, out ScreenMsg_Post))
                            {
                                ScreenMsg = "ИНФО ДО ПЛАТЕЖА:\r\n" + ScreenMsg
                                    + "\r\nИНФО ПОСЛЕ ПЛАТЕЖА:" + ScreenMsg_Post;
                            }
                        }

                        goldCard = false;
                        silverCard = false;
                        platinumCard = false;
                        return res;
                    }
                    catch (Exception e)
                    {
                        log.Write(e.ToString(), 100);
                        log.Write("Ошибка выполнения дебита клиента.");
                        ClientSock.Close();
                        Listener.Stop();
                    }
                }
                goldCard = false;
                silverCard = false;
                platinumCard = false;
                return false;
            }

            /// <summary>
            /// Функция выполнения возврата.
            /// </summary>
            /// <param name="port">Порт через который происходит обмен с терминалом</param>
            /// <param name="Amount">Сумма</param>
            /// <param name="BpRRN">Номер транзакции.</param>
            /// <param name="RespCode">Возвращаемый параметр. Код ответа от терминала.</param>
            /// <returns></returns>
            public bool StartCredit(int port, uint Amount, byte[] BpRRN, out int RespCode)
            {
                goldCard = false;
                silverCard = false;
                platinumCard = false;
                cancel = false;
                RespCode = 0;
                log.Write("Запуск сервера обмена с терминалом для выполнения возврата", 100);
                LocalPort = port;
                Listener = new TcpListener(LocalPort);
                Listener.Start();

                try
                {
                    int z = 0;
                    for (z = 0; (!Listener.Pending()); z++)
                    {
                        Thread.Sleep(500);
                        if (z == connectionTimeout || cancel)
                        {
                            log.Write("Очередь запросов на подключение к серверу пуста.", 100);
                            return false;
                        }
                    }
                    log.Write("Ожидание входящего подключения", 100);
                    ClientSock = Listener.AcceptSocket();

                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    log.Write("Ошибка ожидания входящего подключения", 100);
                    return false;
                }

                if (ClientSock.Connected)
                {
                    log.Write("Подключен клиент", 100);
                    try
                    {
                        log.Write("Выполнение возврата клиенту.");
                        log.Write(string.Format("Сумма: {0}, Номер транзакции: {1},",
                            Amount, BitConverter.ToString(BpRRN)));
                        return Credit(Amount, BpRRN, out RespCode);
                    }
                    catch (Exception e)
                    {
                        log.Write(e.ToString(), 100);
                        ClientSock.Close();
                        Listener.Stop();
                    }
                }
                return false;
            }


            /// <summary>
            /// Функция передачи номера карты эмулятору терминала
            /// </summary>
            /// <param name="CardNum">Номер карты</param>
            /// <returns></returns>
            public bool SetCardNum(string CardNum)
            {
                bool tmp = false;
                return SetCardNum(CardNum, out tmp);
            }

            /// <summary>
            /// Функция передачи номера карты эмулятору терминала
            /// </summary>
            /// <param name="CardNum">Номер карты</param>
            /// <param name="CardValid">Выходной параметр. Состояниее валидации карты.</param>
            /// <returns></returns>
            public bool SetCardNum(string CardNum, out bool CardValid)
            {
                // bool tmp = false;
                int num_disc = 300;
                return SetCardNum(CardNum, out CardValid, out num_disc);
            }

            /// <summary>
            /// Функция передачи номера карты эмулятору терминала
            /// </summary>
            /// <param name="CardNum">Номер карты</param>
            /// <param name="CardValid">Выходной параметр. Состояниее валидации карты.</param>
            /// <returns></returns>
            public bool SetCardNum(string CardNum, out bool CardValid, out int num_disc)
            {
                CardValid = false;
                cardNum = CardNum;
                num_disc = 0;
                try
                {
                    if (!CardNum.StartsWith(conf["CardPrefix"]))
                    {

                        CardValid = false;
                        return false;
                    }
                    else
                        CardValid = true;


                    try
                    {
                        log.Write("Получение стандартной скидки для карты.", 100);
                        long cardnum = long.Parse(CardNum);
                        long goldStart = long.Parse(conf["GoldCardStart"]);
                        long goldEnd = long.Parse(conf["GoldCardEnd"]);
                        int discount = int.Parse(conf["GoldCardDisc"]);

                        if (goldStart <= cardnum && cardnum <= goldEnd)
                        {
                            log.Write("GoldCard");
                            goldCardDisc = discount;
                            num_disc = discount;
                            goldCard = true;
                        }
                        else
                            goldCard = false;
                    }
                    catch
                    {
                        goldCard = false;
                    }

                    try
                    {
                        log.Write("Получение стандартной скидки для карты.", 100);
                        long cardnum = long.Parse(CardNum);
                        long silverStart = long.Parse(conf["SilverCardStart"]);
                        long silverEnd = long.Parse(conf["SilverCardEnd"]);
                        int discount = int.Parse(conf["SilverCardDisc"]);

                        if (silverStart <= cardnum && cardnum <= silverEnd)
                        {
                            log.Write("SilverCard");
                            silverCardDisc = discount;
                            num_disc = discount;
                            silverCard = true;
                        }
                        else
                            silverCard = false;
                    }
                    catch
                    {
                        silverCard = false;
                    }
                    try
                    {
                        log.Write("Получение стандартной скидки для карты.", 100);
                        long cardnum = long.Parse(CardNum);
                        long platinumStart = long.Parse(conf["PlatinumCardStart"]);
                        long platinumEnd = long.Parse(conf["PlatinumCardEnd"]);
                        int discount = int.Parse(conf["PlatinumCardDisc"]);

                        if (platinumStart <= cardnum && cardnum <= platinumEnd)
                        {
                            log.Write("PlatinumCard");
                            platinumCardDisc = discount;
                            num_disc = discount;
                            platinumCard = true;
                        }
                        else
                            platinumCard = false;
                    }
                    catch
                    {
                        platinumCard = false;
                    }



                    if (System.IO.File.Exists(conf["TermExe"]))
                    {
                        bool find = false;
                        System.Diagnostics.Process[] procs = System.Diagnostics.Process.GetProcesses();
                        for (int z = 0; z < procs.Length; z++)
                        {
                            try
                            {

                                if (procs[z].MainModule.FileName.ToUpper().LastIndexOf("ermEmu".ToUpper()) > 1)
                                {
                                    if (conf["RestartTermEmu"] == "true")
                                    {

                                        procs[z].Kill();
                                        Thread.Sleep(2000);
                                    }
                                    else
                                        find = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (!find)
                        {
                            try
                            {
                                System.Diagnostics.ProcessStartInfo inf =
                                    new System.Diagnostics.ProcessStartInfo(conf["TermExe"]);
                                inf.WorkingDirectory = conf["TermPath"];
                                inf.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                                inf.CreateNoWindow = false;
                                System.Diagnostics.Process proc = System.Diagnostics.Process.Start(inf);
                                Thread.Sleep(2000);
                            }
                            catch { }

                        }
                    }
                    if (conf["PathToReader"] == string.Empty)
                    {
                        log.Write("Ошибка передачи номера карты", 100);
                        log.Write("Указан пустой путь до дирректории обмена", 100);
                        return false;
                    }
                    System.IO.File.Create(conf["PathToReader"] + CardNum).Close();
                    return true;
                }
                catch (Exception e)
                {

                    log.Write(e.ToString(), 100);
                    log.Write("Ошибка передачи номера карты", 100);
                    return false;
                }
            }


            public bool ParseCardNum(string Track, out string CardNum)
            {
                CardNum = "";
                try
                {
                    for (int z = 1; z < 100; z++)
                    {
                        try
                        {
                            int LnrNumberStart = 0;
                            if (int.TryParse(conf["LnrNumberStart_" + z.ToString()].Trim(),
                                out LnrNumberStart))
                            {
                                string LnrTrackPattern = conf["LnrTrackPattern_" + z.ToString()].Trim();
                                string LnrNumberPattern = conf["LnrNumberPattern_" + z.ToString()].Trim();
                                if (LnrTrackPattern == "")
                                    break;
                                CardNum = GetNum(Track, LnrTrackPattern, LnrNumberStart, LnrNumberPattern);
                                if (CardNum != "")
                                {
                                    return true;
                                }
                            }
                        }
                        catch { }
                    }


                }
                catch
                {

                }
                return false;
            }
            static string GetNum(string track, string TrackPattern, int NumberStart, string NumberPattern)
            {

                Regex regex = new Regex(TrackPattern);
                if (regex.IsMatch(track) && (regex.Match(track).Value.Length == track.Length))
                {
                    Regex regexNumber = new Regex(NumberPattern);
                    if (regexNumber.IsMatch(track, NumberStart))
                        return regexNumber.Match(track, NumberStart).Value;
                }
                return "";
            }

            /// <summary>
            /// Функция выполнения дебита клиента по системе бонус плюс
            /// </summary>
            /// <param name="Price">Цена</param>
            /// <param name="Amount">Сумма</param>
            /// <param name="Quantity">Кол-во</param>
            /// <param name="GoodCode">Код продукта в системе BonusPlus</param>
            /// <param name="GoodName">Наименование продукта</param>
            /// <param name="withDiscount">Фиксирование суммы заказа</param>
            /// <param name="BpRRN">Возвращаемый параметр. Номер транзакции.</param>
            /// <param name="Discount">Возвращаемый параметр. Скидка.</param>
            /// <param name="RespCode">Возвращаемый параметр. Код ответа от терминала.</param>
            /// <param name="ScreenMsg">Возвращаемый параметр. Текст экранного сообщения терминала.</param>
            /// <returns>Результат выполнения функции</returns>        
            private bool Debit(uint Price, uint Amount, uint Quantity,
                string GoodCode, string GoodName, bool withDiscount, out byte[] BpRRN, out int Discount,
                out int RespCode, out string ScreenMsg)
            {
                if (GoodName == "discount" && (Price == 0) && (Amount == 0) && (Quantity == 0))
                {
                    Price = 1;
                    Amount = 1;
                    Quantity = 1;
                }
                TLV tlvData = new TLV();
                RespCode = 0;
                ScreenMsg = ".......";
                byte[] receiveData = new byte[0];
                byte[] sendData = new byte[0];
                BpRRN = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                Discount = 0;
                bool zeroDiscontBugFixFind = false;

                log.Write("Чтение первого входящего сообщения от терминала", 100);
                // Чтение первого входящего сообщения от терминала
                if (ReceiveECDLC(out receiveData) == -1)
                    return false;
                // Распаковка содержимого сообщения, если оно не информационное - выходим
                tlvData.UnpackData(receiveData);
                if (tlvData.GetTagData(1)[0] != 1)
                    return false;
                //--------------------------------------------------
                log.Write("Читаем инфо по карте", 100);
                //Читаем инфо по карте
                try
                {
                    ScreenMsg = Encoding.GetEncoding(866).GetString(tlvData.GetTagData(6));
                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    ScreenMsg = "..........";
                }
                //--------------------------------------------------
                log.Write("Отправляем терминалу сообщение \"Жди ещё\"", 100);
                // Отправляем терминалу сообщение "Жди ещё"
                tlvData.Clear();
                tlvData.AddTag(1, new byte[] { 5 });
                if (tlvData.PackData(ref sendData))
                    return false;
                if (SendECDLC(sendData) == -1) return false;
                //--------------------------------------------------

                log.Write("Формируем запрос на модификацию чека", 100);
                // Формируем запрос на модификацию чека
                TLV FiscalCheque = new TLV();
                TLV article = new TLV();
                TLV PP = new TLV();
                byte[] articleArray = new byte[0];
                byte[] fiscalArray = new byte[0];
                byte[] PPArray = new byte[0];
                //.................................................
                article.AddTag(1, Encoding.GetEncoding(866).GetBytes(GoodCode));
                article.AddTag(2, reverse(BitConverter.GetBytes(Quantity)));
                article.AddTag(3, reverse(BitConverter.GetBytes(Price)));
                article.AddTag(4, reverse(BitConverter.GetBytes(Amount)));
                article.AddTag(5, Encoding.GetEncoding(866).GetBytes(GoodName));
                if (withDiscount) article.AddTag(6, new byte[] { 1 });
                article.PackData(ref articleArray);
                //.................................................
                PP.AddTag(1, reverse(BitConverter.GetBytes(Amount)));
                PP.AddTag(2, reverse(BitConverter.GetBytes(0)));
                PP.AddTag(3, reverse(BitConverter.GetBytes(0)));
                PP.AddTag(4, reverse(BitConverter.GetBytes(0)));
                PP.AddTag(5, reverse(BitConverter.GetBytes(0)));
                PP.PackData(ref PPArray);
                //.................................................
                FiscalCheque.AddTag(1, articleArray);
                if (withDiscount) FiscalCheque.AddTag(2, new byte[] { 1 });
                FiscalCheque.AddTag(3, reverse(BitConverter.GetBytes(Amount)));
                FiscalCheque.AddTag(0x0A, PPArray);

                FiscalCheque.PackData(ref fiscalArray);
                //.................................................
                tlvData.Clear();
                tlvData.AddTag(1, new byte[] { 6 });
                tlvData.AddTag(3, reverse(BitConverter.GetBytes(Amount)));
                tlvData.AddTag(8, fiscalArray);
                //.................................................
                if (tlvData.PackData(ref sendData))
                    return false;             // В случае ошибки выходим.
                                              //--------------------------------------------------

                log.Write("Отправляем запрос на модификацию чека", 100);
                // Отправляем запрос на модификацию чека
                if (SendECDLC(sendData) == -1) return false;
                // Читаем ответ от терминала
                if (ReceiveECDLC(out receiveData) == -1)
                    return false;             // В случае ошибки выходим.
                                              //--------------------------------------------------


                log.Write("Распаковываем и распознаём принятые данные", 100);
                // Распаковываем и распознаём принятые данные 
                tlvData.UnpackData(receiveData);
                FiscalCheque.Clear();
                if (tlvData.GetTagData(8).Length == 1)
                {
                    log.Write("В ответе терминала отсутствует поле \"Фискальный чек\"", 100);
                }
                else
                {
                    FiscalCheque.UnpackData(tlvData.GetTagData(8));
                }


                RespCode = (int)tlvData.GetTagData(2)[0];
                log.Write("Код ответа от терминала: " + RespCode.ToString());
                try
                {
                    byte[] discountArr = FiscalCheque.GetTagData(9);
                    if (discountArr.Length >= 4)
                        Discount = BitConverter.ToInt32(reverse(discountArr), 0);
                    else
                    {
                        Discount = 0;
                        zeroDiscontBugFixFind = true;
                        if (!ZeroDiscontEnable)
                        {
                            ClientSock.Send(new byte[] { (byte)STX.EOT });
                            return true;
                        }
                    }

                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    Discount = 0;
                    zeroDiscontBugFixFind = true;
                    if (!ZeroDiscontEnable)
                    {
                        ClientSock.Send(new byte[] { (byte)STX.EOT });
                        return true;
                    }
                }

                BpRRN = tlvData.GetTagData(9);
                log.Write(string.Format("Скидка: {0}. Номер транзакции: {1}",
                    Discount, BitConverter.ToString(BpRRN)));

                if (GoodName == "discount")
                {
                    ClientSock.Send(new byte[] { (byte)STX.EOT });
                    return true;
                }



                log.Write("Формируем и отправляем запрос на закрытие чека", 100);
                // Формируем и отправляем запрос на закрытие чека
                tlvData.Clear();
                tlvData.AddTag(1, new byte[] { 4 });
                tlvData.AddTag(2, new byte[] { 0 });
                //if(withDiscount)Amount = Amount - (uint)(Amount / Discount);
                tlvData.AddTag(3, reverse(BitConverter.GetBytes(Amount)));
                tlvData.AddTag(5, GetBCDDateTime());
                tlvData.AddTag(8, fiscalArray);
                tlvData.AddTag(0x0A, BitConverter.GetBytes(0));
                tlvData.AddTag(0x0B, BitConverter.GetBytes(0));
                tlvData.AddTag(0x0D, BitConverter.GetBytes(0));
                tlvData.AddTag(0x10, BitConverter.GetBytes(0));
                tlvData.AddTag(0x1F, BitConverter.GetBytes(0));
                //.................................................
                if (tlvData.PackData(ref sendData))
                    return false;
                if (SendECDLC(sendData) == -1) return false;

                log.Write("Читаем ответ от терминала", 100);
                // Читаем ответ от терминала
                if (ReceiveECDLC(out receiveData) == -1)
                    return false;
                tlvData.UnpackData(receiveData);


                if (tlvData.GetTagData(1)[0] != 1)
                {
                    return zeroDiscontBugFixFind;
                }
                return true;
            }

            /// <summary>
            /// Полный возврат продажи
            /// </summary>
            /// <param name="Amount">Сумма возврата</param>
            /// <param name="BpRRN">Номер транзакции</param>
            /// <param name="RespCode">Код ответа от терминала</param>
            /// <returns>Резульат выполнения возврата</returns>
            private bool Credit(uint Amount, byte[] BpRRN, out int RespCode)
            {
                TLV tlvData = new TLV();
                byte[] receiveData = new byte[0];
                byte[] sendData = new byte[0];
                RespCode = 0;

                log.Write("Чтение первого входящего сообщения от терминала", 100);
                // Чтение первого входящего сообщения от терминала
                if (ReceiveECDLC(out receiveData) == -1)
                    return false;
                log.Write("Распаковка содержимого сообщения", 100);
                // Распаковка содержимого сообщения, если оно не информационное - выходим
                tlvData.UnpackData(receiveData);
                if (tlvData.GetTagData(1)[0] != 1)
                    return false;
                //--------------------------------------------------

                log.Write("Формируем и отправляем запрос на возврат", 100);
                // Формируем и отправляем запрос на возврат 
                tlvData.Clear();
                tlvData.AddTag(1, new byte[] { 10 });
                tlvData.AddTag(3, reverse(BitConverter.GetBytes(Amount)));
                tlvData.AddTag(9, BpRRN);
                if (tlvData.PackData(ref sendData))
                    return false;
                if (SendECDLC(sendData) == -1) return false;

                log.Write("Читаем ответ то терминала", 100);
                if (ReceiveECDLC(out receiveData) == -1)
                    return false;
                tlvData.UnpackData(receiveData);
                RespCode = (int)tlvData.GetTagData(2)[0];
                log.Write("Код ответа от терминала: " + RespCode.ToString());
                if (tlvData.GetTagData(1)[0] != 1)
                    return false;
                if (tlvData.GetTagData(2)[0] != 0)
                    return false;
                try
                {
                    log.Write("Отправляем запрос на закрытие сессии", 100);
                    ClientSock.Send(new byte[] { (byte)STX.EOT });
                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                }
                return true;
            }

            /// <summary>
            /// Возвращает принятый массив в обратном порядке
            /// </summary>
            /// <param name="arr">Массив элементу которого необходимо вернуть в обратном порядке.</param>
            /// <returns>Принятый массив в обратном порядке.</returns>
            private byte[] reverse(byte[] arr)
            {
                Array.Reverse(arr);
                return arr;
            }

            /// <summary>
            /// Функция вычисления временной метки в формате BCD
            /// </summary>
            /// <returns>Временная метка.</returns>
            private byte[] GetBCDDateTime()
            {
                string year = DateTime.Now.Year.ToString();
                string month = DateTime.Now.Month.ToString();
                if (month.Length == 1) month = "0" + month;
                string day = DateTime.Now.Day.ToString();
                if (day.Length == 1) day = "0" + day;
                string hour = DateTime.Now.Hour.ToString();
                if (hour.Length == 1) hour = "0" + hour;
                string min = DateTime.Now.Minute.ToString();
                if (min.Length == 1) min = "0" + min;
                string sec = DateTime.Now.Second.ToString();
                if (sec.Length == 1) sec = "0" + sec;
                return new byte[] { ConvertToBCD(year[0], year[1]),
                ConvertToBCD(year[2], year[3]), ConvertToBCD(month[0], month[1]),
                ConvertToBCD(day[0],day[1]), ConvertToBCD(hour[0], hour[1]),
                ConvertToBCD(min[0], min[1]), ConvertToBCD(sec[0], sec[1])};
            }

            /// <summary>
            /// Конвертирование в BCD формат
            /// </summary>
            /// <param name="a">Десятичная цифра старшего байта</param>
            /// <param name="b">Десятичная цифра младшего байта</param>
            /// <returns>Данные упакованные в BCD</returns>
            private byte ConvertToBCD(char a, char b)
            {
                byte aa = (byte)(byte.Parse(a.ToString()) << 4);
                byte bb = byte.Parse(b.ToString());
                return (byte)(aa + bb);
            }


            /// <summary>
            /// Метод отправки данных по протоколу ECDLC
            /// </summary>
            /// <param name="sendData">Данные которые необходимо передать</param>
            /// <returns>Результат выполнения функции. (успешное выполнение = 1)</returns>
            private int SendECDLC(byte[] sendData)
            {
                obmen.Write("Отправка данных терминалу");
                obmen.Write("Отправляю данные: " + BitConverter.ToString(sendData));
                ECDLC ecdlcData = new ECDLC();
                byte[] receiveData = new byte[0];
                try
                {
                    Start:
                    ecdlcData.PackData(sendData);
                    ClientSock.Send(ecdlcData.Pack);
                    WaitSTX:
                    if (cancel)
                    {
                        log.Write("Принудительное завершение обмена.");
                        this.Close();
                        return -1;
                    }
                    if (readData(out receiveData) < 1)
                    {
                        obmen.Write("Ошибка приёма данных.");
                        return -1;
                    }
                    if (receiveData[0] == (byte)STX.NAK)
                    {
                        obmen.Write("От терминала получен ответ \"Получены искажённые данные\". Отправляем данные заново.");
                        goto Start;
                    }
                    if (receiveData[0] == (byte)STX.ACK)
                    {
                        obmen.Write("От терминала получен ответ \"Данные успешно приняты\".");
                        return 1;
                    }
                    if (receiveData[0] == (byte)STX.BEL)
                    {
                        obmen.Write("От терминала получен ответ \"Жди ещё\". Отправляем подтверждение ожидания.");
                        ClientSock.Send(new byte[] { (byte)STX.BEL });
                        goto WaitSTX;
                    }
                    if (receiveData[0] == (byte)STX.EOT)
                    {
                        obmen.Write("От терминала получен ответ \"Закрытие соединения\". Закрываем порт обмена с терминалом.");
                        ClientSock.Close();
                        Listener.Stop();
                        return -2;
                    }
                    if ((receiveData[0] == (byte)STX.STX))
                    {
                        obmen.Write("От терминала получен ответ содержащий пакет данных, сохраняем его в памяти для дальнейшего использования.");
                        memReciveData = receiveData;
                        return 1;
                    }
                    return -2;
                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    return -2;
                }
            }

            /// <summary>
            /// Метод получения данных по протоколу ECDLC
            /// </summary>
            /// <param name="Data">Возвращаемый параметр содержащий принятые данные.</param>
            /// <returns>Результат выполнения функции. (успешное выполнение = 1)</returns>
            private int ReceiveECDLC(out byte[] Data)
            {
                obmen.Write("Получение данных от терминала");
                byte[] reciveData = new byte[0];
                Data = new byte[0];
                ECDLC ecdlcData = new ECDLC();
                try
                {
                    Start:
                    if (cancel)
                    {
                        log.Write("Принудительное завершение обмена.");
                        this.Close();
                        return -1;
                    }
                    if (memReciveData.Length > 0)
                    {
                        obmen.Write("Данные были прочитаны при предыдущей транзакций.");
                        obmen.Write("Данные: " + BitConverter.ToString(memReciveData));
                        reciveData = memReciveData;
                        memReciveData = new byte[0];
                    }
                    else
                    {
                        obmen.Write("Чтение данных.");
                        if (readData(out reciveData) < 1) return -1;
                        obmen.Write("Получено: " + BitConverter.ToString(reciveData));
                    }
                    if (reciveData[0] == (byte)STX.ENQ)
                    {
                        obmen.Write(">>ENQ");
                        obmen.Write("Распознан символ запроса на установление соединения, отправляем ответ подтверждающий установку соединения.");
                        ClientSock.Send(new byte[] { (byte)STX.ACK });
                        obmen.Write("<<ACK");
                        goto Start;
                    }
                    if (reciveData[0] == (byte)STX.BEL)
                    {
                        obmen.Write(">>BEL");
                        obmen.Write("Распознан символ удержания соединения, отправляем ответ подтверждающий удержание соединения.");
                        ClientSock.Send(new byte[] { (byte)STX.BEL });
                        obmen.Write("<<BEL");
                        goto Start;
                    }
                    if (reciveData[0] == (byte)STX.STX)
                    {
                        obmen.Write(">>STX");
                        obmen.Write("Распознан символ начала пакета.");
                        if (ecdlcData.AddData(reciveData) == -1)
                        {
                            obmen.Write("Ошибка контрольной суммы CRC16. Отправляем запрос на повторную передачу сообщения.");
                            ClientSock.Send(new byte[] { (byte)STX.NAK });
                            obmen.Write("<<NAK");
                            goto Start;
                        }
                        else
                        {
                            obmen.Write("Проверка CRC16 прошла успешно. Отправляем подтверждение успешного приёма.");
                            ClientSock.Send(new byte[] { (byte)STX.ACK });
                            obmen.Write("<<ACK");
                            Data = ecdlcData.Data;
                            return 1;
                        }
                    }
                    return 0;
                }
                catch (Exception e)
                {
                    log.Write(e.ToString(), 100);
                    return -1;
                }

            }

            /// <summary>
            /// Чтение данных из сокета.
            /// </summary>
            /// <param name="readData">Возвращаемый  параметр содержащий прочитанные данные</param>
            /// <returns>Кол-во прочитанных элементов</returns>
            private int readData(out byte[] readData)
            {
                int _timeout = timeout;
                int i = 0;
                readData = new byte[0];
                while (i == 0 && !cancel)
                {
                    try
                    {
                        readData = new byte[ClientSock.Available];
                        i = ClientSock.Receive(readData);
                    }
                    catch (Exception e)
                    {
                        log.Write(e.ToString(), 100);
                        i = -1;
                    }
                    if (i == 0 && _timeout > 0)
                    {
                        Thread.Sleep(1);
                        _timeout--;
                    }
                }
                return i;
            }
        }


    public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }
    }
}
