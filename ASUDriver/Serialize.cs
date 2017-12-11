using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace ProjectSummer.Repository
{
    public class Serialization
    {
        [XmlRoot("dictionary")]
        public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
        {
            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                var keySerializer = new XmlSerializer(typeof(TKey));
                var valueSerializer = new XmlSerializer(typeof(TValue));
                bool wasEmpty = reader.IsEmptyElement;
                reader.Read();
                if (wasEmpty) return;

                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    reader.ReadStartElement("item");
                    reader.ReadStartElement("key");
                    var key = (TKey)keySerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    reader.ReadStartElement("value");
                    var value = (TValue)valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    Add(key, value);
                    reader.ReadEndElement();
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }

            public void WriteXml(XmlWriter writer)
            {
                var keySerializer = new XmlSerializer(typeof(TKey));
                var valueSerializer = new XmlSerializer(typeof(TValue));

                foreach (TKey key in Keys)
                {
                    writer.WriteStartElement("item");
                    writer.WriteStartElement("key");
                    keySerializer.Serialize(writer, key);
                    writer.WriteEndElement();
                    writer.WriteStartElement("value");
                    TValue value = this[key];
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

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
                lock (servBiz)
                {

                    try
                    {
                        servBiz = true;
                        //откроем поток для записи в файл
                        FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        try
                        {
                            XmlSerializer xs = new XmlSerializer(typeof(ObjType));
                            xs.Serialize(fs, obj);
                            //BinaryFormatter bf = new BinaryFormatter();
                            //bf.Serialize(fs, obj);
                        }
                        finally
                        {
                            fs.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        //System.Windows.Forms.ToString()Box.Show(ex.ToString(), "Serialize");
                    }
                    servBiz = false;
                    return true;

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //System.Windows.Forms.ToString()Box.Show(ex.ToString(), "Serialize");
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
                    try
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ObjType));
                        ret = (ObjType)xs.Deserialize(fs);
                        //BinaryFormatter bf = new BinaryFormatter();
                        //ret = (ObjType)bf.Deserialize(fs);
                    }
                    finally
                    {
                        fs.Close();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                //System.Windows.Forms.ToString()Box.Show(ex.ToString(), "Deserialize");
            }
            return ret;

        }
        #endregion
    }
}
