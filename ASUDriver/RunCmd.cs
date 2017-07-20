using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using ProjectSummer.Repository;

//using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class RunCmd //: Form
    {
        public bool ExitOnSuccess
        {
            get; set;
        }
        public RunCmd()
        {
             //Application.EnableVisualStyles();
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
                            //MessageBox.Show(ex.ToString());
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
            //dlg.Text = info;
            dlg.ExitOnSuccess = true;
            foreach(var cmd in Commands)
            {
                if (cmd.RequestParamsAlways)
                {
                    //var c = Request.SetParams(cmd);
                    //if(c != null)
                    //    dlg.AddCommand(c);
                    //else
                    Console.WriteLine(string.Format("Команда {0} выполняться не будет.\r\nНе введены параметры.", cmd.Command));
                        //MessageBox.Show(string.Format("Команда {0} выполняться не будет.\r\nНе введены параметры.", cmd.Command), info, MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
                }
                else
                    dlg.AddCommand(cmd);

            }
            if (dlg.Commands.Count <= 0)
                Console.WriteLine("Отсутствуют команды для выполнения.");
                //MessageBox.Show("Отсутствуют команды для выполнения.", info, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return true;
        }
        delegate void Void();
        private void updateInfo()
        {
            //if (InvokeRequired)
            //{
            //    Invoke(new Void(updateInfo));
            //}
            //else
            {
                try
                {
                    bool exit_enable = true;
                    bool button_retry_visible = false;
                    //Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();
                    foreach (var c in Commands)
                    {
                        //if (!groups.ContainsKey(c.Device))
                        //{
                        //    groups.Add(c.Device, new ListViewGroup(c.DeviceInfo));
                        //}
                        //var item = new ListViewItem(new string[] { c.ToString(), decode_state(c.State) }, groups[c.Device]);
                        //if (c.State == state_enum.Error || c.State == state_enum.Canceled)
                        //{
                        //    item.BackColor = Color.LightPink;
                        //    button_retry_visible = true;
                        //}
                        //else if (c.State == state_enum.Success)
                        //    item.BackColor = Color.LightGreen;
                        //else if (c.State == state_enum.Run)
                        //    item.BackColor = Color.LightBlue;
                        //if (c.State == state_enum.Run)
                        //    exit_enable = false;
                    }
                    //button_cancel.Enabled = !exit_enable && !cancel_flag;
                    //button_exit.Enabled = exit_enable;
                    //button_retry.Visible = button_retry_visible && exit_enable;
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
            public ProjectSummer.Repository.Serialization.SerializableDictionary<string,string> Params;
            public ProjectSummer.Repository.Serialization.SerializableDictionary<string, string[]> ParamsVals;
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

        //private void RunCmd_Shown(object sender, EventArgs e)
        //{
        //    foreach (var c in Commands)
        //    {
        //        if (c.State == state_enum.Canceled || c.State == state_enum.Error)
        //        {
        //            c.State = state_enum.Wait;
        //        }
        //    }
        //    updateInfo();
        //    RunCommands();
        //}

        //private void button_cancel_Click(object sender, EventArgs e)
        //{
        //    try
        //    {
        //        cancel_flag = true;
        //        foreach (var c in Commands)
        //        {
        //            if (c.State == state_enum.Wait)
        //                c.State = state_enum.Canceled;
        //        }
        //        updateInfo();
        //        MessageBox.Show("Пожалуйста дождитесь завершения\r\nпоследней выполняемой операции.", "Отмена операции", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //    }
        //    catch
        //    {
        //    }
        //}

        //private void button_exit_Click(object sender, EventArgs e)
        //{
        //    Close();
        //}

        //private void button_retry_Click(object sender, EventArgs e)
        //{
        //    foreach (var c in Commands)
        //    {
        //        if (c.State == state_enum.Canceled)
        //            c.State = state_enum.Wait;
        //    }
        //    RunCommands();
        //    button_retry.Visible = false;
        //}

        //private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        //{

        //}
    }
}
