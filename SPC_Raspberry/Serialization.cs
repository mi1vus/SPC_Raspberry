    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    namespace ProjectSummer.Repository
    {
        public class Serialization
        {

            #region Методы сериализации/десериализации объектов
            static object servBiz = new object();
            /// <summary>
            /// Сериализация объекта, и сохранение его на диск
            /// </summary>
            /// <param name="obj">Объект, для сериализации</param>
            /// <param name="filename">Имя файла</param>
            /// <returns></returns>
            public static bool Serialize<ObjType>(ObjType obj, string filename)
            {
                try
                {
                    lock(servBiz)
                    {

                        try
                        {
                            servBiz = true;
                            //откроем поток для записи в файл
                            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                            BinaryFormatter bf = new BinaryFormatter();
                            //сериализация
                            bf.Serialize(fs, obj);
                            fs.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                        servBiz = false;
                        return true;

                    }
                }
                catch
                {
                }
                return false;
            }

            /// <summary>
            /// Чтения данных об объекте с диска и десериализация его
            /// </summary>
            /// <param name="filename">Имя файла</param>
            /// <returns></returns>
            public static ObjType Deserialize<ObjType>(string filename)
            {
                ObjType ret = default(ObjType);
                try
                {
                    if (File.Exists(filename))
                    {
                        FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        BinaryFormatter bf = new BinaryFormatter();
                        ret = (ObjType)bf.Deserialize(fs);
                        fs.Close();
                    }
                }
                catch (Exception e)
                {
                }
                return ret;

            }
            #endregion
        }
    }
