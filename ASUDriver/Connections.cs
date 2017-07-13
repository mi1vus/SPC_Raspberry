using ProjectSummer.Repository;
using System;
using System.Linq;
using System.Windows.Forms;
using ASUDriver;
using SPC_Raspberry;

namespace SmartPumpControlRemote
{
    public partial class Connections : Form
    {
        public Connections()
        {
            InitializeComponent();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            groupBox2.Enabled = benzuber_enable.Checked;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //var dlg_result = MessageBox.Show("Сохранить изменения сделанные на странице?", "Сохранение изменений", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            //if ()
            Driver.Params["port"] = server_port.Value.ToString();
            Driver.ServiceOperationTimeout = (int)service_timeout.Value;
            Driver.SaveParams();

            ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");
            config["enable"] = benzuber_enable.Checked.ToString().ToLower();
            config["server"] = benzuber_server.Text;
            config["station_id"] = benzuber_id.Text;
            config["exchangeport"] = benzuber_exchangeport.Value.ToString();
            var fuel_codes = config.GetValueNames("fuel_code_");
            foreach(var code in fuel_codes)
            {
                config.RemoveValue(code);
            }
            foreach (ListViewItem item in listView1.Items)
            {
                try
                {
                    if(item?.SubItems.Count == 4)
                        config["fuel_code_" + item?.SubItems?[2].Text] = item?.SubItems?[3].Text??"";
                }
                catch { }
            }
            config.Save();
            //MessageBox.Show(config["station_id"]);
            Close();
        }

        private void Connections_Load(object sender, EventArgs e)
        {
            

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Connections_Shown(object sender, EventArgs e)
        {
            int port = 1111;
            if(Driver.Params.ContainsKey("port") && int.TryParse(Driver.Params["port"]??"1111", out port))
            {
                server_port.Value = port;
            }
            else
                server_port.Value = 1111;
            service_timeout.Value = Driver.ServiceOperationTimeout;

            ConfigMemory config = ConfigMemory.GetConfigMemory("Benzuber");
            benzuber_enable.Checked = config["enable"] == "true";
            benzuber_server.Text = config["server"];
            benzuber_id.Text = config["station_id"];
            int benzuber_port;
            if(int.TryParse(config["exchangeport"], out benzuber_port))
            {
                benzuber_exchangeport.Value = benzuber_port;
            }
            try
            {
                var fuels = Driver.Fuels.Values.ToArray();
                for(int z=0; z<fuels.Length; z++)
                {
                    listView1.Items.Add(new ListViewItem(new string[] { fuels[z].InternalCode.ToString(), $"{fuels[z].Name}", fuels[z].ID.ToString(), config["fuel_code_" + fuels[z].ID]  }));
                }
            }
            catch { }

        }

        int editable_column = 3;
        private void listView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Clicks > 1)
            {
                TextBox tbox = new TextBox();
                Controls.Add(tbox);
                tbox.Width = listView1.Columns[editable_column].Width;
                ListViewItem item = listView1.GetItemAt(0, e.Y);
                if (item != null)
                {
                    int x_cord = 0;
                    for (int i = 0; i < listView1.Columns.Count - 1; i++)
                        x_cord += listView1.Columns[i].Width;
                    tbox.Left = x_cord;
                    tbox.Top = item.Position.Y;
                    tbox.Text = item.SubItems[editable_column].Text;
                    tbox.Leave += DisposeTextBox;
                    tbox.KeyPress += TextBoxKeyPress;
                    listView1.Controls.Add(tbox);
                    tbox.Focus();
                    tbox.Select(tbox.Text.Length, 1);
                }
            }
        }
        private void DisposeTextBox(object sender, EventArgs e)
        {
            var tb = (sender as TextBox);
            var item = listView1.GetItemAt(0, tb.Top + 1);
            if (item != null)
                item.SubItems[editable_column].Text = tb.Text;
            tb.Dispose();
        }

        private void TextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                DisposeTextBox((sender as TextBox), null);
            if (e.KeyChar == 27)
                (sender as TextBox).Dispose();
        }
    }
}
