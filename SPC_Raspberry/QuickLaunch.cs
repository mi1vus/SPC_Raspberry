using System;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class QuickLaunch : Form
    {
        private string _tid = "";
        public string tid
        {
            get { return _tid; }
            set { _tid = value; this.Text = "Обслуживание \"" + tid+"\""; }

        }
        public QuickLaunch()
        {


            InitializeComponent();
            listView1.ItemActivate += ListView1_ItemActivate;
            listView1.Items.Clear();
            listView1.Groups.Clear();
            Application.EnableVisualStyles();
            tid = Shell.ReqestTID();
            var actions = Shell.GetActions(tid);
            if (actions.Length > 0)
            {
                //if (actions.Length > 1)
                //{
                //    this.Width = 406;
                //}
                //if (actions.Length <= 20)
                //{
                //    this.Height = ((actions.Length + 1) / 2) * 29 + 171;
                //}
                //else
                //{
                //    this.Height = 461;
                //    this.Width = 426;
                //}
                foreach (var action in actions)
                {
                    if(action.Contains("--"))
                    {
                        var group_item = action.Split(new string[] {"--"}, StringSplitOptions.None);
                        var group_name = group_item[0].Trim();
                        var item_name = group_item[1].Trim();
                        var group = new ListViewGroup(group_name, group_name);
                        if (!listView1.Groups.Contains(group))
                            listView1.Groups.Add(group);
                        listView1.Items.Add(new ListViewItem(item_name) { Group = listView1.Groups[group_name], Name=action, ImageIndex = 0 });
                    }
                    else
                    {               
                        listView1.Items.Add(new ListViewItem(action) { Name = action, ImageIndex = 0 });
                    }
                    //var button = new Button() { Text = action, Width = 174 };
                    //button.Click += Button_Click;
                    //flowLayoutPanel.Controls.Add(button);
                }
            }
        }

        private void ListView1_ItemActivate(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                Shell.RunAction(listView1.SelectedItems[0].Name, tid);                
            }

        }

        private void Button_Click(object sender, EventArgs e)
        {
            if(sender is Button)
            {
                Shell.RunAction(((Button)sender).Text, tid);
            }
        }

        private void button_exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Shell.ShowAllToRun(tid);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                Shell.RunAction(listView1.SelectedItems[0].Name, tid);
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            button2.Enabled = (listView1.SelectedItems.Count > 0);
                
        }
    }
}
