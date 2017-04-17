using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class Devices : Form
    {
        public Devices()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
        }
        private void Devices_Load(object sender, EventArgs e)
        {
            try
            {
                var commands = new RunCmd.Cmd[0];
                if (savePath != null && savePath != "" && !string.IsNullOrWhiteSpace(ActionName))
                {
                    try
                    {
                        commands = ProjectSummer.Repository.Serialization.Deserialize<RunCmd.Cmd[]>(savePath+"\\"+ActionName+".xml");
                    }
                    catch { }
                    if(commands == null)
                        commands = new RunCmd.Cmd[0];
                }
                var proxy = RemoteService.IRemoteServiceClient.CreateRemoteService(Shell.IP);
                var devices = proxy.GetDevices();                
                foreach (var dev in devices)
                {
                    var group = new ListViewGroup(dev.Description);
                    CMD_List.Groups.Add(group);
                    foreach (var c in dev.Commands)
                    {
                    
                        var item = new ListViewItem(new string[] { c.Command }, group) { Tag = c };
                        CMD_List.Items.Add(item);
                        foreach (var tmp in commands)
                        {
                            if (tmp.Command == c.Command && tmp.Device == c.Device)
                            {                                
                                item.Checked = true;
                                for(int z=0; z<c.Parameters.Length; z++)
                                {
                                    if(tmp.Params.ContainsKey(c.Parameters[z].Parameter))
                                    {
                                        var t = c.Parameters[z];
                                        t.Value = tmp.Params[t.Parameter];
                                        c.Parameters[z] = t;
                                    }
                                }
                            }
                        }                       
                        
                    }
                }
            

            }
            catch { }
        }

        private string savePath = "";
        public string SavePath
        {
            get
            {
                return savePath;
            }
            set
            {
                savePath = value;
                if (savePath != null && savePath != "")
                    button_run.Text = "Сохранить";
                else
                    button_run.Text = "Выполнить";
            }
        }
        public string ActionName
        {
            get
            {
                return textBox1.Text;
            }
            set
            {
                textBox1.Text = value;
                if(string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    Text = "Добавление действия";
                    textBox1.ReadOnly = false;
                }
                else
                {
                    Text = "Редактирование действия";
                    textBox1.ReadOnly = true;
                }

            }
        }
        private void button_run_Click(object sender, EventArgs e)
        {
            try
            {
                var commands = get_commands();
                if (commands == null || commands.Length==0)
                    return;
                
                if (savePath != null && savePath != "" && !string.IsNullOrWhiteSpace(ActionName))
                    ProjectSummer.Repository.Serialization.Serialize(commands, savePath + "\\" + ActionName + ".xml");
                else
                    RunCmd.ShowDialog(commands);

                this.Close();
            }
            catch { }
        }
        private RunCmd.Cmd[] get_commands()
        {
            List<RunCmd.Cmd> commands = new List<RunCmd.Cmd>();
            if (CMD_List.CheckedItems.Count > 0)
            {
                foreach (ListViewItem item in CMD_List.CheckedItems)
                {
                    var cmd = new RunCmd.Cmd()
                    {
                        Device = ((RemoteService.CommandInfo)item.Tag).Device,
                        DeviceInfo = ((RemoteService.CommandInfo)item.Tag).DeviceInfo,
                        Command = ((RemoteService.CommandInfo)item.Tag).Command,
                        Params = new ProjectSummer.Repository.Serialization.SerializableDictionary<string, string>(),
                        ParamsVals = new ProjectSummer.Repository.Serialization.SerializableDictionary<string, string[]>(),
                        State = RunCmd.state_enum.Wait,
                    };
                    foreach (var p in ((RemoteService.CommandInfo)item.Tag).Parameters)
                    {
                        cmd.Params.Add(p.Parameter, p.Value);
                        cmd.ParamsVals.Add(p.Parameter, p.Values);
                    }
                    commands.Add(Request.SetParams(cmd, !(savePath != null && savePath != "")));
                    if (cmd == null)
                        return null;
                }
                if (commands.Count > 1)
                {
                    SortCommands sorter = new SortCommands();
                    sorter.AddItems(commands.ToArray());
                    if (sorter.ShowDialog() == DialogResult.OK)
                        return sorter.GetItems();
                    else
                        return null;
                }

            }
            return commands.ToArray();
        }
        private void Devices_Shown(object sender, EventArgs e)
        {

        }
        private void button_exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
