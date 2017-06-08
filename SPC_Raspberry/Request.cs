using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class Request : Form
    {
        public Request()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
        }
        public void AddValue(string Name, string[] vals, string value = null)
        {
            
            parameters.Add(Name, new enter_val() { Info = Name, Values = vals, SelectedValue = value});
          
            flowLayoutPanel1.Controls.Add(parameters[Name]);
        }
        private Dictionary<string, enter_val> parameters = new Dictionary<string, enter_val>();
        public string GetValue(string Name)
        {
            return parameters[Name].SelectedValue;
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            //var result = MessageBox.Show("Вы действительно хотите отменить ввод парметров?", "Ввод параметров", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            //MessageBox.Show(result.ToString());
            //if (result == DialogResult.Yes)
            //{
                
                DialogResult = DialogResult.Cancel;
                Close();
            //}
        }

        private void Request_Shown(object sender, EventArgs e)
        {
            if (parameters.Count <= 10)
            {
                Height = 127 + (30 * parameters.Count);
            }
            else
            {
                Height = 127 + (30 * parameters.Count);
                Width = 470;
            }
        }
        public static RunCmd.Cmd SetParams(RunCmd.Cmd cmd, bool hide_checkBox_requestAlways = true)
        {
            if (cmd.Params != null && cmd.Params.Count > 0)
            {
                var req = new Request() { Text = cmd.DeviceInfo + "(" + cmd.Command + ")" };

                foreach (var p in cmd.Params)
                {
                    req.AddValue(p.Key, (cmd.ParamsVals != null && cmd.ParamsVals.ContainsKey(p.Key))?cmd.ParamsVals[p.Key]:null, p.Value);
                }
                req.checkBox_requestAlways.Checked = cmd.RequestParamsAlways;
                req.checkBox_requestAlways.Visible = !hide_checkBox_requestAlways;
                if (req.ShowDialog() == DialogResult.OK)
                {
                    var keys = cmd.Params.Keys.ToArray();
                    foreach (var p in keys)
                    {
                        cmd.Params[p] = req.GetValue(p);
                    }
                    cmd.RequestParamsAlways = req.checkBox_requestAlways.Checked;
                }
                else
                {
                    MessageBox.Show("Ввод параметров отменён.", "Ввод параметров действия", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
//                    MessageBox.Show("Ввод параметров отменён.\r\nЗапрос параметров будет производиться\r\nпри каждом выполнении действие: " + req.Text + ".", "Ввод параметров действия", MessageBoxButtons.OK, MessageBoxIcon.Information);
                   // cmd.RequestParamsAlways = true;
                   
                }
            }
            return cmd;
        }
    }
}
