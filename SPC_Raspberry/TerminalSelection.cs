using ProjectSummer.Repository;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class TerminalSelection : Form
    {
        public TerminalSelection()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
        }
        string[] tids = new string[0];
        public TerminalSelection(string[] TIDS)
        {
            InitializeComponent();
            tids = TIDS;
            listView1.Items.Clear();
            foreach (var TID in TIDS)
            {
                listView1.Items.Add(new ListViewItem(TID, 0) { Name = TID });
            }
            //this.Height = (TIDS.Length) * 46 + this.Height;
            //foreach (var TID in TIDS)
            //{
            //    var button = new Button();
            //    button.Text = TID;
            //    button.Click += Button_Click;
            //    button.Width = 220;
            //    button.Height = 40;
            //    flowLayoutPanel1.Controls.Add(button);

            //}
            
        }



        public string SelectionResult { get; private set; }
        private void Button_Click(object sender, EventArgs e)
        {
            if(sender is Button)
            {
                SelectionResult = ((Button)sender).Text;
                this.Close();
            }
        }
        public static string SelectTerminal(string[] TIDS)
        {
            var dialog = new TerminalSelection(TIDS);
            dialog.ShowDialog();
            return dialog.SelectionResult;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            SelectionResult = "";
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            lock_unlock(false);
        }
        private void lock_unlock(bool unlock)
        {
            foreach (var TID in tids)
            {
                Shell.setIP(TID);
                var par = new ProjectSummer.Repository.Serialization.SerializableDictionary<string, string>();
                par.Add("Текст блокировки", (unlock)?"":"Терминал заблокирован");
                var parv = new ProjectSummer.Repository.Serialization.SerializableDictionary<string, string[]>();
                parv.Add("Текст блокировки", new string[0]);
                RunCmd.Cmd cmd = new RunCmd.Cmd() { Device = "CoreDevice", DeviceInfo = "Система", Command = "Блокировка терминала", Params = par, RequestParamsAlways = false, ParamsVals = parv };
                
                RunCmd.ShowDialog(new RunCmd.Cmd[] { cmd }, ((unlock)?"Разблокировка терминала":"Блокировка терминала ") + TID, true);

            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            lock_unlock(true);
        }

        private void listView1_Click(object sender, EventArgs e)
        {

          
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                SelectionResult = listView1.SelectedItems[0].Name;
                this.Close();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                SelectionResult = listView1.SelectedItems[0].Name;
                this.Close();
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button4.Enabled = (listView1.SelectedItems.Count > 0);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            update_benzuber_state();
        }
        private delegate void update_benzuber_state_delegate();
        private void update_benzuber_state()
        {
            if(label2.InvokeRequired)
            {
                label2.Invoke(new update_benzuber_state_delegate(update_benzuber_state));
            }
            else
            {
                ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");
                if (config["enable"].ToLower() == "true")

                    label4.Text = config["station_id"];

                if (BenzuberServer.Excange.ConnectionState)
                {
                    label2.Text = "Online";
                    label2.BackColor = Color.LightGreen;
                    
                }
                else
                {
                    label2.Text = "Offline";
                    label2.BackColor = Color.Pink;
                }
                
            }
        }

        private void TerminalSelection_Load(object sender, EventArgs e)
        {

        }

        private void TerminalSelection_Shown(object sender, EventArgs e)
        {
            ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");

            if (config["enable"].ToLower() == "true")
            {
                groupBox3.Visible = true;
                update_benzuber_state();
                timer1.Start();
            }
            else
                groupBox3.Visible = false;
        }
    }
}
