using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ProjectSummer.Repository.ASUDriver
{
    /// <summary>
    /// Класс работы с хранилищем параметров изменение параметров осуществляется через индексаторы.
    /// Для получение объекта хранилища параметров используйте статический метод "GetConfigMemory"
    /// </summary>
    public class ConfigMemory
    {  
        #region Внутринние переменные и методы
       /* static ConfigMemory()
        {
            try
            {
                var files = Directory.GetFiles(ConfigDirectory, "*.conf");
                foreach (var file in files)
                    new ConfigMemory(Path.GetFileNameWithoutExtension(file));
            }
            catch { }

        }*/

        /// <summary>Словарь параметров</summary>
        private Dictionary<string, string> values = new Dictionary<string, string>();


        /// <summary> Словарь описаний параметров </summary>
        private Dictionary<string, string> descriptions = new Dictionary<string, string>();

        /// <summary>Словарь загруженных файлов конфигурации</summary>
        private static Dictionary<string, ConfigMemory> loadedConfigs = new Dictionary<string, ConfigMemory>();

        /// <summary>
        /// Чтение файла конфигурации с диска
        /// </summary>
        private Dictionary<string, string> initFromFile(string FileName)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            try
            {
                using (FileStream a_fileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader a_reader = new StreamReader(a_fileStream, Encoding.Unicode))
                    {
                        var lines = a_reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');
                        lock (ret)
                        {
                            foreach (var line in lines)
                            {
                                try
                                {
                                    int separatorIndex = line.IndexOf(NameValueSeparator);
                                    if (separatorIndex > 0)
                                    {
                                        var name = line.Remove(separatorIndex).Trim().ToLower();
                                        var val = line.Remove(0, separatorIndex + 1).Trim().Replace("\\r", "\r").Replace("\\n", "\n");

                                        if (ret.ContainsKey(name))
                                            ret.Add(name, val);
                                        else
                                            ret[name] = val;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return ret;
        }

        #endregion

        /// <summary>
        /// Описание параметра
        /// </summary>
        public struct ValueDescription
        {

            /// <summary>
            /// Идентификатор параметра
            /// </summary>
            public string ID
            { get; set; }

            /// <summary>
            /// Имя параметра
            /// </summary>
            public string Name 
            { get; set; }

            /// <summary>
            /// Описание параметра
            /// </summary>
            public string Description
            { get; set; }

            /// <summary>
            /// Список возможных значений параметра
            /// </summary>
            public List<string> Values
            {
                get;
                set;
            }

            /// <summary>
            /// Значение параметра
            /// </summary>
            public string Value
            {
                get;
                set;
            }

            /// <summary>
            /// Возвращает/задает возможность редактирования занчения параметра
            /// </summary>
            public bool Editable
            {
                get;
                set;
            }

            /// <summary>
            /// Возвращает/задает возможность редактирования занчения параметра
            /// </summary>
            public bool EnableFreeValues
            {
                get;
                set;
            }

        }

        /// <summary>
        /// Возвращает директорию для хранения файлов конфигурации
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
            }
        }


        /// <summary>
        /// Получение объекта для доступа к хранилищу параметров
        /// </summary>
        /// <param name="ModuleName">Имя файла хранилища параметров</param>
        /// <param name="NameValueSeparator">Разделитель Имя/Значение параметра, по умолчанию '='</param>
        /// <returns></returns>
        public static ConfigMemory GetConfigMemory(string ModuleName, char NameValueSeparator = '=')
        {
            if (loadedConfigs.ContainsKey(ModuleName))
                return loadedConfigs[ModuleName];
            else
                return new ConfigMemory(ModuleName, NameValueSeparator);
        }

        /// <summary>
        /// Получение имен загруженных файлов конфигурации
        /// </summary>
        /// <returns></returns>
        public static string[] GetLoadedConfigMemoryNames()
        {
            return loadedConfigs.Keys.OrderBy(i=>i).ToArray();
        }


        /// <summary>
        /// Чтение/запись значение параметра
        /// </summary>
        /// <param name="ValueName">Имя параметра</param>
        /// <returns></returns>
        public string this[string ValueName]
        {
            get
            {
                lock (values)
                {
                    ValueName = ValueName.ToLower();
                    return values.ContainsKey(ValueName) ? values[ValueName] : "";
                }
            }
            set
            {
                lock (values)
                {
                    ValueName = ValueName.ToLower();
                    if (values.ContainsKey(ValueName))
                    {
                        values[ValueName] = value;
                    }
                    else
                    {
                        values.Add(ValueName, value);
                    }
                    RaisePropertyChanged(ValueName);
                    
                }
                if (AutoSave)
                    Save();
            }
        }

        private void RaisePropertyChanged(string ValueName, string NewValueName = "")
        {
            if (ValueChanged != null)
                new System.Threading.Thread(delegate() { ValueChanged(this, new ValueChangedEventArgs(ValueName, NewValueName)); }).Start();
                #warning Здесь возможно будет происходить хрень!!!
        }

        /// <summary>
        /// Аргументы события изменения значения переменной
        /// </summary>
        public class ValueChangedEventArgs : EventArgs
        {           
            /// <summary>
            /// Аргументы события изменения значения переменной
            /// </summary>
            /// <param name="ValueName">Имя паременной значение которой изменилось</param>
            public ValueChangedEventArgs(string ValueName, string NewValueName)
            {
                this.ValueName = ValueName;
                if ((NewValueName == null) || (NewValueName == ""))
                    this.NewValueName = ValueName;
                else
                    this.NewValueName = NewValueName;
            }
            /// <summary>
            /// Имя паременной значение которой изменилось
            /// </summary>
            public virtual string ValueName { get; private set; }

            public virtual string NewValueName { get; private set; }

        }

        /// <summary>
        /// Событие, возникаемое при изменении значения переменной
        /// </summary>
        public event EventHandler<ValueChangedEventArgs> ValueChanged;

        /// <summary>
        /// Возвращает путь до файла конфигурации
        /// </summary>
        public string FileName
        {
            get;
            private set;
        }

        /// <summary>
        /// Возвращает путь до файла описания конфигурации
        /// </summary>
        public string DescriptionFileName
        {
            get;
            private set;
        }

        /// <summary>
        /// Возвращает разделитель Имя/Значение параметра
        /// </summary>
        public char NameValueSeparator
        {
            get;
            private set;
        }
        
        /// <summary>
        /// Возвращает/Задает. Автоматическое сохранение измененых параметров
        /// </summary>
        public bool AutoSave
        {
            get;
            set;
        }

        /// <summary>
        /// Возвращает имя файла хранилища параметров
        /// </summary>
        public string ModuleName
        {
            get;
            private set;
        }

        /// <summary>
        /// Возвращает описание файла хранилища параметров
        /// </summary>
        public string ModuleNameDescription
        {
            get;
            private set;
        }

        /// <summary>
        ///  Класс работы с хранилищем параметров
        /// </summary>
        /// <param name="ModuleName">Имя файла хранилища параметров</param>
        /// <param name="NameValueSeparator">Разделитель Имя/Значение параметра, по умолчанию '='</param>
        private ConfigMemory(string ModuleName, char NameValueSeparator='=')
        {       
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            this.ModuleName = ModuleName;
            this.NameValueSeparator = NameValueSeparator;

            FileName = Path.GetFullPath(Path.Combine(ConfigDirectory, this.ModuleName + ".conf"));
            Console.WriteLine(FileName);
            values = initFromFile(FileName);

            ModuleNameDescription = values.ContainsKey("#name_description") ? string.Format("{0} [{1}]", values["#name_description"], this.ModuleName) : this.ModuleName;

            DescriptionFileName = ((values.ContainsKey("#description") && values["#description"]!="") ? values["#description"] : FileName + "_scheme");
            if(DescriptionFileName != Path.GetFullPath(DescriptionFileName))
                DescriptionFileName = Path.GetFullPath(Path.Combine(ConfigDirectory, DescriptionFileName));

            if(File.Exists(DescriptionFileName))
            {
                descriptions = initFromFile(DescriptionFileName);

                foreach (var item in descriptions)
                {
                    if (!values.ContainsKey(item.Key))
                        values.Add(item.Key, "");
                }    
            }
            if (values.ContainsKey("#description") && !descriptions.ContainsKey("#description"))
                descriptions.Add("#description", "Файл описания конфигурации#Путь до файла, содержащего описание параметров.");

            if (values.ContainsKey("#name_description") && !descriptions.ContainsKey("#name_description"))
                descriptions.Add("#name_description", "Имя модуля#Описание файла хранилища параметров.");

            if (!loadedConfigs.ContainsKey(ModuleName))
                loadedConfigs.Add(ModuleName, this);
            else
                loadedConfigs[ModuleName] = this;

        }

        /// <summary>
        /// Сохранить файл конфигурации
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            //Save(this.DescriptionFileName, descriptions);
            return Save(FileName, values);
        }

        /// <summary>
        /// Сохранить значения словаря в файл
        /// </summary>
        /// <param name="FileName">Имя файла</param>
        /// <param name="values">Словарь для сохранения</param>
        /// <returns></returns>
        private bool Save(string FileName, Dictionary<string, string> values)
        {
            try
            {
                //System.Windows.Forms.ToString()Box.Show(FileName??"null");

                if ((FileName != null) && (FileName != ""))
                {
                    var path = new FileInfo(FileName).DirectoryName;
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    using (var txt = new StreamWriter(FileName, false, Encoding.Unicode))
                    {
                        lock (values)
                        {
                            try
                            {
                                foreach (var value in values)
                                {
                                    txt.WriteLine(value.Key + NameValueSeparator + value.Value.Replace("\r", "\\r").Replace("\n", "\\n"));
                                }
                            }
                            catch { }
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            return false;
        }

        /// <summary>
        /// Получение имен всех переменных
        /// </summary>
        /// <returns></returns>
        public string[] GetValueNames()
        {
            string[] ret = new string[0];
            lock (values)
            {
                ret = values.Keys.OrderBy(i => i).ToArray();
            }
            return ret;
        }
        public string[] GetValueNames(string Prefix)
        {

            lock (values)
            {
                IEnumerable<string> ret = values.Keys.OrderBy(i => i);
                if (!string.IsNullOrEmpty(Prefix))
                    ret = ret.Where(i => i.StartsWith(Prefix));
              //  System.Windows.Forms.ToString()Box.Show(string.Join("\r\n", ret.ToArray()));
                return ret.ToArray();
            }


        }

        /// <summary>
        /// Удаляет параметр, вместе с описанием
        /// </summary>
        /// <returns></returns>
        public void RemoveValue(string ValueName)
        {
            if (descriptions.ContainsKey(ValueName))
                descriptions.Remove(ValueName);
            if (values.ContainsKey(ValueName))
                values.Remove(ValueName);
        }

        /// <summary>
        /// Получение описания параметра
        /// </summary>
        /// <param name="ValueName">Имя параметра</param>
        /// <returns></returns>
        public ValueDescription GetValueDescription(string ValueName)
        {
            if (descriptions.ContainsKey(ValueName))
            {
                var item_splited = descriptions[ValueName].Split('#');

                return new ValueDescription()
                {
                    ID          = ValueName,
                    Name        = (item_splited[0].Trim() == "") ? ValueName : item_splited[0],
                    Description = (item_splited.Length > 1) ? item_splited[1] : "",
                    Editable = (item_splited.Length > 2) ? (item_splited[2].ToLower().Trim() != "false") : true,
                    EnableFreeValues = (item_splited.Length > 3) ? (item_splited[3].ToLower().Trim() != "false") : true,
                    Values      = (item_splited.Length > 4) ? new List<string>(item_splited[4].Split(';')) : new List<string>(),
                    Value       = this[ValueName],
                };
            }
            else
                return new ValueDescription() { ID = ValueName, Name = ValueName, Description = "", EnableFreeValues = true, Editable = true, Value = "", Values = new List<string>() };
        }
        
        /// <summary>
        /// Запись описания параметра в файл описания конфигурации
        /// </summary>
        /// <param name="Description">Описание</param>
        /// <param name="ValueName">Имя параметра</param>
        /// <returns></returns>
        public bool SetValueDescription(string ValueName, ValueDescription Description)
        {
            if (Description.ID == null)
                Description.ID = ValueName;
            if (Description.Values == null)
                Description.Values = new List<string>();
            if (!descriptions.ContainsKey(Description.ID))
                descriptions.Add(Description.ID, Description.Name + "#" + Description.Description + "#" + Description.Editable.ToString() + "#" + Description.EnableFreeValues.ToString() + "#" + string.Join(";", Description.Values.ToArray()));
            else
                descriptions[Description.ID] = Description.Name + "#" + Description.Description + "#" + Description.Editable.ToString() + "#" + Description.EnableFreeValues.ToString() + "#" + string.Join(";", Description.Values.ToArray());

            if ((ValueName != Description.ID) && !values.ContainsKey(Description.ID))
            {
                values.Add(Description.ID, values[ValueName]);
                values.Remove(ValueName);
                if (descriptions.ContainsKey(ValueName))
                    descriptions.Remove(ValueName);
            }
            if (!values.ContainsKey(Description.ID))
                values[Description.ID] = "";
            var ret = Save(DescriptionFileName, descriptions);
            RaisePropertyChanged(ValueName, Description.ID);
            return ret;
        }

        /// <summary>
        /// Определяет, содержится ли в конфигурации параметр с заданым именем
        /// </summary>
        /// <param name="ValueName">Имя параметра</param>
        /// <returns></returns>
        public bool ContainsValueName(string ValueName)
        {
            return values.ContainsKey(ValueName);
        }

        /// <summary>
        /// Удаление значений всех параметров, имя которых начинается с "prefix"
        /// </summary>
        /// <param name="prefix">Префикс имени параметров для удаления</param>
        public void Clear(string prefix = "")
        {
            List<string> toDel = new List<string>();
            foreach (var item in values)
                if (item.Key.StartsWith(prefix)) toDel.Add(item.Key);
            for (int z = 0; z < toDel.Count; z++)
            {
                values.Remove(toDel[z]);
            }
        }
    }
}
