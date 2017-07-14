using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartPumpControlRemote
{
    public class Shell
    {
        public static string IP = "";// "net.tcp://localhost:1120";
        public static string Dir = "";
        private static Dictionary<string, string> TID_to_IP = new Dictionary<string, string>();
        public static void AddTerminal(string TID, string IP)
        {
            if (TID_to_IP.ContainsKey(TID))
                TID_to_IP[TID] = IP;
            else
                TID_to_IP.Add(TID, IP);
            
        }
        public static bool setIP(string TID)
        {
            try
            {
                if (TID_to_IP.ContainsKey(TID))
                {
                    IP = "net.tcp://"+ TID_to_IP[TID]+":1120";
                    Dir = "SPC\\"+TID+"\\";
                    return true;
                }
            }
            catch { }
            return false;
        }
        public static string[] GetTIDS()
        {
            return TID_to_IP.Keys.ToArray();
        }
        public static string ReqestTID()
        {
            //if (TID_to_IP.Count > 0)
            //    return TerminalSelection.SelectTerminal((from item in TID_to_IP orderby item.Key select item.Key).ToArray());
            //else if (TID_to_IP.Count > 0)
            //    return TID_to_IP.First().Key;
            //else
                System.Windows.Forms.MessageBox.Show($"Нет доступных терминалов для обслуживания\r\nСостояние Benzuber.ru: {(BenzuberServer.Excange.ConnectionState?"Online":"Offline")}\r\nКод АЗС Benzuber.ru: {BenzuberServer.Excange.ID}", "Обслуживание", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            return "";
        }
        public static void ShowAllToRun(string TID)
        {
            if (setIP(TID))
            {
                //new Devices().ShowDialog();
            }
        }
        public static string[] GetActions(string TID)
        {
            if (setIP(TID))
            {
                var dir = new System.IO.DirectoryInfo(Dir);
                if (!dir.Exists)
                    dir.Create();
                var files = dir.GetFiles("*.xml");
                if (files.Length > 0)
                {
                    List<string> tmp = new List<string>();
                    foreach (var file in files)
                        tmp.Add(System.IO.Path.GetFileNameWithoutExtension(file.Name));
                    return tmp.ToArray();
                }
                else
                    return new string[0];
            }
            return new string[0];
        }
        public static void RunAction(string ActionName, string TID)
        {
            if (setIP(TID))
            {
                try
                {
                    while (true)
                    {
                        if (!GetActions(TID).Contains(ActionName))
                        {
                            if(System.Windows.Forms.MessageBox.Show(string.Format("Действия: \"{0}\" не существует.\r\nВы хотите добавить его?", ActionName), "Добавление действия", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
                                return;
                            SetAction(ActionName, TID);
                        }
                        else
                        {
                            var Commands = ProjectSummer.Repository.Serialization.Deserialize<RunCmd.Cmd[]>(Dir + ActionName + ".xml");
                            //RunCmd.ShowDialog(Commands, ActionName);
                            break;
                        }
                    }
                }
                catch(Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Ошибка при выполннии действия: " + ActionName+"\r\n"+ex.ToString());
                }
            }
        }
        public static void SetAction(string ActionName, string TID)
        {
            if (setIP(TID))
            {
                try
                {
                    var dir = new System.IO.DirectoryInfo(Dir);
                    if (!dir.Exists)
                        dir.Create();
                    //Devices dev = new Devices() { ActionName = ActionName};
                    //dev.SavePath = Dir;
                    ////  dev.Text = "Редактирование (" + ActionName + ")";
                    //dev.ShowDialog();
                }
                catch { }
            }
        }
        public static  void DeleteAction(string ActionName, string TID)
        {
            if (setIP(TID))
            {
                try
                {
                    var dir = new System.IO.DirectoryInfo(Dir);
                    if (!dir.Exists)
                        dir.Create();
                    if (System.IO.File.Exists(Dir + ActionName + ".xml"))
                    {
                        System.IO.File.Delete(Dir + ActionName + ".xml");
                    }
                }
                catch { }
            }
        }
    }
}
