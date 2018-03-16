using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace ProjectSummer.Repository.ASUDriver
{
    public class Logger
    {
        #region Приватные переменные класса
        private static string _logDir = AppDomain.CurrentDomain.BaseDirectory;
        private string path = Path.Combine(_logDir, "log");
        private List<string> logMemory = new List<string>();
        #endregion

        #region Публичные свойства класса
        /// <summary>
        /// Путь до директории для хранения лог файлов
        /// </summary>
        public static string LogDir
        {
            get
            {
                return _logDir;
            }
            set
            {
                _logDir = Path.GetFullPath(value);
            }
        }

        /// <summary>
        /// Дата/время последней отчистки архива
        /// </summary>
        public DateTime LastArchiveClear
        {
            get;
            private set;
        }

        /// <summary>
        /// Текущий уровень логирования
        /// </summary>
        public int LogLevel
        {
            get;
            set;
        }

        /// <summary>
        /// Включение/отключение записи логов на диск
        /// </summary>
        public bool LogEnable
        {
            get;
            set;
        }

        /// <summary>
        /// Глубина архива логов (дней)
        /// </summary>
        public int ArchiveDepth
        {
            get;
            set;
        }

        /// <summary>
        /// Имя экземпляра логера
        /// </summary>
        public string LoggerName
        {
            get;
            private set;
        }
        #endregion

        #region Структуры класса
        /// <summary>
        /// Строка лога
        /// </summary>
        public struct ParsedLog
        {
            /// <summary>
            /// Дата/время записи
            /// </summary>
            public DateTime DateTime;
            /// <summary>
            /// Текст записи
            /// </summary>
            public string Message;
            /// <summary>
            /// Уровень важности записи
            /// </summary>
            public int MessageLevel;
        }
        #endregion

        /// <summary>Конструктор класса логирования</summary>
        /// <param name="ModuleName">Имя экземпляра лога</param>
        /// <param name="LogEnable">Включение/отключение записи лога на диск</param>
        /// <param name="LogLevel">Максимальный уровень логирования. Чем выше значение, тем детальнее лог.</param>
        public Logger(string ModuleName, bool LogEnable = true, int LogLevel = int.MaxValue)
        {
            this.LogEnable = LogEnable;
            this.LogLevel = LogLevel;
            LastArchiveClear = DateTime.Now.Date.AddDays(-1);
            ArchiveDepth = 60;

            if (ModuleName != Path.GetFullPath(ModuleName))
            {
                LoggerName = ModuleName;
                path = Path.GetFullPath(Path.Combine(path, ModuleName));
            }
            else
            {
                path = ModuleName;
            }
        }

        string fileName = "";

        #region Функции записи информации в лог
        /// <summary>
        /// Запись всех не сохраненых записей из памяти на диск
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            try
            {
                if (fileName != GetFileName(DateTime.Today))
                    fileName = GetFileName(DateTime.Today);
                lock (fileName)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    

                    using (FileStream fileSteam = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write))
                    {
                        using (StreamWriter str = new StreamWriter(fileSteam, Encoding.Unicode))
                        {
                            lock (logMemory)
                            {
                                foreach (var item in logMemory)
                                    str.WriteLine(item);
                                logMemory.Clear();
                            }
                        }
                        fileSteam.Close();
                    }
                    сlearArhive();
                    return true;
                }
            }
            catch
            {
                return false;
            }

        }

        /// <summary>
        /// Добавление записи в лог, с возможностью форматирования.
        /// Аналог string.Format()
        /// </summary>
        /// <param name="format">Строка составного форматирования</param>
        /// <param name="args">Объекты для форматирования</param>
        /// <returns></returns>
        public bool WriteFormated(string format, params object[] args)
        {
            return Write(string.Format(format, args));
        }

        /// <summary>
        /// Добавление записи в лог
        /// </summary>
        /// <param name="Message">Сообщение для добавления в лог</param>
        /// <param name="MessageLevel">Уровень важности сообщения, чем ниже заначение - тем выше важность.</param>
        /// <param name="SendToConsole">Флаг отправки сообщения в отладочную консоль</param>
        /// <param name="ForceSave">Флаг форсирования записи лога на диск</param>
        /// <returns></returns>
        public bool Write(string Message, int MessageLevel = 0, bool SendToConsole = false, bool ForceSave = true)
        {
            var th_id = Thread.CurrentThread.ManagedThreadId;
            var th_name = Thread.CurrentThread.Name;
            var line = string.Format("{0:dd/MM/yy HH:mm:ss.fff}>th[{1}]{2}>[{3:000}]>{4}", DateTime.Now, th_id, th_name, MessageLevel, Message.Replace("\r", "\\r").Replace("\n", "\\n"));
            var line_cons = string.Format("{0:HH:mm:ss.fff}>th[{1}]{2}>[{3:000}]>{4}", DateTime.Now, th_id, th_name, MessageLevel, Message.Replace("\r", "\\r").Replace("\n", "\\n"));
            if ((MessageLevel <= LogLevel) && (LogEnable))
            {
                if (SendToConsole)
                    Console.WriteLine(line_cons);
                lock (logMemory)
                    logMemory.Add(line);
                if (ForceSave)
                    new System.Threading.Thread(delegate() { Save(); }).Start();

            }
            return false;
        }
        #endregion

        #region Методы чтения лога с диска
        /// <summary>
        /// Прочитать лог за текущй день
        /// </summary>
        /// <returns>Массив записей лога, за текущий день</returns>
        public ParsedLog[] ParseLog()
        {
            return ParseLog(DateTime.Today, DateTime.Today.AddDays(1).AddMilliseconds(-1));
        }
        /// <summary>
        /// Прочитать лог за период.
        /// </summary>
        /// <param name="From">Начало периода</param>
        /// <param name="To">Конец периода</param>
        /// <returns>Массив записей лога, за указаный период</returns>
        public ParsedLog[] ParseLog(DateTime From, DateTime To)
        {
            List<ParsedLog> ret = new List<ParsedLog>();
            try
            {
                for (DateTime z = From; z < To.AddDays(1); z = z.AddDays(1))
                {
                    string tmp = readDay(z);
                    if (tmp != "")
                    {
                        var tmpArr = tmp.Split('\n');
                        for (int q = 0; q < tmpArr.Length; q++)
                        {
                            try
                            {
                                var data = tmpArr[q].Split('>');
                                if (data.Length > 2)
                                {
                                    ParsedLog item = new ParsedLog()
                                    {
                                        DateTime = DateTime.Parse(data[0]),
                                        MessageLevel = int.Parse(data[1].Replace("[", "").Replace("]", "")),
                                        Message = string.Join(">", data, 2, data.Length - 2)
                                    };

                                    if ((item.DateTime >= From) && (item.DateTime <= To))
                                    {
                                        ret.Add(item);
                                    }


                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return ret.ToArray();
        }
        #endregion

        #region Вспомогательные методы
        private string GetFileName(DateTime DateAddon)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string tmpFileName = string.Format("{0}_{1:0000}{2:00}{3:00}.log", LoggerName, DateAddon.Year, DateAddon.Month, DateAddon.Day);
            return Path.Combine(path, tmpFileName);
        }

        private string readDay(DateTime dTime)
        {
            string strret = "";
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                string fileName = GetFileName(dTime);
                if (!File.Exists(fileName))
                    return "";
                using (FileStream fileSteam = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    using (StreamReader str = new StreamReader(fileSteam, Encoding.Unicode))
                    {
                        strret = str.ReadToEnd();
                        fileSteam.Close();
                    }
                }
                return strret;
            }
            catch
            {
                return strret;
            }

        }
        private void сlearArhive()
        {
            if (LastArchiveClear != DateTime.Now.Date)
            {

                LastArchiveClear = DateTime.Now.Date;

                if (path == "")
                    return;
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles())
                {
                    try
                    {
                        if (file.LastWriteTime.Ticks < DateTime.Today.AddDays(-1 * ArchiveDepth).Ticks)
                        {
                            file.Delete();
                        }
                    }
                    catch { }
                }
            }
        }
        #endregion
    }
}
