using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartPumpControlRemote
{
    public partial class RunCmd
    {
        public bool ExitOnSuccess
        {
            get; set;
        }
        bool cancel_flag = false;
        public bool RunCommands()
        {
            cancel_flag = false;
            new Task(() =>
            {
                try
                {
                    bool error = false;
                    foreach (var c in Commands)
                    {                        
                        try
                        {
                            if (c.State == state_enum.Wait || c.State == state_enum.Error)
                            {
                                var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(Shell.IP, ASUDriver.Driver.ServiceOperationTimeout);
                              
                                c.State = state_enum.Run;
                                updateInfo();
                                
                                c.State = (proxy.RunCommand(c.Device, c.Command, new Dictionary<string, string>(c.Params))) ? state_enum.Success : state_enum.Error;
                            }
                        }
                        catch(Exception ex)
                        {
                            c.State = state_enum.Error;
                        }
                        if (c.State != state_enum.Success)
                            error = true;
                        updateInfo();
                    }
                    updateInfo();
                    if (!error && ExitOnSuccess)
                        return;
                }
                catch { }
            }).Start();
            return true;
        }
        public static bool ShowDialog(Cmd[] Commands, string info = "", bool exit_on_success = false)
        {
            var dlg = new RunCmd();
            dlg.ExitOnSuccess = true;
            foreach(var cmd in Commands)
            {
                if (cmd.RequestParamsAlways)
                {
                    Console.WriteLine(string.Format("Команда {0} выполняться не будет.\r\nНе введены параметры.", cmd.Command));
                }
                else
                    dlg.AddCommand(cmd);

            }
            if (dlg.Commands.Count <= 0)
                Console.WriteLine("Отсутствуют команды для выполнения.");
            return true;
        }
        delegate void Void();
        private void updateInfo()
        {
            {
                try
                {
                    bool exit_enable = true;
                    bool button_retry_visible = false;
                    foreach (var c in Commands)
                    {
                    }
                }
                catch { }
            }
        }

        [Serializable]
        public class Cmd
        {
            public string Device;
            public string DeviceInfo;
            public string Command;
            public ProjectSummer.Repository.ASUDriver.Serialization.SerializableDictionary<string,string> Params;
            public ProjectSummer.Repository.ASUDriver.Serialization.SerializableDictionary<string, string[]> ParamsVals;
            public bool RequestParamsAlways { get; set; }
            public state_enum State;
            public override string ToString()
            {
                return string.Format("{1}: {0}", Command, DeviceInfo);
            }
        }
        public enum state_enum
        {
            Wait,
            Run,
            Success,
            Error,
            Canceled,
        }
        private string decode_state(state_enum State)
        {
            switch(State)
            {
                case state_enum.Wait: return "Ожидание";
                case state_enum.Run: return "Выполняется";
                case state_enum.Success: return "Успешно";
                case state_enum.Error: return "Ошибка";
                case state_enum.Canceled: return "Отменено";
                default: return "";
            }
        }
        public List<Cmd> Commands = new List<Cmd>();
        public void AddCommand(Cmd _CMD)
        {
            Commands.Add(_CMD);
            updateInfo();
        }
    }
}
